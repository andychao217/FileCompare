using System.Text.Json.Serialization;

namespace WinCompare.Core.Models;

public sealed class ExcelSummary
{
    [JsonPropertyName("sheets")]
    public List<ExcelSheetSummary> Sheets { get; set; } = new();

    [JsonPropertyName("total_diff_count")]
    public int TotalDiffCount { get; set; }
}

public sealed class ExcelSheetSummary
{
    [JsonPropertyName("sheet_name")]
    public string SheetName { get; set; } = string.Empty;

    [JsonPropertyName("left_row_count")]
    public int LeftRowCount { get; set; }

    [JsonPropertyName("right_row_count")]
    public int RightRowCount { get; set; }

    [JsonPropertyName("diff_count")]
    public int DiffCount { get; set; }
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

public sealed class ExcelRowDiff
{
    [JsonPropertyName("row_index")]
    public int RowIndex { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Unchanged"; // Unchanged, Modified, Added, Deleted

    [JsonPropertyName("left_cells")]
    public List<string> LeftCells { get; set; } = new();

    [JsonPropertyName("right_cells")]
    public List<string> RightCells { get; set; } = new();

    [JsonPropertyName("cell_statuses")]
    public List<string> CellStatuses { get; set; } = new();
}
