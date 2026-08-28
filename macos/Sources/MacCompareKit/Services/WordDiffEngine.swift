import Foundation
import AppKit

/// Core Diff Engine specialized for Word document structure, formatting, tables, and metadata comparisons.
public final class WordDiffEngine: Sendable {
    public static let shared = WordDiffEngine()

    public init() {}

    /// Compare two Word Document Models.
    public func compareDocuments(
        left: WordDocumentModel,
        right: WordDocumentModel,
        ignoreWhitespace: Bool = false,
        ignoreFormatting: Bool = false
    ) async -> WordDiffResult {
        // 1. Paragraph & Media Comparison
        let (blocks, adds, dels, mods, formatChanges, mediaChanges) = computeParagraphDiff(
            leftParas: left.paragraphs,
            rightParas: right.paragraphs,
            ignoreWhitespace: ignoreWhitespace,
            ignoreFormatting: ignoreFormatting
        )

        // 2. Table Comparison
        let tableDiffs = computeTableDiffs(leftTables: left.tables, rightTables: right.tables)

        // 3. Metadata Comparison
        let metadataDiffs = computeMetadataDiffs(left: left.metadata, right: right.metadata)

        return WordDiffResult(
            blocks: blocks,
            tableDiffs: tableDiffs,
            metadataDiffs: metadataDiffs,
            totalAdditions: adds,
            totalDeletions: dels,
            totalModifications: mods,
            totalFormatChanges: formatChanges,
            totalMediaChanges: mediaChanges
        )
    }

    // MARK: - Paragraph Diff & Alignment

