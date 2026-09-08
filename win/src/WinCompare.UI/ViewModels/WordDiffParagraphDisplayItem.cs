using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace WinCompare.UI.ViewModels;

public sealed class WordDiffParagraphDisplayItem
{
    public string LeftIndexText { get; set; } = string.Empty;
    public string LeftText { get; set; } = string.Empty;
    public Brush LeftBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Visibility LeftBadgeVisibility { get; set; } = Visibility.Collapsed;

    public string RightIndexText { get; set; } = string.Empty;
    public string RightText { get; set; } = string.Empty;
    public Brush RightBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Visibility RightBadgeVisibility { get; set; } = Visibility.Collapsed;

    public string StatusText { get; set; } = "";
    public Brush StatusBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush StatusFgBrush { get; set; } = new SolidColorBrush(Colors.LightGray);
}
