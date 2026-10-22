using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinCompare.Core.Services;

namespace WinCompare.UI.Views;

public sealed partial class HelpControl : UserControl
{
    public event Action? CloseRequested;
    public event Action<string, string, string>? UpdateRequested;

    public HelpControl() : this(0)
    {
    }

    public HelpControl(int initialTabIndex)
    {
        try
        {
            this.InitializeComponent();

            if (HelpTabs != null && initialTabIndex >= 0 && initialTabIndex < HelpTabs.TabItems.Count)
            {
                HelpTabs.SelectedIndex = initialTabIndex;
            }

            if (DragHeader != null)
            {
                DragHeader.PointerPressed += OnHeaderPointerPressed;
                DragHeader.PointerMoved += OnHeaderPointerMoved;
                DragHeader.PointerReleased += OnHeaderPointerReleased;
                DragHeader.PointerCaptureLost += OnHeaderPointerCaptureLost;
            }

            UpdateLocalization();
            LanguageManager.Instance.LanguageChanged += UpdateLocalization;
        }
        catch (Exception ex)
        {
            App.LogFatalError("HelpControl.ctor", ex);
            throw;
        }
    }

    private Windows.Foundation.Point _startPoint;
    private bool _isDragging = false;

    private UIElement? GetDragReferenceElement()
    {
        return this.XamlRoot?.Content ?? App.GetWindowForElement(this)?.Content;
    }

    private void OnHeaderPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var refElement = GetDragReferenceElement();
        if (refElement == null) return;

        var ptr = e.GetCurrentPoint(refElement);
        if (ptr.Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _startPoint = ptr.Position;
            DragHeader.CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }

    private void OnHeaderPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            var refElement = GetDragReferenceElement();
            if (refElement == null) return;

            var curPoint = e.GetCurrentPoint(refElement).Position;
            double dx = curPoint.X - _startPoint.X;
            double dy = curPoint.Y - _startPoint.Y;

            DragTransform.X += dx;
            DragTransform.Y += dy;

