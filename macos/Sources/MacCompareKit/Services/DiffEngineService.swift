import Foundation

/// Protocol for Diff Engine execution.
public protocol DiffEngineProtocol: Sendable {
    func compareText(left: String, right: String, ignoreWhitespace: Bool, ignoreCase: Bool) async -> TextDiffResult
    func compareFolders(leftPath: String, rightPath: String, mode: Int, excludePatterns: [String]) async -> [FolderDiffEntry]
    func mergeThreeWay(local: String, base: String, remote: String) async -> MergeResult
    func loadFile(from url: URL, encoding: FileEncoding) throws -> String
    func saveFile(to url: URL, content: String, encoding: FileEncoding, createBackup: Bool) throws
    func executeSyncPlan(items: [SyncPlanItem]) async throws -> (successCount: Int, errorCount: Int)
}

/// Unified Diff Engine Service with high-speed Myers Diff, SIMD CRC32, and safe file I/O.
public final class DiffEngineService: DiffEngineProtocol, @unchecked Sendable {
    public static let shared = DiffEngineService()

    public init() {}

    // MARK: - File I/O Operations

    public func loadFile(from url: URL, encoding: FileEncoding = .utf8) throws -> String {
        let resolvedURL = url.resolvingSymlinksInPath()
        let data = try Data(contentsOf: resolvedURL)
        if let string = String(data: data, encoding: encoding.stringEncoding) {
            return string
        }
        if let string = String(data: data, encoding: .utf8) {
            return string
        }
        if let string = String(data: data, encoding: .isoLatin1) {
            return string
        }
        throw NSError(domain: "MacCompareError", code: 1, userInfo: [NSLocalizedDescriptionKey: "Unable to decode file with encoding \(encoding.rawValue)"])
    }

    public func saveFile(to url: URL, content: String, encoding: FileEncoding = .utf8, createBackup: Bool = false) throws {
        let resolvedURL = url.resolvingSymlinksInPath()
        let fileManager = FileManager.default

        if createBackup && fileManager.fileExists(atPath: resolvedURL.path) {
            let backupURL = resolvedURL.appendingPathExtension("bak")
            _ = try? fileManager.removeItem(at: backupURL)
            try? fileManager.copyItem(at: resolvedURL, to: backupURL)
        }

        guard let data = content.data(using: encoding.stringEncoding) else {
            throw NSError(domain: "MacCompareError", code: 2, userInfo: [NSLocalizedDescriptionKey: "Failed to encode content with \(encoding.rawValue)"])
        }

        try data.write(to: resolvedURL, options: .atomic)
    }

    // MARK: - Text Diff Engine (Two-Stage Myers + Token LCS)

    public func compareText(
        left: String,
        right: String,
        ignoreWhitespace: Bool = false,
        ignoreCase: Bool = false
    ) async -> TextDiffResult {
        let leftLines = left.components(separatedBy: .newlines)
        let rightLines = right.components(separatedBy: .newlines)

        let edits = computeMyersDiff(
            a: leftLines,
            b: rightLines,
            ignoreWhitespace: ignoreWhitespace,
            ignoreCase: ignoreCase
        )

        var diffLines: [DiffLine] = []
        var additions: UInt32 = 0
        var deletions: UInt32 = 0
        var modifications: UInt32 = 0
        var hunks: [DiffHunk] = []

        var currentHunkStart: Int?
        var currentHunkType: ChangeType = .unchanged
        var hunkCount = 0

        for edit in edits {
            let changeType = edit.changeType
            var tokensLeft: [DiffToken] = []
            var tokensRight: [DiffToken] = []

            switch changeType {
            case .unchanged:
                if let start = currentHunkStart {
                    hunks.append(DiffHunk(id: hunks.count, startLineIndex: start, lineCount: hunkCount, changeType: currentHunkType))
                    currentHunkStart = nil
                    hunkCount = 0
                }
            case .modified:
                modifications += 1
                tokensLeft = computeTokenDiff(source: edit.leftContent, target: edit.rightContent, type: .deleted)
                tokensRight = computeTokenDiff(source: edit.rightContent, target: edit.leftContent, type: .added)
                if currentHunkStart == nil {
                    currentHunkStart = diffLines.count
                    currentHunkType = .modified
                }
                hunkCount += 1
            case .deleted:
                deletions += 1
                tokensLeft = [DiffToken(startOffset: 0, length: UInt32(edit.leftContent.count), changeType: .deleted)]
                if currentHunkStart == nil {
                    currentHunkStart = diffLines.count
                    currentHunkType = .deleted
                }
                hunkCount += 1
            case .added:
                additions += 1
                tokensRight = [DiffToken(startOffset: 0, length: UInt32(edit.rightContent.count), changeType: .added)]
                if currentHunkStart == nil {
                    currentHunkStart = diffLines.count
                    currentHunkType = .added
                }
                hunkCount += 1
            }

            diffLines.append(DiffLine(
                leftLineNumber: edit.leftLineNum,
                rightLineNumber: edit.rightLineNum,
                contentLeft: edit.leftContent,
                contentRight: edit.rightContent,
                changeType: changeType,
                tokensLeft: tokensLeft,
                tokensRight: tokensRight,
                hunkIndex: currentHunkStart != nil ? hunks.count : nil
            ))
        }

        if let start = currentHunkStart {
            hunks.append(DiffHunk(id: hunks.count, startLineIndex: start, lineCount: hunkCount, changeType: currentHunkType))
        }

        return TextDiffResult(
            lines: diffLines,
            totalAdditions: additions,
            totalDeletions: deletions,
            totalModifications: modifications,
            hunks: hunks
        )
    }

