using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinCompare.Core.Models;
using WinCompare.Core.Services;

namespace WinCompare.UI.Views;

public sealed partial class WelcomePage : Page
{
    public WelcomePage()
    {
        this.InitializeComponent();

        UpdateLocalization();
        LanguageManager.Instance.LanguageChanged += UpdateLocalization;

        RecentListView.ItemsSource = RecentHistoryManager.Instance.Records;
        UpdateRecentHistoryVisibility();
        RecentHistoryManager.Instance.HistoryChanged += UpdateRecentHistoryVisibility;
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

        // Refresh list items to update multi-language relative timestamps
        if (RecentHistoryManager.Instance.Records.Count > 0)
        {
            RecentListView.ItemsSource = null;
            RecentListView.ItemsSource = RecentHistoryManager.Instance.Records;
        }
    }

    private void UpdateRecentHistoryVisibility()
    {
        bool hasRecords = RecentHistoryManager.Instance.Records.Count > 0;
        EmptyHistoryPanel.Visibility = hasRecords ? Visibility.Collapsed : Visibility.Visible;
        RecentListView.Visibility = hasRecords ? Visibility.Visible : Visibility.Collapsed;
        ClearHistoryBtn.Visibility = hasRecords ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnClearHistoryClicked(object sender, RoutedEventArgs e)
    {
        RecentHistoryManager.Instance.ClearAll();
        UpdateRecentHistoryVisibility();
    }

    private void OnRecentItemClicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RecentCompareRecord record)
        {
            if (record.Type == CompareType.ExcelDiff)
            {
                App.MainWindowInstance?.OpenExcelDiffTab(record.LeftPath, record.RightPath);
            }
            else if (record.Type == CompareType.FolderDiff)
            {
                App.MainWindowInstance?.OpenFolderDiffTab(record.LeftPath, record.RightPath);
            }
            else if (record.Type == CompareType.WordDiff)
            {
                App.MainWindowInstance?.OpenWordDiffTab(record.LeftPath, record.RightPath);
            }
            else
            {
                App.MainWindowInstance?.OpenTextDiffTab(record.LeftPath, record.RightPath);
            }
        }
    }

    private void OnTextDiffClicked(object sender, RoutedEventArgs e)
    {
        App.MainWindowInstance?.OpenTextDiffTab();
    }

    private void OnExcelDiffClicked(object sender, RoutedEventArgs e)
    {
        App.MainWindowInstance?.OpenExcelDiffTab();
    }

    private void OnWordDiffClicked(object sender, RoutedEventArgs e)
    {
        App.MainWindowInstance?.OpenWordDiffTab();
    }

    private void OnFolderDiffClicked(object sender, RoutedEventArgs e)
    {
        App.MainWindowInstance?.OpenFolderDiffTab();
    }
}

