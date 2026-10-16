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

public sealed partial class FolderDiffPage : Page, IDisposable
{
    private string? _leftFolder;
    private string? _rightFolder;
    private List<FolderDiffDisplayItem> _allItems = new();
    private string _currentFilter = "All";
    private int _folderDiffMode = 0; // 0: Fast, 1: DeepHash
    private bool _isInitialized = false;
    private bool _isScanning = false;

    private static Brush AddedBg => App.GetThemeBrush("DiffAddedBgBrush");
    private static Brush AddedFg => App.GetThemeBrush("DiffAddedFgBrush");
    private static Brush DeletedBg => App.GetThemeBrush("DiffDeletedBgBrush");
    private static Brush DeletedFg => App.GetThemeBrush("DiffDeletedFgBrush");
    private static Brush ModifiedBg => App.GetThemeBrush("DiffModifiedBgBrush");
    private static Brush ModifiedFg => App.GetThemeBrush("DiffModifiedFgBrush");
    private static Brush IdenticalBg => App.GetThemeBrush("DiffIdenticalBgBrush");
    private static Brush IdenticalFg => App.GetThemeBrush("DiffIdenticalFgBrush");

    private static Brush MissingBg => App.GetThemeBrush("DiffMissingBgBrush");
    private static Brush MissingFg => App.GetThemeBrush("DiffMissingFgBrush");
    private static readonly Brush TransparentBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    private static Brush FolderIconBrush => App.GetThemeBrush("FolderIconBrush");

    private static Brush CodeIconBrush => App.GetThemeBrush("CodeIconBrush");
    private static Brush ExcelIconBrush => App.GetThemeBrush("ExcelIconBrush");
    private static Brush WordIconBrush => App.GetThemeBrush("WordIconBrush");
    private static Brush FileIconBrush => App.GetThemeBrush("GenericFileIconBrush");

    private static Brush ActivePillBg => App.GetThemeBrush("BrandPrimaryBrush");
    private static readonly Brush InactivePillBg = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    private static Brush ActivePillFg => App.GetThemeBrush("TextOnAccentBrush");

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
        if (SyncLeftRightText != null) SyncLeftRightText.Text = LanguageManager.Instance.Text("syncLeftToRight");
        if (SyncRightLeftText != null) SyncRightLeftText.Text = LanguageManager.Instance.Text("syncRightToLeft");
        if (RefreshText != null) RefreshText.Text = LanguageManager.Instance.Text("refreshCompare");
        if (ClearAllText != null) ClearAllText.Text = LanguageManager.Instance.Text("clearAll");
        if (SyncPreviewText != null) SyncPreviewText.Text = LanguageManager.Instance.Text("dryRunPreview");

        if (SyncLeftRightBtn != null) ToolTipService.SetToolTip(SyncLeftRightBtn, LanguageManager.Instance.Text("syncLeftToRight"));
        if (SyncRightLeftBtn != null) ToolTipService.SetToolTip(SyncRightLeftBtn, LanguageManager.Instance.Text("syncRightToLeft"));
        if (RefreshBtn != null) ToolTipService.SetToolTip(RefreshBtn, LanguageManager.Instance.Text("refreshCompare"));
        if (ClearAllBtn != null) ToolTipService.SetToolTip(ClearAllBtn, LanguageManager.Instance.Text("clearAll"));

        // Sidebar
        if (QuickPlacesTitle != null) QuickPlacesTitle.Text = LanguageManager.Instance.Text("quickPlaces");
        if (DocsDirText != null) DocsDirText.Text = LanguageManager.Instance.Text("documents");
        if (DownloadsDirText != null) DownloadsDirText.Text = LanguageManager.Instance.Text("downloads");
        if (DesktopDirText != null) DesktopDirText.Text = LanguageManager.Instance.Text("desktop");
        if (BrowseDirText != null) BrowseDirText.Text = LanguageManager.Instance.Text("browseFolder");
        if (ToolsTitle != null) ToolsTitle.Text = LanguageManager.Instance.Text("tools");
        if (SwapFoldersText != null) SwapFoldersText.Text = LanguageManager.Instance.Text("swapFolders");
        if (RescanFoldersText != null) RescanFoldersText.Text = LanguageManager.Instance.Text("rescanFolders");
        if (ClearAllSidebarText != null) ClearAllSidebarText.Text = LanguageManager.Instance.Text("clearAll");

        if (LeftBrowseBtn != null) LeftBrowseBtn.Content = LanguageManager.Instance.Text("selectEllipsis");
        if (RightBrowseBtn != null) RightBrowseBtn.Content = LanguageManager.Instance.Text("selectEllipsis");
        if (LeftPickBtn != null) LeftPickBtn.Content = LanguageManager.Instance.Text("chooseSourceFolder");
        if (RightPickBtn != null) RightPickBtn.Content = LanguageManager.Instance.Text("chooseTargetFolder");

        if (ColHeaderLeftName != null) ColHeaderLeftName.Text = LanguageManager.Instance.Text("folderColName");
        if (ColHeaderRightName != null) ColHeaderRightName.Text = LanguageManager.Instance.Text("folderColName");
        if (ColHeaderLeftHash != null) ColHeaderLeftHash.Text = LanguageManager.Instance.Text("folderColHash");
        if (ColHeaderRightHash != null) ColHeaderRightHash.Text = LanguageManager.Instance.Text("folderColHash");
        if (ColHeaderLeftSize != null) ColHeaderLeftSize.Text = LanguageManager.Instance.Text("folderColSize");
        if (ColHeaderRightSize != null) ColHeaderRightSize.Text = LanguageManager.Instance.Text("folderColSize");
        if (ColHeaderLeftModified != null) ColHeaderLeftModified.Text = LanguageManager.Instance.Text("folderColModified");
        if (ColHeaderRightModified != null) ColHeaderRightModified.Text = LanguageManager.Instance.Text("folderColModified");

