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
    private int _folderDiffMode = 0; // 0: Fast, 1: DeepHash
    private bool _isInitialized = false;

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

    private static readonly Brush ActivePillBg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 37, 99, 235));
    private static readonly Brush InactivePillBg = new SolidColorBrush(Colors.Transparent);
    private static readonly Brush ActivePillFg = new SolidColorBrush(Colors.White);

    public FolderDiffPage()
    {
        this.InitializeComponent();
        _folderDiffMode = (int)SettingsManager.Instance.DefaultFolderDiffMode;
        SetModeButtonsVisual(_folderDiffMode);

        this.ActualThemeChanged += (s, e) => SetModeButtonsVisual(_folderDiffMode);

        UpdateLocalization();
        LanguageManager.Instance.LanguageChanged += UpdateLocalization;

        _isInitialized = true;
        UpdateWorkspaceVisibility();
    }

    private void UpdateLocalization()
    {
        if (ModeFastText != null) ModeFastText.Text = LanguageManager.Instance.Text("quickMode");
        if (ModeHashText != null) ModeHashText.Text = LanguageManager.Instance.Text("deepHashMode");
        if (SyncLeftRightText != null) SyncLeftRightText.Text = "从左向右同步";
        if (SyncRightLeftText != null) SyncRightLeftText.Text = "从右向左同步";
        if (RefreshText != null) RefreshText.Text = LanguageManager.Instance.Text("refreshCompare");
        if (ClearAllText != null) ClearAllText.Text = LanguageManager.Instance.Text("clearAll");

        if (LeftBrowseBtn != null) LeftBrowseBtn.Content = LanguageManager.Instance.Text("selectEllipsis");
        if (RightBrowseBtn != null) RightBrowseBtn.Content = LanguageManager.Instance.Text("selectEllipsis");
        if (LeftPickBtn != null) LeftPickBtn.Content = "选择源文件夹...";
        if (RightPickBtn != null) RightPickBtn.Content = "选择目标文件夹...";

        if (ColHeaderStatus != null) ColHeaderStatus.Text = LanguageManager.Instance.Text("folderColStatus");
        if (ColHeaderPath != null) ColHeaderPath.Text = LanguageManager.Instance.Text("folderColPath");
        if (ColHeaderLeftSize != null) ColHeaderLeftSize.Text = LanguageManager.Instance.Text("folderColLeftSize");
        if (ColHeaderRightSize != null) ColHeaderRightSize.Text = LanguageManager.Instance.Text("folderColRightSize");

        if (LeftEmptyTitle != null) LeftEmptyTitle.Text = "未选择源文件夹";
        if (RightEmptyTitle != null) RightEmptyTitle.Text = "未选择目标文件夹";
        if (LeftEmptyDesc != null) LeftEmptyDesc.Text = "拖拽文件夹至此处或点击下方按钮选择";
        if (RightEmptyDesc != null) RightEmptyDesc.Text = "拖拽文件夹至此处或点击下方按钮选择";

        if (string.IsNullOrEmpty(_leftFolder) && LeftFolderHeader != null)
        {
            LeftFolderHeader.Text = "未选择源文件夹";
        }
        if (string.IsNullOrEmpty(_rightFolder) && RightFolderHeader != null)
        {
            RightFolderHeader.Text = "未选择目标文件夹";
        }
        if (SyncStatusText != null) SyncStatusText.Text = LanguageManager.Instance.Text("syncStatusReady");
    }

    private void UpdateWorkspaceVisibility()
    {
        if (!_isInitialized) return;

        bool hasLeft = !string.IsNullOrEmpty(_leftFolder);
        bool hasRight = !string.IsNullOrEmpty(_rightFolder);

        if (!hasLeft && !hasRight)
        {
            if (EmptyStateContainer != null) EmptyStateContainer.Visibility = Visibility.Visible;
            if (ResultContainer != null) ResultContainer.Visibility = Visibility.Collapsed;

            if (SyncLeftRightBtn != null) SyncLeftRightBtn.IsEnabled = false;
            if (SyncRightLeftBtn != null) SyncRightLeftBtn.IsEnabled = false;
            if (RefreshBtn != null) RefreshBtn.IsEnabled = false;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = false;
            if (SyncPreviewBtn != null) SyncPreviewBtn.IsEnabled = false;
            if (StatusBarText != null) StatusBarText.Text = "请选择左右两侧文件夹以开始比对。";
        }
        else
        {
            bool hasBoth = hasLeft && hasRight;
            if (EmptyStateContainer != null) EmptyStateContainer.Visibility = hasBoth ? Visibility.Collapsed : Visibility.Visible;
            if (ResultContainer != null) ResultContainer.Visibility = hasBoth ? Visibility.Visible : Visibility.Collapsed;

            if (SyncLeftRightBtn != null) SyncLeftRightBtn.IsEnabled = hasBoth;
            if (SyncRightLeftBtn != null) SyncRightLeftBtn.IsEnabled = hasBoth;
            if (RefreshBtn != null) RefreshBtn.IsEnabled = hasBoth;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = true;
            if (SyncPreviewBtn != null) SyncPreviewBtn.IsEnabled = hasBoth;
        }
    }

    public void LoadFolders(string? leftFolder, string? rightFolder)
    {
        if (!string.IsNullOrEmpty(leftFolder) && Directory.Exists(leftFolder))
        {
            _leftFolder = leftFolder;
            if (LeftFolderHeader != null) LeftFolderHeader.Text = Path.GetFileName(leftFolder);
        }

        if (!string.IsNullOrEmpty(rightFolder) && Directory.Exists(rightFolder))
        {
            _rightFolder = rightFolder;
            if (RightFolderHeader != null) RightFolderHeader.Text = Path.GetFileName(rightFolder);
        }

        UpdateWorkspaceVisibility();

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
        if (!_isInitialized) return;
        StartScan();
    }

    private void OnModeFastClicked(object sender, RoutedEventArgs e)
    {
        _folderDiffMode = 0;
        SetModeButtonsVisual(0);
        if (!string.IsNullOrEmpty(_leftFolder) && !string.IsNullOrEmpty(_rightFolder))
        {
            StartScan();
        }
    }

    private void OnModeHashClicked(object sender, RoutedEventArgs e)
    {
        _folderDiffMode = 1;
        SetModeButtonsVisual(1);
        if (!string.IsNullOrEmpty(_leftFolder) && !string.IsNullOrEmpty(_rightFolder))
        {
            StartScan();
        }
    }

    private Brush GetInactivePillForeground()
    {
        bool isDark = this.ActualTheme == ElementTheme.Dark;
        return isDark 
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 203, 213, 225)) // #CBD5E1 Slate 300 for dark mode
            : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 71, 85, 105));   // #475569 Slate 600 for light mode
    }

    private void SetModeButtonsVisual(int activeMode)
    {
        var inactiveFg = GetInactivePillForeground();

        if (ModeFastBtn != null)
        {
            ModeFastBtn.Background = activeMode == 0 ? ActivePillBg : InactivePillBg;
            ModeFastBtn.Foreground = activeMode == 0 ? ActivePillFg : inactiveFg;
            ModeFastBtn.FontWeight = activeMode == 0 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }
        if (ModeHashBtn != null)
        {
            ModeHashBtn.Background = activeMode == 1 ? ActivePillBg : InactivePillBg;
            ModeHashBtn.Foreground = activeMode == 1 ? ActivePillFg : inactiveFg;
            ModeHashBtn.FontWeight = activeMode == 1 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }
    }

    private async void StartScan()
    {
        if (string.IsNullOrEmpty(_leftFolder) || string.IsNullOrEmpty(_rightFolder))
        {
            UpdateWorkspaceVisibility();
            return;
        }

        UpdateWorkspaceVisibility();

        if (StatusBarText != null)
            StatusBarText.Text = LanguageManager.Instance.Text("scanningStatus");

        var mode = (FolderDiffMode)_folderDiffMode;
        var diffService = DiffEngineService.Instance;

        var entries = await System.Threading.Tasks.Task.Run(() =>
        {
            return diffService.CompareFolders(_leftFolder, _rightFolder, mode);
        });

        _allItems.Clear();
        foreach (var entry in entries)
        {
            var item = new FolderDiffDisplayItem
            {
                RelativePath = entry.RelativePath,
                Status = entry.Status,
                LeftSizeFormatted = entry.LeftSize.HasValue ? FormatSize(entry.LeftSize.Value) : "-",
                RightSizeFormatted = entry.RightSize.HasValue ? FormatSize(entry.RightSize.Value) : "-"
            };

            switch (entry.Status)
            {
                case FolderEntryStatus.Added:
                    item.StatusText = LanguageManager.Instance.Text("statusAdded");
                    item.StatusBgBrush = AddedBg;
                    item.StatusFgBrush = AddedFg;
                    break;
                case FolderEntryStatus.Deleted:
                    item.StatusText = LanguageManager.Instance.Text("statusDeleted");
                    item.StatusBgBrush = DeletedBg;
                    item.StatusFgBrush = DeletedFg;
                    break;
                case FolderEntryStatus.Modified:
                    item.StatusText = LanguageManager.Instance.Text("statusModified");
                    item.StatusBgBrush = ModifiedBg;
                    item.StatusFgBrush = ModifiedFg;
                    break;
                default:
                    item.StatusText = LanguageManager.Instance.Text("statusIdentical");
                    item.StatusBgBrush = IdenticalBg;
                    item.StatusFgBrush = IdenticalFg;
                    break;
            }

            string ext = Path.GetExtension(entry.RelativePath).ToLowerInvariant();
            if (ext is ".cs" or ".rs" or ".js" or ".ts" or ".py" or ".cpp" or ".h" or ".json" or ".xml")
            {
                item.IconGlyph = "\uE943";
                item.IconColorBrush = CodeIconBrush;
            }
            else if (ext is ".xlsx" or ".xls" or ".csv")
            {
                item.IconGlyph = "\uF0EC";
                item.IconColorBrush = ExcelIconBrush;
            }
            else if (ext is ".docx" or ".doc")
            {
                item.IconGlyph = "\uE8A5";
                item.IconColorBrush = WordIconBrush;
            }
            else
            {
                item.IconGlyph = "\uE8A5";
                item.IconColorBrush = FileIconBrush;
            }

            _allItems.Add(item);
        }

        ApplyFilter();

        int modCount = _allItems.Count(i => i.Status == FolderEntryStatus.Modified);
        int addCount = _allItems.Count(i => i.Status == FolderEntryStatus.Added);
        int delCount = _allItems.Count(i => i.Status == FolderEntryStatus.Deleted);
        int sameCount = _allItems.Count(i => i.Status == FolderEntryStatus.Identical);

        if (StatusBarText != null)
        {
            StatusBarText.Text = string.Format(LanguageManager.Instance.Text("folderSummaryStats"),
                _allItems.Count, modCount, addCount, delCount, sameCount);
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<FolderDiffDisplayItem> filtered = _allItems;

        if (_currentFilter == "DiffOnly")
        {
            filtered = filtered.Where(i => i.Status != FolderEntryStatus.Identical);
        }
        else if (_currentFilter == "Modified")
        {
            filtered = filtered.Where(i => i.Status == FolderEntryStatus.Modified);
        }
        else if (_currentFilter == "Added")
        {
            filtered = filtered.Where(i => i.Status == FolderEntryStatus.Added);
        }
        else if (_currentFilter == "Deleted")
        {
            filtered = filtered.Where(i => i.Status == FolderEntryStatus.Deleted);
        }

        if (FolderDiffListView != null)
        {
            FolderDiffListView.ItemsSource = null;
            FolderDiffListView.ItemsSource = filtered.ToList();
        }
    }

    private async void OnPickLeftFolderClicked(object sender, RoutedEventArgs e)
    {
        var folder = await PickerHelper.PickSingleFolderAsync(App.ActiveWindow);
        if (folder != null)
        {
            _leftFolder = folder.Path;
            if (LeftFolderHeader != null) LeftFolderHeader.Text = folder.Name;
            UpdateWorkspaceVisibility();

            if (!string.IsNullOrEmpty(_rightFolder))
            {
                RecentHistoryManager.Instance.AddRecord(_leftFolder, _rightFolder, CompareType.FolderDiff);
                StartScan();
            }
        }
    }

    private async void OnPickRightFolderClicked(object sender, RoutedEventArgs e)
    {
        var folder = await PickerHelper.PickSingleFolderAsync(App.ActiveWindow);
        if (folder != null)
        {
            _rightFolder = folder.Path;
            if (RightFolderHeader != null) RightFolderHeader.Text = folder.Name;
            UpdateWorkspaceVisibility();

            if (!string.IsNullOrEmpty(_leftFolder))
            {
                RecentHistoryManager.Instance.AddRecord(_leftFolder, _rightFolder, CompareType.FolderDiff);
                StartScan();
            }
        }
    }

    private void OnSwapFoldersClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_leftFolder) && string.IsNullOrEmpty(_rightFolder)) return;

        var temp = _leftFolder;
        _leftFolder = _rightFolder;
        _rightFolder = temp;

        if (LeftFolderHeader != null)
            LeftFolderHeader.Text = !string.IsNullOrEmpty(_leftFolder) ? Path.GetFileName(_leftFolder) : "未选择源文件夹";
        if (RightFolderHeader != null)
            RightFolderHeader.Text = !string.IsNullOrEmpty(_rightFolder) ? Path.GetFileName(_rightFolder) : "未选择目标文件夹";

        if (!string.IsNullOrEmpty(_leftFolder) && !string.IsNullOrEmpty(_rightFolder))
        {
            StartScan();
        }
    }

    private void OnSelectDocsDirClicked(object sender, RoutedEventArgs e)
    {
        string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        AssignQuickDirectory(path);
    }

    private void OnSelectDownloadsDirClicked(object sender, RoutedEventArgs e)
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string path = Path.Combine(userProfile, "Downloads");
        AssignQuickDirectory(path);
    }

    private void OnSelectDesktopDirClicked(object sender, RoutedEventArgs e)
    {
        string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        AssignQuickDirectory(path);
    }

    private void AssignQuickDirectory(string path)
    {
        if (!Directory.Exists(path)) return;

        if (string.IsNullOrEmpty(_leftFolder))
        {
            _leftFolder = path;
            if (LeftFolderHeader != null) LeftFolderHeader.Text = Path.GetFileName(path);
        }
        else
        {
            _rightFolder = path;
            if (RightFolderHeader != null) RightFolderHeader.Text = Path.GetFileName(path);
        }

        UpdateWorkspaceVisibility();

        if (!string.IsNullOrEmpty(_leftFolder) && !string.IsNullOrEmpty(_rightFolder))
        {
            RecentHistoryManager.Instance.AddRecord(_leftFolder, _rightFolder, CompareType.FolderDiff);
            StartScan();
        }
    }

    private void OnClearAllClicked(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        _leftFolder = null;
        _rightFolder = null;
        _allItems.Clear();
        if (FolderDiffListView != null) FolderDiffListView.ItemsSource = null;

        if (LeftFolderHeader != null) LeftFolderHeader.Text = "未选择源文件夹";
        if (RightFolderHeader != null) RightFolderHeader.Text = "未选择目标文件夹";

        if (StatusBarText != null) StatusBarText.Text = "请选择左右两侧文件夹以开始比对。";
        if (SyncStatusText != null) SyncStatusText.Text = LanguageManager.Instance.Text("syncStatusReady");

        UpdateWorkspaceVisibility();
    }

    private async void OnSyncLeftToRightClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_leftFolder) || string.IsNullOrEmpty(_rightFolder)) return;

        var dialog = new ContentDialog
        {
            Title = LanguageManager.Instance.Text("syncConfirmTitle"),
            Content = string.Format(LanguageManager.Instance.Text("syncLeftToRightConfirm"), _leftFolder, _rightFolder),
            PrimaryButtonText = LanguageManager.Instance.Text("confirmAction"),
            CloseButtonText = LanguageManager.Instance.Text("cancelAction"),
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ExecuteSync(_leftFolder, _rightFolder);
        }
    }

    private async void OnSyncRightToLeftClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_leftFolder) || string.IsNullOrEmpty(_rightFolder)) return;

        var dialog = new ContentDialog
        {
            Title = LanguageManager.Instance.Text("syncConfirmTitle"),
            Content = string.Format(LanguageManager.Instance.Text("syncRightToLeftConfirm"), _rightFolder, _leftFolder),
            PrimaryButtonText = LanguageManager.Instance.Text("confirmAction"),
            CloseButtonText = LanguageManager.Instance.Text("cancelAction"),
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ExecuteSync(_rightFolder, _leftFolder);
        }
    }

    private async void OnSyncPreviewClicked(object sender, RoutedEventArgs e)
    {
        if (_allItems.Count == 0) return;
        int diffCount = _allItems.Count(i => i.Status != FolderEntryStatus.Identical);

        var dialog = new ContentDialog
        {
            Title = "同步预览",
            Content = $"当前共检测到 {diffCount} 处差异文件需要同步更新。\n点击同步按钮即可执行双向/单向文件镜像覆盖。",
            CloseButtonText = "关闭",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void ExecuteSync(string source, string destination)
    {
        if (SyncStatusText != null)
            SyncStatusText.Text = LanguageManager.Instance.Text("syncInProgress");

        try
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                CopyDirectory(source, destination);
            });

            if (SyncStatusText != null)
                SyncStatusText.Text = LanguageManager.Instance.Text("syncCompleted");
            StartScan();
        }
        catch (Exception ex)
        {
            if (SyncStatusText != null)
                SyncStatusText.Text = string.Format(LanguageManager.Instance.Text("syncFailed"), ex.Message);
        }
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            string destDir = Path.Combine(targetDir, Path.GetFileName(dir));
            CopyDirectory(dir, destDir);
        }
    }

    private void OnItemDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (FolderDiffListView?.SelectedItem is FolderDiffDisplayItem item)
        {
            string leftFile = !string.IsNullOrEmpty(_leftFolder) ? Path.Combine(_leftFolder, item.RelativePath) : "";
            string rightFile = !string.IsNullOrEmpty(_rightFolder) ? Path.Combine(_rightFolder, item.RelativePath) : "";

            if (App.ActiveWindow is MainWindow mainWin)
            {
                string ext = Path.GetExtension(item.RelativePath).ToLowerInvariant();
                if (ext is ".xlsx" or ".xls" or ".csv")
                {
                    mainWin.OpenExcelDiffTab(leftFile, rightFile);
                }
                else if (ext is ".docx" or ".doc")
                {
                    mainWin.OpenWordDiffTab(leftFile, rightFile);
                }
                else
                {
                    mainWin.OpenTextDiffTab(leftFile, rightFile);
                }
            }
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    private void OnLeftFolderDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void OnLeftFolderDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFolder folder)
            {
                _leftFolder = folder.Path;
                if (LeftFolderHeader != null) LeftFolderHeader.Text = folder.Name;
                UpdateWorkspaceVisibility();

                if (!string.IsNullOrEmpty(_rightFolder))
                {
                    RecentHistoryManager.Instance.AddRecord(_leftFolder, _rightFolder, CompareType.FolderDiff);
                    StartScan();
                }
            }
        }
    }

    private void OnRightFolderDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void OnRightFolderDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFolder folder)
            {
                _rightFolder = folder.Path;
                if (RightFolderHeader != null) RightFolderHeader.Text = folder.Name;
                UpdateWorkspaceVisibility();

                if (!string.IsNullOrEmpty(_leftFolder))
                {
                    RecentHistoryManager.Instance.AddRecord(_leftFolder, _rightFolder, CompareType.FolderDiff);
                    StartScan();
                }
            }
        }
    }
}
