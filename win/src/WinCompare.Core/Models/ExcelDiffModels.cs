using System.Text.Json.Serialization;

namespace WinCompare.Core.Models;

public enum ExcelCellType
{
    String,
    Number,
    Boolean,
    Date,
    Formula,
    Blank,
    Error
}

public sealed class ExcelCellData
{
    [JsonPropertyName("column_index")]
    public int ColumnIndex { get; set; }

    [JsonPropertyName("column_letter")]
    public string ColumnLetter { get; set; } = string.Empty;

    [JsonPropertyName("raw_value")]
    public string RawValue { get; set; } = string.Empty;

    [JsonPropertyName("formatted_value")]
    public string FormattedValue { get; set; } = string.Empty;

    [JsonPropertyName("formula")]
    public string? Formula { get; set; }

    [JsonPropertyName("cell_type")]
    public string CellType { get; set; } = "string";

    [JsonIgnore]
    public ExcelCellType Type { get; set; } = ExcelCellType.String;
}

public sealed class ExcelRowData
{
    public int RowIndex { get; set; } // 1-based index
    public List<ExcelCellData> Cells { get; set; } = new();

    public ExcelCellData? Cell(int colIdx) => Cells.FirstOrDefault(c => c.ColumnIndex == colIdx);
}

public sealed class ExcelSheetModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int MaxColumns { get; set; }
    public int MaxRows { get; set; }
    public List<ExcelRowData> Rows { get; set; } = new();
}

public sealed class ExcelWorkbookModel
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileSizeFormatted { get; set; } = string.Empty;
    public List<ExcelSheetModel> Sheets { get; set; } = new();
}

public enum RowDiffType
{
    Unchanged,
    Modified,
    Added,
    Deleted
}

public enum CellDiffType
{
    Unchanged,
    Modified,
    Added,
    Deleted
}

public sealed class CellDiffInlineChunk
{
    public string Text { get; set; } = string.Empty;
    public bool IsDiff { get; set; }
}

public sealed class ExcelCellDiffItem
{
    public int ColumnIndex { get; set; }
    public string ColumnLetter { get; set; } = string.Empty;
    public string HeaderName { get; set; } = string.Empty;
    public ExcelCellData? LeftCell { get; set; }
    public ExcelCellData? RightCell { get; set; }
    public CellDiffType DiffType { get; set; } = CellDiffType.Unchanged;
    public List<CellDiffInlineChunk> LeftInlineChunks { get; set; } = new();
    public List<CellDiffInlineChunk> RightInlineChunks { get; set; } = new();

    public string LeftText => LeftCell?.FormattedValue ?? LeftCell?.RawValue ?? string.Empty;
    public string RightText => RightCell?.FormattedValue ?? RightCell?.RawValue ?? string.Empty;
    public bool IsDiff => DiffType != CellDiffType.Unchanged;
}

public sealed class AlignedExcelRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int? LeftRowIndex { get; set; }   // 1-based, null if phantom
    public int? RightRowIndex { get; set; }  // 1-based, null if phantom
    public string DisplayRowNumberLeft => LeftRowIndex.HasValue ? LeftRowIndex.Value.ToString() : " ";
    public string DisplayRowNumberRight => RightRowIndex.HasValue ? RightRowIndex.Value.ToString() : " ";
    public List<ExcelCellData>? LeftCells { get; set; }
    public List<ExcelCellData>? RightCells { get; set; }
    public RowDiffType RowDiffType { get; set; } = RowDiffType.Unchanged;
    public List<ExcelCellDiffItem> CellDiffs { get; set; } = new();
}

public enum SheetDiffStatus
{
    Same,
    Modified,
    LeftOnly,
    RightOnly
}

public sealed class ExcelSheetDiffResult
{
    public string Id { get; set; } = string.Empty;
    public string SheetName { get; set; } = string.Empty;
    public SheetDiffStatus Status { get; set; } = SheetDiffStatus.Same;
    public List<string> ColumnHeaders { get; set; } = new();
    public List<AlignedExcelRow> AlignedRows { get; set; } = new();
    public int TotalRows { get; set; }
    public int DifferenceRowCount { get; set; }
    public int SameRowCount { get; set; }
    public int AddedRowCount { get; set; }
    public int DeletedRowCount { get; set; }
}

