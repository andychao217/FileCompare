using System.Text.Json;
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

public sealed partial class ExcelDiffPage : Page, IDisposable
{
    private string? _leftPath;
    private string? _rightPath;
    private ExcelDiffSession? _session;
    private List<ExcelRowDisplayItem> _allRows = new();
    private List<ExcelRowDisplayItem> _displayRows = new();
    private int _currentSheetIndex = 0;
    private int _currentDiffJumpIndex = -1;
    private string _searchQuery = string.Empty;
    private int _filterMode = 0; // 0: All, 1: DiffOnly, 2: SameOnly
    private bool _isInitialized = false;

    private static readonly Brush ModBg = new SolidColorBrush(Windows.UI.Color.FromArgb(35, 234, 179, 8));
    private static readonly Brush ModFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 250, 204, 21));
    private static readonly Brush AddBg = new SolidColorBrush(Windows.UI.Color.FromArgb(35, 34, 197, 94));
    private static readonly Brush AddFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 74, 222, 128));
    private static readonly Brush DelBg = new SolidColorBrush(Windows.UI.Color.FromArgb(35, 239, 68, 68));
    private static readonly Brush DelFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 113, 113));
    private static readonly Brush SameBg = new SolidColorBrush(Windows.UI.Color.FromArgb(15, 156, 163, 175));
    private static readonly Brush SameFg = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 156, 163, 175));

    private static readonly Brush ActiveFilterBg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 37, 99, 235));
    private static readonly Brush InactiveFilterBg = new SolidColorBrush(Colors.Transparent);
    private static readonly Brush ActiveFilterFg = new SolidColorBrush(Colors.White);

    public ExcelDiffPage()
    {
        this.InitializeComponent();

        if (WhitespaceSwitch != null)
            WhitespaceSwitch.IsOn = SettingsManager.Instance.DefaultIgnoreWhitespace;
        if (CaseSwitch != null)
            CaseSwitch.IsOn = SettingsManager.Instance.DefaultIgnoreCase;

        UpdateLocalization();
        LanguageManager.Instance.LanguageChanged += UpdateLocalization;

        this.ActualThemeChanged += (s, e) => SetFilterButtonsVisual(_filterMode);

        _isInitialized = true;
        UpdateWorkspaceVisibility();
    }

    private void UpdateLocalization()
    {
        if (PrevDiffText != null) PrevDiffText.Text = LanguageManager.Instance.Text("prevDiff");
        if (NextDiffText != null) NextDiffText.Text = LanguageManager.Instance.Text("nextDiff");
        if (WhitespaceLabel != null) WhitespaceLabel.Text = LanguageManager.Instance.Text("ignoreWhitespace");
        if (CaseLabel != null) CaseLabel.Text = LanguageManager.Instance.Text("ignoreCase");
        if (RuleBtnText != null) RuleBtnText.Text = "比对规则";
        if (SwapSidesText != null) SwapSidesText.Text = LanguageManager.Instance.Text("swapSides");
        if (RefreshText != null) RefreshText.Text = LanguageManager.Instance.Text("refreshCompare");
        if (TakeLeftText != null) TakeLeftText.Text = LanguageManager.Instance.Text("takeLeft");
        if (TakeRightText != null) TakeRightText.Text = LanguageManager.Instance.Text("takeRight");
        if (ClearAllText != null) ClearAllText.Text = LanguageManager.Instance.Text("clearAll");

        if (FilterAllText != null) FilterAllText.Text = LanguageManager.Instance.Text("filterAll") ?? "全部";
        if (FilterDiffText != null) FilterDiffText.Text = LanguageManager.Instance.Text("filterDiffOnly") ?? "仅差异";
        if (FilterSameText != null) FilterSameText.Text = LanguageManager.Instance.Text("filterSameOnly") ?? "仅相同";
        if (ExcelSearchBox != null) ExcelSearchBox.PlaceholderText = "搜索与过滤段落...";

        if (LeftBrowseBtn != null) LeftBrowseBtn.Content = LanguageManager.Instance.Text("selectEllipsis");
        if (RightBrowseBtn != null) RightBrowseBtn.Content = LanguageManager.Instance.Text("selectEllipsis");
        if (LeftPickBtn != null) LeftPickBtn.Content = LanguageManager.Instance.Text("pickSourceFile");
        if (RightPickBtn != null) RightPickBtn.Content = LanguageManager.Instance.Text("pickTargetFile");

        if (LeftEmptyTitle != null) LeftEmptyTitle.Text = LanguageManager.Instance.Text("noFileSelected");
        if (RightEmptyTitle != null) RightEmptyTitle.Text = LanguageManager.Instance.Text("noFileSelected");
        if (LeftEmptyDesc != null) LeftEmptyDesc.Text = LanguageManager.Instance.Text("dropOrPickFile");
        if (RightEmptyDesc != null) RightEmptyDesc.Text = LanguageManager.Instance.Text("dropOrPickFile");

        if (LeftColRowHeader != null) LeftColRowHeader.Text = LanguageManager.Instance.Text("excelColRow");
        if (RightColRowHeader != null) RightColRowHeader.Text = LanguageManager.Instance.Text("excelColRow");
        if (LeftColStatusHeader != null) LeftColStatusHeader.Text = LanguageManager.Instance.Text("excelColStatus");
        if (RightColStatusHeader != null) RightColStatusHeader.Text = LanguageManager.Instance.Text("excelColStatus");
        if (LeftColContentHeader != null) LeftColContentHeader.Text = LanguageManager.Instance.Text("leftColContent");
        if (RightColContentHeader != null) RightColContentHeader.Text = LanguageManager.Instance.Text("rightColContent");

        if (InspectorTitle != null) InspectorTitle.Text = LanguageManager.Instance.Text("inspectorTitle");

        if (string.IsNullOrEmpty(_leftPath) && LeftExcelHeader != null)
        {
            LeftExcelHeader.Text = LanguageManager.Instance.Text("sourceFile");
        }
        if (string.IsNullOrEmpty(_rightPath) && RightExcelHeader != null)
        {
            RightExcelHeader.Text = LanguageManager.Instance.Text("targetFile");
        }
        if (ExcelRowsList != null && ExcelRowsList.SelectedItem == null && InspectorContent != null)
        {
            InspectorContent.Text = LanguageManager.Instance.Text("inspectorNoSelection");
        }
    }

    private void UpdateWorkspaceVisibility()
    {
        if (!_isInitialized) return;

        bool hasLeft = !string.IsNullOrEmpty(_leftPath);
        bool hasRight = !string.IsNullOrEmpty(_rightPath);

        if (!hasLeft && !hasRight)
        {
            if (EmptyStateContainer != null) EmptyStateContainer.Visibility = Visibility.Visible;
            if (DiffGridContainer != null) DiffGridContainer.Visibility = Visibility.Collapsed;

            if (PrevDiffBtn != null) PrevDiffBtn.IsEnabled = false;
            if (NextDiffBtn != null) NextDiffBtn.IsEnabled = false;
            if (RuleSettingsBtn != null) RuleSettingsBtn.IsEnabled = false;
            if (SwapSidesBtn != null) SwapSidesBtn.IsEnabled = false;
            if (RefreshBtn != null) RefreshBtn.IsEnabled = false;
            if (TakeLeftBtn != null) TakeLeftBtn.IsEnabled = false;
            if (TakeRightBtn != null) TakeRightBtn.IsEnabled = false;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = false;
            if (DiffBadge != null) DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountBadge"), 0);
            if (StatusInfoText != null) StatusInfoText.Text = LanguageManager.Instance.Text("notLoaded");
        }
        else
        {
            if (EmptyStateContainer != null) EmptyStateContainer.Visibility = Visibility.Collapsed;
            if (DiffGridContainer != null) DiffGridContainer.Visibility = Visibility.Visible;

            bool hasBoth = hasLeft && hasRight;
            if (PrevDiffBtn != null) PrevDiffBtn.IsEnabled = hasBoth;
            if (NextDiffBtn != null) NextDiffBtn.IsEnabled = hasBoth;
            if (RuleSettingsBtn != null) RuleSettingsBtn.IsEnabled = hasBoth;
            if (SwapSidesBtn != null) SwapSidesBtn.IsEnabled = hasBoth;
            if (RefreshBtn != null) RefreshBtn.IsEnabled = hasBoth;
            if (TakeLeftBtn != null) TakeLeftBtn.IsEnabled = hasBoth;
            if (TakeRightBtn != null) TakeRightBtn.IsEnabled = hasBoth;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = true;
        }
    }

    public void LoadFiles(string? leftPath, string? rightPath)
    {
        if (!string.IsNullOrEmpty(leftPath) && File.Exists(leftPath))
        {
            _leftPath = leftPath;
            if (LeftExcelHeader != null) LeftExcelHeader.Text = Path.GetFileName(leftPath);
        }

        if (!string.IsNullOrEmpty(rightPath) && File.Exists(rightPath))
        {
            _rightPath = rightPath;
            if (RightExcelHeader != null) RightExcelHeader.Text = Path.GetFileName(rightPath);
        }

        UpdateWorkspaceVisibility();

        if (!string.IsNullOrEmpty(_leftPath) && !string.IsNullOrEmpty(_rightPath))
        {
            RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.ExcelDiff);
            InitSessionAndCompare();
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is ValueTuple<string?, string?> paths)
        {
            LoadFiles(paths.Item1, paths.Item2);
        }
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        InitSessionAndCompare();
    }

    private void OnDiffOptionsChanged(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        if (!string.IsNullOrEmpty(_leftPath) && !string.IsNullOrEmpty(_rightPath))
        {
            InitSessionAndCompare();
        }
    }

    private void OnWhitespaceContainerClicked(object sender, PointerRoutedEventArgs e)
    {
        if (WhitespaceSwitch != null && !IsWithinElement(e.OriginalSource as DependencyObject, WhitespaceSwitch))
        {
            WhitespaceSwitch.IsOn = !WhitespaceSwitch.IsOn;
        }
    }

    private void OnCaseContainerClicked(object sender, PointerRoutedEventArgs e)
    {
        if (CaseSwitch != null && !IsWithinElement(e.OriginalSource as DependencyObject, CaseSwitch))
        {
            CaseSwitch.IsOn = !CaseSwitch.IsOn;
        }
    }

    private static bool IsWithinElement(DependencyObject? child, DependencyObject? target)
    {
        if (child == null || target == null) return false;
        var current = child;
        while (current != null)
        {
            if (ReferenceEquals(current, target)) return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private void InitSessionAndCompare()
    {
        if (string.IsNullOrEmpty(_leftPath) || string.IsNullOrEmpty(_rightPath)) return;

        UpdateWorkspaceVisibility();

        _session?.Dispose();
        _session = null;

        try
        {
            var rules = new
            {
                ignore_whitespace = WhitespaceSwitch?.IsOn ?? false,
                ignore_case = CaseSwitch?.IsOn ?? false,
                numeric_tolerance = 0.0001,
                ignore_empty_rows = true,
                compare_formulas = false,
                key_columns = Array.Empty<string>()
            };
            string optionsJson = JsonSerializer.Serialize(rules);
            _session = ExcelDiffSession.Create(_leftPath, _rightPath, optionsJson);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ExcelDiffSession.Create failed: {ex.Message}");
        }

        if (_session != null)
        {
            var summary = _session.GetSummary();
            if (DiffBadge != null)
                DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountBadge"), summary.TotalDiffCount);

            if (SheetComboBox != null)
            {
                SheetComboBox.Items.Clear();
                if (summary.Sheets.Count > 0)
                {
                    foreach (var s in summary.Sheets)
                    {
                        SheetComboBox.Items.Add($"{s.SheetName} ({s.DiffCount})");
                    }
                    _currentSheetIndex = 0;
                    SheetComboBox.SelectedIndex = 0;
                }
                else
                {
                    SheetComboBox.Items.Add("Sheet1");
                    _currentSheetIndex = 0;
                    SheetComboBox.SelectedIndex = 0;
                }
            }

            LoadRowsForSheet(_currentSheetIndex);
        }
        else
        {
            LoadFallbackCsvComparison();
        }
    }

    private void LoadFallbackCsvComparison()
    {
        if (string.IsNullOrEmpty(_leftPath) || string.IsNullOrEmpty(_rightPath)) return;

        if (SheetComboBox != null)
        {
            SheetComboBox.Items.Clear();
            SheetComboBox.Items.Add("Table1");
            SheetComboBox.SelectedIndex = 0;
        }

        string[] leftLines = File.Exists(_leftPath) ? File.ReadAllLines(_leftPath) : Array.Empty<string>();
        string[] rightLines = File.Exists(_rightPath) ? File.ReadAllLines(_rightPath) : Array.Empty<string>();

        int max = Math.Max(leftLines.Length, rightLines.Length);
        _allRows.Clear();
        int diffCount = 0;

        bool ignoreWs = WhitespaceSwitch?.IsOn ?? false;
        bool ignoreCase = CaseSwitch?.IsOn ?? false;

        for (int i = 0; i < max; i++)
        {
            string lStr = i < leftLines.Length ? leftLines[i] : "";
            string rStr = i < rightLines.Length ? rightLines[i] : "";

            var lCells = lStr.Split(new[] { ',', '\t', '|' }).Select(s => s.Trim()).ToList();
            var rCells = rStr.Split(new[] { ',', '\t', '|' }).Select(s => s.Trim()).ToList();

            bool isAdd = i >= leftLines.Length;
            bool isDel = i >= rightLines.Length;

            string compL = ignoreWs ? System.Text.RegularExpressions.Regex.Replace(lStr, @"\s+", "") : lStr;
            string compR = ignoreWs ? System.Text.RegularExpressions.Regex.Replace(rStr, @"\s+", "") : rStr;
            var compType = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            bool isMod = !isAdd && !isDel && !string.Equals(compL, compR, compType);
            bool isSame = !isAdd && !isDel && !isMod;

            var item = new ExcelRowDisplayItem
            {
                RowIndex = i,
                LeftCells = lCells,
                RightCells = rCells,
                LeftRowFormatted = string.Join("  |  ", lCells),
                RightRowFormatted = string.Join("  |  ", rCells),
                IsDiff = !isSame
            };

            if (isAdd)
            {
                item.StatusText = LanguageManager.Instance.Text("statusAdded");
                item.StatusBgBrush = AddBg;
                item.StatusFgBrush = AddFg;
                item.RightBgBrush = AddBg;
                diffCount++;
            }
            else if (isDel)
            {
                item.StatusText = LanguageManager.Instance.Text("statusDeleted");
                item.StatusBgBrush = DelBg;
                item.StatusFgBrush = DelFg;
                item.LeftBgBrush = DelBg;
                diffCount++;
            }
            else if (isMod)
            {
                item.StatusText = LanguageManager.Instance.Text("statusModified");
                item.StatusBgBrush = ModBg;
                item.StatusFgBrush = ModFg;
                item.LeftBgBrush = ModBg;
                item.RightBgBrush = ModBg;
                diffCount++;
            }
            else
            {
                item.StatusText = LanguageManager.Instance.Text("statusIdentical");
                item.StatusBgBrush = SameBg;
                item.StatusFgBrush = SameFg;
            }

            _allRows.Add(item);
        }

        ApplyFilter();
    }

    private void OnSheetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        if (SheetComboBox?.SelectedIndex is int idx && idx >= 0)
        {
            _currentSheetIndex = idx;
            if (_session != null)
            {
                LoadRowsForSheet(_currentSheetIndex);
            }
        }
    }

    private void LoadRowsForSheet(int sheetIdx)
    {
        if (_session == null) return;

        var viewport = _session.GetViewport(sheetIdx, 0, 5000);
        var rows = viewport.Rows;
        _allRows.Clear();

        foreach (var r in rows)
        {
            var item = new ExcelRowDisplayItem
            {
                RowIndex = r.RowIndex,
                LeftCells = r.LeftCells,
                RightCells = r.RightCells,
                LeftRowFormatted = string.Join("  |  ", r.LeftCells),
                RightRowFormatted = string.Join("  |  ", r.RightCells),
                IsDiff = r.Status != "Unchanged" && r.Status != "Identical"
            };

            switch (r.Status)
            {
                case "Added":
                    item.StatusText = LanguageManager.Instance.Text("statusAdded");
                    item.StatusBgBrush = AddBg;
                    item.StatusFgBrush = AddFg;
                    item.RightBgBrush = AddBg;
                    break;
                case "Deleted":
                    item.StatusText = LanguageManager.Instance.Text("statusDeleted");
                    item.StatusBgBrush = DelBg;
                    item.StatusFgBrush = DelFg;
                    item.LeftBgBrush = DelBg;
                    break;
                case "Modified":
                    item.StatusText = LanguageManager.Instance.Text("statusModified");
                    item.StatusBgBrush = ModBg;
                    item.StatusFgBrush = ModFg;
                    item.LeftBgBrush = ModBg;
                    item.RightBgBrush = ModBg;
                    break;
                default:
                    item.StatusText = LanguageManager.Instance.Text("statusIdentical");
                    item.StatusBgBrush = SameBg;
                    item.StatusFgBrush = SameFg;
                    break;
            }

            _allRows.Add(item);
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string query = _searchQuery.Trim().ToLowerInvariant();
        IEnumerable<ExcelRowDisplayItem> filtered = _allRows;

        if (_filterMode == 1) // 仅差异
        {
            filtered = filtered.Where(r => r.IsDiff);
        }
        else if (_filterMode == 2) // 仅相同
        {
            filtered = filtered.Where(r => !r.IsDiff);
        }

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(r =>
                r.LeftRowFormatted.ToLowerInvariant().Contains(query) ||
                r.RightRowFormatted.ToLowerInvariant().Contains(query) ||
                r.RowIndexText.Contains(query));
        }

        _displayRows = filtered.ToList();
        if (ExcelRowsList != null)
        {
            ExcelRowsList.ItemsSource = null;
            ExcelRowsList.ItemsSource = _displayRows;
        }

        int totalDiff = _allRows.Count(r => r.IsDiff);
        if (DiffBadge != null)
            DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountBadge"), totalDiff);

        string sheetName = SheetComboBox?.SelectedItem?.ToString() ?? $"Sheet{_currentSheetIndex + 1}";
        if (StatusInfoText != null)
            StatusInfoText.Text = string.Format(LanguageManager.Instance.Text("excelSummaryStats"), sheetName, _displayRows.Count, totalDiff);
    }

    private void OnFilterAllClicked(object sender, RoutedEventArgs e)
    {
        _filterMode = 0;
        SetFilterButtonsVisual(0);
        ApplyFilter();
    }

    private void OnFilterDiffOnlyClicked(object sender, RoutedEventArgs e)
    {
        _filterMode = 1;
        SetFilterButtonsVisual(1);
        ApplyFilter();
    }

    private void OnFilterSameOnlyClicked(object sender, RoutedEventArgs e)
    {
        _filterMode = 2;
        SetFilterButtonsVisual(2);
        ApplyFilter();
    }

    private Brush GetInactiveFilterForeground()
    {
        bool isDark = this.ActualTheme == ElementTheme.Dark;
        return isDark 
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 203, 213, 225)) // #CBD5E1 Slate 300 for dark mode
            : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 71, 85, 105));   // #475569 Slate 600 for light mode
    }

    private void SetFilterButtonsVisual(int activeIndex)
    {
        var inactiveFg = GetInactiveFilterForeground();

        if (FilterAllBtn != null)
        {
            FilterAllBtn.Background = activeIndex == 0 ? ActiveFilterBg : InactiveFilterBg;
            FilterAllBtn.Foreground = activeIndex == 0 ? ActiveFilterFg : inactiveFg;
            FilterAllBtn.FontWeight = activeIndex == 0 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }
        if (FilterDiffBtn != null)
        {
            FilterDiffBtn.Background = activeIndex == 1 ? ActiveFilterBg : InactiveFilterBg;
            FilterDiffBtn.Foreground = activeIndex == 1 ? ActiveFilterFg : inactiveFg;
            FilterDiffBtn.FontWeight = activeIndex == 1 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }
        if (FilterSameBtn != null)
        {
            FilterSameBtn.Background = activeIndex == 2 ? ActiveFilterBg : InactiveFilterBg;
            FilterSameBtn.Foreground = activeIndex == 2 ? ActiveFilterFg : inactiveFg;
            FilterSameBtn.FontWeight = activeIndex == 2 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (!_isInitialized) return;
        _searchQuery = sender.Text ?? string.Empty;
        ApplyFilter();
    }

    private void OnSwapSidesClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_leftPath) && string.IsNullOrEmpty(_rightPath)) return;
        var temp = _leftPath;
        _leftPath = _rightPath;
        _rightPath = temp;

        if (LeftExcelHeader != null)
            LeftExcelHeader.Text = !string.IsNullOrEmpty(_leftPath) ? Path.GetFileName(_leftPath) : LanguageManager.Instance.Text("sourceFile");
        if (RightExcelHeader != null)
            RightExcelHeader.Text = !string.IsNullOrEmpty(_rightPath) ? Path.GetFileName(_rightPath) : LanguageManager.Instance.Text("targetFile");

        if (!string.IsNullOrEmpty(_leftPath) && !string.IsNullOrEmpty(_rightPath))
        {
            InitSessionAndCompare();
        }
    }

    private void OnTakeLeftClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_leftPath) || string.IsNullOrEmpty(_rightPath)) return;
        try
        {
            File.Copy(_leftPath, _rightPath, true);
            if (StatusInfoText != null) StatusInfoText.Text = "已采纳左侧表格至右侧";
            InitSessionAndCompare();
        }
        catch (Exception ex)
        {
            if (StatusInfoText != null) StatusInfoText.Text = $"采纳失败: {ex.Message}";
        }
    }

    private void OnTakeRightClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_leftPath) || string.IsNullOrEmpty(_rightPath)) return;
        try
        {
            File.Copy(_rightPath, _leftPath, true);
            if (StatusInfoText != null) StatusInfoText.Text = "已采纳右侧表格至左侧";
            InitSessionAndCompare();
        }
        catch (Exception ex)
        {
            if (StatusInfoText != null) StatusInfoText.Text = $"采纳失败: {ex.Message}";
        }
    }

    private void OnClearAllClicked(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        _leftPath = null;
        _rightPath = null;
        _session?.Dispose();
        _session = null;
        _allRows.Clear();
        _displayRows.Clear();
        _searchQuery = string.Empty;
        if (ExcelSearchBox != null) ExcelSearchBox.Text = string.Empty;
        _filterMode = 0;
        SetFilterButtonsVisual(0);

        if (ExcelRowsList != null) ExcelRowsList.ItemsSource = null;
        if (SheetComboBox != null) SheetComboBox.Items.Clear();

        if (LeftExcelHeader != null) LeftExcelHeader.Text = LanguageManager.Instance.Text("sourceFile");
        if (RightExcelHeader != null) RightExcelHeader.Text = LanguageManager.Instance.Text("targetFile");

        if (DiffBadge != null) DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountBadge"), 0);
        if (StatusInfoText != null) StatusInfoText.Text = LanguageManager.Instance.Text("notLoaded");
        if (InspectorTitle != null) InspectorTitle.Text = LanguageManager.Instance.Text("inspectorTitle");
        if (InspectorContent != null) InspectorContent.Text = LanguageManager.Instance.Text("inspectorNoSelection");

        UpdateWorkspaceVisibility();
    }

    private void OnExportDiffClicked(object sender, RoutedEventArgs e)
    {
        if (_allRows.Count == 0) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Format(LanguageManager.Instance.Text("excelDiffReportTitle"), Path.GetFileName(_leftPath), Path.GetFileName(_rightPath)));
        sb.AppendLine(string.Format(LanguageManager.Instance.Text("exportTimeLabel"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        sb.AppendLine();
        string hRow = LanguageManager.Instance.Text("reportHeaderRow");
        string hStatus = LanguageManager.Instance.Text("reportHeaderStatus");
        string hLeft = LanguageManager.Instance.Text("reportHeaderLeft");
        string hRight = LanguageManager.Instance.Text("reportHeaderRight");
        sb.AppendLine($"| {hRow} | {hStatus} | {hLeft} | {hRight} |");
        sb.AppendLine("|---|---|---|---|");

        foreach (var r in _allRows.Where(r => r.IsDiff))
        {
            string leftFormatted = r.LeftRowFormatted.Replace("|", "\\|");
            string rightFormatted = r.RightRowFormatted.Replace("|", "\\|");
            sb.AppendLine($"| {r.RowIndexText} | {r.StatusText} | {leftFormatted} | {rightFormatted} |");
        }

        var dp = new DataPackage();
        dp.SetText(sb.ToString());
        Clipboard.SetContent(dp);

        if (StatusInfoText != null)
            StatusInfoText.Text = LanguageManager.Instance.Text("exportSuccess");
    }

    private void OnPrevDiffClicked(object sender, RoutedEventArgs e)
    {
        if (_displayRows.Count == 0) return;

        int startIndex = _currentDiffJumpIndex <= 0 ? _displayRows.Count - 1 : _currentDiffJumpIndex - 1;
        for (int i = startIndex; i >= 0; i--)
        {
            if (_displayRows[i].IsDiff)
            {
                _currentDiffJumpIndex = i;
                ExcelRowsList.SelectedIndex = i;
                ExcelRowsList.ScrollIntoView(_displayRows[i]);
                return;
            }
        }
        for (int i = _displayRows.Count - 1; i > startIndex; i--)
        {
            if (_displayRows[i].IsDiff)
            {
                _currentDiffJumpIndex = i;
                ExcelRowsList.SelectedIndex = i;
                ExcelRowsList.ScrollIntoView(_displayRows[i]);
                return;
            }
        }
    }

    private void OnNextDiffClicked(object sender, RoutedEventArgs e)
    {
        if (_displayRows.Count == 0) return;

        int startIndex = _currentDiffJumpIndex >= _displayRows.Count - 1 ? 0 : _currentDiffJumpIndex + 1;
        for (int i = startIndex; i < _displayRows.Count; i++)
        {
            if (_displayRows[i].IsDiff)
            {
                _currentDiffJumpIndex = i;
                ExcelRowsList.SelectedIndex = i;
                ExcelRowsList.ScrollIntoView(_displayRows[i]);
                return;
            }
        }
        for (int i = 0; i < startIndex; i++)
        {
            if (_displayRows[i].IsDiff)
            {
                _currentDiffJumpIndex = i;
                ExcelRowsList.SelectedIndex = i;
                ExcelRowsList.ScrollIntoView(_displayRows[i]);
                return;
            }
        }
    }

    private void OnRowSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ExcelRowsList?.SelectedItem is ExcelRowDisplayItem item)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[行 {item.RowIndexText}] 状态: {item.StatusText}");
            sb.AppendLine();
            sb.AppendLine($"[左侧 {Path.GetFileName(_leftPath) ?? "Left"}]:");
            sb.AppendLine(string.Join(", ", item.LeftCells));
            sb.AppendLine();
            sb.AppendLine($"[右侧 {Path.GetFileName(_rightPath) ?? "Right"}]:");
            sb.AppendLine(string.Join(", ", item.RightCells));

            if (InspectorContent != null)
                InspectorContent.Text = sb.ToString();
        }
        else
        {
            if (InspectorContent != null)
                InspectorContent.Text = LanguageManager.Instance.Text("inspectorNoSelection");
        }
    }

    private async void OnPickLeftExcelClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { ".xlsx", ".xls", ".csv" });
        if (file != null)
        {
            _leftPath = file.Path;
            if (LeftExcelHeader != null) LeftExcelHeader.Text = file.Name;
            UpdateWorkspaceVisibility();

            if (!string.IsNullOrEmpty(_rightPath))
            {
                RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.ExcelDiff);
                InitSessionAndCompare();
            }
        }
    }

    private async void OnPickRightExcelClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { ".xlsx", ".xls", ".csv" });
        if (file != null)
        {
            _rightPath = file.Path;
            if (RightExcelHeader != null) RightExcelHeader.Text = file.Name;
            UpdateWorkspaceVisibility();

            if (!string.IsNullOrEmpty(_leftPath))
            {
                RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.ExcelDiff);
                InitSessionAndCompare();
            }
        }
    }

    private void OnLeftExcelDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void OnLeftExcelDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                _leftPath = file.Path;
                if (LeftExcelHeader != null) LeftExcelHeader.Text = file.Name;
                UpdateWorkspaceVisibility();

                if (!string.IsNullOrEmpty(_rightPath))
                {
                    RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.ExcelDiff);
                    InitSessionAndCompare();
                }
            }
        }
    }

    private void OnRightExcelDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void OnRightExcelDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                _rightPath = file.Path;
                if (RightExcelHeader != null) RightExcelHeader.Text = file.Name;
                UpdateWorkspaceVisibility();

                if (!string.IsNullOrEmpty(_leftPath))
                {
                    RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.ExcelDiff);
                    InitSessionAndCompare();
                }
            }
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
    }
}
