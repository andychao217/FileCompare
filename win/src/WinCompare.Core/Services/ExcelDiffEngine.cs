using System.Diagnostics;
using WinCompare.Core.Models;

namespace WinCompare.Core.Services;

/// <summary>
/// Spreadsheet diff engine implementing multi-sheet alignment, column-based cell comparison,
/// fuzzy similarity dynamic programming, and inline character diffs matching MacCompare.
/// </summary>
public static class ExcelDiffEngine
{
    public static async Task<ExcelWorkbookDiffResult> CompareWorkbooksAsync(
        ExcelWorkbookModel left,
        ExcelWorkbookModel right,
        ExcelCompareRules rules)
    {
        return await Task.Run(() => CompareWorkbooks(left, right, rules));
    }

    public static ExcelWorkbookDiffResult CompareWorkbooks(
        ExcelWorkbookModel left,
        ExcelWorkbookModel right,
        ExcelCompareRules rules)
    {
        var sw = Stopwatch.StartNew();
        var result = new ExcelWorkbookDiffResult();

        var matchedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Process left sheets and match with right
        foreach (var leftSheet in left.Sheets)
        {
            var rightSheet = right.Sheets.FirstOrDefault(s => string.Equals(s.Name, leftSheet.Name, StringComparison.OrdinalIgnoreCase));
            if (rightSheet != null)
            {
                matchedSheetNames.Add(rightSheet.Name);
                var sheetDiff = CompareSheets(leftSheet, rightSheet, rules);
                result.SheetDiffs.Add(sheetDiff);
            }
            else
            {
                var sheetDiff = MakeSingleSheetDiff(leftSheet, null, SheetDiffStatus.LeftOnly, rules);
                result.SheetDiffs.Add(sheetDiff);
            }
        }

        // 2. Add remaining right-only sheets
        foreach (var rightSheet in right.Sheets)
        {
            if (!matchedSheetNames.Contains(rightSheet.Name))
            {
                var sheetDiff = MakeSingleSheetDiff(null, rightSheet, SheetDiffStatus.RightOnly, rules);
                result.SheetDiffs.Add(sheetDiff);
            }
        }

        // 3. Fallback: if neither matched by name, pair them by index
        if (result.SheetDiffs.Count == 0 && left.Sheets.Count > 0 && right.Sheets.Count > 0)
        {
            int count = Math.Max(left.Sheets.Count, right.Sheets.Count);
            for (int i = 0; i < count; i++)
            {
                var lSheet = i < left.Sheets.Count ? left.Sheets[i] : null;
                var rSheet = i < right.Sheets.Count ? right.Sheets[i] : null;

                if (lSheet != null && rSheet != null)
                {
                    result.SheetDiffs.Add(CompareSheets(lSheet, rSheet, rules));
                }
                else if (lSheet != null)
                {
                    result.SheetDiffs.Add(MakeSingleSheetDiff(lSheet, null, SheetDiffStatus.LeftOnly, rules));
                }
                else if (rSheet != null)
                {
                    result.SheetDiffs.Add(MakeSingleSheetDiff(null, rSheet, SheetDiffStatus.RightOnly, rules));
                }
            }
        }

        sw.Stop();
        result.LoadTimeSeconds = sw.Elapsed.TotalSeconds;
        result.TotalDifferences = result.SheetDiffs.Sum(s => s.DifferenceRowCount);

        return result;
    }

