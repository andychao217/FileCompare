using Microsoft.UI.Xaml.Media;

namespace WinCompare.UI.ViewModels;

public sealed class FolderSingleDisplayItem
{
    public string RelativePath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public string SizeFormatted { get; set; } = string.Empty;
    public string ModifiedFormatted { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = "\uE8A5";
    public Brush? IconColorBrush { get; set; }
}

