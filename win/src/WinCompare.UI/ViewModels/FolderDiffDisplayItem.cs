using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinCompare.Core.Models;

namespace WinCompare.UI.ViewModels;

public sealed class FolderDiffDisplayItem
{
    public string RelativePath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public FolderEntryStatus Status { get; set; }

    // Left side
    public bool IsLeftMissing { get; set; }
    public string LeftName { get; set; } = string.Empty;
    public string LeftSizeFormatted { get; set; } = "-";
    public string LeftModifiedFormatted { get; set; } = "-";
    public string? LeftHash { get; set; }
    public Brush? LeftBgBrush { get; set; }
    public Brush? LeftFgBrush { get; set; }
    public string LeftIconGlyph { get; set; } = "\uE8A5";
    public Brush? LeftIconColorBrush { get; set; }
    public Visibility LeftItemVisibility => IsLeftMissing ? Visibility.Collapsed : Visibility.Visible;
    public Visibility LeftPlaceholderVisibility => IsLeftMissing ? Visibility.Visible : Visibility.Collapsed;

    // Right side
    public bool IsRightMissing { get; set; }
    public string RightName { get; set; } = string.Empty;
    public string RightSizeFormatted { get; set; } = "-";
    public string RightModifiedFormatted { get; set; } = "-";
    public string? RightHash { get; set; }
    public Brush? RightBgBrush { get; set; }
    public Brush? RightFgBrush { get; set; }
    public string RightIconGlyph { get; set; } = "\uE8A5";
    public Brush? RightIconColorBrush { get; set; }
    public Visibility RightItemVisibility => IsRightMissing ? Visibility.Collapsed : Visibility.Visible;
    public Visibility RightPlaceholderVisibility => IsRightMissing ? Visibility.Visible : Visibility.Collapsed;

    // Mode-dependent columns
    public Visibility HashVisibility { get; set; } = Visibility.Collapsed;

    // Compatibility fields
    public string StatusText { get; set; } = string.Empty;
    public Brush? StatusBgBrush { get; set; }
    public Brush? StatusFgBrush { get; set; }
    public string IconGlyph { get; set; } = "\uE8A5";
    public Brush? IconColorBrush { get; set; }
    public string LeftFullPath { get; set; } = string.Empty;
    public string RightFullPath { get; set; } = string.Empty;
}