        if (SingleLeftColName != null) SingleLeftColName.Text = LanguageManager.Instance.Text("folderColName");
        if (SingleLeftColSize != null) SingleLeftColSize.Text = LanguageManager.Instance.Text("folderColSize");
        if (SingleLeftColModified != null) SingleLeftColModified.Text = LanguageManager.Instance.Text("folderColModified");

        if (SingleRightColName != null) SingleRightColName.Text = LanguageManager.Instance.Text("folderColName");
        if (SingleRightColSize != null) SingleRightColSize.Text = LanguageManager.Instance.Text("folderColSize");
        if (SingleRightColModified != null) SingleRightColModified.Text = LanguageManager.Instance.Text("folderColModified");

        if (LeftEmptyTitle != null) LeftEmptyTitle.Text = LanguageManager.Instance.Text("noSourceFolder");
        if (RightEmptyTitle != null) RightEmptyTitle.Text = LanguageManager.Instance.Text("noTargetFolder");
        if (LeftEmptyDesc != null) LeftEmptyDesc.Text = LanguageManager.Instance.Text("dropFolderPrompt");
        if (RightEmptyDesc != null) RightEmptyDesc.Text = LanguageManager.Instance.Text("dropFolderPrompt");

        if (string.IsNullOrEmpty(_leftFolder) && LeftFolderHeader != null)
        {
            LeftFolderHeader.Text = LanguageManager.Instance.Text("noSourceFolder");
        }
        if (string.IsNullOrEmpty(_rightFolder) && RightFolderHeader != null)
        {
            RightFolderHeader.Text = LanguageManager.Instance.Text("noTargetFolder");
        }
        if (SyncStatusText != null) SyncStatusText.Text = LanguageManager.Instance.Text("syncStatusReady");
        if (LeftLoadingText != null) LeftLoadingText.Text = LanguageManager.Instance.Text("loadingSourceFolder");
        if (RightLoadingText != null) RightLoadingText.Text = LanguageManager.Instance.Text("loadingTargetFolder");
        if (DualLeftLoadingText != null) DualLeftLoadingText.Text = LanguageManager.Instance.Text("loadingSourceFolder");
        if (DualRightLoadingText != null) DualRightLoadingText.Text = LanguageManager.Instance.Text("loadingTargetFolder");
        if (string.IsNullOrEmpty(_leftFolder) && string.IsNullOrEmpty(_rightFolder))
        {
            SetStatusPrompt(LanguageManager.Instance.Text("selectTwoFoldersPrompt"));
        }
    }

    private void SetStatusPrompt(string text)
    {
        if (StatusStatsPanel != null) StatusStatsPanel.Visibility = Visibility.Collapsed;
        if (StatusBarText != null)
        {
            StatusBarText.Visibility = Visibility.Visible;
            StatusBarText.Text = text;
        }
    }

    private void UpdateWorkspaceVisibility()
    {
        if (!_isInitialized) return;

        bool hasLeft = !string.IsNullOrEmpty(_leftFolder);
        bool hasRight = !string.IsNullOrEmpty(_rightFolder);

        if (!hasLeft && !hasRight)
        {
            if (LeftEmptyZone != null) LeftEmptyZone.Visibility = Visibility.Visible;
            if (LeftSingleContainer != null) LeftSingleContainer.Visibility = Visibility.Collapsed;

            if (RightEmptyZone != null) RightEmptyZone.Visibility = Visibility.Visible;
            if (RightSingleContainer != null) RightSingleContainer.Visibility = Visibility.Collapsed;

            if (ResultContainer != null) ResultContainer.Visibility = Visibility.Collapsed;

            if (SyncLeftRightBtn != null) SyncLeftRightBtn.IsEnabled = false;
            if (SyncRightLeftBtn != null) SyncRightLeftBtn.IsEnabled = false;
            if (RefreshBtn != null) RefreshBtn.IsEnabled = false;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = false;
            if (SyncPreviewBtn != null) SyncPreviewBtn.IsEnabled = false;
            SetStatusPrompt(LanguageManager.Instance.Text("selectTwoFoldersPrompt"));
        }
        else if (hasLeft && !hasRight)
        {
            if (LeftEmptyZone != null) LeftEmptyZone.Visibility = Visibility.Collapsed;
            if (LeftSingleContainer != null) LeftSingleContainer.Visibility = Visibility.Visible;

            if (RightEmptyZone != null) RightEmptyZone.Visibility = Visibility.Visible;
            if (RightSingleContainer != null) RightSingleContainer.Visibility = Visibility.Collapsed;

            if (ResultContainer != null) ResultContainer.Visibility = Visibility.Collapsed;

            if (SyncLeftRightBtn != null) SyncLeftRightBtn.IsEnabled = false;
            if (SyncRightLeftBtn != null) SyncRightLeftBtn.IsEnabled = false;
            if (RefreshBtn != null) RefreshBtn.IsEnabled = true;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = true;
            if (SyncPreviewBtn != null) SyncPreviewBtn.IsEnabled = false;

            _ = LoadSingleFolderPreviewAsync(_leftFolder!, isLeft: true);
        }
        else if (!hasLeft && hasRight)
        {
            if (LeftEmptyZone != null) LeftEmptyZone.Visibility = Visibility.Visible;
            if (LeftSingleContainer != null) LeftSingleContainer.Visibility = Visibility.Collapsed;

            if (RightEmptyZone != null) RightEmptyZone.Visibility = Visibility.Collapsed;
            if (RightSingleContainer != null) RightSingleContainer.Visibility = Visibility.Visible;

            if (ResultContainer != null) ResultContainer.Visibility = Visibility.Collapsed;

            if (SyncLeftRightBtn != null) SyncLeftRightBtn.IsEnabled = false;
            if (SyncRightLeftBtn != null) SyncRightLeftBtn.IsEnabled = false;
            if (RefreshBtn != null) RefreshBtn.IsEnabled = true;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = true;
            if (SyncPreviewBtn != null) SyncPreviewBtn.IsEnabled = false;

            _ = LoadSingleFolderPreviewAsync(_rightFolder!, isLeft: false);
        }
        else
        {
            if (LeftEmptyZone != null) LeftEmptyZone.Visibility = Visibility.Collapsed;
            if (LeftSingleContainer != null) LeftSingleContainer.Visibility = Visibility.Collapsed;

            if (RightEmptyZone != null) RightEmptyZone.Visibility = Visibility.Collapsed;
            if (RightSingleContainer != null) RightSingleContainer.Visibility = Visibility.Collapsed;

            if (ResultContainer != null) ResultContainer.Visibility = Visibility.Visible;

            if (SyncLeftRightBtn != null) SyncLeftRightBtn.IsEnabled = true;
            if (SyncRightLeftBtn != null) SyncRightLeftBtn.IsEnabled = true;
            if (RefreshBtn != null) RefreshBtn.IsEnabled = true;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = true;
            if (SyncPreviewBtn != null) SyncPreviewBtn.IsEnabled = true;
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
        UpdateTabHeader();

        if (!string.IsNullOrEmpty(_leftFolder) && !string.IsNullOrEmpty(_rightFolder))
        {
            RecentHistoryManager.Instance.AddRecord(_leftFolder!, _rightFolder!, CompareType.FolderDiff);
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
        if (!string.IsNullOrEmpty(_leftFolder) && !string.IsNullOrEmpty(_rightFolder))
        {
            StartScan();
        }
        else if (!string.IsNullOrEmpty(_leftFolder))
        {
            _ = LoadSingleFolderPreviewAsync(_leftFolder, isLeft: true);
        }
        else if (!string.IsNullOrEmpty(_rightFolder))
        {
            _ = LoadSingleFolderPreviewAsync(_rightFolder, isLeft: false);
        }
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
        return App.GetThemeBrush("TextFillColorSecondaryBrush");
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

        var hashVis = activeMode == 1 ? Visibility.Visible : Visibility.Collapsed;
        if (ColHeaderLeftHash != null) ColHeaderLeftHash.Visibility = hashVis;
        if (ColHeaderRightHash != null) ColHeaderRightHash.Visibility = hashVis;

        foreach (var item in _allItems)
        {
            item.HashVisibility = hashVis;
        }
    }

    private async void StartScan()
    {
        if (_isScanning) return;
        if (string.IsNullOrEmpty(_leftFolder) || string.IsNullOrEmpty(_rightFolder))
        {
            UpdateWorkspaceVisibility();
            return;
        }

        string left = _leftFolder;
        string right = _rightFolder;

        try
        {
            _isScanning = true;
            UpdateWorkspaceVisibility();
            if (DualCompareLoadingOverlay != null) DualCompareLoadingOverlay.Visibility = Visibility.Visible;

            SetStatusPrompt(LanguageManager.Instance.Text("scanningStatus"));

            var mode = (FolderDiffMode)_folderDiffMode;
            var diffService = DiffEngineService.Instance;

            var entries = await System.Threading.Tasks.Task.Run(() =>
            {
                return diffService.CompareFolders(left, right, mode);
            });

            _allItems.Clear();
            var hashVis = _folderDiffMode == 1 ? Visibility.Visible : Visibility.Collapsed;

            foreach (var entry in entries)
            {
                bool isLeftMissing = entry.Status == FolderEntryStatus.Added;
                bool isRightMissing = entry.Status == FolderEntryStatus.Deleted;
                bool isDir = entry.IsDirectory;

                string relPath = entry.RelativePath.Replace('/', '\\');
                string fileName = Path.GetFileName(relPath);
                if (string.IsNullOrEmpty(fileName)) fileName = relPath;

                string leftSizeStr = entry.LeftSize.HasValue ? FormatSize(entry.LeftSize.Value) : (isLeftMissing ? "---" : "-");
                string rightSizeStr = entry.RightSize.HasValue ? FormatSize(entry.RightSize.Value) : (isRightMissing ? "---" : "-");

                string leftModStr = entry.LeftModifiedTimestamp.HasValue && entry.LeftModifiedTimestamp.Value > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(entry.LeftModifiedTimestamp.Value).ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                    : (isLeftMissing ? "---" : "-");

                string rightModStr = entry.RightModifiedTimestamp.HasValue && entry.RightModifiedTimestamp.Value > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(entry.RightModifiedTimestamp.Value).ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                    : (isRightMissing ? "---" : "-");

                Brush leftBg, rightBg;
                Brush leftFg, rightFg;

                switch (entry.Status)
                {
                    case FolderEntryStatus.Added: // Right only
                        leftBg = MissingBg;
                        leftFg = MissingFg;
                        rightBg = AddedBg;
                        rightFg = AddedFg;
                        break;
                    case FolderEntryStatus.Deleted: // Left only
                        leftBg = AddedBg;
                        leftFg = AddedFg;
                        rightBg = MissingBg;
                        rightFg = MissingFg;
                        break;
                    case FolderEntryStatus.Modified: // Content or metadata different
                        leftBg = ModifiedBg;
                        leftFg = ModifiedFg;
                        rightBg = ModifiedBg;
                        rightFg = ModifiedFg;
                        break;
                    default: // Identical
                        leftBg = TransparentBrush;
                        leftFg = IdenticalFg;
                        rightBg = TransparentBrush;
                        rightFg = IdenticalFg;
                        break;
                }

                string glyph;
                Brush iconBrush;
                if (isDir)
                {
                    glyph = "\uED25";
                    iconBrush = FolderIconBrush;
                }
                else
                {
                    string ext = Path.GetExtension(entry.RelativePath).ToLowerInvariant();
                    if (ext is ".cs" or ".rs" or ".js" or ".ts" or ".py" or ".cpp" or ".h" or ".json" or ".xml")
                    {
                        glyph = "\uE943";
                        iconBrush = CodeIconBrush;
                    }
                    else if (ext is ".xlsx" or ".xls" or ".csv")
                    {
                        glyph = "\uF0EC";
                        iconBrush = ExcelIconBrush;
                    }
                    else if (ext is ".docx" or ".doc")
                    {
                        glyph = "\uE8A5";
                        iconBrush = WordIconBrush;
                    }
                    else
                    {
                        glyph = "\uE8A5";
                        iconBrush = FileIconBrush;
                    }
                }

                var item = new FolderDiffDisplayItem
                {
                    RelativePath = relPath,
                    Name = fileName,
                    IsDirectory = isDir,
                    Status = entry.Status,

                    IsLeftMissing = isLeftMissing,
                    LeftName = fileName,
                    LeftSizeFormatted = leftSizeStr,
                    LeftModifiedFormatted = leftModStr,
                    LeftHash = entry.LeftHash ?? "---",
                    LeftBgBrush = leftBg,
                    LeftFgBrush = leftFg,
                    LeftIconGlyph = glyph,
                    LeftIconColorBrush = iconBrush,

                    IsRightMissing = isRightMissing,
                    RightName = fileName,
                    RightSizeFormatted = rightSizeStr,
                    RightModifiedFormatted = rightModStr,
                    RightHash = entry.RightHash ?? "---",
                    RightBgBrush = rightBg,
                    RightFgBrush = rightFg,
                    RightIconGlyph = glyph,
                    RightIconColorBrush = iconBrush,

                    HashVisibility = hashVis,

                    // Legacy compatibility fields
                    LeftFullPath = !string.IsNullOrEmpty(_leftFolder) ? Path.Combine(_leftFolder, relPath) : "",
                    RightFullPath = !string.IsNullOrEmpty(_rightFolder) ? Path.Combine(_rightFolder, relPath) : "",
                    StatusText = entry.StatusText,
                    StatusBgBrush = (entry.Status == FolderEntryStatus.Added) ? AddedBg : (entry.Status == FolderEntryStatus.Deleted ? DeletedBg : (entry.Status == FolderEntryStatus.Modified ? ModifiedBg : IdenticalBg)),
                    StatusFgBrush = (entry.Status == FolderEntryStatus.Added) ? AddedFg : (entry.Status == FolderEntryStatus.Deleted ? DeletedFg : (entry.Status == FolderEntryStatus.Modified ? ModifiedFg : IdenticalFg)),
                    IconGlyph = glyph,
                    IconColorBrush = iconBrush
                };

                _allItems.Add(item);
            }

            ApplyFilter();

            int modCount = _allItems.Count(i => i.Status == FolderEntryStatus.Modified);
            int addCount = _allItems.Count(i => i.Status == FolderEntryStatus.Added);
            int delCount = _allItems.Count(i => i.Status == FolderEntryStatus.Deleted);
            int sameCount = _allItems.Count(i => i.Status == FolderEntryStatus.Identical);

            if (StatusStatsPanel != null && StatusBarText != null)
            {
                StatusBarText.Visibility = Visibility.Collapsed;
                StatusStatsPanel.Visibility = Visibility.Visible;
                if (StatusTotalText != null)
                    StatusTotalText.Text = $"{_allItems.Count} {LanguageManager.Instance.Text("itemsCount")},";
                if (StatusModText != null)
                    StatusModText.Text = $"{modCount} {LanguageManager.Instance.Text("modifiedCount")},";
                if (StatusAddText != null)
                    StatusAddText.Text = $"{addCount} {LanguageManager.Instance.Text("addedCount")},";
                if (StatusDelText != null)
                    StatusDelText.Text = $"{delCount} {LanguageManager.Instance.Text("deletedCount")}";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FolderDiffPage] StartScan exception: {ex.Message}");
            SetStatusPrompt(ex.Message);
        }
        finally
        {
            if (DualCompareLoadingOverlay != null) DualCompareLoadingOverlay.Visibility = Visibility.Collapsed;
            _isScanning = false;
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
            if (!CheckFileValidation(folder.Path)) return;

            _leftFolder = folder.Path;
            if (LeftFolderHeader != null) LeftFolderHeader.Text = folder.Name;
            UpdateWorkspaceVisibility();
            UpdateTabHeader();
            if (!string.IsNullOrEmpty(_leftFolder) && !string.IsNullOrEmpty(_rightFolder))
            {
                RecentHistoryManager.Instance.AddRecord(_leftFolder!, _rightFolder!, CompareType.FolderDiff);
                StartScan();
            }
        }
    }

    private async void OnPickRightFolderClicked(object sender, RoutedEventArgs e)
    {
        var folder = await PickerHelper.PickSingleFolderAsync(App.ActiveWindow);
        if (folder != null)
        {
            if (!CheckFileValidation(folder.Path)) return;

            _rightFolder = folder.Path;
            if (RightFolderHeader != null) RightFolderHeader.Text = folder.Name;
            UpdateWorkspaceVisibility();
            UpdateTabHeader();
            if (!string.IsNullOrEmpty(_leftFolder) && !string.IsNullOrEmpty(_rightFolder))
            {
                RecentHistoryManager.Instance.AddRecord(_leftFolder!, _rightFolder!, CompareType.FolderDiff);
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
            LeftFolderHeader.Text = !string.IsNullOrEmpty(_leftFolder) ? Path.GetFileName(_leftFolder) : LanguageManager.Instance.Text("noSourceFolder");
        if (RightFolderHeader != null)
            RightFolderHeader.Text = !string.IsNullOrEmpty(_rightFolder) ? Path.GetFileName(_rightFolder) : LanguageManager.Instance.Text("noTargetFolder");

        UpdateWorkspaceVisibility();
        UpdateTabHeader();
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
            RecentHistoryManager.Instance.AddRecord(_leftFolder!, _rightFolder!, CompareType.FolderDiff);
            StartScan();
        }
    }

    private void OnClearAllClicked(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;

        if (string.IsNullOrEmpty(_leftFolder) && string.IsNullOrEmpty(_rightFolder) && _allItems.Count == 0)
        {
            return;
        }

        var targetWindow = (App.GetWindowForElement(this) as MainWindow) ?? App.ActiveWindow ?? App.MainWindowInstance;
        if (targetWindow != null)
        {
            targetWindow.ShowConfirmDialog(
                LanguageManager.Instance.Text("confirmClearTitle"),
                LanguageManager.Instance.Text("confirmClearMessage"),
                LanguageManager.Instance.Text("clear"),
                () => ExecuteClearAll(),
                isDestructive: true);
        }
        else
        {
            ExecuteClearAll();
        }
    }

    private void ExecuteClearAll()
    {
        _leftFolder = null;
        _rightFolder = null;
        _allItems.Clear();
        if (FolderDiffListView != null) FolderDiffListView.ItemsSource = null;
        if (LeftSingleListView != null) LeftSingleListView.ItemsSource = null;
        if (RightSingleListView != null) RightSingleListView.ItemsSource = null;

        if (LeftFolderHeader != null) LeftFolderHeader.Text = LanguageManager.Instance.Text("noSourceFolder");
        if (RightFolderHeader != null) RightFolderHeader.Text = LanguageManager.Instance.Text("noTargetFolder");

        SetStatusPrompt(LanguageManager.Instance.Text("selectTwoFoldersPrompt"));
        if (SyncStatusText != null) SyncStatusText.Text = LanguageManager.Instance.Text("syncStatusReady");

        UpdateWorkspaceVisibility();
        UpdateTabHeader();
    }

    private async void OnSyncLeftToRightClicked(object sender, RoutedEventArgs e)
    {
        await ShowSyncPreviewDialogAsync(isLeftToRight: true);
    }

    private async void OnSyncRightToLeftClicked(object sender, RoutedEventArgs e)
    {
        await ShowSyncPreviewDialogAsync(isLeftToRight: false);
    }

    private async void OnSyncPreviewClicked(object sender, RoutedEventArgs e)
    {
        await ShowSyncPreviewDialogAsync(isLeftToRight: true);
    }

    private async System.Threading.Tasks.Task ShowSyncPreviewDialogAsync(bool isLeftToRight)
    {
        if (string.IsNullOrEmpty(_leftFolder) || string.IsNullOrEmpty(_rightFolder)) return;

        var plan = BuildSyncPlan(isLeftToRight);
        var mainWin = App.GetWindowForElement(this) as MainWindow ?? App.ActiveWindow as MainWindow;
        if (mainWin == null) return;

        var dialog = new SyncPreviewDialog(plan);
        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();

        dialog.Confirmed += () =>
        {
            mainWin.CloseInAppDialog();
            tcs.TrySetResult(true);
        };
        dialog.Cancelled += () =>
        {
            mainWin.CloseInAppDialog();
            tcs.TrySetResult(false);
        };
        dialog.Unloaded += (s, e) =>
        {
            tcs.TrySetResult(false);
        };

        mainWin.ShowInAppDialog(dialog);

        bool confirmed = await tcs.Task;
        if (confirmed)
        {
            await ExecuteSyncPlanAsync(plan);
        }
    }

    private List<SyncPlanItemDisplay> BuildSyncPlan(bool isLeftToRight)
    {
        var plan = new List<SyncPlanItemDisplay>();
        if (string.IsNullOrEmpty(_leftFolder) || string.IsNullOrEmpty(_rightFolder))
            return plan;

        foreach (var item in _allItems)
        {
            if (item.Status == FolderEntryStatus.Identical)
                continue;

            string relPath = item.RelativePath.Replace('/', '\\');
            string leftFull = !string.IsNullOrEmpty(item.LeftFullPath) ? item.LeftFullPath : Path.Combine(_leftFolder, relPath);
            string rightFull = !string.IsNullOrEmpty(item.RightFullPath) ? item.RightFullPath : Path.Combine(_rightFolder, relPath);

            if (isLeftToRight)
            {
                // Left -> Right: Left is Source, Right is Target
                switch (item.Status)
                {
                    case FolderEntryStatus.Deleted: // LeftOnly (exists in Source, missing in Target)
                        plan.Add(new SyncPlanItemDisplay
                        {
                            Action = SyncActionType.CopyLeftToRight,
                            RelativePath = relPath,
                            SourcePath = leftFull,
                            TargetPath = rightFull,
                            IsDirectory = item.IsDirectory
                        });
                        break;

                    case FolderEntryStatus.Modified: // Exists in both but differs
                        plan.Add(new SyncPlanItemDisplay
                        {
                            Action = SyncActionType.OverwriteLeftToRight,
                            RelativePath = relPath,
                            SourcePath = leftFull,
                            TargetPath = rightFull,
                            IsDirectory = item.IsDirectory
                        });
                        break;

                    case FolderEntryStatus.Added: // RightOnly (orphan on Target)
                        plan.Add(new SyncPlanItemDisplay
                        {
                            Action = SyncActionType.DeleteRight,
                            RelativePath = relPath,
                            SourcePath = null,
                            TargetPath = rightFull,
                            IsDirectory = item.IsDirectory
                        });
                        break;
                }
            }
            else
            {
                // Right -> Left: Right is Source, Left is Target
                switch (item.Status)
                {
                    case FolderEntryStatus.Added: // RightOnly (exists in Source, missing in Target)
                        plan.Add(new SyncPlanItemDisplay
                        {
                            Action = SyncActionType.CopyRightToLeft,
                            RelativePath = relPath,
                            SourcePath = rightFull,
                            TargetPath = leftFull,
                            IsDirectory = item.IsDirectory
                        });
                        break;

                    case FolderEntryStatus.Modified: // Exists in both but differs
                        plan.Add(new SyncPlanItemDisplay
                        {
                            Action = SyncActionType.OverwriteRightToLeft,
                            RelativePath = relPath,
                            SourcePath = rightFull,
                            TargetPath = leftFull,
                            IsDirectory = item.IsDirectory
                        });
                        break;

                    case FolderEntryStatus.Deleted: // LeftOnly (orphan on Source/Target)
                        plan.Add(new SyncPlanItemDisplay
                        {
                            Action = SyncActionType.DeleteLeft,
                            RelativePath = relPath,
                            SourcePath = leftFull,
                            TargetPath = null,
                            IsDirectory = item.IsDirectory
                        });
                        break;
                }
            }
        }

        return plan;
    }

    private async System.Threading.Tasks.Task ExecuteSyncPlanAsync(List<SyncPlanItemDisplay> plan)
    {
        if (plan.Count == 0) return;

        if (SyncStatusText != null)
            SyncStatusText.Text = LanguageManager.Instance.Text("syncInProgress");

        try
        {
            var (success, errors) = await System.Threading.Tasks.Task.Run(() =>
            {
                int s = 0;
                int err = 0;

                foreach (var item in plan)
                {
                    try
                    {
                        switch (item.Action)
                        {
                            case SyncActionType.CopyLeftToRight:
                            case SyncActionType.OverwriteLeftToRight:
                                if (!string.IsNullOrEmpty(item.SourcePath) && !string.IsNullOrEmpty(item.TargetPath))
                                {
                                    if (item.IsDirectory || Directory.Exists(item.SourcePath))
                                    {
                                        CopyDirectory(item.SourcePath, item.TargetPath);
                                    }
                                    else
                                    {
                                        string? targetDir = Path.GetDirectoryName(item.TargetPath);
                                        if (!string.IsNullOrEmpty(targetDir)) Directory.CreateDirectory(targetDir);
                                        if (File.Exists(item.TargetPath)) File.Delete(item.TargetPath);
                                        File.Copy(item.SourcePath, item.TargetPath, true);
                                    }
                                    s++;
                                }
                                break;

                            case SyncActionType.CopyRightToLeft:
                            case SyncActionType.OverwriteRightToLeft:
                                if (!string.IsNullOrEmpty(item.SourcePath) && !string.IsNullOrEmpty(item.TargetPath))
                                {
                                    if (item.IsDirectory || Directory.Exists(item.SourcePath))
                                    {
                                        CopyDirectory(item.SourcePath, item.TargetPath);
                                    }
                                    else
                                    {
                                        string? targetDir = Path.GetDirectoryName(item.TargetPath);
                                        if (!string.IsNullOrEmpty(targetDir)) Directory.CreateDirectory(targetDir);
                                        if (File.Exists(item.TargetPath)) File.Delete(item.TargetPath);
                                        File.Copy(item.SourcePath, item.TargetPath, true);
                                    }
                                    s++;
                                }
                                break;

                            case SyncActionType.DeleteRight:
                                if (!string.IsNullOrEmpty(item.TargetPath))
                                {
                                    if (Directory.Exists(item.TargetPath))
                                    {
                                        Directory.Delete(item.TargetPath, true);
                                        s++;
                                    }
                                    else if (File.Exists(item.TargetPath))
                                    {
                                        File.Delete(item.TargetPath);
                                        s++;
                                    }
                                }
                                break;

                            case SyncActionType.DeleteLeft:
                                if (!string.IsNullOrEmpty(item.SourcePath))
                                {
                                    if (Directory.Exists(item.SourcePath))
                                    {
                                        Directory.Delete(item.SourcePath, true);
                                        s++;
                                    }
                                    else if (File.Exists(item.SourcePath))
                                    {
                                        File.Delete(item.SourcePath);
                                        s++;
                                    }
                                }
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Sync error for {item.RelativePath}: {ex.Message}");
                        err++;
                    }
                }

                return (s, err);
            });

            if (SyncStatusText != null)
            {
                if (errors == 0)
                {
                    SyncStatusText.Text = LanguageManager.Instance.Text("syncCompleted");
                }
                else
                {
                    SyncStatusText.Text = string.Format(LanguageManager.Instance.Text("syncPlanResult"), success, errors);
                }
            }

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
            if (item.IsDirectory) return;
            string leftFile = (!string.IsNullOrEmpty(_leftFolder) && !item.IsLeftMissing) ? Path.Combine(_leftFolder, item.RelativePath.Replace('/', '\\')) : "";
            string rightFile = (!string.IsNullOrEmpty(_rightFolder) && !item.IsRightMissing) ? Path.Combine(_rightFolder, item.RelativePath.Replace('/', '\\')) : "";

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

    private void OnLeftSingleItemDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (LeftSingleListView?.SelectedItem is FolderSingleDisplayItem item)
        {
            if (item.IsDirectory || string.IsNullOrEmpty(_leftFolder)) return;
            string fullPath = Path.Combine(_leftFolder, item.RelativePath.Replace('/', '\\'));
            OpenSingleFile(fullPath);
        }
    }

    private void OnRightSingleItemDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (RightSingleListView?.SelectedItem is FolderSingleDisplayItem item)
        {
            if (item.IsDirectory || string.IsNullOrEmpty(_rightFolder)) return;
            string fullPath = Path.Combine(_rightFolder, item.RelativePath.Replace('/', '\\'));
            OpenSingleFile(fullPath);
        }
    }

    private void OpenSingleFile(string fullPath)
    {
        if (App.ActiveWindow is MainWindow mainWin && File.Exists(fullPath))
        {
            string ext = Path.GetExtension(fullPath).ToLowerInvariant();
            if (ext is ".xlsx" or ".xls" or ".csv")
            {
                mainWin.OpenExcelDiffTab(fullPath, null);
            }
            else if (ext is ".docx" or ".doc")
            {
                mainWin.OpenWordDiffTab(fullPath, null);
            }
            else
            {
                mainWin.OpenTextDiffTab(fullPath, null);
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

    public void HandleDualDrop(string leftPath, string rightPath)
    {
        if (string.IsNullOrEmpty(leftPath) || string.IsNullOrEmpty(rightPath)) return;
        if (!CheckFileValidation(leftPath) || !CheckFileValidation(rightPath)) return;
        _leftFolder = leftPath;
        _rightFolder = rightPath;
        if (LeftFolderHeader != null) LeftFolderHeader.Text = Path.GetFileName(leftPath);
        if (RightFolderHeader != null) RightFolderHeader.Text = Path.GetFileName(rightPath);
        UpdateWorkspaceVisibility();
        UpdateTabHeader();
        RecentHistoryManager.Instance.AddRecord(_leftFolder!, _rightFolder!, CompareType.FolderDiff);
        StartScan();
    }

    private async void OnLeftFolderDrop(object sender, DragEventArgs e)
    {
        try
        {
            App.Log("[FolderDiffPage] OnLeftFolderDrop received drop event");
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                App.Log($"[FolderDiffPage] OnLeftFolderDrop items count: {items?.Count ?? 0}");
                if (items != null && items.Count > 0)
                {
                    e.Handled = true;
                    if (items.Count >= 2)
                    {
                        HandleDualDrop(items[0].Path, items[1].Path);
                        return;
                    }

                    string targetPath = items[0].Path;
                    App.Log($"[FolderDiffPage] OnLeftFolderDrop targetPath: {targetPath}");
                    HandleDropFromWindow(targetPath, isLeft: true);
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"[FolderDiffPage] OnLeftFolderDrop error: {ex.Message}");
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
        try
        {
            App.Log("[FolderDiffPage] OnRightFolderDrop received drop event");
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                App.Log($"[FolderDiffPage] OnRightFolderDrop items count: {items?.Count ?? 0}");
                if (items != null && items.Count > 0)
                {
                    e.Handled = true;
                    if (items.Count >= 2)
                    {
                        HandleDualDrop(items[0].Path, items[1].Path);
                        return;
                    }

                    string targetPath = items[0].Path;
                    App.Log($"[FolderDiffPage] OnRightFolderDrop targetPath: {targetPath}");
                    HandleDropFromWindow(targetPath, isLeft: false);
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"[FolderDiffPage] OnRightFolderDrop error: {ex.Message}");
        }
    }

    public void HandleDropFromWindow(string path, bool isLeft)
    {
        if (string.IsNullOrEmpty(path)) return;
        App.Log($"[FolderDiffPage] HandleDropFromWindow: isLeft={isLeft}, path={path}");
        if (!CheckFileValidation(path)) return;
        if (isLeft)
        {
            _leftFolder = path;
            if (LeftFolderHeader != null) LeftFolderHeader.Text = Path.GetFileName(path);
            UpdateWorkspaceVisibility();
            UpdateTabHeader();
            if (!string.IsNullOrEmpty(_leftFolder) && !string.IsNullOrEmpty(_rightFolder))
            {
                RecentHistoryManager.Instance.AddRecord(_leftFolder!, _rightFolder!, CompareType.FolderDiff);
                StartScan();
            }
        }
        else
        {
            _rightFolder = path;
            if (RightFolderHeader != null) RightFolderHeader.Text = Path.GetFileName(path);
            UpdateWorkspaceVisibility();
            UpdateTabHeader();
            if (!string.IsNullOrEmpty(_leftFolder) && !string.IsNullOrEmpty(_rightFolder))
            {
                RecentHistoryManager.Instance.AddRecord(_leftFolder!, _rightFolder!, CompareType.FolderDiff);
                StartScan();
            }
        }
    }

    private void OnDiffContainerDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            var pos = e.GetPosition(this);
            double totalWidth = this.ActualWidth > 0 ? this.ActualWidth : 900;
            if (pos.X < totalWidth / 2)
            {
                e.DragUIOverride.Caption = LanguageManager.Instance.Text("dropSourceFolder");
            }
            else
            {
                e.DragUIOverride.Caption = LanguageManager.Instance.Text("dropTargetFolder");
            }
        }
    }

    private async void OnDiffContainerDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        try
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items != null && items.Count >= 2)
                {
                    HandleDualDrop(items[0].Path, items[1].Path);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"[FolderDiffPage] OnDiffContainerDrop error: {ex.Message}");
        }

        var pos = e.GetPosition(this);
        double totalWidth = this.ActualWidth > 0 ? this.ActualWidth : 900;
        if (pos.X < totalWidth / 2)
        {
            OnLeftFolderDrop(sender, e);
        }
        else
        {
            OnRightFolderDrop(sender, e);
        }
    }

    private void UpdateTabHeader()
    {
        try
        {
            string? header = null;
            if (!string.IsNullOrEmpty(_leftFolder) && !string.IsNullOrEmpty(_rightFolder))
            {
                string leftName = Path.GetFileName(_leftFolder);
                string rightName = Path.GetFileName(_rightFolder);
                header = leftName + " \u2194 " + rightName;
            }
            else if (!string.IsNullOrEmpty(_leftFolder))
            {
                header = Path.GetFileName(_leftFolder);
            }
            else if (!string.IsNullOrEmpty(_rightFolder))
            {
                header = Path.GetFileName(_rightFolder);
            }

            var win = App.GetWindowForElement(this) as MainWindow ?? App.ActiveWindow as MainWindow ?? App.MainWindowInstance;
            win?.UpdateTabHeaderForPage(this, header);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("UpdateTabHeader error: " + ex.Message);
        }
    }

    private (string glyph, Brush brush) GetFileIcon(string ext)
    {
        if (ext is ".cs" or ".rs" or ".js" or ".ts" or ".py" or ".cpp" or ".h" or ".json" or ".xml")
        {
            return ("\uE943", CodeIconBrush);
        }
        else if (ext is ".xlsx" or ".xls" or ".csv")
        {
            return ("\uF0EC", ExcelIconBrush);
        }
        else if (ext is ".docx" or ".doc")
        {
            return ("\uE8A5", WordIconBrush);
        }
        else
        {
            return ("\uE8A5", FileIconBrush);
        }
    }

    private sealed class RawSingleFolderEntry
    {
        public string RelativePath { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime ModifiedTime { get; set; }
        public string Extension { get; set; } = string.Empty;

        public RawSingleFolderEntry(string relativePath, string name, bool isDirectory, long size, DateTime modifiedTime, string extension)
        {
            RelativePath = relativePath;
            Name = name;
            IsDirectory = isDirectory;
            Size = size;
            ModifiedTime = modifiedTime;
            Extension = extension;
        }
    }

    private async Task<List<FolderSingleDisplayItem>> ScanSingleFolderAsync(string rootPath)
    {
        var rawList = await Task.Run(() =>
        {
            var results = new List<RawSingleFolderEntry>();
            try
            {
                var dirInfo = new DirectoryInfo(rootPath);
                if (!dirInfo.Exists) return results;

                var enumOptions = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    AttributesToSkip = System.IO.FileAttributes.ReparsePoint
                };
                var allEntries = dirInfo.EnumerateFileSystemInfos("*", enumOptions);
                foreach (var entry in allEntries)
                {
                    try
                    {
                        if ((entry.Attributes & System.IO.FileAttributes.Hidden) != 0) continue;
                        if (entry.Name.StartsWith(".") && entry.Name != ".") continue;

                        string relPath = Path.GetRelativePath(rootPath, entry.FullName).Replace('\\', '/');
                        bool isDir = (entry.Attributes & System.IO.FileAttributes.Directory) != 0;
                        long size = 0;
                        if (!isDir && entry is FileInfo fi)
                        {
                            size = fi.Length;
                        }
                        DateTime modTime = entry.LastWriteTime;

                        results.Add(new RawSingleFolderEntry(relPath, entry.Name, isDir, size, modTime, entry.Extension));
                    }
                    catch
                    {
                        // Ignore inaccessible file or metadata
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ScanSingleFolderAsync background scan error: {ex.Message}");
            }

            return results.OrderBy(x => !x.IsDirectory).ThenBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
        });

        var displayItems = new List<FolderSingleDisplayItem>(rawList.Count);
        foreach (var raw in rawList)
        {
            var (glyph, brush) = raw.IsDirectory ? ("\uED25", FolderIconBrush) : GetFileIcon(raw.Extension);
            displayItems.Add(new FolderSingleDisplayItem
            {
                RelativePath = raw.RelativePath,
                Name = raw.Name,
                IsDirectory = raw.IsDirectory,
                SizeFormatted = raw.IsDirectory ? "---" : FormatSize(raw.Size),
                ModifiedFormatted = raw.ModifiedTime.ToString("yyyy-MM-dd HH:mm"),
                IconGlyph = glyph,
                IconColorBrush = brush
            });
        }

        return displayItems;
    }

    private async Task LoadSingleFolderPreviewAsync(string folderPath, bool isLeft)
    {
        SetStatusPrompt(LanguageManager.Instance.Text("scanningStatus"));

        if (isLeft)
        {
            if (LeftLoadingOverlay != null) LeftLoadingOverlay.Visibility = Visibility.Visible;
            if (LeftSingleContainer != null) LeftSingleContainer.Visibility = Visibility.Collapsed;
        }
        else
        {
            if (RightLoadingOverlay != null) RightLoadingOverlay.Visibility = Visibility.Visible;
            if (RightSingleContainer != null) RightSingleContainer.Visibility = Visibility.Collapsed;
        }

        try
        {
            var items = await ScanSingleFolderAsync(folderPath);

            // If both folders are now selected, comparison view takes precedence
            if (!string.IsNullOrEmpty(_leftFolder) && !string.IsNullOrEmpty(_rightFolder))
            {
                return;
            }

            if (isLeft)
            {
                if (LeftSingleListView != null) LeftSingleListView.ItemsSource = items;
                if (LeftSingleContainer != null) LeftSingleContainer.Visibility = Visibility.Visible;
                SetStatusPrompt(string.Format(LanguageManager.Instance.Text("scannedItemsPleaseSelectTarget"), items.Count));
            }
            else
            {
                if (RightSingleListView != null) RightSingleListView.ItemsSource = items;
                if (RightSingleContainer != null) RightSingleContainer.Visibility = Visibility.Visible;
                SetStatusPrompt(string.Format(LanguageManager.Instance.Text("scannedItemsPleaseSelectSource"), items.Count));
            }
        }
        finally
        {
            if (isLeft && LeftLoadingOverlay != null)
            {
                LeftLoadingOverlay.Visibility = Visibility.Collapsed;
            }
            if (!isLeft && RightLoadingOverlay != null)
            {
                RightLoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }
    }

    private bool CheckFileValidation(string path)
    {
        var check = FileFormatValidator.Validate(path, CompareType.FolderDiff);
        if (!check.IsValid)
        {
            var targetWindow = (App.GetWindowForElement(this) as MainWindow) ?? App.ActiveWindow ?? App.MainWindowInstance;
            if (targetWindow != null)
            {
                string title = LanguageManager.Instance.Text(check.TitleKey);
                string msg = LanguageManager.Instance.Text(check.MessageKey);
                string? btnText = check.PromptType == ValidationPromptType.SuggestSwitch && check.RecommendedMode != null
                    ? LanguageManager.Instance.Text("btnSwitchMode")
                    : null;
                targetWindow.ShowValidationDialog(title, msg, btnText, () =>
                {
                    if (check.RecommendedMode != null)
                    {
                        targetWindow.OpenSingleTargetTab(path, check.RecommendedMode.Value);
                    }
                });
            }
            return false;
        }
        return true;
    }

    public void Dispose()
    {
        try
        {
            App.Log("[FolderDiffPage] Disposing resources...");
            LanguageManager.Instance.LanguageChanged -= UpdateLocalization;

            if (FolderDiffListView != null)
            {
                FolderDiffListView.ItemsSource = null;
            }
            if (LeftSingleListView != null)
            {
                LeftSingleListView.ItemsSource = null;
            }
            if (RightSingleListView != null)
            {
                RightSingleListView.ItemsSource = null;
            }

            _allItems?.Clear();
            App.Log("[FolderDiffPage] Dispose completed.");
        }
        catch (Exception ex)
        {
            App.Log($"[FolderDiffPage] Dispose exception: {ex.Message}");
        }
    }
}