    private func computeParagraphDiff(
        leftParas: [WordParagraph],
        rightParas: [WordParagraph],
        ignoreWhitespace: Bool,
        ignoreFormatting: Bool
    ) -> (blocks: [WordDiffBlock], additions: Int, deletions: Int, modifications: Int, formatChanges: Int, mediaChanges: Int) {
        let leftTexts = leftParas.map { sanitizeText($0.text, ignoreWhitespace: ignoreWhitespace) }
        let rightTexts = rightParas.map { sanitizeText($0.text, ignoreWhitespace: ignoreWhitespace) }

        // Compute LCS on paragraphs
        let edits = computeMyersEdits(a: leftTexts, b: rightTexts)

        var blocks: [WordDiffBlock] = []
        var adds = 0
        var dels = 0
        var mods = 0
        var formatChanges = 0
        var mediaChanges = 0
        var blockIdx = 0

        for edit in edits {
            switch edit {
            case .equal(let leftIdx, let rightIdx):
                let lPara = leftParas[leftIdx]
                let rPara = rightParas[rightIdx]

                var tableDiff: WordTableDiffResult? = nil
                var isTableDiffModified = false
                if lPara.isTableBlock || rPara.isTableBlock {
                    let td = compareSingleTable(leftT: lPara.table, rightT: rPara.table, tableIndex: blockIdx + 1)
                    tableDiff = td
                    if td.changeType != .unchanged {
                        isTableDiffModified = true
                    }
                }

                let formatDiffs = ignoreFormatting ? [] : detectFormatDifferences(left: lPara, right: rPara)
                let isFormatOnly = !formatDiffs.isEmpty

                let mediaDiffs = compareParagraphMedia(leftMedia: lPara.mediaItems, rightMedia: rPara.mediaItems)
                let hasMediaChanges = mediaDiffs.contains { $0.changeType != .unchanged }

                if isFormatOnly {
                    formatChanges += 1
                }
                if hasMediaChanges {
                    mediaChanges += mediaDiffs.filter { $0.changeType != .unchanged }.count
                }
                if isTableDiffModified {
                    mods += 1
                }

                let effectiveChangeType: ChangeType = {
                    if isTableDiffModified || hasMediaChanges || isFormatOnly {
                        return .modified
                    }
                    return .unchanged
                }()

                let block = WordDiffBlock(
                    blockIndex: blockIdx,
                    leftParagraph: lPara,
                    rightParagraph: rPara,
                    changeType: effectiveChangeType,
                    formatDifferences: formatDiffs,
                    mediaDifferences: mediaDiffs,
                    tableDiff: tableDiff,
                    isFormatOnly: isFormatOnly
                )
                blocks.append(block)
                blockIdx += 1

            case .delete(let leftIdx):
                let lPara = leftParas[leftIdx]
                dels += 1
                var tableDiff: WordTableDiffResult? = nil
                if let lTable = lPara.table {
                    tableDiff = compareSingleTable(leftT: lTable, rightT: nil, tableIndex: blockIdx + 1)
                }

                let mediaDiffs = lPara.mediaItems.map {
                    WordMediaDiffItem(changeType: .deleted, mediaType: $0.mediaType, leftMedia: $0, changeDescriptions: ["媒体资源已被移除"])
                }
                if !mediaDiffs.isEmpty {
                    mediaChanges += mediaDiffs.count
                }
                let block = WordDiffBlock(
                    blockIndex: blockIdx,
                    leftParagraph: lPara,
                    rightParagraph: nil,
                    changeType: .deleted,
                    mediaDifferences: mediaDiffs,
                    tableDiff: tableDiff
                )
                blocks.append(block)
                blockIdx += 1

            case .insert(let rightIdx):
                let rPara = rightParas[rightIdx]
                adds += 1
                var tableDiff: WordTableDiffResult? = nil
                if let rTable = rPara.table {
                    tableDiff = compareSingleTable(leftT: nil, rightT: rTable, tableIndex: blockIdx + 1)
                }

                let mediaDiffs = rPara.mediaItems.map {
                    WordMediaDiffItem(changeType: .added, mediaType: $0.mediaType, rightMedia: $0, changeDescriptions: ["新增媒体资源"])
                }
                if !mediaDiffs.isEmpty {
                    mediaChanges += mediaDiffs.count
                }
                let block = WordDiffBlock(
                    blockIndex: blockIdx,
                    leftParagraph: nil,
                    rightParagraph: rPara,
                    changeType: .added,
                    mediaDifferences: mediaDiffs,
                    tableDiff: tableDiff
                )
                blocks.append(block)
                blockIdx += 1

            case .modify(let leftIdx, let rightIdx):
                let lPara = leftParas[leftIdx]
                let rPara = rightParas[rightIdx]
                mods += 1

                var tableDiff: WordTableDiffResult? = nil
                if lPara.isTableBlock || rPara.isTableBlock {
                    tableDiff = compareSingleTable(leftT: lPara.table, rightT: rPara.table, tableIndex: blockIdx + 1)
                }

                // Fine token-level character diff inside paragraph
                let (tokensLeft, tokensRight) = computeTokenDiff(
                    left: lPara.text,
                    right: rPara.text,
                    ignoreWhitespace: ignoreWhitespace
                )

                let formatDiffs = ignoreFormatting ? [] : detectFormatDifferences(left: lPara, right: rPara)
                let mediaDiffs = compareParagraphMedia(leftMedia: lPara.mediaItems, rightMedia: rPara.mediaItems)
                let hasMediaDiffs = mediaDiffs.contains { $0.changeType != .unchanged }

                if hasMediaDiffs {
                    mediaChanges += mediaDiffs.filter { $0.changeType != .unchanged }.count
                }

                let block = WordDiffBlock(
                    blockIndex: blockIdx,
                    leftParagraph: lPara,
                    rightParagraph: rPara,
                    changeType: .modified,
                    tokensLeft: tokensLeft,
                    tokensRight: tokensRight,
                    formatDifferences: formatDiffs,
                    mediaDifferences: mediaDiffs,
                    tableDiff: tableDiff,
                    isFormatOnly: false
                )
                blocks.append(block)
                blockIdx += 1
            }
        }

        return (blocks, adds, dels, mods, formatChanges, mediaChanges)
    }

    // MARK: - Multimedia Diff Detection

