using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinCompare.Core.Models;
using WinCompare.Core.Services;
using WinCompare.UI.Helpers;
using WinCompare.UI.ViewModels;

namespace WinCompare.UI.Views;

public sealed partial class FolderDiffPage : Page
{
    private string? _leftFolder;
    private string? _rightFolder;
    private List<FolderDiffDisplayItem> _allItems = new();
    private string _currentFilter = "All";

    private static readonly Brush AddedBg = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 34, 197, 94));
    private static readonly Brush AddedFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 74, 222, 128));
    private static readonly Brush DeletedBg = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 239, 68, 68));
    private static readonly Brush DeletedFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 113, 113));
    private static readonly Brush ModifiedBg = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 234, 179, 8));
    private static readonly Brush ModifiedFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 250, 204, 21));
    private static readonly Brush IdenticalBg = new SolidColorBrush(Windows.UI.Color.FromArgb(20, 156, 163, 175));
    private static readonly Brush IdenticalFg = new SolidColorBrush(Windows.UI.Color.FromArgb(200, 156, 163, 175));

    private static readonly Brush CodeIconBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 59, 130, 246));
    private static readonly Brush ExcelIconBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 197, 94));
    private static readonly Brush WordIconBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 59, 130, 246));
    private static readonly Brush FileIconBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184));

    public FolderDiffPage()
    {
        this.InitializeComponent();
        ModeComboBox.SelectedIndex = (int)SettingsManager.Instance.DefaultFolderDiffMode;
        UpdateLocalization();
        LanguageManager.Instance.LanguageChanged += UpdateLocalization;
    }

    private void UpdateLocalization()
    {
        PickLeftBtn.Label = LanguageManager.Instance.Text("pickLeftFolder");
        PickRightBtn.Label = LanguageManager.Instance.Text("pickRightFolder");
        StartScanBtn.Label = LanguageManager.Instance.Text("startCompare");
        ModeComboItem0.Content = LanguageManager.Instance.Text("quickMode");
        ModeComboItem1.Content = LanguageManager.Instance.Text("deepHashMode");
        SyncMenuBtn.Label = LanguageManager.Instance.Text("syncAction");
        SyncLeftRightItem.Text = LanguageManager.Instance.Text("syncLeftToRight");
        SyncRightLeftItem.Text = LanguageManager.Instance.Text("syncRightToLeft");
        if (ExportReportBtn != null) ExportReportBtn.Label = LanguageManager.Instance.Text("exportDiff");
        if (ClearAllBtn != null) ClearAllBtn.Label = LanguageManager.Instance.Text("clearAll");

        RadioAll.Content = LanguageManager.Instance.Text("filterAll");
        RadioDiff.Content = LanguageManager.Instance.Text("filterDiffOnly");
        RadioMod.Content = LanguageManager.Instance.Text("filterModified");
        RadioAdd.Content = LanguageManager.Instance.Text("filterAdded");
        RadioDel.Content = LanguageManager.Instance.Text("filterDeleted");

        LeftBrowseBtn.Content = LanguageManager.Instance.Text("browse");
        RightBrowseBtn.Content = LanguageManager.Instance.Text("browse");
        ToolTipService.SetToolTip(SwapBtn, LanguageManager.Instance.Text("swapFolders"));

        if (string.IsNullOrEmpty(_leftFolder))
        {
            LeftFolderPathText.Text = LanguageManager.Instance.Text("leftFolderPrompt");
        }
        if (string.IsNullOrEmpty(_rightFolder))
        {
            RightFolderPathText.Text = LanguageManager.Instance.Text("rightFolderPrompt");
        }

        ColHeaderStatus.Text = LanguageManager.Instance.Text("folderColStatus");
        ColHeaderPath.Text = LanguageManager.Instance.Text("folderColPath");
        ColHeaderLeftSize.Text = LanguageManager.Instance.Text("folderColLeftSize");
        ColHeaderRightSize.Text = LanguageManager.Instance.Text("folderColRightSize");

        EmptyPromptText.Text = LanguageManager.Instance.Text("folderEmptyPrompt");
        EmptyActionBtn.Content = LanguageManager.Instance.Text("folderEmptyAction");
        SyncStatusText.Text = LanguageManager.Instance.Text("syncStatusReady");

        if (_allItems.Count > 0)
        {
            int modCount = _allItems.Count(i => i.Status == FolderEntryStatus.Modified);
            int addCount = _allItems.Count(i => i.Status == FolderEntryStatus.Added);
            int delCount = _allItems.Count(i => i.Status == FolderEntryStatus.Deleted);
            int sameCount = _allItems.Count(i => i.Status == FolderEntryStatus.Identical);

            foreach (var item in _allItems)
            {
                item.StatusText = item.Status switch
                {
                    FolderEntryStatus.Added => LanguageManager.Instance.Text("statusAdded"),
                    FolderEntryStatus.Deleted => LanguageManager.Instance.Text("statusDeleted"),
                    FolderEntryStatus.Modified => LanguageManager.Instance.Text("statusModified"),
                    _ => LanguageManager.Instance.Text("statusIdentical")
                };
            }

            StatusBarText.Text = string.Format(LanguageManager.Instance.Text("folderSummaryStats"),
                _allItems.Count, modCount, addCount, delCount, sameCount);
            ApplyFilter();
        }
    }

    public void LoadFolders(string? leftFolder, string? rightFolder)
    {
        if (!string.IsNullOrEmpty(leftFolder) && Directory.Exists(leftFolder))
        {
            _leftFolder = leftFolder;
            LeftFolderPathText.Text = leftFolder;
        }

        if (!string.IsNullOrEmpty(rightFolder) && Directory.Exists(rightFolder))
        {
            _rightFolder = rightFolder;
            RightFolderPathText.Text = rightFolder;
        }

        if (!string.IsNullOrEmpty(_leftFolder) && !string.IsNullOrEmpty(_rightFolder))
        {
            RecentHistoryManager.Instance.AddRecord(_leftFolder, _rightFolder, CompareType.FolderDiff);
            StartScan();
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is ValueTuple<string?, string?> paths)
        {
            LoadFolders(paths.Item1, paths.Item2);
        }
    }

    private void OnStartScanClicked(object sender, RoutedEventArgs e)
    {
        StartScan();
    }

    private void StartScan()
    {
        if (string.IsNullOrEmpty(_leftFolder) || string.IsNullOrEmpty(_rightFolder))
        {
            return;
        }

        EmptyStatePanel.Visibility = Visibility.Collapsed;

        var mode = ModeComboBox.SelectedIndex == 1 ? FolderDiffMode.DeepHash : FolderDiffMode.Quick;
        var entries = DiffEngineService.Instance.CompareFolders(_leftFolder, _rightFolder, mode);

        _allItems.Clear();
        int modCount = 0, addCount = 0, delCount = 0, sameCount = 0;

        foreach (var entry in entries)
        {
            var item = new FolderDiffDisplayItem
            {
                RelativePath = entry.RelativePath,
                Status = entry.Status,
                LeftFullPath = Path.Combine(_leftFolder, entry.RelativePath),
                RightFullPath = Path.Combine(_rightFolder, entry.RelativePath),
                LeftSizeFormatted = entry.LeftSize.HasValue ? FormatSize(entry.LeftSize.Value) : "-",
                RightSizeFormatted = entry.RightSize.HasValue ? FormatSize(entry.RightSize.Value) : "-"
            };

            // Status Badge
            switch (entry.Status)
            {
                case FolderEntryStatus.Added:
                    item.StatusText = LanguageManager.Instance.Text("statusAdded");
                    item.StatusBgBrush = AddedBg;
                    item.StatusFgBrush = AddedFg;
                    addCount++;
                    break;
                case FolderEntryStatus.Deleted:
                    item.StatusText = LanguageManager.Instance.Text("statusDeleted");
                    item.StatusBgBrush = DeletedBg;
                    item.StatusFgBrush = DeletedFg;
                    delCount++;
                    break;
                case FolderEntryStatus.Modified:
                    item.StatusText = LanguageManager.Instance.Text("statusModified");
                    item.StatusBgBrush = ModifiedBg;
                    item.StatusFgBrush = ModifiedFg;
                    modCount++;
                    break;
                default:
                    item.StatusText = LanguageManager.Instance.Text("statusIdentical");
                    item.StatusBgBrush = IdenticalBg;
                    item.StatusFgBrush = IdenticalFg;
                    sameCount++;
                    break;
            }

            // File extension icon
            string ext = Path.GetExtension(entry.RelativePath).ToLowerInvariant();
            if (ext is ".xlsx" or ".xls" or ".csv")
            {
                item.IconGlyph = "\uF0EC";
                item.IconColorBrush = ExcelIconBrush;
            }
            else if (ext is ".docx" or ".doc")
            {
                item.IconGlyph = "\uE8A5";
                item.IconColorBrush = WordIconBrush;
            }
            else if (ext is ".cs" or ".rs" or ".js" or ".ts" or ".html" or ".css" or ".json" or ".md" or ".py" or ".cpp")
            {
                item.IconGlyph = "\uE943";
                item.IconColorBrush = CodeIconBrush;
            }
            else
            {
                item.IconGlyph = "\uE8A5";
                item.IconColorBrush = FileIconBrush;
            }

            _allItems.Add(item);
        }

        StatusBarText.Text = string.Format(LanguageManager.Instance.Text("folderSummaryStats"),
            _allItems.Count, modCount, addCount, delCount, sameCount);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<FolderDiffDisplayItem> filtered = _allItems;

        switch (_currentFilter)
        {
            case "DiffOnly":
                filtered = _allItems.Where(i => i.Status != FolderEntryStatus.Identical);
                break;
            case "Modified":
                filtered = _allItems.Where(i => i.Status == FolderEntryStatus.Modified);
                break;
            case "Added":
                filtered = _allItems.Where(i => i.Status == FolderEntryStatus.Added);
                break;
            case "Deleted":
                filtered = _allItems.Where(i => i.Status == FolderEntryStatus.Deleted);
                break;
        }

        FolderDiffListView.ItemsSource = filtered.ToList();
    }

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FilterRadioGroup.SelectedItem is RadioButton rb && rb.Tag is string tag)
        {
            _currentFilter = tag;
            ApplyFilter();
        }
    }

    private void OnModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_allItems.Count > 0)
        {
            StartScan();
        }
    }

    private void OnSwapFoldersClicked(object sender, RoutedEventArgs e)
    {
        (_leftFolder, _rightFolder) = (_rightFolder, _leftFolder);
        LeftFolderPathText.Text = _leftFolder ?? LanguageManager.Instance.Text("leftFolderPrompt");
        RightFolderPathText.Text = _rightFolder ?? LanguageManager.Instance.Text("rightFolderPrompt");
        if (!string.IsNullOrEmpty(_leftFolder) && !string.IsNullOrEmpty(_rightFolder))
        {
            StartScan();
        }
    }

    private void OnItemDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (FolderDiffListView.SelectedItem is FolderDiffDisplayItem item)
        {
            string ext = Path.GetExtension(item.RelativePath).ToLowerInvariant();

            string leftPath = File.Exists(item.LeftFullPath) ? item.LeftFullPath : (item.RightFullPath);
            string rightPath = File.Exists(item.RightFullPath) ? item.RightFullPath : (item.LeftFullPath);

            if (ext is ".xlsx" or ".xls" or ".csv")
            {
                App.MainWindowInstance?.OpenExcelDiffTab(leftPath, rightPath);
            }
            else if (ext is ".docx" or ".doc")
            {
                App.MainWindowInstance?.OpenWordDiffTab(leftPath, rightPath);
            }
            else
            {
                App.MainWindowInstance?.OpenTextDiffTab(leftPath, rightPath);
            }
        }
    }

    private async void OnPickLeftFolderClicked(object sender, RoutedEventArgs e)
    {
        var folder = await PickerHelper.PickSingleFolderAsync(App.MainWindowInstance);
        if (folder != null)
        {
            _leftFolder = folder.Path;
            LeftFolderPathText.Text = folder.Path;
            if (!string.IsNullOrEmpty(_rightFolder))
            {
                RecentHistoryManager.Instance.AddRecord(_leftFolder, _rightFolder, CompareType.FolderDiff);
                StartScan();
            }
        }
    }

    private async void OnPickRightFolderClicked(object sender, RoutedEventArgs e)
    {
        var folder = await PickerHelper.PickSingleFolderAsync(App.MainWindowInstance);
        if (folder != null)
        {
            _rightFolder = folder.Path;
            RightFolderPathText.Text = folder.Path;
            if (!string.IsNullOrEmpty(_leftFolder))
            {
                RecentHistoryManager.Instance.AddRecord(_leftFolder, _rightFolder, CompareType.FolderDiff);
                StartScan();
            }
        }
    }

    private void OnLeftFolderDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void OnLeftFolderDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFolder folder)
            {
                _leftFolder = folder.Path;
                LeftFolderPathText.Text = folder.Path;
                if (!string.IsNullOrEmpty(_rightFolder)) StartScan();
            }
        }
    }

    private void OnRightFolderDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void OnRightFolderDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFolder folder)
            {
                _rightFolder = folder.Path;
                RightFolderPathText.Text = folder.Path;
                if (!string.IsNullOrEmpty(_leftFolder)) StartScan();
            }
        }
    }

    private async void OnSyncLeftToRightClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_leftFolder) || string.IsNullOrEmpty(_rightFolder)) return;

        int copied = 0;
        foreach (var item in _allItems.Where(i => i.Status == FolderEntryStatus.Added || i.Status == FolderEntryStatus.Modified))
        {
            if (File.Exists(item.LeftFullPath))
            {
                string dest = Path.Combine(_rightFolder, item.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(item.LeftFullPath, dest, true);
                copied++;
            }
        }

        SyncStatusText.Text = string.Format(LanguageManager.Instance.Text("syncedLeftToRight"), copied);
        StartScan();
    }

    private async void OnSyncRightToLeftClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_leftFolder) || string.IsNullOrEmpty(_rightFolder)) return;

        int copied = 0;
        foreach (var item in _allItems.Where(i => i.Status == FolderEntryStatus.Deleted || i.Status == FolderEntryStatus.Modified))
        {
            if (File.Exists(item.RightFullPath))
            {
                string dest = Path.Combine(_leftFolder, item.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(item.RightFullPath, dest, true);
                copied++;
            }
        }

        SyncStatusText.Text = string.Format(LanguageManager.Instance.Text("syncedRightToLeft"), copied);
        StartScan();
    }

    private void OnExportDiffClicked(object sender, RoutedEventArgs e)
    {
        if (_allItems.Count == 0) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Format(LanguageManager.Instance.Text("folderDiffReportTitle"), _leftFolder, _rightFolder));
        sb.AppendLine(string.Format(LanguageManager.Instance.Text("exportTimeLabel"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        sb.AppendLine();
        string hStatus = LanguageManager.Instance.Text("reportHeaderStatus");
        string hPath = LanguageManager.Instance.Text("reportHeaderPath");
        string hLeft = LanguageManager.Instance.Text("reportHeaderLeftSize");
        string hRight = LanguageManager.Instance.Text("reportHeaderRightSize");
        sb.AppendLine($"| {hStatus} | {hPath} | {hLeft} | {hRight} |");
        sb.AppendLine("|---|---|---|---|");

        foreach (var item in _allItems.Where(i => i.Status != FolderEntryStatus.Identical))
        {
            sb.AppendLine($"| {item.StatusText} | {item.RelativePath} | {item.LeftSizeFormatted} | {item.RightSizeFormatted} |");
        }

        var dp = new DataPackage();
        dp.SetText(sb.ToString());
        Clipboard.SetContent(dp);

        SyncStatusText.Text = LanguageManager.Instance.Text("exportSuccess");
    }

    private void OnClearAllClicked(object sender, RoutedEventArgs e)
    {
        _leftFolder = null;
        _rightFolder = null;
        _allItems.Clear();
        FolderDiffListView.ItemsSource = null;
        LeftFolderPathText.Text = LanguageManager.Instance.Text("leftFolderPrompt");
        RightFolderPathText.Text = LanguageManager.Instance.Text("rightFolderPrompt");
        EmptyStatePanel.Visibility = Visibility.Visible;
        StatusBarText.Text = LanguageManager.Instance.Text("notCompared");
        SyncStatusText.Text = LanguageManager.Instance.Text("syncStatusReady");
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
