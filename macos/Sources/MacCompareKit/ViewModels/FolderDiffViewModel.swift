import Foundation
import SwiftUI
import AppKit

public enum FolderViewMode: String, CaseIterable, Identifiable, Sendable {
    case quick = "Quick Compare (Timestamp/Size)"
    case deepHash = "Deep Hash Compare (CRC32)"

    public var id: String { rawValue }
}

public struct RecentCompareSession: Identifiable, Sendable, Codable, Equatable {
    public let id: UUID
    public let title: String
    public let leftPath: String
    public let rightPath: String
    public let date: Date

    public init(id: UUID = UUID(), title: String, leftPath: String, rightPath: String, date: Date = Date()) {
        self.id = id
        self.title = title
        self.leftPath = leftPath
        self.rightPath = rightPath
        self.date = date
    }
}

public struct AlignedFolderRow: Identifiable, Sendable {
    public var id: String { relativePath }
    public let relativePath: String
    public let name: String
    public let isDirectory: Bool
    public let status: FolderItemStatus

    public let leftSizeFormatted: String
    public let leftModifiedFormatted: String
    public let rightSizeFormatted: String
    public let rightModifiedFormatted: String
    public let leftHash: String?
    public let rightHash: String?

    public let leftURL: URL?
    public let rightURL: URL?

    public var isLeftMissing: Bool { status == .rightOnly }
    public var isRightMissing: Bool { status == .leftOnly }
}

@MainActor
@Observable
public final class FolderDiffViewModel {
    public var leftFolderName: String = "Source Folder"
    public var rightFolderName: String = "Target Folder"

    public var leftFolderURL: URL?
    public var rightFolderURL: URL?

    public var selectedMode: FolderViewMode = .quick {
        didSet { Task { await scanDirectories() } }
    }
    public var filterRulesSummary: String = "*.swift, exclude node_modules"
    public var excludePatterns: [String] = [".git", ".DS_Store", "node_modules", "target", "build"]

    public var isDryRunPresented: Bool = false
    public var pendingSyncPlan: [SyncPlanItem] = []
    public var syncExecutionResult: String?
    public var isScanning: Bool = false

    public var entries: [AlignedFolderRow] = []
    public var totalScanned: Int = 0
    public var modifiedCount: Int = 0
    public var addedCount: Int = 0
    public var deletedCount: Int = 0

    public var recentSessions: [RecentCompareSession] = []
    public var selectedSidebarSection: String?

    public var onOpenFileDiff: ((URL, URL) -> Void)?

    private let diffEngine: DiffEngineProtocol

    public init(
        diffEngine: DiffEngineProtocol = DiffEngineService.shared,
        leftURL: URL? = nil,
        rightURL: URL? = nil
    ) {
        self.diffEngine = diffEngine
        self.leftFolderURL = leftURL
        self.rightFolderURL = rightURL

        if let l = leftURL, let r = rightURL {
            setFolders(left: l, right: r)
        } else {
            loadDefaultFolders()
        }
    }

    public func setFolders(left: URL, right: URL) {
        self.leftFolderURL = left
        self.rightFolderURL = right
        self.leftFolderName = left.lastPathComponent
        self.rightFolderName = right.lastPathComponent

        // Record recent session
        let title = "\(left.lastPathComponent) ↔ \(right.lastPathComponent)"
        if !recentSessions.contains(where: { $0.leftPath == left.path && $0.rightPath == right.path }) {
            recentSessions.insert(RecentCompareSession(title: title, leftPath: left.path, rightPath: right.path), at: 0)
            if recentSessions.count > 10 {
                recentSessions.removeLast()
            }
        }

        Task {
            await scanDirectories()
        }
    }

    public func chooseLeftFolder() {
        let panel = NSOpenPanel()
        panel.title = "Select Source (Left) Directory"
        panel.canChooseFiles = false
        panel.canChooseDirectories = true
        panel.allowsMultipleSelection = false

        if panel.runModal() == .OK, let url = panel.url {
            self.leftFolderURL = url
            self.leftFolderName = url.lastPathComponent
            if let r = rightFolderURL {
                setFolders(left: url, right: r)
            } else {
                Task { await scanDirectories() }
            }
        }
    }