    private func compareParagraphMedia(leftMedia: [WordMediaItem], rightMedia: [WordMediaItem]) -> [WordMediaDiffItem] {
        var diffs: [WordMediaDiffItem] = []
        let maxCount = max(leftMedia.count, rightMedia.count)

        for i in 0..<maxCount {
            if i < leftMedia.count && i < rightMedia.count {
                let l = leftMedia[i]
                let r = rightMedia[i]

                if l.hashSHA256 == r.hashSHA256 {
                    diffs.append(WordMediaDiffItem(
                        changeType: .unchanged,
                        mediaType: l.mediaType,
                        leftMedia: l,
                        rightMedia: r
                    ))
                } else {
                    var desc: [String] = ["媒体资源内容已更改"]
                    if l.fileSize != r.fileSize {
                        desc.append("大小: \(l.formattedSize) → \(r.formattedSize)")
                    }
                    if let lw = l.widthPoints, let lh = l.heightPoints,
                       let rw = r.widthPoints, let rh = r.heightPoints {
                        if Int(lw) != Int(rw) || Int(lh) != Int(rh) {
                            desc.append("尺寸: \(Int(lw))×\(Int(lh))pt → \(Int(rw))×\(Int(rh))pt")
                        }
                    }
                    diffs.append(WordMediaDiffItem(
                        changeType: .modified,
                        mediaType: r.mediaType,
                        leftMedia: l,
                        rightMedia: r,
                        changeDescriptions: desc
                    ))
                }
            } else if i < leftMedia.count {
                let l = leftMedia[i]
                diffs.append(WordMediaDiffItem(
                    changeType: .deleted,
                    mediaType: l.mediaType,
                    leftMedia: l,
                    rightMedia: nil,
                    changeDescriptions: ["\(l.mediaType.rawValue) 已被移除 (\(l.fileName))"]
                ))
            } else if i < rightMedia.count {
                let r = rightMedia[i]
                diffs.append(WordMediaDiffItem(
                    changeType: .added,
                    mediaType: r.mediaType,
                    leftMedia: nil,
                    rightMedia: r,
                    changeDescriptions: ["新增 \(r.mediaType.rawValue) (\(r.fileName))"]
                ))
            }
        }

        return diffs
    }

    // MARK: - Format & Style Diff Detection

    private func detectFormatDifferences(left: WordParagraph, right: WordParagraph) -> [FormatDiffItem] {
        var items: [FormatDiffItem] = []

        // Heading Level change
        if left.headingLevel != right.headingLevel {
            let leftStr = left.headingLevel != nil ? "Heading \(left.headingLevel!)" : "Body Text"
            let rightStr = right.headingLevel != nil ? "Heading \(right.headingLevel!)" : "Body Text"
            items.append(FormatDiffItem(propertyName: "Heading Level", oldValue: leftStr, newValue: rightStr))
        }

        // Paragraph Alignment
        if left.style.alignment != right.style.alignment {
            items.append(FormatDiffItem(
                propertyName: "Alignment",
                oldValue: alignmentName(left.style.alignment),
                newValue: alignmentName(right.style.alignment)
            ))
        }

        // Check primary run styles
        let leftRuns = left.runs
        let rightRuns = right.runs

        let leftIsBold = leftRuns.contains { $0.isBold }
        let rightIsBold = rightRuns.contains { $0.isBold }
        if leftIsBold != rightIsBold {
            items.append(FormatDiffItem(
                propertyName: "Bold",
                oldValue: leftIsBold ? "Bold" : "Regular",
                newValue: rightIsBold ? "Bold" : "Regular"
            ))
        }

        let leftIsItalic = leftRuns.contains { $0.isItalic }
        let rightIsItalic = rightRuns.contains { $0.isItalic }
        if leftIsItalic != rightIsItalic {
            items.append(FormatDiffItem(
                propertyName: "Italic",
                oldValue: leftIsItalic ? "Italic" : "Regular",
                newValue: rightIsItalic ? "Italic" : "Regular"
            ))
        }

        let leftIsUnderline = leftRuns.contains { $0.isUnderline }
        let rightIsUnderline = rightRuns.contains { $0.isUnderline }
        if leftIsUnderline != rightIsUnderline {
            items.append(FormatDiffItem(
                propertyName: "Underline",
                oldValue: leftIsUnderline ? "Underline" : "None",
                newValue: rightIsUnderline ? "Underline" : "None"
            ))
        }

        // Font Size comparison
        let leftSize = leftRuns.compactMap { $0.fontSize }.first
        let rightSize = rightRuns.compactMap { $0.fontSize }.first
        if let lSize = leftSize, let rSize = rightSize, abs(lSize - rSize) > 0.5 {
            items.append(FormatDiffItem(
                propertyName: "Font Size",
                oldValue: String(format: "%.1f pt", lSize),
                newValue: String(format: "%.1f pt", rSize)
            ))
        }

        // Font Color comparison
        let leftColor = leftRuns.compactMap { $0.fontColorHex }.first
        let rightColor = rightRuns.compactMap { $0.fontColorHex }.first
        if let lColor = leftColor, let rColor = rightColor, lColor.lowercased() != rColor.lowercased() {
            items.append(FormatDiffItem(
                propertyName: "Font Color",
                oldValue: lColor,
                newValue: rColor
            ))
        }

        return items
    }

