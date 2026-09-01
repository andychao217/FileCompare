import Foundation

/// Engine responsible for aligning Excel sheets, matching rows (Key-based or LCS Dynamic Programming),
/// and performing cell-level & character-level diff calculations.
public final class ExcelDiffEngine: Sendable {
    public static let shared = ExcelDiffEngine()

    public init() {}

    public func compareWorkbooks(
        left: ExcelWorkbookModel,
        right: ExcelWorkbookModel,
        rules: ExcelCompareRules = ExcelCompareRules()
    ) async -> ExcelWorkbookDiffResult {
        let startTime = CFAbsoluteTimeGetCurrent()

        var sheetDiffs: [ExcelSheetDiffResult] = []
        var totalDifferences = 0

        var matchedRightSheetNames = Set<String>()

        // 1. Process left sheets and match with right
        for leftSheet in left.sheets {
            if let rightSheet = right.sheets.first(where: { $0.name.lowercased() == leftSheet.name.lowercased() }) {
                matchedRightSheetNames.insert(rightSheet.name)
                let diffRes = compareSheets(left: leftSheet, right: rightSheet, rules: rules)
                sheetDiffs.append(diffRes)
                totalDifferences += diffRes.differenceRowCount
            } else {
                // Left only sheet
                let diffRes = makeSingleSideSheetDiff(sheet: leftSheet, isLeft: true, rules: rules)
                sheetDiffs.append(diffRes)
                totalDifferences += diffRes.differenceRowCount
            }
        }

        // 2. Process remaining right sheets (Right only)
        for rightSheet in right.sheets where !matchedRightSheetNames.contains(rightSheet.name) {
            let diffRes = makeSingleSideSheetDiff(sheet: rightSheet, isLeft: false, rules: rules)
            sheetDiffs.append(diffRes)
            totalDifferences += diffRes.differenceRowCount
        }

        // If no matching by name was found and both have sheets, match by index
        if sheetDiffs.allSatisfy({ $0.status == .leftOnly || $0.status == .rightOnly }) && !left.sheets.isEmpty && !right.sheets.isEmpty {
            sheetDiffs.removeAll()
            totalDifferences = 0
            let maxCount = max(left.sheets.count, right.sheets.count)
            for idx in 0..<maxCount {
                if idx < left.sheets.count && idx < right.sheets.count {
                    let diffRes = compareSheets(left: left.sheets[idx], right: right.sheets[idx], rules: rules)
                    sheetDiffs.append(diffRes)
                    totalDifferences += diffRes.differenceRowCount
                } else if idx < left.sheets.count {
                    let diffRes = makeSingleSideSheetDiff(sheet: left.sheets[idx], isLeft: true, rules: rules)
                    sheetDiffs.append(diffRes)
                    totalDifferences += diffRes.differenceRowCount
                } else {
                    let diffRes = makeSingleSideSheetDiff(sheet: right.sheets[idx], isLeft: false, rules: rules)
                    sheetDiffs.append(diffRes)
                    totalDifferences += diffRes.differenceRowCount
                }
            }
        }

        let loadTime = CFAbsoluteTimeGetCurrent() - startTime
        return ExcelWorkbookDiffResult(
            sheetDiffs: sheetDiffs,
            totalDifferences: totalDifferences,
            loadTimeSeconds: (loadTime * 100).rounded() / 100.0
        )
    }

    // MARK: - Single Sheet Comparison

