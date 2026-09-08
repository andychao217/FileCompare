using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace WinCompare.UI.ViewModels;

public sealed class DiffLineDisplayItem
{
    public int LineIndex { get; set; }
    public string LeftLineNumText { get; set; } = string.Empty;
    public string LeftSign { get; set; } = " ";
    public string LeftText { get; set; } = string.Empty;
    public Brush LeftBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush LeftFgBrush { get; set; } = new SolidColorBrush(Colors.LightGray);

    public string RightLineNumText { get; set; } = string.Empty;
    public string RightSign { get; set; } = " ";
    public string RightText { get; set; } = string.Empty;
    public Brush RightBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush RightFgBrush { get; set; } = new SolidColorBrush(Colors.LightGray);

    public bool IsDifference { get; set; }
}
