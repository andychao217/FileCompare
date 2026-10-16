using Microsoft.UI.Xaml.Media;
using WinCompare.Core.Models;
using WinCompare.Core.Services;

namespace WinCompare.UI.ViewModels;

public sealed class RecentCompareDisplayItem
{
    public RecentCompareRecord Record { get; init; } = null!;
    public string DisplayTitle { get; init; } = string.Empty;
    public string PathSummary { get; init; } = string.Empty;
    public string FormattedDate { get; init; } = string.Empty;
    public string TypeName { get; init; } = string.Empty;
    public CompareType EffectiveType { get; init; }
    public string IconGlyph { get; init; } = "\uE8A5";
    public Brush IconForegroundBrush { get; init; } = null!;
    public Brush IconBackgroundBrush { get; init; } = null!;
    public Brush BadgeForegroundBrush { get; init; } = null!;
    public Brush BadgeBackgroundBrush { get; init; } = null!;
    public string DeleteTooltip { get; init; } = string.Empty;

    public static RecentCompareDisplayItem Create(RecentCompareRecord record)
    {
        var type = record.EffectiveType;
        string glyph;
        Brush fg, bg;
        string typeKey;

        switch (type)
        {
            case CompareType.FolderDiff:
                glyph = "\uED25";
                fg = App.GetThemeBrush("WelcomeFolderDiffFgBrush");
                bg = App.GetThemeBrush("WelcomeFolderDiffBgBrush");
                typeKey = "folderDiffTitle";
                break;
            case CompareType.ExcelDiff:
                glyph = "\uF0EC";
                fg = App.GetThemeBrush("WelcomeExcelDiffFgBrush");
                bg = App.GetThemeBrush("WelcomeExcelDiffBgBrush");
                typeKey = "excelDiffTitle";
                break;
            case CompareType.WordDiff:
                glyph = "\uE8A5";
                fg = App.GetThemeBrush("WelcomeWordDiffFgBrush");
                bg = App.GetThemeBrush("WelcomeWordDiffBgBrush");
                typeKey = "wordDiffTitle";
                break;
            case CompareType.ThreeWayMerge:
                glyph = "\uE8D7";
                fg = App.GetThemeBrush("WelcomeThreeWayMergeFgBrush");
                bg = App.GetThemeBrush("WelcomeThreeWayMergeBgBrush");
                typeKey = "threeWayMerge";
                break;
            default:
                glyph = "\uE8C8";
                fg = App.GetThemeBrush("WelcomeTextDiffFgBrush");
                bg = App.GetThemeBrush("WelcomeTextDiffBgBrush");
                typeKey = "textDiffTitle";
                break;
        }

        string typeName = LanguageManager.Instance.Text(typeKey);

        string pathSummary = !string.IsNullOrEmpty(record.LeftPath) && !string.IsNullOrEmpty(record.RightPath)
            ? $"{record.LeftPath}  \u2194  {record.RightPath}"
            : (record.LeftPath ?? string.Empty);

        return new RecentCompareDisplayItem
        {
            Record = record,
            DisplayTitle = record.DisplayTitle,
            PathSummary = pathSummary,
            FormattedDate = record.FormattedDate,
            TypeName = typeName,
            EffectiveType = type,
            IconGlyph = glyph,
            IconForegroundBrush = fg,
            IconBackgroundBrush = bg,
            BadgeForegroundBrush = fg,
            BadgeBackgroundBrush = bg,
            DeleteTooltip = LanguageManager.Instance.Text("deleteRecord")
        };
    }
}