    public func compareSheets(
        left: ExcelSheetModel,
        right: ExcelSheetModel,
        rules: ExcelCompareRules
    ) -> ExcelSheetDiffResult {
        let maxCols = max(left.maxColumns, right.maxColumns)
        var columnHeaders: [String] = []

        var leftRows = left.rows
        var rightRows = right.rows

        // If firstRowAsHeader, extract headers from row 1
        if rules.firstRowAsHeader {
            let leftHeaderCells = leftRows.first?.cells ?? []
            let rightHeaderCells = rightRows.first?.cells ?? []

            for colIdx in 0..<maxCols {
                let leftH = leftHeaderCells.first(where: { $0.columnIndex == colIdx })?.rawValue.trimmingCharacters(in: .whitespaces)
                let rightH = rightHeaderCells.first(where: { $0.columnIndex == colIdx })?.rawValue.trimmingCharacters(in: .whitespaces)
                let letter = ExcelModelsHelper.columnLetter(for: colIdx)

                if let lh = leftH, !lh.isEmpty {
                    columnHeaders.append(lh)
                } else if let rh = rightH, !rh.isEmpty {
                    columnHeaders.append(rh)
                } else {
                    columnHeaders.append(letter)
                }
            }

            if !leftRows.isEmpty { leftRows.removeFirst() }
            if !rightRows.isEmpty { rightRows.removeFirst() }
        } else {
            for colIdx in 0..<maxCols {
                columnHeaders.append(ExcelModelsHelper.columnLetter(for: colIdx))
            }
        }

        // Align rows
        let alignedRows: [AlignedExcelRow]
        if !rules.keyColumnIndices.isEmpty {
            alignedRows = alignRowsByKey(leftRows: leftRows, rightRows: rightRows, maxCols: maxCols, columnHeaders: columnHeaders, rules: rules)
        } else {
            alignedRows = alignRowsByLCS(leftRows: leftRows, rightRows: rightRows, maxCols: maxCols, columnHeaders: columnHeaders, rules: rules)
        }

        var diffCount = 0
        var sameCount = 0
        var addedCount = 0
        var deletedCount = 0

        for row in alignedRows {
            switch row.rowDiffType {
            case .unchanged: sameCount += 1
            case .modified: diffCount += 1
            case .added: addedCount += 1; diffCount += 1
            case .deleted: deletedCount += 1; diffCount += 1
            }
        }

        let status: SheetDiffStatus = (diffCount == 0) ? .same : .modified

        return ExcelSheetDiffResult(
            id: left.id,
            sheetName: left.name,
            leftSheetName: left.name,
            rightSheetName: right.name,
            status: status,
            columnHeaders: columnHeaders,
            alignedRows: alignedRows,
            totalRows: alignedRows.count,
            differenceRowCount: diffCount,
            sameRowCount: sameCount,
            addedRowCount: addedCount,
            deletedRowCount: deletedCount
        )
    }

    // MARK: - Row Alignment by Key

    private func alignRowsByKey(
        leftRows: [ExcelRowData],
        rightRows: [ExcelRowData],
        maxCols: Int,
        columnHeaders: [String],
        rules: ExcelCompareRules
    ) -> [AlignedExcelRow] {
        var results: [AlignedExcelRow] = []
        var rightMap: [String: (index: Int, row: ExcelRowData)] = [:]

        for (idx, r) in rightRows.enumerated() {
            let key = makeKey(for: r, keyIndices: rules.keyColumnIndices, rules: rules)
            rightMap[key] = (idx, r)
        }

        var processedRightIndices = Set<Int>()

        for leftRow in leftRows {
            let key = makeKey(for: leftRow, keyIndices: rules.keyColumnIndices, rules: rules)
            if let matchedRight = rightMap[key] {
                processedRightIndices.insert(matchedRight.index)
                let rowDiff = compareRowCells(
                    leftRow: leftRow,
                    rightRow: matchedRight.row,
                    maxCols: maxCols,
                    columnHeaders: columnHeaders,
                    rules: rules
                )
                results.append(rowDiff)
            } else {
                // Left only (deleted)
                let rowDiff = makeSingleSideAlignedRow(leftRow: leftRow, rightRow: nil, maxCols: maxCols, columnHeaders: columnHeaders, type: .deleted)
                results.append(rowDiff)
            }
        }

        // Add remaining right rows
        for (idx, rightRow) in rightRows.enumerated() where !processedRightIndices.contains(idx) {
            let rowDiff = makeSingleSideAlignedRow(leftRow: nil, rightRow: rightRow, maxCols: maxCols, columnHeaders: columnHeaders, type: .added)
            results.append(rowDiff)
        }

        return results
    }

    // MARK: - Row Alignment by LCS / Myers Similarity

