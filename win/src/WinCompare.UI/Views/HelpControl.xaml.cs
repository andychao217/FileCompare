using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using WinCompare.Core.Services;

namespace WinCompare.UI.Views;

public sealed partial class HelpControl : UserControl
{
    public event Action? CloseRequested;

    public HelpControl()
    {
        try
        {
            this.InitializeComponent();

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
            System.Diagnostics.Debug.WriteLine($"HelpControl constructor error: {ex}");
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
            if (HelpSubtitleText != null) HelpSubtitleText.Text = LanguageManager.Instance.Text("helpSubtitle");

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

            if (FolderDiffHelpTitleText != null) FolderDiffHelpTitleText.Text = LanguageManager.Instance.Text("folderDiffTitle") + " (Folder Diff)";
            if (FolderDiffHelpDescText != null) FolderDiffHelpDescText.Text = LanguageManager.Instance.Text("folderDiffDesc");
            if (FolderDiffDetailText != null) FolderDiffDetailText.Text = LanguageManager.Instance.Text("folderDiffGuideDetail");

            if (MergeHelpTitleText != null) MergeHelpTitleText.Text = LanguageManager.Instance.Text("threeWayMerge") + " (3-Way Merge & Git Integration)";
            if (MergeHelpDescText != null) MergeHelpDescText.Text = LanguageManager.Instance.Text("threeWayMergeDesc");
            if (MergeDetailText != null) MergeDetailText.Text = LanguageManager.Instance.Text("threeWayMergeGuideDetail");

            if (GitConfigLabel != null) GitConfigLabel.Text = LanguageManager.Instance.Text("gitMergetoolConfigGuide");
            if (CopyBtnText != null) CopyBtnText.Text = LanguageManager.Instance.Text("copyCommand");

            if (AiHelpTitleText != null) AiHelpTitleText.Text = LanguageManager.Instance.Text("aiEngine") + " (AI Diff Assistant)";
            if (AiHelpDescText != null) AiHelpDescText.Text = LanguageManager.Instance.Text("aiFeatureHelpTitle");
            if (AiDetailText != null) AiDetailText.Text = LanguageManager.Instance.Text("aiAssistantGuideDetail");
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
}
