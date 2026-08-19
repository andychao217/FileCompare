import Foundation
import SwiftUI
import AppKit

public enum FolderViewMode: String, CaseIterable, Identifiable, Sendable {
    case quick = "Quick Compare (Timestamp/Size)"
    case deepHash = "Deep Hash Compare (CRC32)"

    public var id: String { rawValue }
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
    public var leftFolderName: String = "Source Folder (with SF Symbols)"
    public var rightFolderName: String = "Remote Target Folder"

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
            loadSampleData()
        }
    }

    public func setFolders(left: URL, right: URL) {
        self.leftFolderURL = left
        self.rightFolderURL = right
        self.leftFolderName = left.lastPathComponent
        self.rightFolderName = right.lastPathComponent
        Task {
            await scanDirectories()
        }
    }

    public func chooseLeftFolder() {
        let panel = NSOpenPanel()
        panel.title = "Select Left Directory"
        panel.canChooseFiles = false
        panel.canChooseDirectories = true
        panel.allowsMultipleSelection = false

        if panel.runModal() == .OK, let url = panel.url {
            self.leftFolderURL = url
            self.leftFolderName = url.lastPathComponent
            Task { await scanDirectories() }
        }
    }

    public func chooseRightFolder() {
        let panel = NSOpenPanel()
        panel.title = "Select Right Directory"
        panel.canChooseFiles = false
        panel.canChooseDirectories = true
        panel.allowsMultipleSelection = false

        if panel.runModal() == .OK, let url = panel.url {
            self.rightFolderURL = url
            self.rightFolderName = url.lastPathComponent
            Task { await scanDirectories() }
        }
    }

    public func scanDirectories() async {
        guard let left = leftFolderURL, let right = rightFolderURL else {
            return
        }

        isScanning = true
        let modeInt = selectedMode == .deepHash ? 1 : 0
        let results = await diffEngine.compareFolders(
            leftPath: left.path,
            rightPath: right.path,
            mode: modeInt,
            excludePatterns: excludePatterns
        )

        var rows: [AlignedFolderRow] = []
        var mod = 0
        var add = 0
        var del = 0

        let dateFormatter = DateFormatter()
        dateFormatter.dateFormat = "MMM d, yyyy 'at' HH:mm"

        for entry in results {
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

            let lSizeStr = entry.leftSize.map { formatByteSize($0) } ?? ""
            let rSizeStr = entry.rightSize.map { formatByteSize($0) } ?? ""

            let lDateStr = entry.leftModifiedTimestamp.map { dateFormatter.string(from: Date(timeIntervalSince1970: TimeInterval($0))) } ?? "---"
            let rDateStr = entry.rightModifiedTimestamp.map { dateFormatter.string(from: Date(timeIntervalSince1970: TimeInterval($0))) } ?? "---"

            let fileName = (entry.relativePath as NSString).lastPathComponent

            rows.append(AlignedFolderRow(
                relativePath: entry.relativePath,
                name: fileName,
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
            ))
        }

        self.entries = rows
        self.totalScanned = rows.count
        self.modifiedCount = mod
        self.addedCount = add
        self.deletedCount = del
        self.isScanning = false
    }

    private func formatByteSize(_ bytes: UInt64) -> String {
        if bytes < 1024 { return "\(bytes) B" }
        let kb = Double(bytes) / 1024.0
        if kb < 1024 { return String(format: "%.1f KB", kb) }
        let mb = kb / 1024.0
        return String(format: "%.1f MB", mb)
    }

    // MARK: - Double Click File Action

    public func handleRowDoubleClick(entry: AlignedFolderRow) {
        guard !entry.isDirectory else { return }
        if let l = entry.leftURL, let r = entry.rightURL {
            onOpenFileDiff?(l, r)
        } else if let l = entry.leftURL, let rightBase = rightFolderURL {
            let r = rightBase.appendingPathComponent(entry.relativePath)
            onOpenFileDiff?(l, r)
        } else if let r = entry.rightURL, let leftBase = leftFolderURL {
            let l = leftBase.appendingPathComponent(entry.relativePath)
            onOpenFileDiff?(l, r)
        }
    }

    // MARK: - Sync Plan Generation & Execution

    public func syncLeftToRight() {
        guard let leftBase = leftFolderURL, let rightBase = rightFolderURL else {
            isDryRunPresented = true
            return
        }

        var plan: [SyncPlanItem] = []
        for entry in entries {
            let src = leftBase.appendingPathComponent(entry.relativePath)
            let dst = rightBase.appendingPathComponent(entry.relativePath)

            switch entry.status {
            case .leftOnly:
                plan.append(SyncPlanItem(action: .copyLeftToRight, relativePath: entry.relativePath, sourceURL: src, targetURL: dst, size: nil))
            case .contentDifferent, .metadataDifferent:
                plan.append(SyncPlanItem(action: .overwriteLeftToRight, relativePath: entry.relativePath, sourceURL: src, targetURL: dst, size: nil))
            case .rightOnly:
                plan.append(SyncPlanItem(action: .deleteRight, relativePath: entry.relativePath, sourceURL: nil, targetURL: dst, size: nil))
            case .equal:
                break
            }
        }
        self.pendingSyncPlan = plan
        self.isDryRunPresented = true
    }

    public func syncRightToLeft() {
        guard let leftBase = leftFolderURL, let rightBase = rightFolderURL else {
            isDryRunPresented = true
            return
        }

        var plan: [SyncPlanItem] = []
        for entry in entries {
            let src = rightBase.appendingPathComponent(entry.relativePath)
            let dst = leftBase.appendingPathComponent(entry.relativePath)

            switch entry.status {
            case .rightOnly:
                plan.append(SyncPlanItem(action: .copyRightToLeft, relativePath: entry.relativePath, sourceURL: dst, targetURL: src, size: nil))
            case .contentDifferent, .metadataDifferent:
                plan.append(SyncPlanItem(action: .overwriteRightToLeft, relativePath: entry.relativePath, sourceURL: dst, targetURL: src, size: nil))
            case .leftOnly:
                plan.append(SyncPlanItem(action: .deleteLeft, relativePath: entry.relativePath, sourceURL: dst, targetURL: nil, size: nil))
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
            let result = try await diffEngine.executeSyncPlan(items: pendingSyncPlan)
            self.syncExecutionResult = "Sync Complete: \(result.successCount) files synchronized."
            await scanDirectories()
        } catch {
            self.syncExecutionResult = "Sync Error: \(error.localizedDescription)"
        }
    }

    public func loadSampleData() {
        self.entries = [
            AlignedFolderRow(
                relativePath: "blts",
                name: "blts",
                isDirectory: true,
                status: .equal,
                leftSizeFormatted: "",
                leftModifiedFormatted: "Nov 3, 2021 at 22:35",
                rightSizeFormatted: "",
                rightModifiedFormatted: "Nov 3, 2021 at 12:38",
                leftHash: nil,
                rightHash: nil,
                leftURL: nil,
                rightURL: nil
            ),
            AlignedFolderRow(
                relativePath: "source/config",
                name: "config",
                isDirectory: true,
                status: .equal,
                leftSizeFormatted: "",
                leftModifiedFormatted: "Nov 3, 2021 at 22:35",
                rightSizeFormatted: "",
                rightModifiedFormatted: "Nov 3, 2021 at 12:30",
                leftHash: nil,
                rightHash: nil,
                leftURL: nil,
                rightURL: nil
            ),
            AlignedFolderRow(
                relativePath: "source/config/.gitigmode.swift",
                name: ".gitigmode.swift",
                isDirectory: false,
                status: .equal,
                leftSizeFormatted: "1.5 KB",
                leftModifiedFormatted: "Nov 3, 2021 at 22:35",
                rightSizeFormatted: "1.5 KB",
                rightModifiedFormatted: "Nov 3, 2021 at 12:39",
                leftHash: "A1B2C3D4",
                rightHash: "A1B2C3D4",
                leftURL: nil,
                rightURL: nil
            ),
            AlignedFolderRow(
                relativePath: "source/config/.gitignore.swift",
                name: ".gitignore.swift",
                isDirectory: false,
                status: .contentDifferent,
                leftSizeFormatted: "159 KB",
                leftModifiedFormatted: "Nov 3, 2021 at 22:35",
                rightSizeFormatted: "153 KB",
                rightModifiedFormatted: "Dec 3, 2021 at 12:37",
                leftHash: "5F8A1B2C",
                rightHash: "9E7D4C3B",
                leftURL: nil,
                rightURL: nil
            ),
            AlignedFolderRow(
                relativePath: "source/config/macCompare.swift",
                name: "macCompare.swift",
                isDirectory: false,
                status: .contentDifferent,
                leftSizeFormatted: "15.4 KB",
                leftModifiedFormatted: "Dec 3, 2021 at 20:56",
                rightSizeFormatted: "---",
                rightModifiedFormatted: "---",
                leftHash: "7C3D2E1F",
                rightHash: nil,
                leftURL: nil,
                rightURL: nil
            ),
            AlignedFolderRow(
                relativePath: "source/config/licensed.swift",
                name: "licensed.swift",
                isDirectory: false,
                status: .leftOnly,
                leftSizeFormatted: "3.3 KB",
                leftModifiedFormatted: "Dec 3, 2021 at 13:38",
                rightSizeFormatted: "---",
                rightModifiedFormatted: "---",
                leftHash: "8D4E1F2A",
                rightHash: nil,
                leftURL: nil,
                rightURL: nil
            ),
            AlignedFolderRow(
                relativePath: "source/config/result.swift",
                name: "result.swift",
                isDirectory: false,
                status: .rightOnly,
                leftSizeFormatted: "---",
                leftModifiedFormatted: "---",
                rightSizeFormatted: "1.1 KB",
                rightModifiedFormatted: "Dec 3, 2021 at 10:58",
                leftHash: nil,
                rightHash: "3B2A1F9E",
                leftURL: nil,
                rightURL: nil
            )
        ]
        self.totalScanned = 1420
        self.modifiedCount = 18
        self.addedCount = 5
        self.deletedCount = 2
    }
}