    private func alignRowsByLCS(
        leftRows: [ExcelRowData],
        rightRows: [ExcelRowData],
        maxCols: Int,
        columnHeaders: [String],
        rules: ExcelCompareRules
    ) -> [AlignedExcelRow] {
        let m = leftRows.count
        let n = rightRows.count

        if m == 0 {
            return rightRows.map { makeSingleSideAlignedRow(leftRow: nil, rightRow: $0, maxCols: maxCols, columnHeaders: columnHeaders, type: .added) }
        }
        if n == 0 {
            return leftRows.map { makeSingleSideAlignedRow(leftRow: $0, rightRow: nil, maxCols: maxCols, columnHeaders: columnHeaders, type: .deleted) }
        }

        // Build LCS DP table based on row fingerprint equality
        var dp = Array(repeating: Array(repeating: 0, count: n + 1), count: m + 1)
        for i in 0..<m {
            let leftFP = rowFingerprint(leftRows[i], rules: rules)
            for j in 0..<n {
                let rightFP = rowFingerprint(rightRows[j], rules: rules)
                if leftFP == rightFP {
                    dp[i + 1][j + 1] = dp[i][j] + 1
                } else {
                    dp[i + 1][j + 1] = max(dp[i + 1][j], dp[i][j + 1])
                }
            }
        }

        // Backtrack to build aligned rows
        var aligned: [AlignedExcelRow] = []
        var i = m
        var j = n

        while i > 0 || j > 0 {
            if i > 0 && j > 0 && rowFingerprint(leftRows[i - 1], rules: rules) == rowFingerprint(rightRows[j - 1], rules: rules) {
                let rowDiff = compareRowCells(
                    leftRow: leftRows[i - 1],
                    rightRow: rightRows[j - 1],
                    maxCols: maxCols,
                    columnHeaders: columnHeaders,
                    rules: rules
                )
                aligned.append(rowDiff)
                i -= 1
                j -= 1
            } else if j > 0 && (i == 0 || dp[i][j - 1] >= dp[i - 1][j]) {
                // Right only (added)
                let rowDiff = makeSingleSideAlignedRow(leftRow: nil, rightRow: rightRows[j - 1], maxCols: maxCols, columnHeaders: columnHeaders, type: .added)
                aligned.append(rowDiff)
                j -= 1
            } else if i > 0 && (j == 0 || dp[i][j - 1] < dp[i - 1][j]) {
                // Left only (deleted)
                let rowDiff = makeSingleSideAlignedRow(leftRow: leftRows[i - 1], rightRow: nil, maxCols: maxCols, columnHeaders: columnHeaders, type: .deleted)
                aligned.append(rowDiff)
                i -= 1
            }
        }

        aligned.reverse()

        // Post-pass: Group consecutive deleted + added rows into modified rows if similarity is high
        var consolidated: [AlignedExcelRow] = []
        var k = 0
        while k < aligned.count {
            if k + 1 < aligned.count && aligned[k].rowDiffType == .deleted && aligned[k + 1].rowDiffType == .added {
                if let leftR = aligned[k].leftCells, let rightR = aligned[k + 1].rightCells {
                    let leftRowData = ExcelRowData(rowIndex: aligned[k].leftRowIndex ?? 0, cells: leftR)
                    let rightRowData = ExcelRowData(rowIndex: aligned[k + 1].rightRowIndex ?? 0, cells: rightR)
                    let modRow = compareRowCells(
                        leftRow: leftRowData,
                        rightRow: rightRowData,
                        maxCols: maxCols,
                        columnHeaders: columnHeaders,
                        rules: rules
                    )
                    consolidated.append(modRow)
                    k += 2
                    continue
                }
            }
            consolidated.append(aligned[k])
            k += 1
        }

        return consolidated
    }

    // MARK: - Cell-level Comparison & Inline Diff