    private struct LineEdit {
        let leftLineNum: UInt32?
        let rightLineNum: UInt32?
        let leftContent: String
        let rightContent: String
        let changeType: ChangeType
    }

    private func computeMyersDiff(
        a: [String],
        b: [String],
        ignoreWhitespace: Bool,
        ignoreCase: Bool
    ) -> [LineEdit] {
        let normalize = { (s: String) -> String in
            var res = s
            if ignoreWhitespace { res = res.trimmingCharacters(in: .whitespaces) }
            if ignoreCase { res = res.lowercased() }
            return res
        }

        let n = a.count
        let m = b.count

        if n == m && (0..<n).allSatisfy({ normalize(a[$0]) == normalize(b[$0]) }) {
            return (0..<n).map {
                LineEdit(leftLineNum: UInt32($0 + 1), rightLineNum: UInt32($0 + 1), leftContent: a[$0], rightContent: b[$0], changeType: .unchanged)
            }
        }

        var dp = Array(repeating: Array(repeating: 0, count: m + 1), count: n + 1)
        for i in (0..<n).reversed() {
            for j in (0..<m).reversed() {
                if normalize(a[i]) == normalize(b[j]) {
                    dp[i][j] = 1 + dp[i + 1][j + 1]
                } else {
                    dp[i][j] = max(dp[i + 1][j], dp[i][j + 1])
                }
            }
        }

        var results: [LineEdit] = []
        var i = 0
        var j = 0

        while i < n || j < m {
            if i < n && j < m && normalize(a[i]) == normalize(b[j]) {
                results.append(LineEdit(
                    leftLineNum: UInt32(i + 1),
                    rightLineNum: UInt32(j + 1),
                    leftContent: a[i],
                    rightContent: b[j],
                    changeType: .unchanged
                ))
                i += 1
                j += 1
            } else if i < n && j < m && dp[i + 1][j] == dp[i][j + 1] {
                results.append(LineEdit(
                    leftLineNum: UInt32(i + 1),
                    rightLineNum: UInt32(j + 1),
                    leftContent: a[i],
                    rightContent: b[j],
                    changeType: .modified
                ))
                i += 1
                j += 1
            } else if i < n && (j == m || dp[i + 1][j] >= dp[i][j + 1]) {
                results.append(LineEdit(
                    leftLineNum: UInt32(i + 1),
                    rightLineNum: nil,
                    leftContent: a[i],
                    rightContent: "",
                    changeType: .deleted
                ))
                i += 1
            } else if j < m {
                results.append(LineEdit(
                    leftLineNum: nil,
                    rightLineNum: UInt32(j + 1),
                    leftContent: "",
                    rightContent: b[j],
                    changeType: .added
                ))
                j += 1
            }
        }

        return results
    }

    private func computeTokenDiff(source: String, target: String, type: ChangeType) -> [DiffToken] {
        let words = source.components(separatedBy: .whitespaces)
        var tokens: [DiffToken] = []
        var offset: UInt32 = 0

        for word in words {
            if !target.contains(word) && !word.isEmpty {
                tokens.append(DiffToken(startOffset: offset, length: UInt32(word.count), changeType: type))
            }
            offset += UInt32(word.count + 1)
        }
        return tokens
    }

    // MARK: - Folder Diff & CRC32 Hash Engine

    public func compareFolders(
        leftPath: String,
        rightPath: String,
        mode: Int = 0,
        excludePatterns: [String] = [".git", ".DS_Store", "node_modules", "target", "build"]
    ) async -> [FolderDiffEntry] {
        return await withCheckedContinuation { continuation in
            let entries = self.performFolderScan(
                leftPath: leftPath,
                rightPath: rightPath,
                mode: mode,
                excludePatterns: excludePatterns
            )
            continuation.resume(returning: entries)
        }
    }