public sealed class ExcelWorkbookDiffResult
{
    public List<ExcelSheetDiffResult> SheetDiffs { get; set; } = new();
    public int TotalDifferences { get; set; }
    public double LoadTimeSeconds { get; set; }
}

public sealed class ExcelCompareRules
{
    public bool IgnoreWhitespace { get; set; }
    public bool IgnoreCase { get; set; }
    public bool FirstRowAsHeader { get; set; } = true;
    public double NumericTolerance { get; set; } = 0.0001;
}

// MARK: - Legacy Session Serialization Models

public sealed class ExcelSummary
{
    [JsonPropertyName("sheet_summaries")]
    public List<ExcelSheetSummary> Sheets { get; set; } = new();

    [JsonPropertyName("total_differences")]
    public int TotalDiffCount { get; set; }

    [JsonPropertyName("duration_ms")]
    public ulong DurationMs { get; set; }
}

public sealed class ExcelSheetSummary
{
    [JsonPropertyName("sheet_name_left")]
    public string SheetNameLeft { get; set; } = string.Empty;

    [JsonPropertyName("sheet_name_right")]
    public string SheetNameRight { get; set; } = string.Empty;

    [JsonIgnore]
    public string SheetName => !string.IsNullOrEmpty(SheetNameLeft) ? SheetNameLeft : SheetNameRight;

    [JsonPropertyName("total_rows")]
    public int TotalRows { get; set; }

    [JsonPropertyName("total_columns")]
    public int TotalColumns { get; set; }

    [JsonPropertyName("difference_row_count")]
    public int DiffCount { get; set; }

    [JsonPropertyName("equal_row_count")]
    public int EqualRowCount { get; set; }

    [JsonPropertyName("left_only_row_count")]
    public int LeftOnlyRowCount { get; set; }

    [JsonPropertyName("right_only_row_count")]
    public int RightOnlyRowCount { get; set; }

    [JsonPropertyName("diff_row_indices")]
    public List<int> DiffRowIndices { get; set; } = new();
}

public sealed class ExcelCellDiffDetail
{
    [JsonPropertyName("column_index")]
    public int ColumnIndex { get; set; }

    [JsonPropertyName("column_letter")]
    public string ColumnLetter { get; set; } = string.Empty;

    [JsonPropertyName("left_cell")]
    public ExcelCellData? LeftCell { get; set; }

    [JsonPropertyName("right_cell")]
    public ExcelCellData? RightCell { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("diff_reason")]
    public string? DiffReason { get; set; }
}

public sealed class ExcelRowDiff
{
    [JsonPropertyName("row_index_left")]
    public int? RowIndexLeft { get; set; }

    [JsonPropertyName("row_index_right")]
    public int? RowIndexRight { get; set; }

    [JsonIgnore]
    public int RowIndex => RowIndexLeft ?? RowIndexRight ?? 0;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Equal";

    [JsonPropertyName("cell_diffs")]
    public List<ExcelCellDiffDetail> CellDiffs { get; set; } = new();

    [JsonPropertyName("has_diff")]
    public bool HasDiff { get; set; }

    [JsonIgnore]
    public List<string> LeftCells => CellDiffs.Select(c => c.LeftCell?.FormattedValue ?? "").ToList();

    [JsonIgnore]
    public List<string> RightCells => CellDiffs.Select(c => c.RightCell?.FormattedValue ?? "").ToList();

    [JsonIgnore]
    public List<string> CellStatuses => CellDiffs.Select(c => c.Status).ToList();
}

public sealed class ExcelViewportData
{
    [JsonPropertyName("sheet_index")]
    public int SheetIndex { get; set; }

    [JsonPropertyName("start_row")]
    public int StartRow { get; set; }

    [JsonPropertyName("rows")]
    public List<ExcelRowDiff> Rows { get; set; } = new();
}