    private func compareRowCells(
        leftRow: ExcelRowData,
        rightRow: ExcelRowData,
        maxCols: Int,
        columnHeaders: [String],
        rules: ExcelCompareRules
    ) -> AlignedExcelRow {
        var cellDiffs: [CellDiffItem] = []
        var hasDifferences = false

        for colIdx in 0..<maxCols {
            let leftCell = leftRow.cell(at: colIdx)
            let rightCell = rightRow.cell(at: colIdx)
            let header = colIdx < columnHeaders.count ? columnHeaders[colIdx] : ExcelModelsHelper.columnLetter(for: colIdx)
            let letter = ExcelModelsHelper.columnLetter(for: colIdx)

            let (diffType, leftChunks, rightChunks) = diffTwoCells(left: leftCell, right: rightCell, rules: rules)
            if diffType != .unchanged {
                hasDifferences = true
            }

            cellDiffs.append(CellDiffItem(
                columnIndex: colIdx,
                columnLetter: letter,
                headerName: header,
                leftCell: leftCell,
                rightCell: rightCell,
                diffType: diffType,
                leftInlineChunks: leftChunks,
                rightInlineChunks: rightChunks
            ))
        }

        let rowType: RowDiffType = hasDifferences ? .modified : .unchanged

        return AlignedExcelRow(
            leftRowIndex: leftRow.rowIndex,
            rightRowIndex: rightRow.rowIndex,
            leftCells: leftRow.cells,
            rightCells: rightRow.cells,
            rowDiffType: rowType,
            cellDiffs: cellDiffs
        )
    }

    private func diffTwoCells(
        left: ExcelCellData?,
        right: ExcelCellData?,
        rules: ExcelCompareRules
    ) -> (CellDiffType, [CellDiffInlineChunk], [CellDiffInlineChunk]) {
        let leftVal = left?.rawValue ?? ""
        let rightVal = right?.rawValue ?? ""

        if left == nil && right == nil {
            return (.unchanged, [], [])
        }
        if left == nil && right != nil {
            return (rightVal.isEmpty ? .unchanged : .added, [], [CellDiffInlineChunk(text: rightVal, isDiff: true)])
        }
        if left != nil && right == nil {
            return (leftVal.isEmpty ? .unchanged : .deleted, [CellDiffInlineChunk(text: leftVal, isDiff: true)], [])
        }

        var lStr = leftVal
        var rStr = rightVal

        if rules.ignoreWhitespace {
            lStr = lStr.trimmingCharacters(in: .whitespacesAndNewlines)
            rStr = rStr.trimmingCharacters(in: .whitespacesAndNewlines)
        }

        var isIdentical = false
        if rules.ignoreCase {
            isIdentical = (lStr.caseInsensitiveCompare(rStr) == .orderedSame)
        } else {
            isIdentical = (lStr == rStr)
        }

        // Check numeric tolerance
        if !isIdentical, rules.numericTolerance > 0.0 {
            if let numL = Double(lStr), let numR = Double(rStr) {
                if abs(numL - numR) <= rules.numericTolerance {
                    isIdentical = true
                }
            }
        }

        if isIdentical {
            return (.unchanged, [CellDiffInlineChunk(text: leftVal, isDiff: false)], [CellDiffInlineChunk(text: rightVal, isDiff: false)])
        }

        // Inline character diff
        let (lChunks, rChunks) = computeInlineChunks(left: leftVal, right: rightVal)
        return (.modified, lChunks, rChunks)
    }

    private func computeInlineChunks(left: String, right: String) -> ([CellDiffInlineChunk], [CellDiffInlineChunk]) {
        let leftChars = Array(left)
        let rightChars = Array(right)

        var lPrefix = 0
        while lPrefix < leftChars.count && lPrefix < rightChars.count && leftChars[lPrefix] == rightChars[lPrefix] {
            lPrefix += 1
        }

        var lSuffix = 0
        while lSuffix < (leftChars.count - lPrefix) && lSuffix < (rightChars.count - lPrefix) &&
              leftChars[leftChars.count - 1 - lSuffix] == rightChars[rightChars.count - 1 - lSuffix] {
            lSuffix += 1
        }

        var leftResult: [CellDiffInlineChunk] = []
        var rightResult: [CellDiffInlineChunk] = []

        let prefixStr = String(leftChars.prefix(lPrefix))
        if !prefixStr.isEmpty {
            leftResult.append(CellDiffInlineChunk(text: prefixStr, isDiff: false))
            rightResult.append(CellDiffInlineChunk(text: prefixStr, isDiff: false))
        }

        let leftMid = String(leftChars[lPrefix..<(leftChars.count - lSuffix)])
        if !leftMid.isEmpty {
            leftResult.append(CellDiffInlineChunk(text: leftMid, isDiff: true))
        }

        let rightMid = String(rightChars[lPrefix..<(rightChars.count - lSuffix)])
        if !rightMid.isEmpty {
            rightResult.append(CellDiffInlineChunk(text: rightMid, isDiff: true))
        }

        let suffixStr = String(leftChars.suffix(lSuffix))
        if !suffixStr.isEmpty {
            leftResult.append(CellDiffInlineChunk(text: suffixStr, isDiff: false))
            rightResult.append(CellDiffInlineChunk(text: suffixStr, isDiff: false))
        }

        return (leftResult, rightResult)
    }