    private func alignmentName(_ align: NSTextAlignment) -> String {
        switch align {
        case .left: return "Left"
        case .right: return "Right"
        case .center: return "Center"
        case .justified: return "Justified"
        default: return "Natural"
        }
    }

    // MARK: - Token Diff for Words/Characters

    private func computeTokenDiff(left: String, right: String, ignoreWhitespace: Bool) -> (tokensLeft: [DiffToken], tokensRight: [DiffToken]) {
        let leftChars = Array(left)
        let rightChars = Array(right)

        var lTokens: [DiffToken] = []
        var rTokens: [DiffToken] = []

        let n = leftChars.count
        let m = rightChars.count

        if n == 0 && m == 0 { return ([], []) }
        if n == 0 {
            return ([], [DiffToken(startOffset: 0, length: UInt32(m), changeType: .added)])
        }
        if m == 0 {
            return ([DiffToken(startOffset: 0, length: UInt32(n), changeType: .deleted)], [])
        }

        // Longest Common Subsequence of characters
        var dp = Array(repeating: Array(repeating: 0, count: m + 1), count: n + 1)
        for i in 1...n {
            for j in 1...m {
                if leftChars[i - 1] == rightChars[j - 1] {
                    dp[i][j] = dp[i - 1][j - 1] + 1
                } else {
                    dp[i][j] = max(dp[i - 1][j], dp[i][j - 1])
                }
            }
        }

        var i = n
        var j = m
        var leftMatches = Set<Int>()
        var rightMatches = Set<Int>()

        while i > 0 && j > 0 {
            if leftChars[i - 1] == rightChars[j - 1] {
                leftMatches.insert(i - 1)
                rightMatches.insert(j - 1)
                i -= 1
                j -= 1
            } else if dp[i - 1][j] >= dp[i][j - 1] {
                i -= 1
            } else {
                j -= 1
            }
        }

        // Group left differences
        var currentStart: Int? = nil
        for idx in 0..<n {
            if !leftMatches.contains(idx) {
                if currentStart == nil { currentStart = idx }
            } else {
                if let start = currentStart {
                    lTokens.append(DiffToken(startOffset: UInt32(start), length: UInt32(idx - start), changeType: .deleted))
                    currentStart = nil
                }
            }
        }
        if let start = currentStart {
            lTokens.append(DiffToken(startOffset: UInt32(start), length: UInt32(n - start), changeType: .deleted))
        }

        // Group right differences
        currentStart = nil
        for idx in 0..<m {
            if !rightMatches.contains(idx) {
                if currentStart == nil { currentStart = idx }
            } else {
                if let start = currentStart {
                    rTokens.append(DiffToken(startOffset: UInt32(start), length: UInt32(idx - start), changeType: .added))
                    currentStart = nil
                }
            }
        }
        if let start = currentStart {
            rTokens.append(DiffToken(startOffset: UInt32(start), length: UInt32(m - start), changeType: .added))
        }

        return (lTokens, rTokens)
    }

    // MARK: - Table Diff

    private func compareSingleTable(leftT: WordTable?, rightT: WordTable?, tableIndex: Int = 1) -> WordTableDiffResult {
        let maxRows = max(leftT?.rowCount ?? 0, rightT?.rowCount ?? 0)
        let maxCols = max(leftT?.columnCount ?? 0, rightT?.columnCount ?? 0)

        var cellDiffs: [WordTableCellDiff] = []
        var hasDiff = false

        for r in 0..<maxRows {
            for c in 0..<maxCols {
                let lCell = (r < (leftT?.rows.count ?? 0) && c < (leftT?.rows[r].cells.count ?? 0)) ? leftT?.rows[r].cells[c] : nil
                let rCell = (r < (rightT?.rows.count ?? 0) && c < (rightT?.rows[r].cells.count ?? 0)) ? rightT?.rows[r].cells[c] : nil

                let lText = lCell?.text ?? ""
                let rText = rCell?.text ?? ""

                var cType: ChangeType = .unchanged
                var tokensL: [DiffToken] = []
                var tokensR: [DiffToken] = []

                if lCell == nil && rCell != nil {
                    cType = .added
                    hasDiff = true
                } else if lCell != nil && rCell == nil {
                    cType = .deleted
                    hasDiff = true
                } else if lText != rText {
                    cType = .modified
                    hasDiff = true
                    let (tl, tr) = computeTokenDiff(left: lText, right: rText, ignoreWhitespace: false)
                    tokensL = tl
                    tokensR = tr
                }

                cellDiffs.append(WordTableCellDiff(
                    rowIndex: r,
                    colIndex: c,
                    leftCell: lCell,
                    rightCell: rCell,
                    changeType: cType,
                    tokensLeft: tokensL,
                    tokensRight: tokensR
                ))
            }
        }

        let overallType: ChangeType
        if leftT == nil && rightT != nil {
            overallType = .added
        } else if leftT != nil && rightT == nil {
            overallType = .deleted
        } else if hasDiff {
            overallType = .modified
        } else {
            overallType = .unchanged
        }

        return WordTableDiffResult(
            tableIndex: tableIndex,
            leftTable: leftT,
            rightTable: rightT,
            cellDiffs: cellDiffs,
            maxRows: maxRows,
            maxCols: maxCols,
            changeType: overallType
        )
    }

