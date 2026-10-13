using Microsoft.UI.Xaml.Media;
using WinCompare.Core.Services;

namespace WinCompare.UI.ViewModels;

public enum SyncActionType
{
    CopyLeftToRight,
    CopyRightToLeft,
    OverwriteLeftToRight,
    OverwriteRightToLeft,
    DeleteRight,
    DeleteLeft
}

public sealed class SyncPlanItemDisplay
{
    public SyncActionType Action { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string? SourcePath { get; set; }
    public string? TargetPath { get; set; }
    public bool IsDirectory { get; set; }

    public string ActionDescription => Action switch
    {
        SyncActionType.CopyLeftToRight => LanguageManager.Instance.Text("syncActionCopyLeftToRight"),
        SyncActionType.CopyRightToLeft => LanguageManager.Instance.Text("syncActionCopyRightToLeft"),
        SyncActionType.OverwriteLeftToRight => LanguageManager.Instance.Text("syncActionOverwriteLeftToRight"),
        SyncActionType.OverwriteRightToLeft => LanguageManager.Instance.Text("syncActionOverwriteRightToLeft"),
        SyncActionType.DeleteRight => LanguageManager.Instance.Text("syncActionDeleteRight"),
        SyncActionType.DeleteLeft => LanguageManager.Instance.Text("syncActionDeleteLeft"),
        _ => string.Empty
    };

    public string ActionBadgeGlyph => Action switch
    {
        SyncActionType.CopyLeftToRight or SyncActionType.CopyRightToLeft => "\uE710",
        SyncActionType.OverwriteLeftToRight or SyncActionType.OverwriteRightToLeft => "\uE895",
        SyncActionType.DeleteRight or SyncActionType.DeleteLeft => "\uE74D",
        _ => "\uE895"
    };

    public Brush ActionBadgeBrush => Action switch
    {
        SyncActionType.CopyLeftToRight or SyncActionType.CopyRightToLeft => App.GetThemeBrush("StatusAddTextBrush"),
        SyncActionType.OverwriteLeftToRight or SyncActionType.OverwriteRightToLeft => App.GetThemeBrush("StatusInfoTextBrush"),
        SyncActionType.DeleteRight or SyncActionType.DeleteLeft => App.GetThemeBrush("StatusDelTextBrush"),
        _ => App.GetThemeBrush("StatusInfoTextBrush")
    };
}