    // MARK: - Helpers

    private func makeSingleSideSheetDiff(
        sheet: ExcelSheetModel,
        isLeft: Bool,
        rules: ExcelCompareRules
    ) -> ExcelSheetDiffResult {
        let columnHeaders = (0..<sheet.maxColumns).map { ExcelModelsHelper.columnLetter(for: $0) }
        let alignedRows = sheet.rows.map { row in
            makeSingleSideAlignedRow(
                leftRow: isLeft ? row : nil,
                rightRow: isLeft ? nil : row,
                maxCols: sheet.maxColumns,
                columnHeaders: columnHeaders,
                type: isLeft ? .deleted : .added
            )
        }

        return ExcelSheetDiffResult(
            id: sheet.id,
            sheetName: sheet.name,
            leftSheetName: isLeft ? sheet.name : nil,
            rightSheetName: isLeft ? nil : sheet.name,
            status: isLeft ? .leftOnly : .rightOnly,
            columnHeaders: columnHeaders,
            alignedRows: alignedRows,
            totalRows: alignedRows.count,
            differenceRowCount: alignedRows.count,
            sameRowCount: 0,
            addedRowCount: isLeft ? 0 : alignedRows.count,
            deletedRowCount: isLeft ? alignedRows.count : 0
        )
    }

    private func makeSingleSideAlignedRow(
        leftRow: ExcelRowData?,
        rightRow: ExcelRowData?,
        maxCols: Int,
        columnHeaders: [String],
        type: RowDiffType
    ) -> AlignedExcelRow {
        var cellDiffs: [CellDiffItem] = []
        for colIdx in 0..<maxCols {
            let leftC = leftRow?.cell(at: colIdx)
            let rightC = rightRow?.cell(at: colIdx)
            let header = colIdx < columnHeaders.count ? columnHeaders[colIdx] : ExcelModelsHelper.columnLetter(for: colIdx)
            let letter = ExcelModelsHelper.columnLetter(for: colIdx)

            let cellDiffType: CellDiffType = (type == .added) ? .added : .deleted
            let leftChunk = leftC.map { [CellDiffInlineChunk(text: $0.rawValue, isDiff: true)] } ?? []
            let rightChunk = rightC.map { [CellDiffInlineChunk(text: $0.rawValue, isDiff: true)] } ?? []

            cellDiffs.append(CellDiffItem(
                columnIndex: colIdx,
                columnLetter: letter,
                headerName: header,
                leftCell: leftC,
                rightCell: rightC,
                diffType: cellDiffType,
                leftInlineChunks: leftChunk,
                rightInlineChunks: rightChunk
            ))
        }

        return AlignedExcelRow(
            leftRowIndex: leftRow?.rowIndex,
            rightRowIndex: rightRow?.rowIndex,
            leftCells: leftRow?.cells,
            rightCells: rightRow?.cells,
            rowDiffType: type,
            cellDiffs: cellDiffs
        )
    }

    private func makeKey(for row: ExcelRowData, keyIndices: [Int], rules: ExcelCompareRules) -> String {
        var keyParts: [String] = []
        for idx in keyIndices {
            var val = row.cell(at: idx)?.rawValue ?? ""
            if rules.ignoreWhitespace { val = val.trimmingCharacters(in: .whitespacesAndNewlines) }
            if rules.ignoreCase { val = val.lowercased() }
            keyParts.append(val)
        }
        return keyParts.joined(separator: "|||")
    }

    private func rowFingerprint(_ row: ExcelRowData, rules: ExcelCompareRules) -> String {
        return row.cells.map { cell in
            var val = cell.rawValue
            if rules.ignoreWhitespace { val = val.trimmingCharacters(in: .whitespacesAndNewlines) }
            if rules.ignoreCase { val = val.lowercased() }
            return val
        }.joined(separator: "\t")
    }
}
