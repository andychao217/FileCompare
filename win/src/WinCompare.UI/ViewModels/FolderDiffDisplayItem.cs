using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using WinCompare.Core.Models;

namespace WinCompare.UI.ViewModels;

public sealed class FolderDiffDisplayItem
{
    public string RelativePath { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public Brush StatusBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush StatusFgBrush { get; set; } = new SolidColorBrush(Colors.LightGray);
    public string IconGlyph { get; set; } = "\uE8A5";
    public Brush IconColorBrush { get; set; } = new SolidColorBrush(Colors.LightGray);
    public string LeftSizeFormatted { get; set; } = "-";
    public string RightSizeFormatted { get; set; } = "-";
    public FolderEntryStatus Status { get; set; }
    public string LeftFullPath { get; set; } = string.Empty;
    public string RightFullPath { get; set; } = string.Empty;
}
