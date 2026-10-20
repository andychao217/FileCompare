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

public struct AlignedFolderRow: Identifiable, Sendable, Equatable {
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

    public let depth: Int
    public let parentPath: String?
    public let ancestorPaths: [String]

    public var isLeftMissing: Bool { status == .rightOnly }
    public var isRightMissing: Bool { status == .leftOnly }

    public init(
        relativePath: String,
        name: String,
        isDirectory: Bool,
        status: FolderItemStatus,
        leftSizeFormatted: String,
        leftModifiedFormatted: String,
        rightSizeFormatted: String,
        rightModifiedFormatted: String,
        leftHash: String?,
        rightHash: String?,
        leftURL: URL?,
        rightURL: URL?
    ) {
        self.relativePath = relativePath
        self.name = name
        self.isDirectory = isDirectory
        self.status = status
        self.leftSizeFormatted = leftSizeFormatted
        self.leftModifiedFormatted = leftModifiedFormatted
        self.rightSizeFormatted = rightSizeFormatted
        self.rightModifiedFormatted = rightModifiedFormatted
        self.leftHash = leftHash
        self.rightHash = rightHash
        self.leftURL = leftURL
        self.rightURL = rightURL

        let parts = relativePath.split(separator: "/")
        self.depth = max(0, parts.count - 1)
        if parts.count > 1 {
            self.parentPath = parts.dropLast().joined(separator: "/")
            var ancestors: [String] = []
            var current = ""
            for i in 0..<(parts.count - 1) {
                if i > 0 { current += "/" }
                current += parts[i]
                ancestors.append(current)
            }
            self.ancestorPaths = ancestors
        } else {
            self.parentPath = nil
            self.ancestorPaths = []
        }
    }
}

@MainActor
@Observable
public final class FolderDiffViewModel {
    public var leftFolderName: String = "No Source Folder Selected"
    public var rightFolderName: String = "No Target Folder Selected"

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

    public var entries: [AlignedFolderRow] = [] {
        didSet { recomputeVisibleEntries() }
    }
    public var collapsedPaths: Set<String> = []
    public private(set) var visibleEntries: [AlignedFolderRow] = []

    public var totalScanned: Int = 0
    public var modifiedCount: Int = 0
    public var addedCount: Int = 0
    public var deletedCount: Int = 0

    public var recentSessions: [RecentCompareSession] = []
    public var selectedSidebarSection: String?

    public var onOpenFileDiff: ((URL, URL) -> Void)?

    private let diffEngine: DiffEngineProtocol