    private func computeTableDiffs(leftTables: [WordTable], rightTables: [WordTable]) -> [WordTableDiffResult] {
        var results: [WordTableDiffResult] = []
        let maxCount = max(leftTables.count, rightTables.count)

        for i in 0..<maxCount {
            let leftT = i < leftTables.count ? leftTables[i] : nil
            let rightT = i < rightTables.count ? rightTables[i] : nil
            let res = compareSingleTable(leftT: leftT, rightT: rightT, tableIndex: i + 1)
            results.append(res)
        }

        return results
    }

    // MARK: - Metadata Diff

    private func computeMetadataDiffs(left: WordMetadata, right: WordMetadata) -> [MetadataDiffItem] {
        var items: [MetadataDiffItem] = []

        items.append(MetadataDiffItem(fieldName: "Title", leftValue: left.title ?? "(None)", rightValue: right.title ?? "(None)"))
        items.append(MetadataDiffItem(fieldName: "Author / Creator", leftValue: left.author ?? "(Unknown)", rightValue: right.author ?? "(Unknown)"))
        items.append(MetadataDiffItem(fieldName: "Last Modified By", leftValue: left.lastModifiedBy ?? "(Unknown)", rightValue: right.lastModifiedBy ?? "(Unknown)"))
        items.append(MetadataDiffItem(fieldName: "Created Date", leftValue: left.createdAt ?? "-", rightValue: right.createdAt ?? "-"))
        items.append(MetadataDiffItem(fieldName: "Modified Date", leftValue: left.modifiedAt ?? "-", rightValue: right.modifiedAt ?? "-"))
        items.append(MetadataDiffItem(fieldName: "Revision", leftValue: left.revision ?? "1", rightValue: right.revision ?? "1"))
        items.append(MetadataDiffItem(fieldName: "Word Count", leftValue: "\(left.wordCount)", rightValue: "\(right.wordCount)"))
        items.append(MetadataDiffItem(fieldName: "Paragraphs", leftValue: "\(left.paragraphCount)", rightValue: "\(right.paragraphCount)"))
        items.append(MetadataDiffItem(fieldName: "Tables", leftValue: "\(left.tableCount)", rightValue: "\(right.tableCount)"))
        items.append(MetadataDiffItem(fieldName: "File Format", leftValue: left.fileFormat, rightValue: right.fileFormat))
        items.append(MetadataDiffItem(fieldName: "File Size", leftValue: left.fileSizeFormatted, rightValue: right.fileSizeFormatted))

        return items
    }

    // MARK: - Myers Sequence Algorithm Helper

    private enum ArrayEdit {
        case equal(Int, Int)
        case delete(Int)
        case insert(Int)
        case modify(Int, Int)
    }

