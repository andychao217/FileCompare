using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinCompare.Core.Models;
using WinCompare.Core.Services;
using WinCompare.UI.ViewModels;

namespace WinCompare.UI.Views;

public sealed partial class WelcomePage : Page
{
    public WelcomePage()
    {
        this.InitializeComponent();

        UpdateLocalization();
        LanguageManager.Instance.LanguageChanged += UpdateLocalization;

        UpdateRecentHistoryVisibility();
        RecentHistoryManager.Instance.HistoryChanged += UpdateRecentHistoryVisibility;

        this.Unloaded += (s, e) =>
        {
            LanguageManager.Instance.LanguageChanged -= UpdateLocalization;
            RecentHistoryManager.Instance.HistoryChanged -= UpdateRecentHistoryVisibility;
        };
    }

    private void UpdateLocalization()
    {
        AppTitleBlock.Text = LanguageManager.Instance.Text("welcomeTitle");
        AppSubtitleBlock.Text = LanguageManager.Instance.Text("welcomeSubtitle");
        TextDiffTitleBlock.Text = LanguageManager.Instance.Text("textDiffTitle");
        TextDiffDescBlock.Text = LanguageManager.Instance.Text("textDiffDesc");
        ExcelDiffTitleBlock.Text = LanguageManager.Instance.Text("excelDiffTitle");
        ExcelDiffDescBlock.Text = LanguageManager.Instance.Text("excelDiffDesc");
        WordDiffTitleBlock.Text = LanguageManager.Instance.Text("wordDiffTitle");
        WordDiffDescBlock.Text = LanguageManager.Instance.Text("wordDiffDesc");
        FolderDiffTitleBlock.Text = LanguageManager.Instance.Text("folderDiffTitle");
        FolderDiffDescBlock.Text = LanguageManager.Instance.Text("folderDiffDesc");
        RecentTitleBlock.Text = LanguageManager.Instance.Text("recentComparisons");
        NoRecentBlock.Text = LanguageManager.Instance.Text("noRecentRecords");
        ClearHistoryBtnText.Text = LanguageManager.Instance.Text("clearHistory");

        RefreshRecentList();
    }

    private void RefreshRecentList()
    {
        var records = RecentHistoryManager.Instance.Records;
        var displayItems = records.Select(RecentCompareDisplayItem.Create).ToList();
        RecentListView.ItemsSource = displayItems;
    }

    public void UpdateRecentHistoryVisibility()
    {
        bool hasRecords = RecentHistoryManager.Instance.Records.Count > 0;
        EmptyHistoryPanel.Visibility = hasRecords ? Visibility.Collapsed : Visibility.Visible;
        RecentListView.Visibility = hasRecords ? Visibility.Visible : Visibility.Collapsed;
        ClearHistoryBtn.Visibility = hasRecords ? Visibility.Visible : Visibility.Collapsed;
        RefreshRecentList();
    }

    private void OnClearHistoryClicked(object sender, RoutedEventArgs e)
    {
        if (RecentHistoryManager.Instance.Records.Count == 0) return;

        var targetWindow = GetTargetWindow();
        if (targetWindow != null)
        {
            targetWindow.ShowConfirmDialog(
                LanguageManager.Instance.Text("confirmClearHistoryTitle"),
                LanguageManager.Instance.Text("confirmClearHistoryMessage"),
                LanguageManager.Instance.Text("clear"),
                () =>
                {
                    RecentHistoryManager.Instance.ClearAll();
                    UpdateRecentHistoryVisibility();
                },
                isDestructive: true);
        }
        else
        {
            RecentHistoryManager.Instance.ClearAll();
            UpdateRecentHistoryVisibility();
        }
    }

    private void OnDeleteRecentItemClicked(object sender, RoutedEventArgs e)
    {
        RecentCompareRecord? targetRecord = null;
        if (sender is FrameworkElement fe)
        {
            if (fe.Tag is RecentCompareRecord rec)
            {
                targetRecord = rec;
            }
            else if (fe.DataContext is RecentCompareDisplayItem displayItem)
            {
                targetRecord = displayItem.Record;
            }
            else if (fe.DataContext is RecentCompareRecord ctxRec)
            {
                targetRecord = ctxRec;
            }
        }

        if (targetRecord != null)
        {
            RecentHistoryManager.Instance.RemoveRecord(targetRecord.Id);
            UpdateRecentHistoryVisibility();
        }
    }

    private MainWindow? GetTargetWindow() => (App.GetWindowForElement(this) as MainWindow) ?? App.ActiveWindow ?? App.MainWindowInstance;

    private void OnRecentItemClicked(object sender, ItemClickEventArgs e)
    {
        RecentCompareRecord? record = null;
        if (e.ClickedItem is RecentCompareDisplayItem displayItem)
        {
            record = displayItem.Record;
        }
        else if (e.ClickedItem is RecentCompareRecord rec)
        {
            record = rec;
        }

        if (record == null) return;

        try
        {
            var target = GetTargetWindow();
            if (target == null) return;

            var effectiveType = record.EffectiveType;

            // Defensive check: Verify existence of at least one path
            bool leftExists = File.Exists(record.LeftPath) || Directory.Exists(record.LeftPath);
            bool rightExists = File.Exists(record.RightPath) || Directory.Exists(record.RightPath);

            if (!leftExists && !rightExists)
            {
                target.ShowNoticeDialog(
                    LanguageManager.Instance.Text("recentComparisons"),
                    $"{LanguageManager.Instance.Text("noFileSelected")}:\n{record.LeftPath}\n{record.RightPath}"
                );
                return;
            }

            switch (effectiveType)
            {
                case CompareType.FolderDiff:
                    target.OpenFolderDiffTab(record.LeftPath, record.RightPath);
                    break;
                case CompareType.ExcelDiff:
                    target.OpenExcelDiffTab(record.LeftPath, record.RightPath);
                    break;
                case CompareType.WordDiff:
                    target.OpenWordDiffTab(record.LeftPath, record.RightPath);
                    break;
                case CompareType.ThreeWayMerge:
                    target.OpenThreeWayMergeTab(record.LeftPath, null, record.RightPath);
                    break;
                default:
                    target.OpenTextDiffTab(record.LeftPath, record.RightPath);
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WelcomePage] OnRecentItemClicked error: {ex}");
            var target = GetTargetWindow();
            target?.ShowNoticeDialog(
                LanguageManager.Instance.Text("recentComparisons"),
                ex.Message
            );
        }
    }

    private void OnTextDiffClicked(object sender, RoutedEventArgs e)
    {
        GetTargetWindow()?.OpenTextDiffTab();
    }

    private void OnExcelDiffClicked(object sender, RoutedEventArgs e)
    {
        GetTargetWindow()?.OpenExcelDiffTab();
    }

    private void OnWordDiffClicked(object sender, RoutedEventArgs e)
    {
        GetTargetWindow()?.OpenWordDiffTab();
    }

    private void OnFolderDiffClicked(object sender, RoutedEventArgs e)
    {
        GetTargetWindow()?.OpenFolderDiffTab();
    }

    private void OnWelcomeDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void OnWelcomeDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        try
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items != null && items.Count > 0)
                {
                    var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
                    var win = GetTargetWindow();
                    if (win != null && paths.Count > 0)
                    {
                        var pos = e.GetPosition(this);
                        win.HandleGlobalDrop(paths, pos, this.ActualWidth);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"[WelcomePage] OnWelcomeDrop error: {ex.Message}");
        }
    }
}