    private static ExcelSheetDiffResult CompareSheets(
        ExcelSheetModel left,
        ExcelSheetModel right,
        ExcelCompareRules rules)
    {
        int maxCols = Math.Max(left.MaxColumns, right.MaxColumns);
        var columnHeaders = new List<string>();

        var leftRows = new List<ExcelRowData>(left.Rows);
        var rightRows = new List<ExcelRowData>(right.Rows);

        // Header extraction
        if (rules.FirstRowAsHeader)
        {
            var leftHeaderRow = leftRows.FirstOrDefault();
            var rightHeaderRow = rightRows.FirstOrDefault();

            for (int colIdx = 0; colIdx < maxCols; colIdx++)
            {
                string? lh = leftHeaderRow?.Cell(colIdx)?.RawValue?.Trim();
                string? rh = rightHeaderRow?.Cell(colIdx)?.RawValue?.Trim();
                string letter = ExcelDocumentParser.IndexToColumnLetter(colIdx);

                if (!string.IsNullOrEmpty(lh))
                {
                    columnHeaders.Add(lh);
                }
                else if (!string.IsNullOrEmpty(rh))
                {
                    columnHeaders.Add(rh);
                }
                else
                {
                    columnHeaders.Add(letter);
                }
            }

            if (leftRows.Count > 0) leftRows.RemoveAt(0);
            if (rightRows.Count > 0) rightRows.RemoveAt(0);
        }
        else
        {
            for (int colIdx = 0; colIdx < maxCols; colIdx++)
            {
                columnHeaders.Add(ExcelDocumentParser.IndexToColumnLetter(colIdx));
            }
        }

        var alignedRows = AlignRows(leftRows, rightRows, maxCols, columnHeaders, rules);

        int diffCount = alignedRows.Count(r => r.RowDiffType != RowDiffType.Unchanged);
        int sameCount = alignedRows.Count(r => r.RowDiffType == RowDiffType.Unchanged);
        int addCount = alignedRows.Count(r => r.RowDiffType == RowDiffType.Added);
        int delCount = alignedRows.Count(r => r.RowDiffType == RowDiffType.Deleted);

        return new ExcelSheetDiffResult
        {
            Id = !string.IsNullOrEmpty(left.Id) ? left.Id : right.Id,
            SheetName = left.Name,
            Status = diffCount > 0 ? SheetDiffStatus.Modified : SheetDiffStatus.Same,
            ColumnHeaders = columnHeaders,
            AlignedRows = alignedRows,
            TotalRows = alignedRows.Count,
            DifferenceRowCount = diffCount,
            SameRowCount = sameCount,
            AddedRowCount = addCount,
            DeletedRowCount = delCount
        };
    }

    private static double RowSimilarity(
        ExcelRowData left,
        ExcelRowData right,
        int maxCols,
        ExcelCompareRules rules)
    {
        int matchCount = 0;
        int totalNonEmpty = 0;

        for (int colIdx = 0; colIdx < maxCols; colIdx++)
        {
            var leftC = left.Cell(colIdx);
            var rightC = right.Cell(colIdx);
            var (diffType, _, _) = DiffTwoCells(leftC, rightC, rules);

            bool leftEmpty = string.IsNullOrWhiteSpace(leftC?.RawValue);
            bool rightEmpty = string.IsNullOrWhiteSpace(rightC?.RawValue);

            if (leftEmpty && rightEmpty) continue;

            totalNonEmpty++;
            if (diffType == CellDiffType.Unchanged)
            {
                matchCount++;
            }
        }

        if (totalNonEmpty == 0) return 1.0;
        return matchCount / (double)totalNonEmpty;
    }