    private func computeMyersEdits(a: [String], b: [String]) -> [ArrayEdit] {
        let n = a.count
        let m = b.count

        if n == 0 && m == 0 { return [] }
        if n == 0 {
            return (0..<m).map { .insert($0) }
        }
        if m == 0 {
            return (0..<n).map { .delete($0) }
        }

        // Fast path for exact identical
        if a == b {
            return (0..<n).map { .equal($0, $0) }
        }

        // Standard Dynamic Programming LCS for sequence alignment
        var dp = Array(repeating: Array(repeating: 0, count: m + 1), count: n + 1)
        for i in 1...n {
            for j in 1...m {
                if a[i - 1] == b[j - 1] {
                    dp[i][j] = dp[i - 1][j - 1] + 1
                } else {
                    dp[i][j] = max(dp[i - 1][j], dp[i][j - 1])
                }
            }
        }

        var i = n
        var j = m
        var rawEdits: [ArrayEdit] = []

        while i > 0 || j > 0 {
            if i > 0 && j > 0 && a[i - 1] == b[j - 1] {
                rawEdits.append(.equal(i - 1, j - 1))
                i -= 1
                j -= 1
            } else if j > 0 && (i == 0 || dp[i][j - 1] >= dp[i - 1][j]) {
                rawEdits.append(.insert(j - 1))
                j -= 1
            } else if i > 0 && (j == 0 || dp[i][j - 1] < dp[i - 1][j]) {
                rawEdits.append(.delete(i - 1))
                i -= 1
            }
        }

        rawEdits.reverse()

        // Chunk-based smart pairing
        var finalEdits: [ArrayEdit] = []
        var idx = 0

        while idx < rawEdits.count {
            if case .equal = rawEdits[idx] {
                finalEdits.append(rawEdits[idx])
                idx += 1
                continue
            }

            // Collect contiguous deletes and inserts
            var dels: [Int] = []
            var inss: [Int] = []

            while idx < rawEdits.count {
                if case .delete(let lIdx) = rawEdits[idx] {
                    dels.append(lIdx)
                    idx += 1
                } else if case .insert(let rIdx) = rawEdits[idx] {
                    inss.append(rIdx)
                    idx += 1
                } else {
                    break
                }
            }

            // Pair deletes and inserts based on maximum LCS similarity (threshold >= 0.55)
            var remainingDels = dels
            var remainingInss = inss
            var matchedPairs: [(d: Int, i: Int)] = []

            let pairCount = min(dels.count, inss.count)
            for _ in 0..<pairCount {
                var bestDIdx: Int? = nil
                var bestIIdx: Int? = nil
                var bestScore = 0.55

                for (di, d) in remainingDels.enumerated() {
                    for (ii, ins) in remainingInss.enumerated() {
                        let score = computeSimilarity(a[d], b[ins])
                        if score >= bestScore {
                            bestScore = score
                            bestDIdx = di
                            bestIIdx = ii
                        }
                    }
                }

                if let di = bestDIdx, let ii = bestIIdx {
                    let d = remainingDels.remove(at: di)
                    let ins = remainingInss.remove(at: ii)
                    matchedPairs.append((d: d, i: ins))
                } else {
                    break
                }
            }

            // Order matched pairs by original delete order
            matchedPairs.sort { $0.d < $1.d }

            for pair in matchedPairs {
                finalEdits.append(.modify(pair.d, pair.i))
            }

            for d in remainingDels.sorted() {
                finalEdits.append(.delete(d))
            }

            for ins in remainingInss.sorted() {
                finalEdits.append(.insert(ins))
            }
        }

        return finalEdits
    }

    private func computeSimilarity(_ s1: String, _ s2: String) -> Double {
        let t1 = s1.trimmingCharacters(in: .whitespacesAndNewlines)
        let t2 = s2.trimmingCharacters(in: .whitespacesAndNewlines)
        if t1 == t2 { return 1.0 }
        if t1.isEmpty && t2.isEmpty { return 1.0 }
        if t1.isEmpty || t2.isEmpty { return 0.0 }

        let c1 = Array(t1)
        let c2 = Array(t2)
        let n = c1.count
        let m = c2.count

        // Dynamic programming for exact LCS length
        var prev = [Int](repeating: 0, count: m + 1)
        var curr = [Int](repeating: 0, count: m + 1)

        for i in 1...n {
            for j in 1...m {
                if c1[i - 1] == c2[j - 1] {
                    curr[j] = prev[j - 1] + 1
                } else {
                    curr[j] = max(prev[j], curr[j - 1])
                }
            }
            prev = curr
        }

        let lcsLen = Double(curr[m])
        return (2.0 * lcsLen) / Double(n + m)
    }

    private func sanitizeText(_ text: String, ignoreWhitespace: Bool) -> String {
        if ignoreWhitespace {
            return text.components(separatedBy: .whitespacesAndNewlines).filter { !$0.isEmpty }.joined(separator: " ")
        }
        return text
    }
}