    public func chooseRightFolder() {
        let panel = NSOpenPanel()
        panel.title = "Select Target (Right) Directory"
        panel.canChooseFiles = false
        panel.canChooseDirectories = true
        panel.allowsMultipleSelection = false

        if panel.runModal() == .OK, let url = panel.url {
            self.rightFolderURL = url
            self.rightFolderName = url.lastPathComponent
            if let l = leftFolderURL {
                setFolders(left: l, right: url)
            } else {
                Task { await scanDirectories() }
            }
        }
    }

    // MARK: - Quick Sidebar Actions

    public func openDocumentsFolder(forLeft: Bool = true) {
        let docs = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask).first
        if let docs = docs {
            if forLeft {
                leftFolderURL = docs
                leftFolderName = "Documents"
            } else {
                rightFolderURL = docs
                rightFolderName = "Documents"
            }
            Task { await scanDirectories() }
        }
    }

    public func openDownloadsFolder(forLeft: Bool = true) {
        let downloads = FileManager.default.urls(for: .downloadsDirectory, in: .userDomainMask).first
        if let downloads = downloads {
            if forLeft {
                leftFolderURL = downloads
                leftFolderName = "Downloads"
            } else {
                rightFolderURL = downloads
                rightFolderName = "Downloads"
            }
            Task { await scanDirectories() }
        }
    }

    public func openDesktopFolder(forLeft: Bool = true) {
        let desktop = FileManager.default.urls(for: .desktopDirectory, in: .userDomainMask).first
        if let desktop = desktop {
            if forLeft {
                leftFolderURL = desktop
                leftFolderName = "Desktop"
            } else {
                rightFolderURL = desktop
                rightFolderName = "Desktop"
            }
            Task { await scanDirectories() }
        }
    }

    public func swapFolders() {
        let tempURL = leftFolderURL
        let tempName = leftFolderName
        leftFolderURL = rightFolderURL
        leftFolderName = rightFolderName
        rightFolderURL = tempURL
        rightFolderName = tempName
        Task { await scanDirectories() }
    }

    public func loadRecentSession(_ session: RecentCompareSession) {
        let l = URL(fileURLWithPath: session.leftPath)
        let r = URL(fileURLWithPath: session.rightPath)
        setFolders(left: l, right: r)
    }

    public func scanDirectories() async {
        guard let left = leftFolderURL, let right = rightFolderURL else {
            return
        }

        isScanning = true
        let rawEntries = await diffEngine.compareFolders(
            leftPath: left.path,
            rightPath: right.path,
            mode: selectedMode == .deepHash ? 1 : 0,
            excludePatterns: excludePatterns
        )

        var mod = 0
        var add = 0
        var del = 0

        let dateFormatter = DateFormatter()
        dateFormatter.dateFormat = "MMM d, yyyy 'at' HH:mm"

        let mapped = rawEntries.map { entry -> AlignedFolderRow in
            switch entry.status {
            case .contentDifferent, .metadataDifferent:
                mod += 1
            case .rightOnly:
                add += 1
            case .leftOnly:
                del += 1
            case .equal:
                break
            }

            let lSizeStr = entry.leftSize.map { formatByteCount($0) } ?? "---"
            let rSizeStr = entry.rightSize.map { formatByteCount($0) } ?? "---"

            let lDateStr = entry.leftModifiedTimestamp.map {
                dateFormatter.string(from: Date(timeIntervalSince1970: Double($0)))
            } ?? "---"
            let rDateStr = entry.rightModifiedTimestamp.map {
                dateFormatter.string(from: Date(timeIntervalSince1970: Double($0)))
            } ?? "---"

            return AlignedFolderRow(
                relativePath: entry.relativePath,
                name: URL(fileURLWithPath: entry.relativePath).lastPathComponent,
                isDirectory: entry.isDirectory,
                status: entry.status,
                leftSizeFormatted: lSizeStr,
                leftModifiedFormatted: lDateStr,
                rightSizeFormatted: rSizeStr,
                rightModifiedFormatted: rDateStr,
                leftHash: entry.leftHash,
                rightHash: entry.rightHash,
                leftURL: entry.leftURL,
                rightURL: entry.rightURL
            )
        }

        self.entries = mapped
        self.totalScanned = mapped.count
        self.modifiedCount = mod
        self.addedCount = add
        self.deletedCount = del
        self.isScanning = false
    }

    public func handleRowDoubleClick(entry: AlignedFolderRow) {
        guard !entry.isDirectory else { return }
        guard let lURL = entry.leftURL ?? leftFolderURL?.appendingPathComponent(entry.relativePath),
              let rURL = entry.rightURL ?? rightFolderURL?.appendingPathComponent(entry.relativePath) else {
            return
        }
        onOpenFileDiff?(lURL, rURL)
    }

    public func syncLeftToRight() {
        var plan: [SyncPlanItem] = []
        for entry in entries {
            switch entry.status {
            case .leftOnly:
                plan.append(SyncPlanItem(
                    action: .copyLeftToRight,
                    relativePath: entry.relativePath,
                    sourceURL: entry.leftURL ?? leftFolderURL?.appendingPathComponent(entry.relativePath),
                    targetURL: rightFolderURL?.appendingPathComponent(entry.relativePath),
                    size: nil
                ))
            case .contentDifferent, .metadataDifferent:
                plan.append(SyncPlanItem(
                    action: .overwriteLeftToRight,
                    relativePath: entry.relativePath,
                    sourceURL: entry.leftURL ?? leftFolderURL?.appendingPathComponent(entry.relativePath),
                    targetURL: entry.rightURL ?? rightFolderURL?.appendingPathComponent(entry.relativePath),
                    size: nil
                ))
            case .rightOnly:
                plan.append(SyncPlanItem(
                    action: .deleteRight,
                    relativePath: entry.relativePath,
                    sourceURL: nil,
                    targetURL: entry.rightURL ?? rightFolderURL?.appendingPathComponent(entry.relativePath),
                    size: nil
                ))
            case .equal:
                break
            }
        }
        self.pendingSyncPlan = plan
        self.isDryRunPresented = true
    }

    public func syncRightToLeft() {
        var plan: [SyncPlanItem] = []
        for entry in entries {
            switch entry.status {
            case .rightOnly:
                plan.append(SyncPlanItem(
                    action: .copyRightToLeft,
                    relativePath: entry.relativePath,
                    sourceURL: entry.rightURL ?? rightFolderURL?.appendingPathComponent(entry.relativePath),
                    targetURL: leftFolderURL?.appendingPathComponent(entry.relativePath),
                    size: nil
                ))
            case .contentDifferent, .metadataDifferent:
                plan.append(SyncPlanItem(
                    action: .overwriteRightToLeft,
                    relativePath: entry.relativePath,
                    sourceURL: entry.rightURL ?? rightFolderURL?.appendingPathComponent(entry.relativePath),
                    targetURL: entry.leftURL ?? leftFolderURL?.appendingPathComponent(entry.relativePath),
                    size: nil
                ))
            case .leftOnly:
                plan.append(SyncPlanItem(
                    action: .deleteLeft,
                    relativePath: entry.relativePath,
                    sourceURL: entry.leftURL ?? leftFolderURL?.appendingPathComponent(entry.relativePath),
                    targetURL: nil,
                    size: nil
                ))
            case .equal:
                break
            }
        }
        self.pendingSyncPlan = plan
        self.isDryRunPresented = true
    }

    public func executePendingSync() async {
        guard !pendingSyncPlan.isEmpty else { return }
        do {
            let res = try await diffEngine.executeSyncPlan(items: pendingSyncPlan)
            syncExecutionResult = "Sync completed: \(res.successCount) succeeded, \(res.errorCount) errors."
            pendingSyncPlan = []
            await scanDirectories()
        } catch {
            syncExecutionResult = "Sync Failed: \(error.localizedDescription)"
        }
    }

    private func loadDefaultFolders() {
        let cwd = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        self.leftFolderURL = cwd
        self.leftFolderName = cwd.lastPathComponent
        self.rightFolderURL = cwd
        self.rightFolderName = cwd.lastPathComponent

        Task {
            await scanDirectories()
        }
    }

    private func formatByteCount(_ bytes: UInt64) -> String {
        let formatter = ByteCountFormatter()
        formatter.allowedUnits = [.useAll]
        formatter.countStyle = .file
        return formatter.string(fromByteCount: Int64(bytes))
    }
}