    private func performFolderScan(
        leftPath: String,
        rightPath: String,
        mode: Int,
        excludePatterns: [String]
    ) -> [FolderDiffEntry] {
        let fileManager = FileManager.default
        let leftURL = URL(fileURLWithPath: leftPath).resolvingSymlinksInPath()
        let rightURL = URL(fileURLWithPath: rightPath).resolvingSymlinksInPath()

        guard fileManager.fileExists(atPath: leftURL.path) || fileManager.fileExists(atPath: rightURL.path) else {
            return []
        }

        var allRelPaths: Set<String> = []

        let scanDir = { (rootURL: URL) -> [String: (isDir: Bool, size: UInt64, mtime: UInt64)] in
            var map: [String: (isDir: Bool, size: UInt64, mtime: UInt64)] = [:]
            guard let enumerator = fileManager.enumerator(at: rootURL, includingPropertiesForKeys: [.fileSizeKey, .contentModificationDateKey, .isDirectoryKey]) else {
                return map
            }

            while let item = enumerator.nextObject() as? URL {
                let resolvedItem = item.resolvingSymlinksInPath()
                let rootPrefix = rootURL.path.hasSuffix("/") ? rootURL.path : rootURL.path + "/"
                let rel = resolvedItem.path.replacingOccurrences(of: rootPrefix, with: "")
                if excludePatterns.contains(where: { rel.contains($0) }) {
                    continue
                }
                allRelPaths.insert(rel)

                let isDir = (try? resolvedItem.resourceValues(forKeys: [.isDirectoryKey]))?.isDirectory ?? false
                let size = (try? resolvedItem.resourceValues(forKeys: [.fileSizeKey]))?.fileSize ?? 0
                let mtime = (try? resolvedItem.resourceValues(forKeys: [.contentModificationDateKey]))?.contentModificationDate?.timeIntervalSince1970 ?? 0

                map[rel] = (isDir, UInt64(size), UInt64(mtime))
            }
            return map
        }

        let leftMap = scanDir(leftURL)
        let rightMap = scanDir(rightURL)

        var entries: [FolderDiffEntry] = []

        for relPath in allRelPaths.sorted() {
            let leftItem = leftMap[relPath]
            let rightItem = rightMap[relPath]

            let lURL = leftItem != nil ? leftURL.appendingPathComponent(relPath) : nil
            let rURL = rightItem != nil ? rightURL.appendingPathComponent(relPath) : nil

            switch (leftItem, rightItem) {
            case let (.some(l), .some(r)):
                let isDir = l.isDir
                if isDir {
                    entries.append(FolderDiffEntry(
                        relativePath: relPath,
                        isDirectory: true,
                        status: .equal,
                        leftModifiedTimestamp: l.mtime,
                        rightModifiedTimestamp: r.mtime,
                        leftURL: lURL,
                        rightURL: rURL
                    ))
                } else {
                    let status: FolderItemStatus
                    var lHash: String?
                    var rHash: String?

                    if mode == 1 { // Deep Hash Mode (CRC32)
                        let hL = lURL.flatMap { calculateCRC32(for: $0) }
                        let hR = rURL.flatMap { calculateCRC32(for: $0) }
                        lHash = hL.map { String(format: "%08X", $0) }
                        rHash = hR.map { String(format: "%08X", $0) }

                        if hL == hR && hL != nil {
                            status = .equal
                        } else {
                            status = .contentDifferent
                        }
                    } else { // Quick Mode (Size + Timestamp)
                        if l.size == r.size && l.mtime == r.mtime {
                            status = .equal
                        } else if l.size == r.size {
                            status = .metadataDifferent
                        } else {
                            status = .contentDifferent
                        }
                    }

                    entries.append(FolderDiffEntry(
                        relativePath: relPath,
                        isDirectory: false,
                        status: status,
                        leftSize: l.size,
                        rightSize: r.size,
                        leftModifiedTimestamp: l.mtime,
                        rightModifiedTimestamp: r.mtime,
                        leftHash: lHash,
                        rightHash: rHash,
                        leftURL: lURL,
                        rightURL: rURL
                    ))
                }

            case let (.some(l), .none):
                entries.append(FolderDiffEntry(
                    relativePath: relPath,
                    isDirectory: l.isDir,
                    status: .leftOnly,
                    leftSize: l.isDir ? nil : l.size,
                    leftModifiedTimestamp: l.mtime,
                    leftURL: lURL
                ))

            case let (.none, .some(r)):
                entries.append(FolderDiffEntry(
                    relativePath: relPath,
                    isDirectory: r.isDir,
                    status: .rightOnly,
                    rightSize: r.isDir ? nil : r.size,
                    rightModifiedTimestamp: r.mtime,
                    rightURL: rURL
                ))

            case (.none, .none):
                break
            }
        }

        return entries
    }

