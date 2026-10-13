using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinCompare.Core.Models;

namespace WinCompare.UI.ViewModels;

public sealed class ExcelCellDisplayItem
{
    public int ColumnIndex { get; set; }
    public string ColumnLetter { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public CellDiffType DiffType { get; set; } = CellDiffType.Unchanged;
    public Brush TextBrush { get; set; } = App.GetThemeBrush("TextFillColorPrimaryBrush");
    public Brush BgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public double Width { get; set; } = 140;
    public bool IsPhantom { get; set; }
}

public sealed class ExcelRowDisplayItem
{
    public Guid RowId { get; set; } = Guid.NewGuid();
    public int RowIndex { get; set; }
    public string DisplayRowNumber { get; set; } = string.Empty;
    public string RowIndexText => DisplayRowNumber;

    public RowDiffType DiffType { get; set; } = RowDiffType.Unchanged;
    public Brush DotBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Visibility DotVisibility => (DiffType != RowDiffType.Unchanged) ? Visibility.Visible : Visibility.Collapsed;

    public Brush RowBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush RowNumberBgBrush { get; set; } = App.GetThemeBrush("TableRowNumberBgBrush");
    public Brush RowNumberFgBrush { get; set; } = App.GetThemeBrush("TextFillColorSecondaryBrush");
    public bool IsSelected { get; set; }

    public List<ExcelCellDisplayItem> Cells { get; set; } = new();

    // Legacy compatibility fields
    public string StatusText { get; set; } = "";
    public Brush StatusBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush StatusFgBrush { get; set; } = App.GetThemeBrush("TextMutedBrush");
    public string LeftRowFormatted { get; set; } = string.Empty;
    public string RightRowFormatted { get; set; } = string.Empty;
    public Brush LeftBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush RightBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public bool IsDiff { get; set; }
    public List<string> LeftCells { get; set; } = new();
    public List<string> RightCells { get; set; } = new();
}