            _startPoint = curPoint;
            e.Handled = true;
        }
    }

    private void OnHeaderPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            DragHeader.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }
    }

    private void OnHeaderPointerCaptureLost(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _isDragging = false;
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke();
    }

    private void UpdateLocalization()
    {
        try
        {
            if (HelpTitleText != null) HelpTitleText.Text = LanguageManager.Instance.Text("helpTitle");
            string helpSub = LanguageManager.Instance.Text("helpSubtitle");
            if (HelpSubtitleText != null) HelpSubtitleText.Text = $"WinCompare v{UpdateCheckerService.CurrentVersion} \u2022 {helpSub}";

            if (FeaturesTabItem != null) FeaturesTabItem.Header = LanguageManager.Instance.Text("coreFeatures");
            if (ShortcutsTabItem != null) ShortcutsTabItem.Header = LanguageManager.Instance.Text("shortcuts");
            if (AboutTabItem != null) AboutTabItem.Header = LanguageManager.Instance.Text("aboutArch");

            if (TextDiffTitleText != null) TextDiffTitleText.Text = LanguageManager.Instance.Text("textDiffTitle") + " (Text Diff)";
            if (TextDiffDescText != null) TextDiffDescText.Text = LanguageManager.Instance.Text("textDiffDesc");
            if (TextDiffDetailText != null) TextDiffDetailText.Text = LanguageManager.Instance.Text("textDiffGuideDetail");

            if (ExcelDiffTitleText != null) ExcelDiffTitleText.Text = LanguageManager.Instance.Text("excelDiffTitle") + " (Excel Diff)";
            if (ExcelDiffDescText != null) ExcelDiffDescText.Text = LanguageManager.Instance.Text("excelDiffDesc");
            if (ExcelDiffDetailText != null) ExcelDiffDetailText.Text = LanguageManager.Instance.Text("excelDiffGuideDetail");

            if (WordDiffTitleText != null) WordDiffTitleText.Text = LanguageManager.Instance.Text("wordDiffTitle") + " (Word Diff)";
            if (WordDiffDescText != null) WordDiffDescText.Text = LanguageManager.Instance.Text("wordDiffDesc");
                        if (WordDiffDetailText != null) WordDiffDetailText.Text = LanguageManager.Instance.Text("wordDiffGuideDetail");

            if (PdfDiffTitleText != null) PdfDiffTitleText.Text = LanguageManager.Instance.Text("pdfDiffTitle") + " (PDF Diff)";
            if (PdfDiffDescText != null) PdfDiffDescText.Text = LanguageManager.Instance.Text("pdfDiffDesc");
            if (PdfDiffDetailText != null) PdfDiffDetailText.Text = LanguageManager.Instance.Text("pdfDiffGuideDetail");

            if (FolderDiffHelpTitleText != null) FolderDiffHelpTitleText.Text = LanguageManager.Instance.Text("folderDiffTitle") + " (Folder Diff)";
            if (FolderDiffHelpDescText != null) FolderDiffHelpDescText.Text = LanguageManager.Instance.Text("folderDiffDesc");
            if (FolderDiffDetailText != null) FolderDiffDetailText.Text = LanguageManager.Instance.Text("folderDiffGuideDetail");

            if (MergeHelpTitleText != null) MergeHelpTitleText.Text = LanguageManager.Instance.Text("threeWayMerge") + " (3-Way Merge & Git Integration)";
            if (MergeHelpDescText != null) MergeHelpDescText.Text = LanguageManager.Instance.Text("threeWayMergeDesc");
            if (MergeDetailText != null) MergeDetailText.Text = LanguageManager.Instance.Text("threeWayMergeGuideDetail");

            if (GitConfigLabel != null) GitConfigLabel.Text = LanguageManager.Instance.Text("gitMergetoolConfigGuide");
            if (CopyBtnText != null) CopyBtnText.Text = LanguageManager.Instance.Text("copyCommand");

            if (TabDragHelpTitleText != null) TabDragHelpTitleText.Text = LanguageManager.Instance.Text("tabDragMergeTitle") + " (Tab Drag & Multi-Window)";
            if (TabDragHelpDescText != null) TabDragHelpDescText.Text = LanguageManager.Instance.Text("tabDragMergeDesc");

            if (AiHelpTitleText != null) AiHelpTitleText.Text = LanguageManager.Instance.Text("aiEngine") + " (AI Diff Assistant)";
            if (AiHelpDescText != null) AiHelpDescText.Text = LanguageManager.Instance.Text("aiFeatureHelpTitle");
            if (AiDetailText != null) AiDetailText.Text = LanguageManager.Instance.Text("aiAssistantGuideDetail");

            // Shortcuts Tab
            if (ShortcutNewTextText != null) ShortcutNewTextText.Text = LanguageManager.Instance.Text("textDiffTitle");
            if (ShortcutNewExcelText != null) ShortcutNewExcelText.Text = LanguageManager.Instance.Text("excelDiffTitle");
            if (ShortcutNewWordText != null) ShortcutNewWordText.Text = LanguageManager.Instance.Text("wordDiffTitle");
            if (ShortcutNewPdfText != null) ShortcutNewPdfText.Text = LanguageManager.Instance.Text("pdfDiffTitle");
            if (ShortcutNewFolderText != null) ShortcutNewFolderText.Text = LanguageManager.Instance.Text("folderDiffTitle");
            if (ShortcutNewMergeText != null) ShortcutNewMergeText.Text = LanguageManager.Instance.Text("threeWayMerge");
            if (ShortcutNextTabText != null) ShortcutNextTabText.Text = LanguageManager.Instance.Text("nextTab");
            if (ShortcutPrevTabText != null) ShortcutPrevTabText.Text = LanguageManager.Instance.Text("prevTab");
            if (ShortcutOpenFileText != null) ShortcutOpenFileText.Text = LanguageManager.Instance.Text("openFile");
            if (ShortcutSaveText != null) ShortcutSaveText.Text = LanguageManager.Instance.Text("saveChanges");
            if (ShortcutCloseTabText != null) ShortcutCloseTabText.Text = LanguageManager.Instance.Text("closeTab");
            if (ShortcutRefreshText != null) ShortcutRefreshText.Text = LanguageManager.Instance.Text("refreshCompare");
            if (ShortcutNextDiffText != null) ShortcutNextDiffText.Text = LanguageManager.Instance.Text("nextDiff");
            if (ShortcutPrevDiffText != null) ShortcutPrevDiffText.Text = LanguageManager.Instance.Text("prevDiff");
            if (ShortcutTakeLeftText != null) ShortcutTakeLeftText.Text = LanguageManager.Instance.Text("takeLeft");
            if (ShortcutTakeRightText != null) ShortcutTakeRightText.Text = LanguageManager.Instance.Text("takeRight");
            if (ShortcutIgnoreWsText != null) ShortcutIgnoreWsText.Text = LanguageManager.Instance.Text("ignoreWhitespace");
            if (ShortcutIgnoreCaseText != null) ShortcutIgnoreCaseText.Text = LanguageManager.Instance.Text("ignoreCase");
            if (ShortcutAiSummaryText != null) ShortcutAiSummaryText.Text = LanguageManager.Instance.Text("aiSummary");
            if (ShortcutSettingsText != null) ShortcutSettingsText.Text = LanguageManager.Instance.Text("openPreferences");
            if (ShortcutHelpText != null) ShortcutHelpText.Text = LanguageManager.Instance.Text("helpAndGuide");
            if (ShortcutCancelText != null) ShortcutCancelText.Text = LanguageManager.Instance.Text("cancelTabDragDesc");

            // About Tab & Close Button
            if (AboutVersionText != null) AboutVersionText.Text = string.Format(LanguageManager.Instance.Text("aboutVersion"), UpdateCheckerService.CurrentVersion);
            if (AboutDescText != null) AboutDescText.Text = LanguageManager.Instance.Text("aboutDesc");
            if (AboutCheckUpdateBtnText != null) AboutCheckUpdateBtnText.Text = LanguageManager.Instance.Text("checkForUpdates");
            if (CloseButton != null) ToolTipService.SetToolTip(CloseButton, LanguageManager.Instance.Text("close"));
        }
        catch { }
    }

    private async void OnCopyGitCmdClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(GitCmdSnippet.Text);
            Clipboard.SetContent(dataPackage);

            CopyBtnText.Text = LanguageManager.Instance.Text("copied");
            await Task.Delay(1800);
            CopyBtnText.Text = LanguageManager.Instance.Text("copyCommand");
        }
        catch
        {
            // Clipboard access fallback
        }
    }

    private async void OnCheckUpdateClicked(object sender, RoutedEventArgs e)
    {
        AboutCheckUpdateBtn.IsEnabled = false;
        AboutUpdateProgressRing.Visibility = Visibility.Visible;
        AboutUpdateProgressRing.IsActive = true;
        AboutUpdateStatusText.Text = LanguageManager.Instance.Text("checkingForUpdates");
        AboutUpdateStatusText.Foreground = App.GetThemeBrush("TextMutedBrush");

        var (status, newVer, notes, url) = await UpdateCheckerService.Instance.CheckForUpdatesAsync(isUserInitiated: true);

        AboutUpdateProgressRing.IsActive = false;
        AboutUpdateProgressRing.Visibility = Visibility.Collapsed;
        AboutCheckUpdateBtn.IsEnabled = true;

        if (status == UpdateCheckStatus.UpdateAvailable)
        {
            AboutUpdateStatusText.Text = string.Format(LanguageManager.Instance.Text("newVersionFoundFormat"), newVer, UpdateCheckerService.CurrentVersion);
            AboutUpdateStatusText.Foreground = App.GetThemeBrush("StatusSuccessBrush");

            UpdateRequested?.Invoke(newVer, notes, url);
        }
        else if (status == UpdateCheckStatus.UpToDate)
        {
            string upToDate = LanguageManager.Instance.Text("upToDateTitle");
            string lastChecked = LanguageManager.Instance.Text("lastChecked");
            AboutUpdateStatusText.Text = $"{upToDate} (v{UpdateCheckerService.CurrentVersion}) \u00B7 {lastChecked}: {DateTime.Now:HH:mm:ss}";
            AboutUpdateStatusText.Foreground = App.GetThemeBrush("TextMutedBrush");
        }
        else
        {
            AboutUpdateStatusText.Text = LanguageManager.Instance.Text("checkFailed");
            AboutUpdateStatusText.Foreground = App.GetThemeBrush("StatusDangerBrush");
        }
    }
}
