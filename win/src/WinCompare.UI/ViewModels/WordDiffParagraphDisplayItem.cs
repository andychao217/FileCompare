using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinCompare.Core.Models;

namespace WinCompare.UI.ViewModels;

public sealed class WordMediaDisplayItem
{
    public WordMediaItem MediaItem { get; set; } = null!;
    public ImageSource? ImageSource { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FormattedSize { get; set; } = string.Empty;
    public bool IsImage { get; set; } = true;
    public bool IsShape { get; set; }
    public Brush ShapeFillBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush ShapeStrokeBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public string ShapeName { get; set; } = string.Empty;

    public string StatusBadgeText { get; set; } = string.Empty;
    public Brush StatusBadgeBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush StatusBadgeFgBrush { get; set; } = App.GetThemeBrush("TextMutedBrush");
    public Visibility StatusBadgeVisibility
    {
        get
        {
            return !string.IsNullOrEmpty(StatusBadgeText) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public Visibility ImageVisibility
    {
        get
        {
            return IsImage ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    public Visibility ImageViewVisibility
    {
        get
        {
            return ImageSource != null ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    public Visibility ShapeVisibility
    {
        get
        {
            return IsShape ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}

public sealed class WordTableCellDisplayItem
{
    public string Text { get; set; } = string.Empty;
    public Brush BackgroundBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush ForegroundBrush { get; set; } = App.GetThemeBrush("TextFillColorPrimaryBrush");
    public Brush BorderBrush { get; set; } = App.GetThemeBrush("DividerStrokeColorDefaultBrush");
    public bool IsHeader { get; set; }
    public double Width { get; set; } = 90.0;
    public Windows.UI.Text.FontWeight FontWeight
    {
        get
        {
            return IsHeader ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }
    }
}

public sealed class WordTableRowDisplayItem
{
    public List<WordTableCellDisplayItem> Cells { get; set; } = new();
}

public sealed class WordDiffTokenItem
{
    public string Text { get; set; } = string.Empty;
    public Brush? ForegroundBrush { get; set; }
    public bool IsStrikethrough { get; set; }
    public Windows.UI.Text.FontWeight FontWeight { get; set; } = Microsoft.UI.Text.FontWeights.Normal;
}

public sealed class WordDiffParagraphDisplayItem
{
    public string LeftIndexText { get; set; } = string.Empty;
    public string LeftText { get; set; } = string.Empty;
    public Brush LeftBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush LeftFgBrush { get; set; } = App.GetThemeBrush("TextFillColorPrimaryBrush");
    public Visibility LeftBadgeVisibility { get; set; } = Visibility.Collapsed;

    public string RightIndexText { get; set; } = string.Empty;
    public string RightText { get; set; } = string.Empty;
    public Brush RightBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush RightFgBrush { get; set; } = App.GetThemeBrush("TextFillColorPrimaryBrush");
    public Visibility RightBadgeVisibility { get; set; } = Visibility.Collapsed;

    public string StatusText { get; set; } = "";
    public Brush StatusBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush StatusFgBrush { get; set; } = App.GetThemeBrush("TextMutedBrush");

    public List<WordDiffTokenItem> LeftTokens { get; set; } = new();
    public List<WordDiffTokenItem> RightTokens { get; set; } = new();

    // Center Gutter Status Marker (matching macOS)
    public string CenterIconGlyph { get; set; } = string.Empty;
    public Brush CenterIconBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Visibility CenterIconVisibility
    {
        get
        {
            return !string.IsNullOrEmpty(CenterIconGlyph) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public string LeftTableTitle { get; set; } = string.Empty;
    public string RightTableTitle { get; set; } = string.Empty;
    public Visibility LeftTableTitleVisibility
    {
        get
        {
            return (IsTableBlock && !string.IsNullOrEmpty(LeftTableTitle)) ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    public Visibility RightTableTitleVisibility
    {
        get
        {
            return (IsTableBlock && !string.IsNullOrEmpty(RightTableTitle)) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public List<WordMediaDisplayItem> LeftMediaItems { get; set; } = new();
    public List<WordMediaDisplayItem> RightMediaItems { get; set; } = new();

    public Visibility LeftMediaVisibility
    {
        get
        {
            return LeftMediaItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    public Visibility RightMediaVisibility
    {
        get
        {
            return RightMediaItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public List<WordTableRowDisplayItem> LeftTableRows { get; set; } = new();
    public List<WordTableRowDisplayItem> RightTableRows { get; set; } = new();

    public string LeftTableSummary { get; set; } = string.Empty;
    public string RightTableSummary { get; set; } = string.Empty;

    public Visibility LeftTableVisibility
    {
        get
        {
            return (IsTableBlock || LeftTableRows.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    public Visibility RightTableVisibility
    {
        get
        {
            return (IsTableBlock || RightTableRows.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public Visibility LeftTextVisibility
    {
        get
        {
            if (IsTableBlock) return Visibility.Collapsed;
            if (!string.IsNullOrWhiteSpace(LeftText) || LeftTokens.Count > 0) return Visibility.Visible;
            return Visibility.Collapsed;
        }
    }
    public Visibility RightTextVisibility
    {
        get
        {
            if (IsTableBlock) return Visibility.Collapsed;
            if (!string.IsNullOrWhiteSpace(RightText) || RightTokens.Count > 0) return Visibility.Visible;
            return Visibility.Collapsed;
        }
    }

    public bool IsTableBlock { get; set; }
    public Visibility TableBadgeVisibility
    {
        get
        {
            return IsTableBlock ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}

public sealed class WordSingleParagraphDisplayItem
{
    public string IndexText { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;

    public string TableTitle { get; set; } = string.Empty;
    public string TableSummary { get; set; } = string.Empty;
    public Visibility TableTitleVisibility
    {
        get
        {
            return (IsTableBlock && !string.IsNullOrEmpty(TableTitle)) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public List<WordMediaDisplayItem> MediaItems { get; set; } = new();

    public Visibility MediaVisibility
    {
        get
        {
            return MediaItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public List<WordTableRowDisplayItem> TableRows { get; set; } = new();
    public Visibility TableVisibility
    {
        get
        {
            return TableRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public Visibility TextVisibility
    {
        get
        {
            if (IsTableBlock) return Visibility.Collapsed;
            if (!string.IsNullOrWhiteSpace(Text)) return Visibility.Visible;
            return Visibility.Collapsed;
        }
    }

    public bool IsTableBlock { get; set; }
    public Visibility TableBadgeVisibility
    {
        get
        {
            return IsTableBlock ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
