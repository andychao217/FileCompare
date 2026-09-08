using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    private static readonly Brush ModBg = new SolidColorBrush(Windows.UI.Color.FromArgb(35, 234, 179, 8));
    private static readonly Brush ModFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 250, 204, 21));
    private static readonly Brush AddBg = new SolidColorBrush(Windows.UI.Color.FromArgb(35, 34, 197, 94));
    private static readonly Brush AddFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 74, 222, 128));
    private static readonly Brush DelBg = new SolidColorBrush(Windows.UI.Color.FromArgb(35, 239, 68, 68));
    private static readonly Brush DelFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 113, 113));
    private static readonly Brush SameBg = new SolidColorBrush(Windows.UI.Color.FromArgb(15, 156, 163, 175));
    private static readonly Brush SameFg = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 156, 163, 175));

    public ExcelDiffPage()
    {
        this.InitializeComponent();
        UpdateLocalization();
        LanguageManager.Instance.LanguageChanged += UpdateLocalization;
    }

    private void UpdateLocalization()
    {
        if (SheetLabel != null) SheetLabel.Text = LanguageManager.Instance.Text("worksheet") + ":";
        OpenFileBtn.Label = LanguageManager.Instance.Text("openFiles");
        PickLeftExcelItem.Text = LanguageManager.Instance.Text("pickLeftExcel");
        PickRightExcelItem.Text = LanguageManager.Instance.Text("pickRightExcel");
        PrevDiffBtn.Label = LanguageManager.Instance.Text("prevDiff");
        NextDiffBtn.Label = LanguageManager.Instance.Text("nextDiff");
        if (DiffOnlyToggle != null) DiffOnlyToggle.Label = LanguageManager.Instance.Text("filterDiffOnly");
        if (SwapSidesBtn != null) SwapSidesBtn.Label = LanguageManager.Instance.Text("swapSides");
        if (ExportReportBtn != null) ExportReportBtn.Label = LanguageManager.Instance.Text("exportDiff");
        if (ClearAllBtn != null) ClearAllBtn.Label = LanguageManager.Instance.Text("clearAll");
        if (ExcelSearchBox != null) ExcelSearchBox.PlaceholderText = LanguageManager.Instance.Text("searchOrFilter");
        RefreshBtn.Label = LanguageManager.Instance.Text("refreshCompare");

        LeftBrowseBtn.Content = LanguageManager.Instance.Text("browse");
        RightBrowseBtn.Content = LanguageManager.Instance.Text("browse");

        LeftColRowHeader.Text = LanguageManager.Instance.Text("excelColRow");
        RightColRowHeader.Text = LanguageManager.Instance.Text("excelColRow");
        LeftColStatusHeader.Text = LanguageManager.Instance.Text("excelColStatus");
        RightColStatusHeader.Text = LanguageManager.Instance.Text("excelColStatus");
        LeftColContentHeader.Text = LanguageManager.Instance.Text("leftColContent");
        RightColContentHeader.Text = LanguageManager.Instance.Text("rightColContent");

        EmptyPromptText.Text = LanguageManager.Instance.Text("excelEmptyPrompt");
        EmptyActionBtn.Content = LanguageManager.Instance.Text("excelEmptyAction");
        InspectorTitle.Text = LanguageManager.Instance.Text("inspectorTitle");

        if (string.IsNullOrEmpty(_leftPath))
        {
            LeftExcelPathText.Text = LanguageManager.Instance.Text("leftExcelPrompt");
        }
        if (string.IsNullOrEmpty(_rightPath))
        {
            RightExcelPathText.Text = LanguageManager.Instance.Text("rightExcelPrompt");
        }
        if (ExcelRowsList.SelectedItem == null)
        {
            InspectorContent.Text = LanguageManager.Instance.Text("inspectorNoSelection");
        }

        if (_displayRows.Count > 0)
        {
            int diffCount = _displayRows.Count(r => r.IsDiff);
            DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountBadge"), diffCount);

            foreach (var r in _displayRows)
            {
                if (r.StatusBgBrush == AddBg)
                    r.StatusText = LanguageManager.Instance.Text("statusAdded");
                else if (r.StatusBgBrush == DelBg)
                    r.StatusText = LanguageManager.Instance.Text("statusDeleted");
                else if (r.StatusBgBrush == ModBg)
                    r.StatusText = LanguageManager.Instance.Text("statusModified");
                else
                    r.StatusText = LanguageManager.Instance.Text("statusIdentical");
            }

            string sheetName = SheetComboBox.SelectedItem?.ToString() ?? "Sheet1";
            StatusInfoText.Text = string.Format(LanguageManager.Instance.Text("excelSummaryStats"), sheetName, _displayRows.Count, diffCount);
        }
        else
        {
            DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountBadge"), 0);
            StatusInfoText.Text = LanguageManager.Instance.Text("notLoaded");
        }
    }

    public void LoadFiles(string? leftPath, string? rightPath)
    {
        if (!string.IsNullOrEmpty(leftPath) && File.Exists(leftPath))
        {
            _leftPath = leftPath;
            LeftExcelPathText.Text = leftPath;
        }

        if (!string.IsNullOrEmpty(rightPath) && File.Exists(rightPath))
        {
            _rightPath = rightPath;
            RightExcelPathText.Text = rightPath;
        }

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
        InitSessionAndCompare();
    }

    private void InitSessionAndCompare()
    {
        if (string.IsNullOrEmpty(_leftPath) || string.IsNullOrEmpty(_rightPath)) return;

        EmptyStatePanel.Visibility = Visibility.Collapsed;

        // Dispose previous session if any
        _session?.Dispose();
        _session = null;

        try
        {
            _session = ExcelDiffSession.Create(_leftPath, _rightPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ExcelDiffSession.Create failed: {ex.Message}");
        }

        if (_session != null)
        {
            var summary = _session.GetSummary();
            DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountBadge"), summary.TotalDiffCount);

            SheetComboBox.Items.Clear();
            if (summary.Sheets.Count > 0)
            {
                foreach (var s in summary.Sheets)
                {
                    SheetComboBox.Items.Add($"{s.SheetName} ({s.DiffCount})");
                }
                SheetComboBox.SelectedIndex = 0;
            }
            else
            {
                SheetComboBox.Items.Add("Sheet1");
                SheetComboBox.SelectedIndex = 0;
            }
        }
        else
        {
            // Fallback: CSV / Delimited parser
            LoadFallbackCsvComparison();
        }
    }

    private void LoadFallbackCsvComparison()
    {
        if (string.IsNullOrEmpty(_leftPath) || string.IsNullOrEmpty(_rightPath)) return;

        SheetComboBox.Items.Clear();
        SheetComboBox.Items.Add("Table1");
        SheetComboBox.SelectedIndex = 0;

        string[] leftLines = File.Exists(_leftPath) ? File.ReadAllLines(_leftPath) : Array.Empty<string>();
        string[] rightLines = File.Exists(_rightPath) ? File.ReadAllLines(_rightPath) : Array.Empty<string>();

        int max = Math.Max(leftLines.Length, rightLines.Length);
        _allRows.Clear();
        int diffCount = 0;

        for (int i = 0; i < max; i++)
        {
            string lStr = i < leftLines.Length ? leftLines[i] : "";
            string rStr = i < rightLines.Length ? rightLines[i] : "";

            var lCells = lStr.Split(new[] { ',', '\t', '|' }).Select(s => s.Trim()).ToList();
            var rCells = rStr.Split(new[] { ',', '\t', '|' }).Select(s => s.Trim()).ToList();

            bool isAdd = i >= leftLines.Length;
            bool isDel = i >= rightLines.Length;
            bool isMod = !isAdd && !isDel && lStr != rStr;
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
        _currentSheetIndex = Math.Max(0, SheetComboBox.SelectedIndex);
        LoadSheetViewport();
    }

    private void LoadSheetViewport()
    {
        if (_session == null) return;

        var viewport = _session.GetViewport(_currentSheetIndex, 0, 5000);
        _allRows.Clear();

        foreach (var r in viewport.Rows)
        {
            var item = new ExcelRowDisplayItem
            {
                RowIndex = r.RowIndex,
                LeftCells = r.LeftCells,
                RightCells = r.RightCells,
                LeftRowFormatted = string.Join("  |  ", r.LeftCells),
                RightRowFormatted = string.Join("  |  ", r.RightCells),
                IsDiff = r.Status != "Unchanged"
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
        bool diffOnly = DiffOnlyToggle?.IsChecked == true;
        string query = _searchQuery.Trim().ToLowerInvariant();

        IEnumerable<ExcelRowDisplayItem> filtered = _allRows;

        if (diffOnly)
        {
            filtered = filtered.Where(r => r.IsDiff);
        }

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(r =>
                r.LeftRowFormatted.ToLowerInvariant().Contains(query) ||
                r.RightRowFormatted.ToLowerInvariant().Contains(query) ||
                r.RowIndexText.Contains(query));
        }

        _displayRows = filtered.ToList();
        ExcelRowsList.ItemsSource = null;
        ExcelRowsList.ItemsSource = _displayRows;

        int totalDiff = _allRows.Count(r => r.IsDiff);
        DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountBadge"), totalDiff);
        string sheetName = SheetComboBox.SelectedItem?.ToString() ?? $"Sheet{_currentSheetIndex + 1}";
        StatusInfoText.Text = string.Format(LanguageManager.Instance.Text("excelSummaryStats"), sheetName, _displayRows.Count, totalDiff);
    }

    private void OnDiffOnlyToggled(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _searchQuery = sender.Text ?? string.Empty;
        ApplyFilter();
    }

    private void OnSwapSidesClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_leftPath) && string.IsNullOrEmpty(_rightPath)) return;
        var temp = _leftPath;
        _leftPath = _rightPath;
        _rightPath = temp;

        LeftExcelPathText.Text = _leftPath ?? LanguageManager.Instance.Text("leftExcelPrompt");
        RightExcelPathText.Text = _rightPath ?? LanguageManager.Instance.Text("rightExcelPrompt");

        if (!string.IsNullOrEmpty(_leftPath) && !string.IsNullOrEmpty(_rightPath))
        {
            InitSessionAndCompare();
        }
    }

    private void OnClearAllClicked(object sender, RoutedEventArgs e)
    {
        _leftPath = null;
        _rightPath = null;
        _session?.Dispose();
        _session = null;
        _allRows.Clear();
        _displayRows.Clear();
        _searchQuery = string.Empty;
        if (ExcelSearchBox != null) ExcelSearchBox.Text = string.Empty;
        if (DiffOnlyToggle != null) DiffOnlyToggle.IsChecked = false;
        ExcelRowsList.ItemsSource = null;
        SheetComboBox.Items.Clear();
        LeftExcelPathText.Text = LanguageManager.Instance.Text("leftExcelPrompt");
        RightExcelPathText.Text = LanguageManager.Instance.Text("rightExcelPrompt");
        EmptyStatePanel.Visibility = Visibility.Visible;
        DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountBadge"), 0);
        StatusInfoText.Text = LanguageManager.Instance.Text("notLoaded");
        InspectorTitle.Text = LanguageManager.Instance.Text("inspectorTitle");
        InspectorContent.Text = LanguageManager.Instance.Text("inspectorNoSelection");
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
        if (ExcelRowsList.SelectedItem is ExcelRowDisplayItem item)
        {
            InspectorTitle.Text = string.Format(LanguageManager.Instance.Text("inspectorRowDetailTitle"), item.RowIndexText, item.StatusText);
            var sb = new System.Text.StringBuilder();

            string noneText = LanguageManager.Instance.Text("inspectorNone");
            int maxCols = Math.Max(item.LeftCells.Count, item.RightCells.Count);
            for (int col = 0; col < maxCols; col++)
            {
                string l = col < item.LeftCells.Count ? item.LeftCells[col] : noneText;
                string r = col < item.RightCells.Count ? item.RightCells[col] : noneText;

                if (l != r)
                {
                    sb.AppendLine(string.Format(LanguageManager.Instance.Text("inspectorColModified"), col + 1, l, r));
                }
                else
                {
                    sb.AppendLine(string.Format(LanguageManager.Instance.Text("inspectorColIdentical"), col + 1, l));
                }
            }

            InspectorContent.Text = sb.ToString();
        }
    }

    private async void OnPickLeftExcelClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { ".xlsx", ".xls", ".csv" }, App.MainWindowInstance);
        if (file != null)
        {
            _leftPath = file.Path;
            LeftExcelPathText.Text = file.Path;
            if (!string.IsNullOrEmpty(_rightPath))
            {
                RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.ExcelDiff);
                InitSessionAndCompare();
            }
        }
    }

    private async void OnPickRightExcelClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { ".xlsx", ".xls", ".csv" }, App.MainWindowInstance);
        if (file != null)
        {
            _rightPath = file.Path;
            RightExcelPathText.Text = file.Path;
            if (!string.IsNullOrEmpty(_leftPath))
            {
                RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.ExcelDiff);
                InitSessionAndCompare();
            }
        }
    }

    private void OnLeftExcelDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void OnLeftExcelDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                _leftPath = file.Path;
                LeftExcelPathText.Text = file.Path;
                if (!string.IsNullOrEmpty(_rightPath)) InitSessionAndCompare();
            }
        }
    }

    private void OnRightExcelDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void OnRightExcelDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                _rightPath = file.Path;
                RightExcelPathText.Text = file.Path;
                if (!string.IsNullOrEmpty(_leftPath)) InitSessionAndCompare();
            }
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
    }
}