    public var hasFoldersLoaded: Bool {
        leftFolderURL != nil || rightFolderURL != nil
    }

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
        } else if let l = leftURL {
            loadSingleFolder(from: l, isLeft: true)
        } else if let r = rightURL {
            loadSingleFolder(from: r, isLeft: false)
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
        RecentHistoryManager.shared.addRecord(left: left, right: right, type: .folderDiff)

        Task {
            await scanDirectories()
        }
    }

    public func clearFolders() {
        leftFolderURL = nil
        rightFolderURL = nil
        leftFolderName = LanguageManager.shared.text(.noSourceFolder)
        rightFolderName = LanguageManager.shared.text(.noTargetFolder)
        entries = []
        collapsedPaths.removeAll()
        visibleEntries = []
        pendingSyncPlan = []
        syncExecutionResult = nil
        isScanning = false
        totalScanned = 0
        modifiedCount = 0
        addedCount = 0
        deletedCount = 0
    }

    public func recomputeVisibleEntries() {
        guard !collapsedPaths.isEmpty else {
            self.visibleEntries = entries
            return
        }

        var visible: [AlignedFolderRow] = []
        visible.reserveCapacity(entries.count)

        var skipPrefix: String? = nil

        for entry in entries {
            if let activeSkip = skipPrefix {
                if entry.relativePath.hasPrefix(activeSkip) {
                    continue
                } else {
                    skipPrefix = nil
                }
            }

            var isHidden = false
            for ancestor in entry.ancestorPaths {
                if collapsedPaths.contains(ancestor) {
                    isHidden = true
                    skipPrefix = ancestor + "/"
                    break
                }
            }

            if isHidden {
                continue
            }

            visible.append(entry)

            if entry.isDirectory && collapsedPaths.contains(entry.relativePath) {
                skipPrefix = entry.relativePath + "/"
            }
        }

        self.visibleEntries = visible
    }

    public func isFolderExpanded(path: String) -> Bool {
        !collapsedPaths.contains(path)
    }

    public func toggleFolderExpansion(path: String) {
        if collapsedPaths.contains(path) {
            collapsedPaths.remove(path)
        } else {
            collapsedPaths.insert(path)
        }
        recomputeVisibleEntries()
    }

    public func expandAllFolders() {
        collapsedPaths.removeAll()
        recomputeVisibleEntries()
    }

    public func collapseAllFolders() {
        let dirPaths = entries.filter { $0.isDirectory }.map { $0.relativePath }
        collapsedPaths = Set(dirPaths)
        recomputeVisibleEntries()
    }

    public static func sortFolderRows(_ rows: [AlignedFolderRow]) -> [AlignedFolderRow] {
        rows.sorted { (r1: AlignedFolderRow, r2: AlignedFolderRow) in
            let c1 = r1.relativePath.split(separator: "/").map(String.init)
            let c2 = r2.relativePath.split(separator: "/").map(String.init)
            let minCount = min(c1.count, c2.count)

            for i in 0..<minCount {
                if c1[i] != c2[i] {
                    let isDir1 = (i < c1.count - 1) ? true : r1.isDirectory
                    let isDir2 = (i < c2.count - 1) ? true : r2.isDirectory
                    if isDir1 != isDir2 {
                        return isDir1
                    }
                    return c1[i].localizedStandardCompare(c2[i]) == .orderedAscending
                }
            }

            if c1.count != c2.count {
                return c1.count < c2.count
            }
            return r1.isDirectory && !r2.isDirectory
        }
    }

    public func loadSingleFolder(from url: URL, isLeft: Bool) {
        if isLeft {
            self.leftFolderURL = url
            self.leftFolderName = url.lastPathComponent
        } else {
            self.rightFolderURL = url
            self.rightFolderName = url.lastPathComponent
        }

        if let l = leftFolderURL, let r = rightFolderURL {
            setFolders(left: l, right: r)
        } else {
            Task { await scanDirectories() }
        }
    }

    public func chooseLeftFolder() {
        let panel = NSOpenPanel()
        panel.title = "Select Source (Left) Directory"
        panel.canChooseFiles = false
        panel.canChooseDirectories = true
        panel.allowsMultipleSelection = false

        if panel.runModal() == .OK, let url = panel.url {
            loadSingleFolder(from: url, isLeft: true)
        }
    }

    public func chooseRightFolder() {
        let panel = NSOpenPanel()
        panel.title = "Select Target (Right) Directory"
        panel.canChooseFiles = false
        panel.canChooseDirectories = true
        panel.allowsMultipleSelection = false

        if panel.runModal() == .OK, let url = panel.url {
            loadSingleFolder(from: url, isLeft: false)
        }
    }

    // MARK: - Quick Sidebar Actions

    public func openDocumentsFolder(forLeft: Bool = true) {
        if let docs = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask).first {
            loadSingleFolder(from: docs, isLeft: forLeft)
        }
    }

    public func openDownloadsFolder(forLeft: Bool = true) {
        if let downloads = FileManager.default.urls(for: .downloadsDirectory, in: .userDomainMask).first {
            loadSingleFolder(from: downloads, isLeft: forLeft)
        }
    }

    public func openDesktopFolder(forLeft: Bool = true) {
        if let desktop = FileManager.default.urls(for: .desktopDirectory, in: .userDomainMask).first {
            loadSingleFolder(from: desktop, isLeft: forLeft)
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

    private func scanSingleDirectory(url: URL, isLeft: Bool) async -> [AlignedFolderRow] {
        let fileManager = FileManager.default
        let dateFormatter = DateFormatter()
        dateFormatter.dateFormat = "MMM d, yyyy 'at' HH:mm"

        guard let enumerator = fileManager.enumerator(
            at: url,
            includingPropertiesForKeys: [.isDirectoryKey, .fileSizeKey, .contentModificationDateKey],
            options: [.skipsHiddenFiles]
        ) else { return [] }

        var results: [AlignedFolderRow] = []

        while let fileURL = enumerator.nextObject() as? URL {
            let relativePath = fileURL.path.replacingOccurrences(of: url.path, with: "").trimmingCharacters(in: CharacterSet(charactersIn: "/"))
            if relativePath.isEmpty { continue }

            // Check exclude patterns
            if excludePatterns.contains(where: { pattern in
                let pat = pattern.trimmingCharacters(in: .whitespaces)
                return !pat.isEmpty && (relativePath == pat || relativePath.hasPrefix(pat + "/") || relativePath.hasSuffix("/" + pat))
            }) {
                continue
            }

            guard let resourceValues = try? fileURL.resourceValues(forKeys: [.isDirectoryKey, .fileSizeKey, .contentModificationDateKey]) else {
                continue
            }

            let isDir = resourceValues.isDirectory ?? false
            let size = isDir ? nil : UInt64(resourceValues.fileSize ?? 0)
            let modDate = resourceValues.contentModificationDate
            let sizeStr = size.map { formatByteCount($0) } ?? "---"
            let dateStr = modDate.map { dateFormatter.string(from: $0) } ?? "---"

            var hashStr: String? = nil
            if selectedMode == .deepHash && !isDir {
                if let data = try? Data(contentsOf: fileURL, options: .mappedIfSafe) {
                    hashStr = String(format: "%08X", data.withUnsafeBytes { ptr in
                        var crc: UInt32 = 0xFFFFFFFF
                        for byte in ptr {
                            crc ^= UInt32(byte)
                            for _ in 0..<8 {
                                crc = (crc & 1 != 0) ? (crc >> 1) ^ 0xEDB88320 : (crc >> 1)
                            }
                        }
                        return ~crc
                    })
                }
            }

            let row = AlignedFolderRow(
                relativePath: relativePath,
                name: fileURL.lastPathComponent,
                isDirectory: isDir,
                status: isLeft ? .leftOnly : .rightOnly,
                leftSizeFormatted: isLeft ? sizeStr : "---",
                leftModifiedFormatted: isLeft ? dateStr : "---",
                rightSizeFormatted: isLeft ? "---" : sizeStr,
                rightModifiedFormatted: isLeft ? "---" : dateStr,
                leftHash: isLeft ? hashStr : nil,
                rightHash: isLeft ? nil : hashStr,
                leftURL: isLeft ? fileURL : nil,
                rightURL: isLeft ? nil : fileURL
            )
            results.append(row)
        }

        return Self.sortFolderRows(results)
    }

    public func scanDirectories() async {
        if let left = leftFolderURL, let right = rightFolderURL {
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

            self.entries = Self.sortFolderRows(mapped)
            self.totalScanned = mapped.count
            self.modifiedCount = mod
            self.addedCount = add
            self.deletedCount = del
            self.isScanning = false
        } else if let left = leftFolderURL {
            isScanning = true
            let mapped = await scanSingleDirectory(url: left, isLeft: true)
            self.entries = Self.sortFolderRows(mapped)
            self.totalScanned = mapped.count
            self.modifiedCount = 0
            self.addedCount = 0
            self.deletedCount = mapped.count
            self.isScanning = false
        } else if let right = rightFolderURL {
            isScanning = true
            let mapped = await scanSingleDirectory(url: right, isLeft: false)
            self.entries = Self.sortFolderRows(mapped)
            self.totalScanned = mapped.count
            self.modifiedCount = 0
            self.addedCount = mapped.count
            self.deletedCount = 0
            self.isScanning = false
        } else {
            self.entries = []
            self.totalScanned = 0
            self.modifiedCount = 0
            self.addedCount = 0
            self.deletedCount = 0
            self.isScanning = false
        }
    }

    public func handleRowDoubleClick(entry: AlignedFolderRow) {
        if entry.isDirectory {
            toggleFolderExpansion(path: entry.relativePath)
            return
        }
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

    private func formatByteCount(_ bytes: UInt64) -> String {
        let formatter = ByteCountFormatter()
        formatter.allowedUnits = [.useAll]
        formatter.countStyle = .file
        return formatter.string(fromByteCount: Int64(bytes))
    }
}
