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
    public Brush LeftFgBrush { get; set; } = App.GetThemeBrush("TextFillColorPrimaryBrush");

    public string RightLineNumText { get; set; } = string.Empty;
    public string RightSign { get; set; } = " ";
    public string RightText { get; set; } = string.Empty;
    public Brush RightBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush RightFgBrush { get; set; } = App.GetThemeBrush("TextFillColorPrimaryBrush");

    public bool IsDifference { get; set; }
}

public sealed class SingleTextLineDisplayItem
{
    private string? _lineNumText;
    public int LineNumber { get; set; }
    public string LineNumText
    {
        get { return _lineNumText ?? LineNumber.ToString(); }
        set { _lineNumText = value; }
    }
    public string Content { get; set; } = string.Empty;
}

public sealed class ThreeWayMergeLineDisplayItem
{
    public int LineIndex { get; set; }

    public string LocalLineNumText { get; set; } = string.Empty;
    public string LocalSign { get; set; } = " ";
    public string LocalText { get; set; } = string.Empty;
    public Brush LocalBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush LocalFgBrush { get; set; } = App.GetThemeBrush("TextFillColorPrimaryBrush");

    public string BaseLineNumText { get; set; } = string.Empty;
    public string BaseSign { get; set; } = " ";
    public string BaseText { get; set; } = string.Empty;
    public Brush BaseBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush BaseFgBrush { get; set; } = App.GetThemeBrush("TextFillColorPrimaryBrush");

    public string RemoteLineNumText { get; set; } = string.Empty;
    public string RemoteSign { get; set; } = " ";
    public string RemoteText { get; set; } = string.Empty;
    public Brush RemoteBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush RemoteFgBrush { get; set; } = App.GetThemeBrush("TextFillColorPrimaryBrush");

    public string Status { get; set; } = "Unchanged";
    public bool IsConflict => Status == "Conflict";
}