    private static List<AlignedExcelRow> AlignRows(
        List<ExcelRowData> leftRows,
        List<ExcelRowData> rightRows,
        int maxCols,
        List<string> columnHeaders,
        ExcelCompareRules rules)
    {
        if (leftRows.Count == 0 && rightRows.Count == 0) return new List<AlignedExcelRow>();
        if (leftRows.Count == 0)
        {
            return rightRows.Select(r => MakeSingleSideAlignedRow(null, r, maxCols, columnHeaders, RowDiffType.Added)).ToList();
        }
        if (rightRows.Count == 0)
        {
            return leftRows.Select(l => MakeSingleSideAlignedRow(l, null, maxCols, columnHeaders, RowDiffType.Deleted)).ToList();
        }

        // Fast path: Equal row counts with high similarity -> direct 1:1 row alignment
        if (leftRows.Count == rightRows.Count)
        {
            bool allMatched = true;
            for (int i = 0; i < leftRows.Count; i++)
            {
                double sim = RowSimilarity(leftRows[i], rightRows[i], maxCols, rules);
                if (sim < 0.35)
                {
                    allMatched = false;
                    break;
                }
            }

            if (allMatched)
            {
                var directAligned = new List<AlignedExcelRow>(leftRows.Count);
                for (int i = 0; i < leftRows.Count; i++)
                {
                    directAligned.Add(MakeTwoSideAlignedRow(leftRows[i], rightRows[i], maxCols, columnHeaders, rules));
                }
                return directAligned;
            }
        }

        // Dynamic Programming similarity alignment
        int m = leftRows.Count;
        int n = rightRows.Count;
        double[,] dp = new double[m + 1, n + 1];

        for (int i = 1; i <= m; i++) dp[i, 0] = i * -0.5;
        for (int j = 1; j <= n; j++) dp[0, j] = j * -0.5;

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                double sim = RowSimilarity(leftRows[i - 1], rightRows[j - 1], maxCols, rules);
                double matchBonus = (leftRows[i - 1].RowIndex == rightRows[j - 1].RowIndex) ? 0.05 : 0.0;
                double matchScore = (sim >= 0.35) ? (sim * 2.0 + matchBonus) : -0.5;

                double scoreDiag = dp[i - 1, j - 1] + matchScore;
                double scoreDel = dp[i - 1, j] - 0.5;
                double scoreAdd = dp[i, j - 1] - 0.5;

                dp[i, j] = Math.Max(scoreDiag, Math.Max(scoreDel, scoreAdd));
            }
        }

        // Traceback
        int ti = m, tj = n;
        var rawResult = new List<AlignedExcelRow>();

        while (ti > 0 || tj > 0)
        {
            if (ti > 0 && tj > 0)
            {
                double sim = RowSimilarity(leftRows[ti - 1], rightRows[tj - 1], maxCols, rules);
                double matchBonus = (leftRows[ti - 1].RowIndex == rightRows[tj - 1].RowIndex) ? 0.05 : 0.0;
                double matchScore = (sim >= 0.35) ? (sim * 2.0 + matchBonus) : -0.5;

                if (Math.Abs(dp[ti, tj] - (dp[ti - 1, tj - 1] + matchScore)) < 0.0001 && sim >= 0.35)
                {
                    rawResult.Add(MakeTwoSideAlignedRow(leftRows[ti - 1], rightRows[tj - 1], maxCols, columnHeaders, rules));
                    ti--;
                    tj--;
                    continue;
                }
            }

            if (tj > 0 && (ti == 0 || Math.Abs(dp[ti, tj] - (dp[ti, tj - 1] - 0.5)) < 0.0001))
            {
                rawResult.Add(MakeSingleSideAlignedRow(null, rightRows[tj - 1], maxCols, columnHeaders, RowDiffType.Added));
                tj--;
            }
            else if (ti > 0)
            {
                rawResult.Add(MakeSingleSideAlignedRow(leftRows[ti - 1], null, maxCols, columnHeaders, RowDiffType.Deleted));
                ti--;
            }
        }

        rawResult.Reverse();

        // Block consolidation
        var consolidated = new List<AlignedExcelRow>();
        int idx = 0;

        while (idx < rawResult.Count)
        {
            if (rawResult[idx].RowDiffType == RowDiffType.Deleted)
            {
                int delStart = idx;
                while (idx < rawResult.Count && rawResult[idx].RowDiffType == RowDiffType.Deleted) idx++;
                int delEnd = idx;

                int addStart = idx;
                while (idx < rawResult.Count && rawResult[idx].RowDiffType == RowDiffType.Added) idx++;
                int addEnd = idx;

                var deletedGroup = rawResult.GetRange(delStart, delEnd - delStart);
                var addedGroup = rawResult.GetRange(addStart, addEnd - addStart);

                if (deletedGroup.Count > 0 && addedGroup.Count > 0)
                {
                    var matchedAddIndices = new HashSet<int>();
                    foreach (var del in deletedGroup)
                    {
                        int bestAddIdx = -1;
                        double bestSim = 0.35;

                        for (int a = 0; a < addedGroup.Count; a++)
                        {
                            if (matchedAddIndices.Contains(a)) continue;
                            var add = addedGroup[a];

                            if (del.LeftRowIndex.HasValue && add.RightRowIndex.HasValue && del.LeftRowIndex == add.RightRowIndex)
                            {
                                bestAddIdx = a;
                                break;
                            }

                            if (del.LeftCells != null && add.RightCells != null)
                            {
                                var lDummy = new ExcelRowData { RowIndex = del.LeftRowIndex ?? 0, Cells = del.LeftCells };
                                var rDummy = new ExcelRowData { RowIndex = add.RightRowIndex ?? 0, Cells = add.RightCells };
                                double sim = RowSimilarity(lDummy, rDummy, maxCols, rules);
                                if (sim > bestSim)
                                {
                                    bestSim = sim;
                                    bestAddIdx = a;
                                }
                            }
                        }

                        if (bestAddIdx != -1)
                        {
                            matchedAddIndices.Add(bestAddIdx);
                            var add = addedGroup[bestAddIdx];
                            var lObj = new ExcelRowData { RowIndex = del.LeftRowIndex ?? 0, Cells = del.LeftCells ?? new List<ExcelCellData>() };
                            var rObj = new ExcelRowData { RowIndex = add.RightRowIndex ?? 0, Cells = add.RightCells ?? new List<ExcelCellData>() };
                            consolidated.Add(MakeTwoSideAlignedRow(lObj, rObj, maxCols, columnHeaders, rules));
                        }
                        else
                        {
                            consolidated.Add(del);
                        }
                    }

                    for (int a = 0; a < addedGroup.Count; a++)
                    {
                        if (!matchedAddIndices.Contains(a))
                        {
                            consolidated.Add(addedGroup[a]);
                        }
                    }
                }
                else
                {
                    consolidated.AddRange(deletedGroup);
                    consolidated.AddRange(addedGroup);
                }
            }
            else
            {
                consolidated.Add(rawResult[idx]);
                idx++;
            }
        }

        return consolidated;
    }

    private static AlignedExcelRow MakeTwoSideAlignedRow(
        ExcelRowData leftRow,
        ExcelRowData rightRow,
        int maxCols,
        List<string> columnHeaders,
        ExcelCompareRules rules)
    {
        var cellDiffs = new List<ExcelCellDiffItem>();
        bool hasDifferences = false;

        for (int colIdx = 0; colIdx < maxCols; colIdx++)
        {
            var leftCell = leftRow.Cell(colIdx);
            var rightCell = rightRow.Cell(colIdx);
            string header = colIdx < columnHeaders.Count ? columnHeaders[colIdx] : ExcelDocumentParser.IndexToColumnLetter(colIdx);
            string letter = ExcelDocumentParser.IndexToColumnLetter(colIdx);

            var (diffType, leftChunks, rightChunks) = DiffTwoCells(leftCell, rightCell, rules);
            if (diffType != CellDiffType.Unchanged)
            {
                hasDifferences = true;
            }

            cellDiffs.Add(new ExcelCellDiffItem
            {
                ColumnIndex = colIdx,
                ColumnLetter = letter,
                HeaderName = header,
                LeftCell = leftCell,
                RightCell = rightCell,
                DiffType = diffType,
                LeftInlineChunks = leftChunks,
                RightInlineChunks = rightChunks
            });
        }

        return new AlignedExcelRow
        {
            LeftRowIndex = leftRow.RowIndex,
            RightRowIndex = rightRow.RowIndex,
            LeftCells = leftRow.Cells,
            RightCells = rightRow.Cells,
            RowDiffType = hasDifferences ? RowDiffType.Modified : RowDiffType.Unchanged,
            CellDiffs = cellDiffs
        };
    }

    private static AlignedExcelRow MakeSingleSideAlignedRow(
        ExcelRowData? leftRow,
        ExcelRowData? rightRow,
        int maxCols,
        List<string> columnHeaders,
        RowDiffType type)
    {
        var cellDiffs = new List<ExcelCellDiffItem>();
        bool isLeft = (leftRow != null);

        for (int colIdx = 0; colIdx < maxCols; colIdx++)
        {
            var cell = isLeft ? leftRow?.Cell(colIdx) : rightRow?.Cell(colIdx);
            string header = colIdx < columnHeaders.Count ? columnHeaders[colIdx] : ExcelDocumentParser.IndexToColumnLetter(colIdx);
            string letter = ExcelDocumentParser.IndexToColumnLetter(colIdx);

            var diffType = (cell == null || string.IsNullOrWhiteSpace(cell.RawValue))
                ? CellDiffType.Unchanged
                : (isLeft ? CellDiffType.Deleted : CellDiffType.Added);

            cellDiffs.Add(new ExcelCellDiffItem
            {
                ColumnIndex = colIdx,
                ColumnLetter = letter,
                HeaderName = header,
                LeftCell = isLeft ? cell : null,
                RightCell = isLeft ? null : cell,
                DiffType = diffType
            });
        }

        return new AlignedExcelRow
        {
            LeftRowIndex = leftRow?.RowIndex,
            RightRowIndex = rightRow?.RowIndex,
            LeftCells = leftRow?.Cells,
            RightCells = rightRow?.Cells,
            RowDiffType = type,
            CellDiffs = cellDiffs
        };
    }

    private static (CellDiffType Type, List<CellDiffInlineChunk> LeftChunks, List<CellDiffInlineChunk> RightChunks) DiffTwoCells(
        ExcelCellData? left,
        ExcelCellData? right,
        ExcelCompareRules rules)
    {
        string leftVal = left?.RawValue ?? string.Empty;
        string rightVal = right?.RawValue ?? string.Empty;

        if (left == null && right == null) return (CellDiffType.Unchanged, new(), new());
        if (left == null && right != null)
        {
            return (string.IsNullOrWhiteSpace(rightVal) ? CellDiffType.Unchanged : CellDiffType.Added, new(), new() { new() { Text = rightVal, IsDiff = true } });
        }
        if (left != null && right == null)
        {
            return (string.IsNullOrWhiteSpace(leftVal) ? CellDiffType.Unchanged : CellDiffType.Deleted, new() { new() { Text = leftVal, IsDiff = true } }, new());
        }

        string lStr = leftVal;
        string rStr = rightVal;

        if (rules.IgnoreWhitespace)
        {
            lStr = System.Text.RegularExpressions.Regex.Replace(lStr, @"\s+", "");
            rStr = System.Text.RegularExpressions.Regex.Replace(rStr, @"\s+", "");
        }

        var comparison = rules.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(lStr, rStr, comparison))
        {
            return (CellDiffType.Unchanged, new(), new());
        }

        // Numeric tolerance
        if (double.TryParse(lStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lNum) &&
            double.TryParse(rStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double rNum))
        {
            if (Math.Abs(lNum - rNum) <= rules.NumericTolerance)
            {
                return (CellDiffType.Unchanged, new(), new());
            }
        }

        // Inline character diff calculation
        var (lChunks, rChunks) = ComputeInlineChunks(leftVal, rightVal);
        return (CellDiffType.Modified, lChunks, rChunks);
    }

    private static (List<CellDiffInlineChunk> Left, List<CellDiffInlineChunk> Right) ComputeInlineChunks(string left, string right)
    {
        // Simple prefix-suffix diff chunking
        int prefixLen = 0;
        int minLen = Math.Min(left.Length, right.Length);
        while (prefixLen < minLen && left[prefixLen] == right[prefixLen])
        {
            prefixLen++;
        }

        int suffixLen = 0;
        while (suffixLen < (minLen - prefixLen) && left[left.Length - 1 - suffixLen] == right[right.Length - 1 - suffixLen])
        {
            suffixLen++;
        }

        var lChunks = new List<CellDiffInlineChunk>();
        var rChunks = new List<CellDiffInlineChunk>();

        if (prefixLen > 0)
        {
            string commonPrefix = left.Substring(0, prefixLen);
            lChunks.Add(new CellDiffInlineChunk { Text = commonPrefix, IsDiff = false });
            rChunks.Add(new CellDiffInlineChunk { Text = commonPrefix, IsDiff = false });
        }

        string lDiff = left.Substring(prefixLen, left.Length - prefixLen - suffixLen);
        string rDiff = right.Substring(prefixLen, right.Length - prefixLen - suffixLen);

        if (!string.IsNullOrEmpty(lDiff))
        {
            lChunks.Add(new CellDiffInlineChunk { Text = lDiff, IsDiff = true });
        }
        if (!string.IsNullOrEmpty(rDiff))
        {
            rChunks.Add(new CellDiffInlineChunk { Text = rDiff, IsDiff = true });
        }

        if (suffixLen > 0)
        {
            string commonSuffix = left.Substring(left.Length - suffixLen);
            lChunks.Add(new CellDiffInlineChunk { Text = commonSuffix, IsDiff = false });
            rChunks.Add(new CellDiffInlineChunk { Text = commonSuffix, IsDiff = false });
        }

        return (lChunks, rChunks);
    }

    private static ExcelSheetDiffResult MakeSingleSheetDiff(
        ExcelSheetModel? leftSheet,
        ExcelSheetModel? rightSheet,
        SheetDiffStatus status,
        ExcelCompareRules rules)
    {
        var sheet = leftSheet ?? rightSheet!;
        int maxCols = sheet.MaxColumns;
        var headers = new List<string>();

        for (int i = 0; i < maxCols; i++)
        {
            headers.Add(ExcelDocumentParser.IndexToColumnLetter(i));
        }

        var alignedRows = new List<AlignedExcelRow>();
        var rowType = (status == SheetDiffStatus.LeftOnly) ? RowDiffType.Deleted : RowDiffType.Added;

        foreach (var r in sheet.Rows)
        {
            alignedRows.Add(MakeSingleSideAlignedRow(leftSheet != null ? r : null, rightSheet != null ? r : null, maxCols, headers, rowType));
        }

        return new ExcelSheetDiffResult
        {
            Id = sheet.Id,
            SheetName = sheet.Name,
            Status = status,
            ColumnHeaders = headers,
            AlignedRows = alignedRows,
            TotalRows = alignedRows.Count,
            DifferenceRowCount = alignedRows.Count,
            SameRowCount = 0,
            AddedRowCount = status == SheetDiffStatus.RightOnly ? alignedRows.Count : 0,
            DeletedRowCount = status == SheetDiffStatus.LeftOnly ? alignedRows.Count : 0
        };
    }
}
