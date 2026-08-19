import Foundation

/// Protocol for Diff Engine execution.
public protocol DiffEngineProtocol: Sendable {
    func compareText(left: String, right: String, ignoreWhitespace: Bool, ignoreCase: Bool) async -> TextDiffResult
    func compareFolders(leftPath: String, rightPath: String, mode: Int) async -> [FolderDiffEntry]
    func mergeThreeWay(local: String, base: String, remote: String) async -> MergeResult
}

/// Unified Diff Engine Service with Swift fallback and C-ABI bridge support.
public final class DiffEngineService: DiffEngineProtocol, @unchecked Sendable {
    public static let shared = DiffEngineService()

    public init() {}

    public func compareText(
        left: String,
        right: String,
        ignoreWhitespace: Bool = false,
        ignoreCase: Bool = false
    ) async -> TextDiffResult {
        let leftLines = left.components(separatedBy: .newlines)
        let rightLines = right.components(separatedBy: .newlines)

        var diffLines: [DiffLine] = []
        var additions: UInt32 = 0
        var deletions: UInt32 = 0
        var modifications: UInt32 = 0

        var lIdx = 0
        var rIdx = 0

        while lIdx < leftLines.count || rIdx < rightLines.count {
            let leftLine = lIdx < leftLines.count ? leftLines[lIdx] : nil
            let rightLine = rIdx < rightLines.count ? rightLines[rIdx] : nil

            switch (leftLine, rightLine) {
            case let (.some(l), .some(r)):
                let isMatch: Bool
                if ignoreWhitespace {
                    isMatch = l.trimmingCharacters(in: .whitespaces) == r.trimmingCharacters(in: .whitespaces)
                } else if ignoreCase {
                    isMatch = l.lowercased() == r.lowercased()
                } else {
                    isMatch = l == r
                }

                if isMatch {
                    diffLines.append(DiffLine(
                        leftLineNumber: UInt32(lIdx + 1),
                        rightLineNumber: UInt32(rIdx + 1),
                        contentLeft: l,
                        contentRight: r,
                        changeType: .unchanged
                    ))
                    lIdx += 1
                    rIdx += 1
                } else {
                    // Check token differences
                    let tokensLeft = computeTokens(source: l, target: r, type: .deleted)
                    let tokensRight = computeTokens(source: r, target: l, type: .added)

                    diffLines.append(DiffLine(
                        leftLineNumber: UInt32(lIdx + 1),
                        rightLineNumber: UInt32(rIdx + 1),
                        contentLeft: l,
                        contentRight: r,
                        changeType: .modified,
                        tokensLeft: tokensLeft,
                        tokensRight: tokensRight
                    ))
                    modifications += 1
                    lIdx += 1
                    rIdx += 1
                }

            case let (.some(l), .none):
                diffLines.append(DiffLine(
                    leftLineNumber: UInt32(lIdx + 1),
                    rightLineNumber: nil,
                    contentLeft: l,
                    contentRight: "",
                    changeType: .deleted,
                    tokensLeft: [DiffToken(startOffset: 0, length: UInt32(l.count), changeType: .deleted)]
                ))
                deletions += 1
                lIdx += 1

            case let (.none, .some(r)):
                diffLines.append(DiffLine(
                    leftLineNumber: nil,
                    rightLineNumber: UInt32(rIdx + 1),
                    contentLeft: "",
                    contentRight: r,
                    changeType: .added,
                    tokensRight: [DiffToken(startOffset: 0, length: UInt32(r.count), changeType: .added)]
                ))
                additions += 1
                rIdx += 1

            case (.none, .none):
                break
            }
        }

        return TextDiffResult(
            lines: diffLines,
            totalAdditions: additions,
            totalDeletions: deletions,
            totalModifications: modifications
        )
    }

    private func computeTokens(source: String, target: String, type: ChangeType) -> [DiffToken] {
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

    public func compareFolders(
        leftPath: String,
        rightPath: String,
        mode: Int = 0
    ) async -> [FolderDiffEntry] {
        return await withCheckedContinuation { continuation in
            let entries = self.performFolderScan(leftPath: leftPath, rightPath: rightPath)
            continuation.resume(returning: entries)
        }
    }

    private func performFolderScan(leftPath: String, rightPath: String) -> [FolderDiffEntry] {
        let fileManager = FileManager.default
        let leftURL = URL(fileURLWithPath: leftPath)
        let rightURL = URL(fileURLWithPath: rightPath)

        guard let leftEnum = fileManager.enumerator(at: leftURL, includingPropertiesForKeys: [.fileSizeKey, .contentModificationDateKey, .isDirectoryKey]) else {
            return []
        }

        var results: [FolderDiffEntry] = []

        while let item = leftEnum.nextObject() as? URL {
            let relPath = item.path.replacingOccurrences(of: leftURL.path + "/", with: "")
            if relPath.contains(".git") || relPath.contains(".DS_Store") { continue }

            let rURL = rightURL.appendingPathComponent(relPath)
            let isDir = (try? item.resourceValues(forKeys: [.isDirectoryKey]))?.isDirectory ?? false
            let lSize = (try? item.resourceValues(forKeys: [.fileSizeKey]))?.fileSize
            let lDate = (try? item.resourceValues(forKeys: [.contentModificationDateKey]))?.contentModificationDate

            if fileManager.fileExists(atPath: rURL.path) {
                let rSize = (try? rURL.resourceValues(forKeys: [.fileSizeKey]))?.fileSize
                let rDate = (try? rURL.resourceValues(forKeys: [.contentModificationDateKey]))?.contentModificationDate

                let status: FolderItemStatus
                if lSize == rSize && lDate == rDate {
                    status = .equal
                } else if lSize == rSize {
                    status = .metadataDifferent
                } else {
                    status = .contentDifferent
                }

                results.append(FolderDiffEntry(
                    relativePath: relPath,
                    isDirectory: isDir,
                    status: status,
                    leftSize: lSize.map { UInt64($0) },
                    rightSize: rSize.map { UInt64($0) },
                    leftModifiedTimestamp: lDate.map { UInt64($0.timeIntervalSince1970) },
                    rightModifiedTimestamp: rDate.map { UInt64($0.timeIntervalSince1970) }
                ))
            } else {
                results.append(FolderDiffEntry(
                    relativePath: relPath,
                    isDirectory: isDir,
                    status: .leftOnly,
                    leftSize: lSize.map { UInt64($0) },
                    leftModifiedTimestamp: lDate.map { UInt64($0.timeIntervalSince1970) }
                ))
            }
        }

        return results
    }

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
                resolvedContent: resolved
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
