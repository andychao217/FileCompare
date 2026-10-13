using System.IO;
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
    private ExcelWorkbookModel? _leftWorkbook;
    private ExcelWorkbookModel? _rightWorkbook;
    private ExcelWorkbookDiffResult? _diffResult;
    private ExcelWorkbookModel? _singleWorkbook;
    private bool _isSingleLeft;
    private int _singleSheetIndex = 0;

    private int _currentSheetIndex = 0;
    private int _selectedRowIndex = -1;
    private int _currentDiffJumpIndex = -1;
    private string _searchQuery = string.Empty;
    private int _filterMode = 0; // 0: All, 1: DiffOnly, 2: SameOnly
    private bool _isInitialized = false;
    private bool _isSyncingScroll = false;

    private List<AlignedExcelRow> _allAlignedRows = new();
    private List<AlignedExcelRow> _displayAlignedRows = new();
    private List<ExcelRowDisplayItem> _leftDisplayRows = new();
    private List<ExcelRowDisplayItem> _rightDisplayRows = new();

    private static Brush ModDotBrush => App.GetThemeBrush("StatusModTextBrush");
    private static Brush AddDotBrush => App.GetThemeBrush("StatusAddTextBrush");
    private static Brush DelDotBrush => App.GetThemeBrush("StatusDelTextBrush");
    private static readonly Brush TransBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    private static Brush ModRowBg => App.GetThemeBrush("DiffModifiedBgBrush");
    private static Brush AddRowBg => App.GetThemeBrush("DiffAddedBgBrush");
    private static Brush DelRowBg => App.GetThemeBrush("DiffDeletedBgBrush");
    private static Brush SelectedRowBg => App.GetThemeBrush("DiffSelectedRowBgBrush");
    private static Brush PhantomRowBg => App.GetThemeBrush("DiffPhantomBgBrush");

    private Brush PrimaryTextBrush => App.GetThemeBrush("TextFillColorPrimaryBrush");
    private Brush SecondaryTextBrush => App.GetThemeBrush("TextFillColorSecondaryBrush");
    private Brush TertiaryTextBrush => App.GetThemeBrush("TextFillColorTertiaryBrush");

    private Brush CellDelFg => App.GetThemeBrush("DiffDeletedFgBrush");
    private Brush CellAddFg => App.GetThemeBrush("DiffAddedFgBrush");
    private Brush CellModFg => App.GetThemeBrush("DiffModifiedFgBrush");

    private Brush CellDelBg => App.GetThemeBrush("DiffDeletedBgBrush");
    private Brush CellAddBg => App.GetThemeBrush("DiffAddedBgBrush");

    private static Brush ModBorderBrush => App.GetThemeBrush("StatusModTextBrush");
    private static Brush NormalBorderBrush => App.GetThemeBrush("DividerStrokeColorDefaultBrush");

    public ExcelDiffPage()
    {
        this.InitializeComponent();

        if (WhitespaceSwitch != null)
            WhitespaceSwitch.IsOn = SettingsManager.Instance.DefaultIgnoreWhitespace;
        if (CaseSwitch != null)
            CaseSwitch.IsOn = SettingsManager.Instance.DefaultIgnoreCase;

        UpdateLocalization();
        LanguageManager.Instance.LanguageChanged += UpdateLocalization;
        this.ActualThemeChanged += OnActualThemeChanged;

        _isInitialized = true;
        UpdateWorkspaceVisibility();
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        SetFilterButtonsVisual(_filterMode);
        RenderSheetTabs();
        if (_diffResult != null && _currentSheetIndex >= 0 && _currentSheetIndex < _diffResult.SheetDiffs.Count)
        {
            var sheetDiff = _diffResult.SheetDiffs[_currentSheetIndex];
            RenderColumnHeaders(sheetDiff.ColumnHeaders);
            ApplyFilter();
            if (_selectedRowIndex >= 0 && _selectedRowIndex < _displayAlignedRows.Count)
            {
                UpdateInspector(_displayAlignedRows[_selectedRowIndex]);
            }
        }
    }

    private void UpdateLocalization()
    {
        if (PrevDiffText != null) PrevDiffText.Text = LanguageManager.Instance.Text("prevDiff");
        if (NextDiffText != null) NextDiffText.Text = LanguageManager.Instance.Text("nextDiff");
        if (WhitespaceLabel != null) WhitespaceLabel.Text = LanguageManager.Instance.Text("ignoreWhitespace");
        if (CaseLabel != null) CaseLabel.Text = LanguageManager.Instance.Text("ignoreCase");
        if (RuleBtnText != null) RuleBtnText.Text = LanguageManager.Instance.Text("diffRules");
        if (SwapSidesText != null) SwapSidesText.Text = LanguageManager.Instance.Text("swapSides");
        if (RefreshText != null) RefreshText.Text = LanguageManager.Instance.Text("refreshCompare");
        if (TakeLeftText != null) TakeLeftText.Text = LanguageManager.Instance.Text("takeLeft");
        if (TakeRightText != null) TakeRightText.Text = LanguageManager.Instance.Text("takeRight");
        if (ClearAllText != null) ClearAllText.Text = LanguageManager.Instance.Text("clearAll");

        if (FilterAllText != null) FilterAllText.Text = LanguageManager.Instance.Text("filterAll");
        if (FilterDiffText != null) FilterDiffText.Text = LanguageManager.Instance.Text("filterDiffOnly");
        if (FilterSameText != null) FilterSameText.Text = LanguageManager.Instance.Text("filterSameOnly");
        if (ExcelSearchBox != null) ExcelSearchBox.PlaceholderText = LanguageManager.Instance.Text("searchCellsPlaceholder");

        if (LeftBrowseBtn != null) LeftBrowseBtn.Content = LanguageManager.Instance.Text("selectEllipsis");
        if (RightBrowseBtn != null) RightBrowseBtn.Content = LanguageManager.Instance.Text("selectEllipsis");
        if (LeftPickBtn != null) LeftPickBtn.Content = LanguageManager.Instance.Text("pickSourceFile");
        if (RightPickBtn != null) RightPickBtn.Content = LanguageManager.Instance.Text("pickTargetFile");

        if (LeftEmptyTitle != null) LeftEmptyTitle.Text = LanguageManager.Instance.Text("noFileSelected");
        if (RightEmptyTitle != null) RightEmptyTitle.Text = LanguageManager.Instance.Text("noFileSelected");
        if (LeftEmptyDesc != null) LeftEmptyDesc.Text = LanguageManager.Instance.Text("dropOrPickFile");
        if (RightEmptyDesc != null) RightEmptyDesc.Text = LanguageManager.Instance.Text("dropOrPickFile");

        if (LeftLoadingText != null) LeftLoadingText.Text = LanguageManager.Instance.Text("loadingSourceSheet");
        if (RightLoadingText != null) RightLoadingText.Text = LanguageManager.Instance.Text("loadingTargetSheet");

        if (InspectorTitle != null) InspectorTitle.Text = LanguageManager.Instance.Text("inspectorTitle");

        if (string.IsNullOrEmpty(_leftPath) && LeftExcelHeader != null)
        {
            LeftExcelHeader.Text = LanguageManager.Instance.Text("sourceFile");
        }
        if (string.IsNullOrEmpty(_rightPath) && RightExcelHeader != null)
        {
            RightExcelHeader.Text = LanguageManager.Instance.Text("targetFile");
        }
    }

    private void UpdateWorkspaceVisibility()
    {
        if (!_isInitialized) return;

        bool hasLeft = !string.IsNullOrEmpty(_leftPath);
        bool hasRight = !string.IsNullOrEmpty(_rightPath);
        bool hasBoth = hasLeft && hasRight;

        if (!hasLeft && !hasRight)
        {
            if (LeftDropZone != null) LeftDropZone.Visibility = Visibility.Visible;
            if (LeftTablePane != null) LeftTablePane.Visibility = Visibility.Collapsed;
            if (RightDropZone != null) RightDropZone.Visibility = Visibility.Visible;
            if (RightTablePane != null) RightTablePane.Visibility = Visibility.Collapsed;
            if (InspectorContainer != null) InspectorContainer.Visibility = Visibility.Collapsed;

            if (PrevDiffBtn != null) PrevDiffBtn.IsEnabled = false;
            if (NextDiffBtn != null) NextDiffBtn.IsEnabled = false;
            if (RuleSettingsBtn != null) RuleSettingsBtn.IsEnabled = false;
            if (SwapSidesBtn != null) SwapSidesBtn.IsEnabled = false;
            if (RefreshBtn != null) RefreshBtn.IsEnabled = false;
            if (TakeLeftBtn != null) TakeLeftBtn.IsEnabled = false;
            if (TakeRightBtn != null) TakeRightBtn.IsEnabled = false;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = false;
            if (ExcelStatsPanel != null) ExcelStatsPanel.Visibility = Visibility.Collapsed;
            if (SheetTabsPanel != null) SheetTabsPanel.Children.Clear();
            if (SheetComboBox != null) SheetComboBox.Items.Clear();
        }
        else if (hasLeft && !hasRight)
        {
            if (LeftDropZone != null) LeftDropZone.Visibility = Visibility.Collapsed;
            if (LeftTablePane != null) LeftTablePane.Visibility = Visibility.Visible;
            if (RightDropZone != null) RightDropZone.Visibility = Visibility.Visible;
            if (RightTablePane != null) RightTablePane.Visibility = Visibility.Collapsed;
            if (InspectorContainer != null) InspectorContainer.Visibility = Visibility.Collapsed;

            LoadSingleFilePreview(_leftPath!, true);

            if (PrevDiffBtn != null) PrevDiffBtn.IsEnabled = false;
            if (NextDiffBtn != null) NextDiffBtn.IsEnabled = false;
            if (RuleSettingsBtn != null) RuleSettingsBtn.IsEnabled = true;
            if (SwapSidesBtn != null) SwapSidesBtn.IsEnabled = true;
            if (RefreshBtn != null) RefreshBtn.IsEnabled = true;
            if (TakeLeftBtn != null) TakeLeftBtn.IsEnabled = false;
            if (TakeRightBtn != null) TakeRightBtn.IsEnabled = false;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = true;
        }
        else if (!hasLeft && hasRight)
        {
            if (LeftDropZone != null) LeftDropZone.Visibility = Visibility.Visible;
            if (LeftTablePane != null) LeftTablePane.Visibility = Visibility.Collapsed;
            if (RightDropZone != null) RightDropZone.Visibility = Visibility.Collapsed;
            if (RightTablePane != null) RightTablePane.Visibility = Visibility.Visible;
            if (InspectorContainer != null) InspectorContainer.Visibility = Visibility.Collapsed;

            LoadSingleFilePreview(_rightPath!, false);

            if (PrevDiffBtn != null) PrevDiffBtn.IsEnabled = false;
            if (NextDiffBtn != null) NextDiffBtn.IsEnabled = false;
            if (RuleSettingsBtn != null) RuleSettingsBtn.IsEnabled = true;
            if (SwapSidesBtn != null) SwapSidesBtn.IsEnabled = true;
            if (RefreshBtn != null) RefreshBtn.IsEnabled = true;
            if (TakeLeftBtn != null) TakeLeftBtn.IsEnabled = false;
            if (TakeRightBtn != null) TakeRightBtn.IsEnabled = false;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = true;
        }
        else
        {
            if (LeftDropZone != null) LeftDropZone.Visibility = Visibility.Collapsed;
            if (LeftTablePane != null) LeftTablePane.Visibility = Visibility.Visible;
            if (RightDropZone != null) RightDropZone.Visibility = Visibility.Collapsed;
            if (RightTablePane != null) RightTablePane.Visibility = Visibility.Visible;

            if (PrevDiffBtn != null) PrevDiffBtn.IsEnabled = true;
            if (NextDiffBtn != null) NextDiffBtn.IsEnabled = true;
            if (RuleSettingsBtn != null) RuleSettingsBtn.IsEnabled = true;
            if (SwapSidesBtn != null) SwapSidesBtn.IsEnabled = true;
            if (RefreshBtn != null) RefreshBtn.IsEnabled = true;
            if (TakeLeftBtn != null) TakeLeftBtn.IsEnabled = true;
            if (TakeRightBtn != null) TakeRightBtn.IsEnabled = true;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = true;
        }
    }

    private async void LoadSingleFilePreview(string filePath, bool isLeft)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

        if (isLeft && LeftLoadingOverlay != null) LeftLoadingOverlay.Visibility = Visibility.Visible;
        if (!isLeft && RightLoadingOverlay != null) RightLoadingOverlay.Visibility = Visibility.Visible;

        try
        {
            var wb = await ExcelDocumentParser.ParseWorkbookAsync(filePath);
            _singleWorkbook = wb;
            _isSingleLeft = isLeft;
            _singleSheetIndex = 0;

            RenderSingleWorkbookSheetTabs(wb, isLeft);
            LoadSingleSheet(0, isLeft);
        }
        catch (Exception ex)
        {
            App.Log($"[ExcelDiffPage] LoadSingleFilePreview error: {ex.Message}");
        }
        finally
        {
            if (isLeft && LeftLoadingOverlay != null) LeftLoadingOverlay.Visibility = Visibility.Collapsed;
            if (!isLeft && RightLoadingOverlay != null) RightLoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void RenderSingleWorkbookSheetTabs(ExcelWorkbookModel wb, bool isLeft)
    {
        if (SheetTabsPanel == null) return;
        SheetTabsPanel.Children.Clear();

        if (SheetComboBox != null)
        {
            SheetComboBox.Items.Clear();
        }

        for (int i = 0; i < wb.Sheets.Count; i++)
        {
            int idx = i;
            var sheet = wb.Sheets[i];

            if (SheetComboBox != null)
            {
                SheetComboBox.Items.Add(sheet.Name);
            }

            var pillBtn = new Button
            {
                Padding = new Thickness(10, 3, 10, 3),
                CornerRadius = new CornerRadius(5),
                Background = (idx == _singleSheetIndex)
                    ? (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"]
                    : new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Height = 26
            };

            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

            var dot = new Microsoft.UI.Xaml.Shapes.Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = App.GetThemeBrush("TextMutedBrush"),
                VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(dot);

            var nameText = new TextBlock
            {
                Text = sheet.Name,
                FontSize = 11,
                FontWeight = (idx == _singleSheetIndex) ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(nameText);

            pillBtn.Content = sp;
            pillBtn.Click += (s, e) =>
            {
                _singleSheetIndex = idx;
                RenderSingleWorkbookSheetTabs(wb, isLeft);
                LoadSingleSheet(idx, isLeft);
            };

            SheetTabsPanel.Children.Add(pillBtn);
        }

        if (SheetComboBox != null && SheetComboBox.Items.Count > _singleSheetIndex)
        {
            SheetComboBox.SelectedIndex = _singleSheetIndex;
        }
    }

    private void LoadSingleSheet(int sheetIdx, bool isLeft)
    {
        if (_singleWorkbook == null || sheetIdx < 0 || sheetIdx >= _singleWorkbook.Sheets.Count) return;

        var sheet = _singleWorkbook.Sheets[sheetIdx];
        int maxCols = sheet.MaxColumns;

        // Determine headers
        var headers = new List<string>();
        bool firstRowHeader = true;
        var firstRow = sheet.Rows.FirstOrDefault();

        for (int i = 0; i < maxCols; i++)
        {
            string? h = (firstRowHeader && firstRow != null) ? firstRow.Cell(i)?.FormattedValue : null;
            if (string.IsNullOrWhiteSpace(h))
            {
                headers.Add(ExcelDocumentParser.IndexToColumnLetter(i));
            }
            else
            {
                headers.Add(h);
            }
        }

        // Render header
        if (isLeft && LeftHeaderPanel != null)
        {
            LeftHeaderPanel.Children.Clear();
            BuildHeaderControls(LeftHeaderPanel, headers);
        }
        else if (!isLeft && RightHeaderPanel != null)
        {
            RightHeaderPanel.Children.Clear();
            BuildHeaderControls(RightHeaderPanel, headers);
        }

        // Build display rows
        var displayRows = new List<ExcelRowDisplayItem>();
        var primaryBrush = PrimaryTextBrush;
        var secondaryBrush = SecondaryTextBrush;

        int startRowIdx = (firstRowHeader && sheet.Rows.Count > 1) ? 1 : 0;
        for (int r = startRowIdx; r < sheet.Rows.Count; r++)
        {
            var rowData = sheet.Rows[r];
            var rowItem = new ExcelRowDisplayItem
            {
                RowId = Guid.NewGuid(),
                RowIndex = r,
                DisplayRowNumber = rowData.RowIndex.ToString(),
                RowNumberFgBrush = secondaryBrush,
                DiffType = RowDiffType.Unchanged,
                DotBrush = TransBrush,
                RowBgBrush = TransBrush,
                IsSelected = false
            };

            for (int c = 0; c < maxCols; c++)
            {
                var cell = rowData.Cell(c);
                string text = cell?.FormattedValue ?? cell?.RawValue ?? string.Empty;
                rowItem.Cells.Add(new ExcelCellDisplayItem
                {
                    ColumnIndex = c,
                    ColumnLetter = ExcelDocumentParser.IndexToColumnLetter(c),
                    Text = text,
                    DiffType = CellDiffType.Unchanged,
                    TextBrush = primaryBrush,
                    BgBrush = TransBrush,
                    Width = 140
                });
            }

            displayRows.Add(rowItem);
        }

        if (isLeft)
        {
            _leftDisplayRows.Clear();
            _leftDisplayRows.AddRange(displayRows);
            if (LeftRowsControl != null) LeftRowsControl.ItemsSource = null;
            if (LeftRowsControl != null) LeftRowsControl.ItemsSource = _leftDisplayRows;
        }
        else
        {
            _rightDisplayRows.Clear();
            _rightDisplayRows.AddRange(displayRows);
            if (RightRowsControl != null) RightRowsControl.ItemsSource = null;
            if (RightRowsControl != null) RightRowsControl.ItemsSource = _rightDisplayRows;
        }

        // Update stats bar
        if (ExcelStatsPanel != null) ExcelStatsPanel.Visibility = Visibility.Visible;
        if (ExcelDiffCountText != null)
            ExcelDiffCountText.Text = $"{sheet.Rows.Count} 行";
        if (ExcelSameCountText != null)
            ExcelSameCountText.Text = $"{maxCols} 列";
        if (ExcelLoadTimeText != null)
            ExcelLoadTimeText.Text = $"工作表: {sheet.Name}";
    }

    public void LoadFiles(string? leftPath, string? rightPath)
    {
        _leftPath = leftPath;
        _rightPath = rightPath;

        if (LeftExcelHeader != null)
            LeftExcelHeader.Text = !string.IsNullOrEmpty(_leftPath) ? Path.GetFileName(_leftPath) : LanguageManager.Instance.Text("sourceFile");
        if (RightExcelHeader != null)
            RightExcelHeader.Text = !string.IsNullOrEmpty(_rightPath) ? Path.GetFileName(_rightPath) : LanguageManager.Instance.Text("targetFile");

        UpdateWorkspaceVisibility();
        UpdateTabHeader();

        if (!string.IsNullOrEmpty(_leftPath) && !string.IsNullOrEmpty(_rightPath))
        {
            InitSessionAndCompare();
        }
    }

    private void InitSessionAndCompare()
    {
        _ = InitSessionAndCompareAsync();
    }

    private async Task InitSessionAndCompareAsync()
    {
        if (string.IsNullOrEmpty(_leftPath) || string.IsNullOrEmpty(_rightPath)) return;

        UpdateWorkspaceVisibility();

        if (DualCompareLoadingOverlay != null)
        {
            DualCompareLoadingOverlay.Visibility = Visibility.Visible;
        }

        try
        {
            string leftPath = _leftPath;
            string rightPath = _rightPath;

            var rules = new ExcelCompareRules
            {
                IgnoreWhitespace = WhitespaceSwitch?.IsOn ?? false,
                IgnoreCase = CaseSwitch?.IsOn ?? false,
                FirstRowAsHeader = true,
                NumericTolerance = 0.0001
            };

            var (leftWb, rightWb) = await Task.Run(() =>
            {
                var l = ExcelDocumentParser.ParseWorkbook(leftPath);
                var r = ExcelDocumentParser.ParseWorkbook(rightPath);
                return (l, r);
            });

            _leftWorkbook = leftWb;
            _rightWorkbook = rightWb;

            if (LeftExcelHeader != null)
            {
                string sizeStr = !string.IsNullOrEmpty(leftWb.FileSizeFormatted) ? $" ({leftWb.FileSizeFormatted})" : "";
                LeftExcelHeader.Text = $"{leftWb.FileName}{sizeStr}";
            }
            if (RightExcelHeader != null)
            {
                string sizeStr = !string.IsNullOrEmpty(rightWb.FileSizeFormatted) ? $" ({rightWb.FileSizeFormatted})" : "";
                RightExcelHeader.Text = $"{rightWb.FileName}{sizeStr}";
            }

            _diffResult = await ExcelDiffEngine.CompareWorkbooksAsync(leftWb, rightWb, rules);

            RenderSheetTabs();

            _currentSheetIndex = 0;
            LoadSheet(_currentSheetIndex);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"InitSessionAndCompareAsync failed: {ex.Message}");
        }
        finally
        {
            if (DualCompareLoadingOverlay != null)
            {
                DualCompareLoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void RenderSheetTabs()
    {
        if (SheetTabsPanel == null || _diffResult == null) return;
        SheetTabsPanel.Children.Clear();

        if (SheetComboBox != null)
        {
            SheetComboBox.Items.Clear();
        }

        for (int i = 0; i < _diffResult.SheetDiffs.Count; i++)
        {
            int idx = i;
            var sheet = _diffResult.SheetDiffs[i];

            if (SheetComboBox != null)
            {
                SheetComboBox.Items.Add($"{sheet.SheetName} ({sheet.DifferenceRowCount})");
            }

            var pillBtn = new Button
            {
                Padding = new Thickness(10, 3, 10, 3),
                CornerRadius = new CornerRadius(5),
                Background = (idx == _currentSheetIndex)
                    ? (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"]
                    : new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Height = 26
            };

            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

            // Dot status
            var dot = new Microsoft.UI.Xaml.Shapes.Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = (sheet.DifferenceRowCount > 0) ? ModDotBrush : App.GetThemeBrush("TextMutedBrush"),
                VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(dot);

            // Sheet Name
            var nameText = new TextBlock
            {
                Text = sheet.SheetName,
                FontSize = 11,
                FontWeight = (idx == _currentSheetIndex) ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(nameText);

            // Diff count
            if (sheet.DifferenceRowCount > 0)
            {
                var countText = new TextBlock
                {
                    Text = $"({sheet.DifferenceRowCount})",
                    FontSize = 10,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = ModDotBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                sp.Children.Add(countText);
            }

            pillBtn.Content = sp;
            pillBtn.Click += (s, e) =>
            {
                _currentSheetIndex = idx;
                RenderSheetTabs();
                LoadSheet(_currentSheetIndex);
            };

            SheetTabsPanel.Children.Add(pillBtn);
        }

        if (SheetComboBox != null && SheetComboBox.Items.Count > _currentSheetIndex)
        {
            SheetComboBox.SelectedIndex = _currentSheetIndex;
        }
    }

    private void LoadSheet(int sheetIdx)
    {
        if (_diffResult == null || sheetIdx < 0 || sheetIdx >= _diffResult.SheetDiffs.Count) return;

        var sheetDiff = _diffResult.SheetDiffs[sheetIdx];
        _allAlignedRows = sheetDiff.AlignedRows;

        // Render Column Headers for Left and Right panes
        RenderColumnHeaders(sheetDiff.ColumnHeaders);

        // Apply filters & populate rows
        ApplyFilter();

        // Update stats bar
        if (ExcelStatsPanel != null) ExcelStatsPanel.Visibility = Visibility.Visible;
        if (ExcelDiffCountText != null)
            ExcelDiffCountText.Text = $"{sheetDiff.DifferenceRowCount} {LanguageManager.Instance.Text("excelDifferenceRows")}";
        if (ExcelSameCountText != null)
            ExcelSameCountText.Text = $"{sheetDiff.SameRowCount} {LanguageManager.Instance.Text("excelSameRows")}";
        if (ExcelLoadTimeText != null)
            ExcelLoadTimeText.Text = $"{LanguageManager.Instance.Text("excelLoadTime")}: {_diffResult.LoadTimeSeconds:F2}s";

        // Auto select first diff or first row
        if (_displayAlignedRows.Count > 0)
        {
            int firstDiffIdx = _displayAlignedRows.FindIndex(r => r.RowDiffType != RowDiffType.Unchanged);
            SelectRow(firstDiffIdx >= 0 ? firstDiffIdx : 0);
        }
        else
        {
            if (InspectorContainer != null) InspectorContainer.Visibility = Visibility.Collapsed;
        }
    }

    private void RenderColumnHeaders(List<string> headers)
    {
        if (LeftHeaderPanel != null)
        {
            LeftHeaderPanel.Children.Clear();
            BuildHeaderControls(LeftHeaderPanel, headers);
        }
        if (RightHeaderPanel != null)
        {
            RightHeaderPanel.Children.Clear();
            BuildHeaderControls(RightHeaderPanel, headers);
        }
    }

    private void BuildHeaderControls(StackPanel panel, List<string> headers)
    {
        // 1. # Column Header (Width 44)
        var rowNumHeader = new Border
        {
            Width = 44,
            Height = 28,
            BorderThickness = new Thickness(0, 0, 1, 0),
            BorderBrush = NormalBorderBrush,
            Background = TransBrush
        };
        var hashText = new TextBlock
        {
            Text = "#",
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            FontFamily = (FontFamily)Application.Current.Resources["CodeFontFamily"],
            Foreground = TertiaryTextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        rowNumHeader.Child = hashText;
        panel.Children.Add(rowNumHeader);

        // 2. Data Column Headers (Width 140 each)
        for (int i = 0; i < headers.Count; i++)
        {
            string colLetter = ExcelDocumentParser.IndexToColumnLetter(i);
            string headerName = headers[i];

            var colBorder = new Border
            {
                Width = 140,
                Height = 28,
                BorderThickness = new Thickness(0, 0, 1, 0),
                BorderBrush = NormalBorderBrush,
                Background = TransBrush,
                Padding = new Thickness(8, 0, 8, 0)
            };

            var sp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center
            };

            var letterTb = new TextBlock
            {
                Text = colLetter,
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontFamily = (FontFamily)Application.Current.Resources["CodeFontFamily"],
                Foreground = TertiaryTextBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            var nameTb = new TextBlock
            {
                Text = headerName,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = PrimaryTextBrush,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 105
            };

            sp.Children.Add(letterTb);
            sp.Children.Add(nameTb);
            colBorder.Child = sp;
            panel.Children.Add(colBorder);
        }
    }

    private void ApplyFilter()
    {
        string query = _searchQuery.Trim().ToLowerInvariant();
        IEnumerable<AlignedExcelRow> filtered = _allAlignedRows;

        if (_filterMode == 1) // Diff only
        {
            filtered = filtered.Where(r => r.RowDiffType != RowDiffType.Unchanged);
        }
        else if (_filterMode == 2) // Same only
        {
            filtered = filtered.Where(r => r.RowDiffType == RowDiffType.Unchanged);
        }

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(r =>
            {
                bool lMatch = r.LeftCells?.Any(c => c.RawValue.ToLowerInvariant().Contains(query)) ?? false;
                bool rMatch = r.RightCells?.Any(c => c.RawValue.ToLowerInvariant().Contains(query)) ?? false;
                return lMatch || rMatch;
            });
        }

        _displayAlignedRows = filtered.ToList();

        // Build Left & Right Display Rows
        _leftDisplayRows.Clear();
        _rightDisplayRows.Clear();

        var primaryBrush = PrimaryTextBrush;
        var secondaryBrush = SecondaryTextBrush;
        var cellDelFg = CellDelFg;
        var cellAddFg = CellAddFg;
        var cellDelBg = CellDelBg;
        var cellAddBg = CellAddBg;

        for (int i = 0; i < _displayAlignedRows.Count; i++)
        {
            var row = _displayAlignedRows[i];
            bool isSelected = (i == _selectedRowIndex);

            // Left display row
            var leftItem = new ExcelRowDisplayItem
            {
                RowId = row.Id,
                RowIndex = i,
                DisplayRowNumber = row.DisplayRowNumberLeft,
                RowNumberFgBrush = secondaryBrush,
                RowNumberBgBrush = App.GetThemeBrush("TableRowNumberBgBrush"),
                DiffType = row.RowDiffType,
                DotBrush = GetDotBrush(row.RowDiffType, isPhantom: !row.LeftRowIndex.HasValue),
                RowBgBrush = GetRowBgBrush(row.RowDiffType, isSelected, isPhantom: !row.LeftRowIndex.HasValue),
                IsSelected = isSelected
            };

            if (!row.LeftRowIndex.HasValue) // Phantom row
            {
                foreach (var cellDiff in row.CellDiffs)
                {
                    leftItem.Cells.Add(new ExcelCellDisplayItem
                    {
                        ColumnIndex = cellDiff.ColumnIndex,
                        ColumnLetter = cellDiff.ColumnLetter,
                        Text = string.Empty,
                        DiffType = CellDiffType.Deleted,
                        BgBrush = PhantomRowBg,
                        Width = 140,
                        IsPhantom = true
                    });
                }
            }
            else
            {
                foreach (var cellDiff in row.CellDiffs)
                {
                    bool isDiff = cellDiff.IsDiff;
                    leftItem.Cells.Add(new ExcelCellDisplayItem
                    {
                        ColumnIndex = cellDiff.ColumnIndex,
                        ColumnLetter = cellDiff.ColumnLetter,
                        Text = cellDiff.LeftText,
                        DiffType = cellDiff.DiffType,
                        TextBrush = isDiff ? cellDelFg : primaryBrush,
                        BgBrush = isDiff ? cellDelBg : TransBrush,
                        Width = 140
                    });
                }
            }
            _leftDisplayRows.Add(leftItem);

            // Right display row
            var rightItem = new ExcelRowDisplayItem
            {
                RowId = row.Id,
                RowIndex = i,
                DisplayRowNumber = row.DisplayRowNumberRight,
                RowNumberFgBrush = secondaryBrush,
                RowNumberBgBrush = App.GetThemeBrush("TableRowNumberBgBrush"),
                DiffType = row.RowDiffType,
                DotBrush = GetDotBrush(row.RowDiffType, isPhantom: !row.RightRowIndex.HasValue),
                RowBgBrush = GetRowBgBrush(row.RowDiffType, isSelected, isPhantom: !row.RightRowIndex.HasValue),
                IsSelected = isSelected
            };

            if (!row.RightRowIndex.HasValue) // Phantom row
            {
                foreach (var cellDiff in row.CellDiffs)
                {
                    rightItem.Cells.Add(new ExcelCellDisplayItem
                    {
                        ColumnIndex = cellDiff.ColumnIndex,
                        ColumnLetter = cellDiff.ColumnLetter,
                        Text = string.Empty,
                        DiffType = CellDiffType.Added,
                        BgBrush = PhantomRowBg,
                        Width = 140,
                        IsPhantom = true
                    });
                }
            }
            else
            {
                foreach (var cellDiff in row.CellDiffs)
                {
                    bool isDiff = cellDiff.IsDiff;
                    rightItem.Cells.Add(new ExcelCellDisplayItem
                    {
                        ColumnIndex = cellDiff.ColumnIndex,
                        ColumnLetter = cellDiff.ColumnLetter,
                        Text = cellDiff.RightText,
                        DiffType = cellDiff.DiffType,
                        TextBrush = isDiff ? cellAddFg : primaryBrush,
                        BgBrush = isDiff ? cellAddBg : TransBrush,
                        Width = 140
                    });
                }
            }
            _rightDisplayRows.Add(rightItem);
        }

        if (LeftRowsControl != null) LeftRowsControl.ItemsSource = null;
        if (LeftRowsControl != null) LeftRowsControl.ItemsSource = _leftDisplayRows;

        if (RightRowsControl != null) RightRowsControl.ItemsSource = null;
        if (RightRowsControl != null) RightRowsControl.ItemsSource = _rightDisplayRows;
    }

    private Brush GetDotBrush(RowDiffType type, bool isPhantom)
    {
        if (isPhantom) return App.GetThemeBrush("TextMutedBrush");
        return type switch
        {
            RowDiffType.Modified => ModDotBrush,
            RowDiffType.Added => AddDotBrush,
            RowDiffType.Deleted => DelDotBrush,
            _ => TransBrush
        };
    }

    private Brush GetRowBgBrush(RowDiffType type, bool isSelected, bool isPhantom)
    {
        if (isSelected) return SelectedRowBg;
        if (isPhantom) return PhantomRowBg;
        return type switch
        {
            RowDiffType.Modified => ModRowBg,
            RowDiffType.Added => AddRowBg,
            RowDiffType.Deleted => DelRowBg,
            _ => TransBrush
        };
    }

    private void SelectRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _displayAlignedRows.Count) return;
        _selectedRowIndex = rowIndex;

        var selectedRow = _displayAlignedRows[rowIndex];

        // Update visuals in display rows
        for (int i = 0; i < _leftDisplayRows.Count; i++)
        {
            bool sel = (i == _selectedRowIndex);
            _leftDisplayRows[i].IsSelected = sel;
            _leftDisplayRows[i].RowBgBrush = GetRowBgBrush(_leftDisplayRows[i].DiffType, sel, isPhantom: string.IsNullOrWhiteSpace(_leftDisplayRows[i].DisplayRowNumber));
            _leftDisplayRows[i].RowNumberBgBrush = sel ? SelectedRowBg : App.GetThemeBrush("TableRowNumberBgBrush");

            _rightDisplayRows[i].IsSelected = sel;
            _rightDisplayRows[i].RowBgBrush = GetRowBgBrush(_rightDisplayRows[i].DiffType, sel, isPhantom: string.IsNullOrWhiteSpace(_rightDisplayRows[i].DisplayRowNumber));
            _rightDisplayRows[i].RowNumberBgBrush = sel ? SelectedRowBg : App.GetThemeBrush("TableRowNumberBgBrush");
        }

        // Refresh bindings
        if (LeftRowsControl != null) LeftRowsControl.ItemsSource = null;
        if (LeftRowsControl != null) LeftRowsControl.ItemsSource = _leftDisplayRows;
        if (RightRowsControl != null) RightRowsControl.ItemsSource = null;
        if (RightRowsControl != null) RightRowsControl.ItemsSource = _rightDisplayRows;

        // Update Selection Detail Inspector
        UpdateInspector(selectedRow);
    }

    private void UpdateInspector(AlignedExcelRow row)
    {
        if (InspectorContainer == null) return;
        InspectorContainer.Visibility = Visibility.Visible;

        string leftIdxStr = row.LeftRowIndex.HasValue ? $"Row {row.LeftRowIndex.Value}" : "None";
        string rightIdxStr = row.RightRowIndex.HasValue ? $"Row {row.RightRowIndex.Value}" : "None";

        if (InspectorSubtitleText != null)
        {
            InspectorSubtitleText.Text = $"(Left: {leftIdxStr} \u2194 Right: {rightIdxStr})";
        }

        if (InspectorBadgeBorder != null && InspectorBadgeText != null)
        {
            switch (row.RowDiffType)
            {
                case RowDiffType.Modified:
                    InspectorBadgeBorder.Background = App.GetThemeBrush("DiffModifiedBgBrush");
                    InspectorBadgeText.Foreground = ModDotBrush;
                    InspectorBadgeText.Text = LanguageManager.Instance.Text("statusModified");
                    break;
                case RowDiffType.Added:
                    InspectorBadgeBorder.Background = App.GetThemeBrush("DiffAddedBgBrush");
                    InspectorBadgeText.Foreground = AddDotBrush;
                    InspectorBadgeText.Text = LanguageManager.Instance.Text("statusAdded");
                    break;
                case RowDiffType.Deleted:
                    InspectorBadgeBorder.Background = App.GetThemeBrush("DiffDeletedBgBrush");
                    InspectorBadgeText.Foreground = DelDotBrush;
                    InspectorBadgeText.Text = LanguageManager.Instance.Text("statusDeleted");
                    break;
                default:
                    InspectorBadgeBorder.Background = App.GetThemeBrush("DiffIdenticalBgBrush");
                    InspectorBadgeText.Foreground = SecondaryTextBrush;
                    InspectorBadgeText.Text = LanguageManager.Instance.Text("statusIdentical");
                    break;
            }
        }

        // Build Cards
        var cards = new List<ExcelInspectorCardItem>();
        var primaryBrush = PrimaryTextBrush;
        var mutedBrush = SecondaryTextBrush;
        var cellDelFg = CellDelFg;
        var cellAddFg = CellAddFg;
        var labelBrush = SecondaryTextBrush;
        var headerNameBrush = PrimaryTextBrush;
        var colLetterBrush = TertiaryTextBrush;

        foreach (var cellDiff in row.CellDiffs)
        {
            bool isDiff = cellDiff.IsDiff;
            cards.Add(new ExcelInspectorCardItem
            {
                ColumnIndex = cellDiff.ColumnIndex,
                ColumnLetter = cellDiff.ColumnLetter,
                HeaderName = cellDiff.HeaderName,
                HeaderNameFgBrush = headerNameBrush,
                ColLetterFgBrush = colLetterBrush,
                LabelFgBrush = labelBrush,
                LeftDisplayValue = string.IsNullOrEmpty(cellDiff.LeftText) ? "<\u7a7a>" : cellDiff.LeftText,
                RightDisplayValue = string.IsNullOrEmpty(cellDiff.RightText) ? "<\u7a7a>" : cellDiff.RightText,
                LeftFgBrush = string.IsNullOrEmpty(cellDiff.LeftText) ? mutedBrush : (isDiff ? cellDelFg : primaryBrush),
                RightFgBrush = string.IsNullOrEmpty(cellDiff.RightText) ? mutedBrush : (isDiff ? cellAddFg : primaryBrush),
                BorderBrush = isDiff ? ModBorderBrush : NormalBorderBrush,
                DiffDotVisibility = isDiff ? Visibility.Visible : Visibility.Collapsed,
                IsDiff = isDiff
            });
        }

        if (InspectorCardsControl != null)
        {
            InspectorCardsControl.ItemsSource = cards;
        }
    }

    private void OnRowPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Tag is ExcelRowDisplayItem item)
        {
            SelectRow(item.RowIndex);
        }
    }

    // Synchronized Dual-Pane Scrolling
    private void OnLeftScrollViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_isSyncingScroll || LeftTableScrollViewer == null) return;
        try
        {
            _isSyncingScroll = true;
            LeftHeaderScrollViewer?.ChangeView(LeftTableScrollViewer.HorizontalOffset, null, null, true);
            RightTableScrollViewer?.ChangeView(LeftTableScrollViewer.HorizontalOffset, LeftTableScrollViewer.VerticalOffset, null, true);
            RightHeaderScrollViewer?.ChangeView(LeftTableScrollViewer.HorizontalOffset, null, null, true);
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }

    private void OnRightScrollViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_isSyncingScroll || RightTableScrollViewer == null) return;
        try
        {
            _isSyncingScroll = true;
            RightHeaderScrollViewer?.ChangeView(RightTableScrollViewer.HorizontalOffset, null, null, true);
            LeftTableScrollViewer?.ChangeView(RightTableScrollViewer.HorizontalOffset, RightTableScrollViewer.VerticalOffset, null, true);
            LeftHeaderScrollViewer?.ChangeView(RightTableScrollViewer.HorizontalOffset, null, null, true);
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }

    // Filter Buttons
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

    private void SetFilterButtonsVisual(int mode)
    {
        var activeBg = App.GetThemeBrush("BrandPrimaryBrush");
        var inactiveBg = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        var activeFg = App.GetThemeBrush("TextOnAccentBrush");
        var inactiveFg = GetInactiveFilterForeground();

        if (FilterAllBtn != null)
        {
            FilterAllBtn.Background = (mode == 0) ? activeBg : inactiveBg;
            FilterAllBtn.Foreground = (mode == 0) ? activeFg : inactiveFg;
            FilterAllBtn.FontWeight = (mode == 0) ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }
        if (FilterDiffBtn != null)
        {
            FilterDiffBtn.Background = (mode == 1) ? activeBg : inactiveBg;
            FilterDiffBtn.Foreground = (mode == 1) ? activeFg : inactiveFg;
            FilterDiffBtn.FontWeight = (mode == 1) ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }
        if (FilterSameBtn != null)
        {
            FilterSameBtn.Background = (mode == 2) ? activeBg : inactiveBg;
            FilterSameBtn.Foreground = (mode == 2) ? activeFg : inactiveFg;
            FilterSameBtn.FontWeight = (mode == 2) ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }
    }

    private Brush GetInactiveFilterForeground()
    {
        return App.GetThemeBrush("TextFillColorSecondaryBrush");
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _searchQuery = sender.Text ?? string.Empty;
        ApplyFilter();
    }

    private void OnPrevDiffClicked(object sender, RoutedEventArgs e)
    {
        if (_displayAlignedRows.Count == 0) return;

        int startIndex = _currentDiffJumpIndex <= 0 ? _displayAlignedRows.Count - 1 : _currentDiffJumpIndex - 1;
        for (int i = startIndex; i >= 0; i--)
        {
            if (_displayAlignedRows[i].RowDiffType != RowDiffType.Unchanged)
            {
                JumpToDiff(i);
                return;
            }
        }
        for (int i = _displayAlignedRows.Count - 1; i > startIndex; i--)
        {
            if (_displayAlignedRows[i].RowDiffType != RowDiffType.Unchanged)
            {
                JumpToDiff(i);
                return;
            }
        }
    }

    private void OnNextDiffClicked(object sender, RoutedEventArgs e)
    {
        if (_displayAlignedRows.Count == 0) return;

        int startIndex = _currentDiffJumpIndex >= _displayAlignedRows.Count - 1 ? 0 : _currentDiffJumpIndex + 1;
        for (int i = startIndex; i < _displayAlignedRows.Count; i++)
        {
            if (_displayAlignedRows[i].RowDiffType != RowDiffType.Unchanged)
            {
                JumpToDiff(i);
                return;
            }
        }
        for (int i = 0; i < startIndex; i++)
        {
            if (_displayAlignedRows[i].RowDiffType != RowDiffType.Unchanged)
            {
                JumpToDiff(i);
                return;
            }
        }
    }

    private void JumpToDiff(int index)
    {
        _currentDiffJumpIndex = index;
        SelectRow(index);

        double targetY = Math.Max(0, (index - 2) * 26);
        LeftTableScrollViewer?.ChangeView(null, targetY, null, false);
        RightTableScrollViewer?.ChangeView(null, targetY, null, false);
    }

    private void OnSwapSidesClicked(object sender, RoutedEventArgs e)
    {
        var temp = _leftPath;
        _leftPath = _rightPath;
        _rightPath = temp;

        if (LeftExcelHeader != null)
            LeftExcelHeader.Text = !string.IsNullOrEmpty(_leftPath) ? Path.GetFileName(_leftPath) : LanguageManager.Instance.Text("sourceFile");
        if (RightExcelHeader != null)
            RightExcelHeader.Text = !string.IsNullOrEmpty(_rightPath) ? Path.GetFileName(_rightPath) : LanguageManager.Instance.Text("targetFile");

        UpdateWorkspaceVisibility();
        UpdateTabHeader();

        if (!string.IsNullOrEmpty(_leftPath) && !string.IsNullOrEmpty(_rightPath))
        {
            InitSessionAndCompare();
        }
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_leftPath) && !string.IsNullOrEmpty(_rightPath))
        {
            InitSessionAndCompare();
        }
        else
        {
            UpdateWorkspaceVisibility();
        }
    }

    private void OnTakeLeftClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_leftPath) || string.IsNullOrEmpty(_rightPath)) return;
        try
        {
            File.Copy(_leftPath, _rightPath, true);
            InitSessionAndCompare();
        }
        catch { }
    }

    private void OnTakeRightClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_leftPath) || string.IsNullOrEmpty(_rightPath)) return;
        try
        {
            File.Copy(_rightPath, _leftPath, true);
            InitSessionAndCompare();
        }
        catch { }
    }

    private void OnClearAllClicked(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;

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
        _leftPath = null;
        _rightPath = null;
        _leftWorkbook = null;
        _rightWorkbook = null;
        _singleWorkbook = null;
        _diffResult = null;
        _allAlignedRows.Clear();
        _displayAlignedRows.Clear();
        _leftDisplayRows.Clear();
        _rightDisplayRows.Clear();
        _searchQuery = string.Empty;
        if (ExcelSearchBox != null) ExcelSearchBox.Text = string.Empty;
        _filterMode = 0;
        SetFilterButtonsVisual(0);

        if (LeftTablePane != null) LeftTablePane.Visibility = Visibility.Collapsed;
        if (RightTablePane != null) RightTablePane.Visibility = Visibility.Collapsed;
        if (LeftRowsControl != null) LeftRowsControl.ItemsSource = null;
        if (RightRowsControl != null) RightRowsControl.ItemsSource = null;
        if (LeftHeaderPanel != null) LeftHeaderPanel.Children.Clear();
        if (RightHeaderPanel != null) RightHeaderPanel.Children.Clear();
        if (SheetTabsPanel != null) SheetTabsPanel.Children.Clear();
        if (SheetComboBox != null) SheetComboBox.Items.Clear();
        if (InspectorCardsControl != null) InspectorCardsControl.ItemsSource = null;
        if (InspectorContainer != null) InspectorContainer.Visibility = Visibility.Collapsed;

        if (LeftExcelHeader != null) LeftExcelHeader.Text = LanguageManager.Instance.Text("sourceFile");
        if (RightExcelHeader != null) RightExcelHeader.Text = LanguageManager.Instance.Text("targetFile");

        if (ExcelStatsPanel != null) ExcelStatsPanel.Visibility = Visibility.Collapsed;
        UpdateWorkspaceVisibility();
        UpdateTabHeader();
    }

    private void OnExportDiffClicked(object sender, RoutedEventArgs e)
    {
        if (_allAlignedRows.Count == 0) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Format(LanguageManager.Instance.Text("excelDiffReportTitle"), Path.GetFileName(_leftPath), Path.GetFileName(_rightPath)));
        sb.AppendLine(string.Format(LanguageManager.Instance.Text("exportTimeLabel"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        sb.AppendLine();
        sb.AppendLine("| Row | Status | Left | Right |");
        sb.AppendLine("|---|---|---|---|");

        foreach (var r in _allAlignedRows.Where(r => r.RowDiffType != RowDiffType.Unchanged))
        {
            string lCells = string.Join(", ", r.CellDiffs.Select(c => c.LeftText));
            string rCells = string.Join(", ", r.CellDiffs.Select(c => c.RightText));
            sb.AppendLine($"| {r.DisplayRowNumberLeft} | {r.RowDiffType} | {lCells} | {rCells} |");
        }

        var dp = new DataPackage();
        dp.SetText(sb.ToString());
        Clipboard.SetContent(dp);
    }

    private void OnSheetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        if (SheetComboBox?.SelectedIndex is int idx && idx >= 0)
        {
            if (_diffResult != null && idx != _currentSheetIndex)
            {
                _currentSheetIndex = idx;
                RenderSheetTabs();
                LoadSheet(_currentSheetIndex);
            }
            else if (_singleWorkbook != null && idx != _singleSheetIndex)
            {
                _singleSheetIndex = idx;
                RenderSingleWorkbookSheetTabs(_singleWorkbook, _isSingleLeft);
                LoadSingleSheet(_singleSheetIndex, _isSingleLeft);
            }
        }
    }

    private void OnDiffOptionsChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitialized && !string.IsNullOrEmpty(_leftPath) && !string.IsNullOrEmpty(_rightPath))
        {
            InitSessionAndCompare();
        }
    }

    private void OnWhitespaceContainerClicked(object sender, PointerRoutedEventArgs e)
    {
        if (WhitespaceSwitch != null)
        {
            WhitespaceSwitch.IsOn = !WhitespaceSwitch.IsOn;
        }
    }

    private void OnCaseContainerClicked(object sender, PointerRoutedEventArgs e)
    {
        if (CaseSwitch != null)
        {
            CaseSwitch.IsOn = !CaseSwitch.IsOn;
        }
    }

    private async void OnPickLeftExcelClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { ".xlsx", ".xls", ".csv" });
        if (file != null)
        {
            if (!CheckFileValidation(file.Path)) return;
            _leftPath = file.Path;
            if (LeftExcelHeader != null) LeftExcelHeader.Text = file.Name;
            UpdateWorkspaceVisibility();
            UpdateTabHeader();
            if (!string.IsNullOrEmpty(_rightPath))
            {
                InitSessionAndCompare();
            }
        }
    }

    private async void OnPickRightExcelClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { ".xlsx", ".xls", ".csv" });
        if (file != null)
        {
            if (!CheckFileValidation(file.Path)) return;
            _rightPath = file.Path;
            if (RightExcelHeader != null) RightExcelHeader.Text = file.Name;
            UpdateWorkspaceVisibility();
            UpdateTabHeader();
            if (!string.IsNullOrEmpty(_leftPath))
            {
                InitSessionAndCompare();
            }
        }
    }

    private bool CheckFileValidation(string path)
    {
        var check = FileFormatValidator.Validate(path, CompareType.ExcelDiff);
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

    public void HandleDualDrop(string leftPath, string rightPath)
    {
        if (string.IsNullOrEmpty(leftPath) || string.IsNullOrEmpty(rightPath)) return;
        if (!CheckFileValidation(leftPath) || !CheckFileValidation(rightPath)) return;
        _leftPath = leftPath;
        _rightPath = rightPath;
        if (LeftExcelHeader != null) LeftExcelHeader.Text = Path.GetFileName(leftPath);
        if (RightExcelHeader != null) RightExcelHeader.Text = Path.GetFileName(rightPath);
        UpdateWorkspaceVisibility();
        UpdateTabHeader();
        InitSessionAndCompare();
    }

    private void OnLeftExcelDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = LanguageManager.Instance.Text("dropSourceFile");
        }
    }

    private async void OnLeftExcelDrop(object sender, DragEventArgs e)
    {
        try
        {
            App.Log("[ExcelDiffPage] OnLeftExcelDrop triggered");
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                App.Log($"[ExcelDiffPage] OnLeftExcelDrop got {items?.Count ?? 0} items");
                if (items != null && items.Count > 0)
                {
                    e.Handled = true;
                    if (items.Count >= 2)
                    {
                        HandleDualDrop(items[0].Path, items[1].Path);
                        return;
                    }

                    string targetPath = items[0].Path;
                    App.Log($"[ExcelDiffPage] OnLeftExcelDrop targetPath: {targetPath}");
                    HandleDropFromWindow(targetPath, isLeft: true);
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"[ExcelDiffPage] OnLeftExcelDrop error: {ex.Message}");
        }
    }

    private void OnRightExcelDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = LanguageManager.Instance.Text("dropTargetFile");
        }
    }

    private async void OnRightExcelDrop(object sender, DragEventArgs e)
    {
        try
        {
            App.Log("[ExcelDiffPage] OnRightExcelDrop triggered");
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                App.Log($"[ExcelDiffPage] OnRightExcelDrop got {items?.Count ?? 0} items");
                if (items != null && items.Count > 0)
                {
                    e.Handled = true;
                    if (items.Count >= 2)
                    {
                        HandleDualDrop(items[0].Path, items[1].Path);
                        return;
                    }

                    string targetPath = items[0].Path;
                    App.Log($"[ExcelDiffPage] OnRightExcelDrop targetPath: {targetPath}");
                    HandleDropFromWindow(targetPath, isLeft: false);
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"[ExcelDiffPage] OnRightExcelDrop error: {ex.Message}");
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
                e.DragUIOverride.Caption = LanguageManager.Instance.Text("dropSourceFile");
            }
            else
            {
                e.DragUIOverride.Caption = LanguageManager.Instance.Text("dropTargetFile");
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
                else if (items != null && items.Count == 1)
                {
                    var pos = e.GetPosition(this);
                    double totalWidth = this.ActualWidth > 0 ? this.ActualWidth : 900;
                    bool isLeft = pos.X < (totalWidth / 2);
                    HandleDropFromWindow(items[0].Path, isLeft);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"[ExcelDiffPage] OnDiffContainerDrop error: {ex.Message}");
        }
    }

    public void HandleDropFromWindow(string path, bool isLeft)
    {
        if (string.IsNullOrEmpty(path)) return;
        App.Log($"[ExcelDiffPage] HandleDropFromWindow: isLeft={isLeft}, path={path}");
        if (!CheckFileValidation(path)) return;
        if (isLeft)
        {
            _leftPath = path;
            if (LeftExcelHeader != null) LeftExcelHeader.Text = Path.GetFileName(path);
            UpdateWorkspaceVisibility();
            UpdateTabHeader();
            if (!string.IsNullOrEmpty(_rightPath))
            {
                InitSessionAndCompare();
            }
        }
        else
        {
            _rightPath = path;
            if (RightExcelHeader != null) RightExcelHeader.Text = Path.GetFileName(path);
            UpdateWorkspaceVisibility();
            UpdateTabHeader();
            if (!string.IsNullOrEmpty(_leftPath))
            {
                InitSessionAndCompare();
            }
        }
    }

    private void UpdateTabHeader()
    {
        try
        {
            string? header = null;
            if (!string.IsNullOrEmpty(_leftPath) && !string.IsNullOrEmpty(_rightPath))
            {
                string leftName = Path.GetFileName(_leftPath);
                string rightName = Path.GetFileName(_rightPath);
                header = leftName + " \u2194 " + rightName;
            }
            else if (!string.IsNullOrEmpty(_leftPath))
            {
                header = Path.GetFileName(_leftPath);
            }
            else if (!string.IsNullOrEmpty(_rightPath))
            {
                header = Path.GetFileName(_rightPath);
            }

            var win = App.GetWindowForElement(this) as MainWindow ?? App.ActiveWindow as MainWindow ?? App.MainWindowInstance;
            win?.UpdateTabHeaderForPage(this, header);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("UpdateTabHeader error: " + ex.Message);
        }
    }

    public void Dispose()
    {
        LanguageManager.Instance.LanguageChanged -= UpdateLocalization;
    }
}