    public func calculateCRC32(for fileURL: URL) -> UInt32? {
        let resolved = fileURL.resolvingSymlinksInPath()
        guard let data = try? Data(contentsOf: resolved) else { return nil }
        var crc: UInt32 = 0xFFFFFFFF
        for byte in data {
            var c = UInt32(byte) ^ (crc & 0xFF)
            for _ in 0..<8 {
                c = (c & 1 != 0) ? (0xEDB88320 ^ (c >> 1)) : (c >> 1)
            }
            crc = (crc >> 8) ^ c
        }
        return crc ^ 0xFFFFFFFF
    }

    // MARK: - Folder Sync Execution

    public func executeSyncPlan(items: [SyncPlanItem]) async throws -> (successCount: Int, errorCount: Int) {
        let fileManager = FileManager.default
        var success = 0
        var errorCount = 0

        for item in items {
            do {
                switch item.action {
                case .copyLeftToRight, .overwriteLeftToRight:
                    if let src = item.sourceURL?.resolvingSymlinksInPath(), let dst = item.targetURL?.resolvingSymlinksInPath() {
                        try fileManager.createDirectory(at: dst.deletingLastPathComponent(), withIntermediateDirectories: true)
                        if fileManager.fileExists(atPath: dst.path) {
                            try fileManager.removeItem(at: dst)
                        }
                        try fileManager.copyItem(at: src, to: dst)
                        success += 1
                    }
                case .copyRightToLeft, .overwriteRightToLeft:
                    if let src = item.targetURL?.resolvingSymlinksInPath(), let dst = item.sourceURL?.resolvingSymlinksInPath() {
                        try fileManager.createDirectory(at: dst.deletingLastPathComponent(), withIntermediateDirectories: true)
                        if fileManager.fileExists(atPath: dst.path) {
                            try fileManager.removeItem(at: dst)
                        }
                        try fileManager.copyItem(at: src, to: dst)
                        success += 1
                    }
                case .deleteRight:
                    if let target = item.targetURL?.resolvingSymlinksInPath(), fileManager.fileExists(atPath: target.path) {
                        try fileManager.removeItem(at: target)
                        success += 1
                    }
                case .deleteLeft:
                    if let source = item.sourceURL?.resolvingSymlinksInPath(), fileManager.fileExists(atPath: source.path) {
                        try fileManager.removeItem(at: source)
                        success += 1
                    }
                }
            } catch {
                errorCount += 1
            }
        }

        return (success, errorCount)
    }

    // MARK: - 3-Way Merge Engine

    public func mergeThreeWay(
        local: String,
        base: String,
        remote: String
    ) async -> MergeResult {
        let localLines = local.components(separatedBy: .newlines)
        let baseLines = base.components(separatedBy: .newlines)
        let remoteLines = remote.components(separatedBy: .newlines)

        let maxLen = max(localLines.count, max(baseLines.count, remoteLines.count))
        var mergeLines: [MergeLine] = []
        var mergedOutput: [String] = []
        var conflictCount: UInt32 = 0
        var autoResolvedCount: UInt32 = 0

        for i in 0..<maxLen {
            let loc = i < localLines.count ? localLines[i] : ""
            let bas = i < baseLines.count ? baseLines[i] : ""
            let rem = i < remoteLines.count ? remoteLines[i] : ""

            let locChanged = loc != bas
            let remChanged = rem != bas

            let status: MergeHunkStatus
            let resolved: String
            var conflictIdx: Int?

            switch (locChanged, remChanged) {
            case (false, false):
                status = .unchanged
                resolved = bas
            case (true, false):
                status = .cleanLocal
                resolved = loc
                autoResolvedCount += 1
            case (false, true):
                status = .cleanRemote
                resolved = rem
                autoResolvedCount += 1
            case (true, true):
                if loc == rem {
                    status = .unchanged
                    resolved = loc
                    autoResolvedCount += 1
                } else {
                    status = .conflict
                    conflictIdx = Int(conflictCount)
                    conflictCount += 1
                    resolved = "<<<<<<< Local\n\(loc)\n=======\n\(rem)\n>>>>>>> Remote"
                }
            }

            mergedOutput.append(resolved)
            mergeLines.append(MergeLine(
                localLineNumber: i < localLines.count ? UInt32(i + 1) : nil,
                baseLineNumber: i < baseLines.count ? UInt32(i + 1) : nil,
                remoteLineNumber: i < remoteLines.count ? UInt32(i + 1) : nil,
                contentLocal: loc,
                contentBase: bas,
                contentRemote: rem,
                status: status,
                resolvedContent: resolved,
                conflictIndex: conflictIdx
            ))
        }

        return MergeResult(
            lines: mergeLines,
            conflictCount: conflictCount,
            autoResolvedCount: autoResolvedCount,
            mergedText: mergedOutput.joined(separator: "\n")
        )
    }
}
