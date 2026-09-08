using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace WinCompare.UI.ViewModels;

public sealed class ExcelRowDisplayItem
{
    public int RowIndex { get; set; }
    public string RowIndexText => (RowIndex + 1).ToString();
    public string StatusText { get; set; } = "";
    public Brush StatusBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush StatusFgBrush { get; set; } = new SolidColorBrush(Colors.LightGray);
    public string LeftRowFormatted { get; set; } = string.Empty;
    public string RightRowFormatted { get; set; } = string.Empty;
    public Brush LeftBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush RightBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public bool IsDiff { get; set; }
    public List<string> LeftCells { get; set; } = new();
    public List<string> RightCells { get; set; } = new();
}
