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

public sealed partial class TextDiffPage : Page, IDisposable
{
    private string? _leftPath;
    private string? _rightPath;
    private string _leftText = string.Empty;
    private string _rightText = string.Empty;
    private List<DiffLineDisplayItem> _displayItems = new();
    private int _currentDiffJumpIndex = -1;
    private bool _isDiffViewMode = true;
    private System.Text.Encoding _currentEncoding = System.Text.Encoding.UTF8;
    private AISummaryResult? _lastSummaryResult;
    private bool _isAiGenerating = false;
    private bool _isLoadingFiles = false;
    private CancellationTokenSource? _diffCts;

    private static Brush AddBg => App.GetThemeBrush("DiffAddedBgBrush");
    private static Brush AddFg => App.GetThemeBrush("DiffAddedFgBrush");
    private static Brush DelBg => App.GetThemeBrush("DiffDeletedBgBrush");
    private static Brush DelFg => App.GetThemeBrush("DiffDeletedFgBrush");
    private static Brush ModBg => App.GetThemeBrush("DiffModifiedBgBrush");
    private static Brush ModFg => App.GetThemeBrush("DiffModifiedFgBrush");
    private static Brush PhantomBg => App.GetThemeBrush("DiffPhantomBgBrush");
    private static Brush DimFg => App.GetThemeBrush("TextMutedBrush");

    private bool _isInitialized = false;

    public TextDiffPage()
    {
        this.InitializeComponent();
        
        // 隐藏 AI 摘要按钮
        if (AISummaryButton != null)
        {
            AISummaryButton.Visibility = Visibility.Collapsed;
        }

        UpdateLocalization();
        LanguageManager.Instance.LanguageChanged += UpdateLocalization;

        if (WhitespaceSwitch != null)
            WhitespaceSwitch.IsOn = SettingsManager.Instance.DefaultIgnoreWhitespace;
        if (CaseSwitch != null)
            CaseSwitch.IsOn = SettingsManager.Instance.DefaultIgnoreCase;
        if (EncodingComboBox != null)
            EncodingComboBox.SelectedIndex = 0;

        if (LeftEditor != null) LeftEditor.Text = _leftText;
        if (RightEditor != null) RightEditor.Text = _rightText;

        _isInitialized = true;
        UpdateWorkspaceVisibility();
        this.ActualThemeChanged += (s, e) =>
        {
            if (_displayItems.Count > 0)
            {
                RecomputeDiff();
            }
        };
    }

    private void UpdateLocalization()
    {
        if (PrevDiffText != null) PrevDiffText.Text = LanguageManager.Instance.Text("prevDiff");
        if (NextDiffText != null) NextDiffText.Text = LanguageManager.Instance.Text("nextDiff");
        if (WhitespaceLabel != null) WhitespaceLabel.Text = LanguageManager.Instance.Text("ignoreWhitespace");
        if (CaseLabel != null) CaseLabel.Text = LanguageManager.Instance.Text("ignoreCase");
        if (TakeLeftText != null) TakeLeftText.Text = LanguageManager.Instance.Text("takeLeft");
        if (TakeRightText != null) TakeRightText.Text = LanguageManager.Instance.Text("takeRight");
        if (AIBtnText != null) AIBtnText.Text = LanguageManager.Instance.Text("aiSummary");
        if (ClearAllText != null) ClearAllText.Text = LanguageManager.Instance.Text("clearAll");

        if (LeftHeader != null && string.IsNullOrEmpty(_leftPath)) LeftHeader.Text = LanguageManager.Instance.Text("sourceFile");
        if (RightHeader != null && string.IsNullOrEmpty(_rightPath)) RightHeader.Text = LanguageManager.Instance.Text("targetFile");
        if (LeftBrowseBtn != null) LeftBrowseBtn.Content = LanguageManager.Instance.Text("selectEllipsis");
        if (RightBrowseBtn != null) RightBrowseBtn.Content = LanguageManager.Instance.Text("selectEllipsis");
        if (LeftSaveBtn != null) LeftSaveBtn.Content = LanguageManager.Instance.Text("save");
        if (RightSaveBtn != null) RightSaveBtn.Content = LanguageManager.Instance.Text("save");

        if (LeftEmptyTitle != null) LeftEmptyTitle.Text = LanguageManager.Instance.Text("noFileSelected");
        if (RightEmptyTitle != null) RightEmptyTitle.Text = LanguageManager.Instance.Text("noFileSelected");
        if (LeftEmptyDesc != null) LeftEmptyDesc.Text = LanguageManager.Instance.Text("dropOrPickFile");
        if (RightEmptyDesc != null) RightEmptyDesc.Text = LanguageManager.Instance.Text("dropOrPickFile");
        if (LeftPickBtn != null) LeftPickBtn.Content = LanguageManager.Instance.Text("pickSourceFile");
        if (RightPickBtn != null) RightPickBtn.Content = LanguageManager.Instance.Text("pickTargetFile");

        if (AICardTitle != null) AICardTitle.Text = LanguageManager.Instance.Text("aiSummaryTitle");
        if (AICopyCommitBtnText != null) AICopyCommitBtnText.Text = LanguageManager.Instance.Text("copyCommitMsg");
        if (AICopyTextBtnText != null) AICopyTextBtnText.Text = LanguageManager.Instance.Text("copyText");
        if (AIReanalyzeText != null) AIReanalyzeText.Text = LanguageManager.Instance.Text("reanalyze");
        if (AICommitPrompt != null) AICommitPrompt.Text = LanguageManager.Instance.Text("recommendedCommit");

        if (LeftLoadingText != null) LeftLoadingText.Text = LanguageManager.Instance.Text("loadingSourceFile");
        if (RightLoadingText != null) RightLoadingText.Text = LanguageManager.Instance.Text("loadingTargetFile");
        if (DualLeftLoadingText != null) DualLeftLoadingText.Text = LanguageManager.Instance.Text("loadingSourceFile");
        if (DualRightLoadingText != null) DualRightLoadingText.Text = LanguageManager.Instance.Text("loadingTargetFile");

        if (WhitespaceContainer != null) ToolTipService.SetToolTip(WhitespaceContainer, LanguageManager.Instance.Text("ignoreWhitespaceTooltip"));
        if (CaseContainer != null) ToolTipService.SetToolTip(CaseContainer, LanguageManager.Instance.Text("ignoreCaseTooltip"));
        if (TakeLeftBtn != null) ToolTipService.SetToolTip(TakeLeftBtn, LanguageManager.Instance.Text("takeLeftTooltip"));
        if (TakeRightBtn != null) ToolTipService.SetToolTip(TakeRightBtn, LanguageManager.Instance.Text("takeRightTooltip"));
        if (AISummaryButton != null) ToolTipService.SetToolTip(AISummaryButton, LanguageManager.Instance.Text("aiSummaryTooltip"));
        if (ClearAllBtn != null) ToolTipService.SetToolTip(ClearAllBtn, LanguageManager.Instance.Text("clearAllTooltip"));
    }

    private void UpdateSingleFileView(bool isLeft)
    {
        string text = isLeft ? (_leftText ?? string.Empty) : (_rightText ?? string.Empty);
        var listView = isLeft ? LeftSingleListView : RightSingleListView;
        if (listView == null) return;

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var items = new List<SingleTextLineDisplayItem>(lines.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            items.Add(new SingleTextLineDisplayItem
            {
                LineNumber = i + 1,
                LineNumText = (i + 1).ToString(),
                Content = lines[i]
            });
        }
        listView.ItemsSource = items;
    }

    private void UpdateWorkspaceVisibility()
    {
        if (!_isInitialized) return;

        bool hasLeft = !string.IsNullOrEmpty(_leftPath) || !string.IsNullOrEmpty(_leftText);
        bool hasRight = !string.IsNullOrEmpty(_rightPath) || !string.IsNullOrEmpty(_rightText);
        bool hasBoth = hasLeft && hasRight;

        if (!hasLeft && !hasRight)
        {
            if (LeftDropZone != null) LeftDropZone.Visibility = Visibility.Visible;
            if (RightDropZone != null) RightDropZone.Visibility = Visibility.Visible;
            if (LeftEditor != null) LeftEditor.Visibility = Visibility.Collapsed;
            if (RightEditor != null) RightEditor.Visibility = Visibility.Collapsed;
            if (LeftSingleListView != null) LeftSingleListView.Visibility = Visibility.Collapsed;
            if (RightSingleListView != null) RightSingleListView.Visibility = Visibility.Collapsed;
            if (DiffListContainer != null) DiffListContainer.Visibility = Visibility.Collapsed;

            if (PrevDiffBtn != null) PrevDiffBtn.IsEnabled = false;
            if (NextDiffBtn != null) NextDiffBtn.IsEnabled = false;
            if (TakeLeftBtn != null) TakeLeftBtn.IsEnabled = false;
            if (TakeRightBtn != null) TakeRightBtn.IsEnabled = false;
            if (AISummaryButton != null) AISummaryButton.IsEnabled = false;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = false;
            if (LeftSaveBtn != null) LeftSaveBtn.IsEnabled = false;
            if (RightSaveBtn != null) RightSaveBtn.IsEnabled = false;
            if (StatusStatsPanel != null) StatusStatsPanel.Visibility = Visibility.Collapsed;
            if (StatsText != null)
            {
                StatsText.Visibility = Visibility.Visible;
                StatsText.Text = $"0 {LanguageManager.Instance.Text("itemsCount")}";
            }
            if (StatusMessageText != null) StatusMessageText.Text = string.Empty;
        }
        else if (hasLeft && !hasRight)
        {
            if (LeftDropZone != null) LeftDropZone.Visibility = Visibility.Collapsed;
            if (LeftEditor != null) LeftEditor.Visibility = Visibility.Collapsed;
            if (LeftSingleListView != null) LeftSingleListView.Visibility = Visibility.Visible;
            if (RightDropZone != null) RightDropZone.Visibility = Visibility.Visible;
            if (RightEditor != null) RightEditor.Visibility = Visibility.Collapsed;
            if (RightSingleListView != null) RightSingleListView.Visibility = Visibility.Collapsed;
            if (DiffListContainer != null) DiffListContainer.Visibility = Visibility.Collapsed;

            UpdateSingleFileView(true);

            if (PrevDiffBtn != null) PrevDiffBtn.IsEnabled = false;
            if (NextDiffBtn != null) NextDiffBtn.IsEnabled = false;
            if (TakeLeftBtn != null) TakeLeftBtn.IsEnabled = false;
            if (TakeRightBtn != null) TakeRightBtn.IsEnabled = false;
            if (AISummaryButton != null) AISummaryButton.IsEnabled = false;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = true;
            if (LeftSaveBtn != null) LeftSaveBtn.IsEnabled = true;
            if (RightSaveBtn != null) RightSaveBtn.IsEnabled = false;
            if (StatusStatsPanel != null) StatusStatsPanel.Visibility = Visibility.Collapsed;
            if (StatsText != null)
            {
                StatsText.Visibility = Visibility.Visible;
                int lineCount = !string.IsNullOrEmpty(_leftText) ? _leftText.Split('\n').Length : 0;
                StatsText.Text = $"{lineCount} {LanguageManager.Instance.Text("itemsCount")}";
            }
            if (StatusMessageText != null) StatusMessageText.Text = LanguageManager.Instance.Text("loadedSourceFilePleaseSelectTarget");
        }
        else if (!hasLeft && hasRight)
        {
            if (LeftDropZone != null) LeftDropZone.Visibility = Visibility.Visible;
            if (LeftEditor != null) LeftEditor.Visibility = Visibility.Collapsed;
            if (LeftSingleListView != null) LeftSingleListView.Visibility = Visibility.Collapsed;
            if (RightDropZone != null) RightDropZone.Visibility = Visibility.Collapsed;
            if (RightEditor != null) RightEditor.Visibility = Visibility.Collapsed;
            if (RightSingleListView != null) RightSingleListView.Visibility = Visibility.Visible;
            if (DiffListContainer != null) DiffListContainer.Visibility = Visibility.Collapsed;

            UpdateSingleFileView(false);

            if (PrevDiffBtn != null) PrevDiffBtn.IsEnabled = false;
            if (NextDiffBtn != null) NextDiffBtn.IsEnabled = false;
            if (TakeLeftBtn != null) TakeLeftBtn.IsEnabled = false;
            if (TakeRightBtn != null) TakeRightBtn.IsEnabled = false;
            if (AISummaryButton != null) AISummaryButton.IsEnabled = false;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = true;
            if (LeftSaveBtn != null) LeftSaveBtn.IsEnabled = false;
            if (RightSaveBtn != null) RightSaveBtn.IsEnabled = true;
            if (StatusStatsPanel != null) StatusStatsPanel.Visibility = Visibility.Collapsed;
            if (StatsText != null)
            {
                StatsText.Visibility = Visibility.Visible;
                int lineCount = !string.IsNullOrEmpty(_rightText) ? _rightText.Split('\n').Length : 0;
                StatsText.Text = $"{lineCount} {LanguageManager.Instance.Text("itemsCount")}";
            }
            if (StatusMessageText != null) StatusMessageText.Text = LanguageManager.Instance.Text("loadedTargetFilePleaseSelectSource");
        }
        else
        {
            if (LeftDropZone != null) LeftDropZone.Visibility = Visibility.Collapsed;
            if (RightDropZone != null) RightDropZone.Visibility = Visibility.Collapsed;
            if (LeftSingleListView != null) LeftSingleListView.Visibility = Visibility.Collapsed;
            if (RightSingleListView != null) RightSingleListView.Visibility = Visibility.Collapsed;

            if (_isDiffViewMode)
            {
                if (DiffListContainer != null) DiffListContainer.Visibility = Visibility.Visible;
                if (LeftEditor != null) LeftEditor.Visibility = Visibility.Collapsed;
                if (RightEditor != null) RightEditor.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (DiffListContainer != null) DiffListContainer.Visibility = Visibility.Collapsed;
                if (LeftEditor != null) LeftEditor.Visibility = Visibility.Visible;
                if (RightEditor != null) RightEditor.Visibility = Visibility.Visible;
            }

            if (PrevDiffBtn != null) PrevDiffBtn.IsEnabled = true;
            if (NextDiffBtn != null) NextDiffBtn.IsEnabled = true;
            if (TakeLeftBtn != null) TakeLeftBtn.IsEnabled = true;
            if (TakeRightBtn != null) TakeRightBtn.IsEnabled = true;
            if (AISummaryButton != null) AISummaryButton.IsEnabled = true;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = true;
            if (LeftSaveBtn != null) LeftSaveBtn.IsEnabled = true;
            if (RightSaveBtn != null) RightSaveBtn.IsEnabled = true;
        }
    }

    private void OnTakeLeftClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_leftText) && string.IsNullOrEmpty(LeftEditor?.Text)) return;

        _isLoadingFiles = true;
        try
        {
            _rightText = _isDiffViewMode ? _leftText : (LeftEditor?.Text ?? _leftText);
            if (RightEditor != null) RightEditor.Text = _rightText;
        }
        finally
        {
            _isLoadingFiles = false;
        }
        RecomputeDiff();
        if (StatusMessageText != null) StatusMessageText.Text = LanguageManager.Instance.Text("acceptedLeftToRight");
    }

    private void OnTakeRightClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_rightText) && string.IsNullOrEmpty(RightEditor?.Text)) return;

        _isLoadingFiles = true;
        try
        {
            _leftText = _isDiffViewMode ? _rightText : (RightEditor?.Text ?? _rightText);
            if (LeftEditor != null) LeftEditor.Text = _leftText;
        }
        finally
        {
            _isLoadingFiles = false;
        }
        RecomputeDiff();
        if (StatusMessageText != null) StatusMessageText.Text = LanguageManager.Instance.Text("acceptedRightToLeft");
    }

    private async void OnEncodingChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;

        if (EncodingComboBox?.SelectedItem is ComboBoxItem item && item.Content is string encName)
        {
            _currentEncoding = GetEncodingByName(encName);
            if (EncodingStatusText != null) EncodingStatusText.Text = encName;

            bool hasLeft = !string.IsNullOrEmpty(_leftPath) && File.Exists(_leftPath);
            bool hasRight = !string.IsNullOrEmpty(_rightPath) && File.Exists(_rightPath);

            if (hasLeft && hasRight)
            {
                await LoadFilesAsync(_leftPath, _rightPath);
            }
            else if (hasLeft)
            {
                if (LeftLoadingOverlay != null) LeftLoadingOverlay.Visibility = Visibility.Visible;
                try
                {
                    _leftText = await Task.Run(() => File.ReadAllText(_leftPath!, _currentEncoding));
                    if (LeftEditor != null) LeftEditor.Text = _leftText;
                    await RecomputeDiffAsync(false);
                }
                finally
                {
                    if (LeftLoadingOverlay != null) LeftLoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
            else if (hasRight)
            {
                if (RightLoadingOverlay != null) RightLoadingOverlay.Visibility = Visibility.Visible;
                try
                {
                    _rightText = await Task.Run(() => File.ReadAllText(_rightPath!, _currentEncoding));
                    if (RightEditor != null) RightEditor.Text = _rightText;
                    await RecomputeDiffAsync(false);
                }
                finally
                {
                    if (RightLoadingOverlay != null) RightLoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }
    }

    private static System.Text.Encoding GetEncodingByName(string name) => name switch
    {
        "GBK" => System.Text.Encoding.GetEncoding("GBK"),
        "UTF-16 LE" => System.Text.Encoding.Unicode,
        "UTF-16 BE" => System.Text.Encoding.BigEndianUnicode,
        "ASCII" => System.Text.Encoding.ASCII,
        _ => System.Text.Encoding.UTF8
    };

    private void OnClearAllClicked(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;

        if (string.IsNullOrEmpty(_leftPath) && string.IsNullOrEmpty(_rightPath) &&
            string.IsNullOrEmpty(_leftText) && string.IsNullOrEmpty(_rightText) &&
            _displayItems.Count == 0)
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
        _leftPath = null;
        _rightPath = null;
        if (LeftHeader != null) LeftHeader.Text = LanguageManager.Instance.Text("sourceFile");
        if (RightHeader != null) RightHeader.Text = LanguageManager.Instance.Text("targetFile");
        if (LeftEditor != null) LeftEditor.Text = string.Empty;
        if (RightEditor != null) RightEditor.Text = string.Empty;
        _leftText = string.Empty;
        _rightText = string.Empty;
        _displayItems.Clear();
        if (DiffListView != null) DiffListView.ItemsSource = null;
        if (LeftSingleListView != null) LeftSingleListView.ItemsSource = null;
        if (RightSingleListView != null) RightSingleListView.ItemsSource = null;
        _currentDiffJumpIndex = -1;
        if (StatusMessageText != null) StatusMessageText.Text = string.Empty;
        if (CursorInfoText != null) CursorInfoText.Text = "Ln 1, Col 1";
        if (AICard != null) AICard.Visibility = Visibility.Collapsed;
        _lastSummaryResult = null;
        _isAiGenerating = false;
        UpdateWorkspaceVisibility();
        UpdateTabHeader();
    }

    public void LoadFiles(string? leftPath, string? rightPath)
    {
        _ = LoadFilesAsync(leftPath, rightPath);
    }

    public async Task LoadFilesAsync(string? leftPath, string? rightPath)
    {
        bool hasLeft = !string.IsNullOrEmpty(leftPath) && File.Exists(leftPath);
        bool hasRight = !string.IsNullOrEmpty(rightPath) && File.Exists(rightPath);

        _isLoadingFiles = true;
        try
        {
            if (hasLeft && hasRight)
            {
                if (DualCompareLoadingOverlay != null) DualCompareLoadingOverlay.Visibility = Visibility.Visible;
                try
                {
                    _leftPath = leftPath;
                    _rightPath = rightPath;
                    if (LeftHeader != null) LeftHeader.Text = Path.GetFileName(leftPath);
                    if (RightHeader != null) RightHeader.Text = Path.GetFileName(rightPath);

                    var (leftT, rightT) = await Task.Run(() =>
                    {
                        string l = File.ReadAllText(leftPath!, _currentEncoding);
                        string r = File.ReadAllText(rightPath!, _currentEncoding);
                        return (l, r);
                    });

                    _leftText = leftT;
                    _rightText = rightT;
                    if (LeftEditor != null) LeftEditor.Text = _leftText;
                    if (RightEditor != null) RightEditor.Text = _rightText;

                    if (!string.IsNullOrEmpty(_leftPath) && !string.IsNullOrEmpty(_rightPath))
                    {
                        RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.TextDiff);
                    }
                    UpdateWorkspaceVisibility();
                    await RecomputeDiffAsync(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"LoadFilesAsync dual error: {ex.Message}");
                }
                finally
                {
                    if (DualCompareLoadingOverlay != null) DualCompareLoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
            else if (hasLeft)
            {
                if (LeftLoadingOverlay != null) LeftLoadingOverlay.Visibility = Visibility.Visible;
                try
                {
                    _leftPath = leftPath;
                    if (LeftHeader != null) LeftHeader.Text = Path.GetFileName(leftPath);
                    _leftText = await Task.Run(() => File.ReadAllText(leftPath!, _currentEncoding));
                    if (LeftEditor != null) LeftEditor.Text = _leftText;
                    UpdateWorkspaceVisibility();
                    await RecomputeDiffAsync(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"LoadFilesAsync left error: {ex.Message}");
                }
                finally
                {
                    if (LeftLoadingOverlay != null) LeftLoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
            else if (hasRight)
            {
                if (RightLoadingOverlay != null) RightLoadingOverlay.Visibility = Visibility.Visible;
                try
                {
                    _rightPath = rightPath;
                    if (RightHeader != null) RightHeader.Text = Path.GetFileName(rightPath);
                    _rightText = await Task.Run(() => File.ReadAllText(rightPath!, _currentEncoding));
                    if (RightEditor != null) RightEditor.Text = _rightText;
                    UpdateWorkspaceVisibility();
                    await RecomputeDiffAsync(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"LoadFilesAsync right error: {ex.Message}");
                }
                finally
                {
                    if (RightLoadingOverlay != null) RightLoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }
        finally
        {
            _isLoadingFiles = false;
        }

        UpdateTabHeader();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is ValueTuple<string?, string?> paths)
        {
            LoadFiles(paths.Item1, paths.Item2);
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized || _isLoadingFiles) return;
        if (_isDiffViewMode) return;

        _leftText = LeftEditor?.Text ?? string.Empty;
        _rightText = RightEditor?.Text ?? string.Empty;
        RecomputeDiff();
    }

    private void OnDiffOptionsChanged(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        RecomputeDiff();
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

    private void RecomputeDiff()
    {
        _ = RecomputeDiffAsync(false);
    }

    private async Task RecomputeDiffAsync(bool showOverlay)
    {
        if (!_isInitialized) return;
        UpdateWorkspaceVisibility();

        bool hasLeft = !string.IsNullOrEmpty(_leftPath) || !string.IsNullOrEmpty(_leftText);
        bool hasRight = !string.IsNullOrEmpty(_rightPath) || !string.IsNullOrEmpty(_rightText);

        if (!hasLeft || !hasRight)
        {
            _displayItems.Clear();
            if (DiffListView != null) DiffListView.ItemsSource = null;
            if (StatusStatsPanel != null) StatusStatsPanel.Visibility = Visibility.Collapsed;
            if (StatsText != null)
            {
                StatsText.Visibility = Visibility.Visible;
                StatsText.Text = string.Format(LanguageManager.Instance.Text("diffItemCount"), 0);
            }
            return;
        }

        if (showOverlay && DualCompareLoadingOverlay != null)
        {
            DualCompareLoadingOverlay.Visibility = Visibility.Visible;
        }

        _diffCts?.Cancel();
        _diffCts?.Dispose();
        _diffCts = new CancellationTokenSource();
        var ct = _diffCts.Token;

        try
        {
            bool ignoreWs = WhitespaceSwitch?.IsOn ?? false;
            bool ignoreCase = CaseSwitch?.IsOn ?? false;

            string left = _isDiffViewMode ? _leftText : (LeftEditor?.Text ?? _leftText);
            string right = _isDiffViewMode ? _rightText : (RightEditor?.Text ?? _rightText);

            if (left.Contains('\r')) left = left.Replace("\r\n", "\n").Replace('\r', '\n');
            if (right.Contains('\r')) right = right.Replace("\r\n", "\n").Replace('\r', '\n');

            var (result, alignedLines) = await Task.Run(() =>
            {
                if (ct.IsCancellationRequested) return (new DiffResult(), new List<AlignedDiffLine>());
                var r = DiffEngineService.Instance.CompareText(left, right, ignoreWs, ignoreCase);
                var a = DiffEngineService.Instance.BuildAlignedLines(r);
                return (r, a);
            }, ct);

            if (ct.IsCancellationRequested) return;

            var newItems = new List<DiffLineDisplayItem>(alignedLines.Count);
            int idx = 0;
            foreach (var line in alignedLines)
            {
                var item = new DiffLineDisplayItem
                {
                    LineIndex = idx++,
                    LeftLineNumText = line.LeftLineNumber?.ToString() ?? "",
                    LeftText = line.LeftContent,
                    RightLineNumText = line.RightLineNumber?.ToString() ?? "",
                    RightText = line.RightContent,
                    IsDifference = line.IsDifference
                };

                // Left styling
                switch (line.LeftType)
                {
                    case DiffChangeType.Deleted:
                        item.LeftBgBrush = DelBg;
                        item.LeftFgBrush = DelFg;
                        item.LeftSign = "-";
                        break;
                    case DiffChangeType.Modified:
                        item.LeftBgBrush = ModBg;
                        item.LeftFgBrush = ModFg;
                        item.LeftSign = "~";
                        break;
                    default:
                        if (line.LeftLineNumber == null)
                        {
                            item.LeftBgBrush = PhantomBg;
                            item.LeftFgBrush = DimFg;
                        }
                        break;
                }

                // Right styling
                switch (line.RightType)
                {
                    case DiffChangeType.Added:
                        item.RightBgBrush = AddBg;
                        item.RightFgBrush = AddFg;
                        item.RightSign = "+";
                        break;
                    case DiffChangeType.Modified:
                        item.RightBgBrush = ModBg;
                        item.RightFgBrush = ModFg;
                        item.RightSign = "~";
                        break;
                    default:
                        if (line.RightLineNumber == null)
                        {
                            item.RightBgBrush = PhantomBg;
                            item.RightFgBrush = DimFg;
                        }
                        break;
                }

                newItems.Add(item);
            }

            _displayItems = newItems;

            if (DiffListView != null)
            {
                DiffListView.ItemsSource = null;
                DiffListView.ItemsSource = _displayItems;
            }

            int totalChanges = result.Stats.Additions + result.Stats.Deletions + result.Stats.Modifications;
            if (StatusStatsPanel != null)
            {
                if (StatsText != null) StatsText.Visibility = Visibility.Collapsed;
                StatusStatsPanel.Visibility = Visibility.Visible;
                if (StatusModText != null)
                    StatusModText.Text = $"{totalChanges} {LanguageManager.Instance.Text("totalChanges")},";
                if (StatusAddText != null)
                    StatusAddText.Text = $"{result.Stats.Additions} {LanguageManager.Instance.Text("additions")},";
                if (StatusDelText != null)
                    StatusDelText.Text = $"{result.Stats.Deletions} {LanguageManager.Instance.Text("deletions")}";
            }
            else if (StatsText != null)
            {
                StatsText.Visibility = Visibility.Visible;
                StatsText.Text = $"{totalChanges} {LanguageManager.Instance.Text("totalChanges")}, {result.Stats.Additions} {LanguageManager.Instance.Text("additions")}, {result.Stats.Deletions} {LanguageManager.Instance.Text("deletions")}";
            }
        }
        catch (OperationCanceledException)
        {
            // Ignored
        }
        finally
        {
            if (showOverlay && DualCompareLoadingOverlay != null)
            {
                DualCompareLoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void OnPrevDiffClicked(object sender, RoutedEventArgs e)
    {
        if (_displayItems.Count == 0) return;

        int startIndex = _currentDiffJumpIndex <= 0 ? _displayItems.Count - 1 : _currentDiffJumpIndex - 1;
        for (int i = startIndex; i >= 0; i--)
        {
            if (_displayItems[i].IsDifference)
            {
                _currentDiffJumpIndex = i;
                DiffListView.SelectedIndex = i;
                DiffListView.ScrollIntoView(_displayItems[i]);
                return;
            }
        }
        for (int i = _displayItems.Count - 1; i > startIndex; i--)
        {
            if (_displayItems[i].IsDifference)
            {
                _currentDiffJumpIndex = i;
                DiffListView.SelectedIndex = i;
                DiffListView.ScrollIntoView(_displayItems[i]);
                return;
            }
        }
    }

    private void OnNextDiffClicked(object sender, RoutedEventArgs e)
    {
        if (_displayItems.Count == 0) return;

        int startIndex = _currentDiffJumpIndex >= _displayItems.Count - 1 ? 0 : _currentDiffJumpIndex + 1;
        for (int i = startIndex; i < _displayItems.Count; i++)
        {
            if (_displayItems[i].IsDifference)
            {
                _currentDiffJumpIndex = i;
                DiffListView.SelectedIndex = i;
                DiffListView.ScrollIntoView(_displayItems[i]);
                return;
            }
        }
        for (int i = 0; i < startIndex; i++)
        {
            if (_displayItems[i].IsDifference)
            {
                _currentDiffJumpIndex = i;
                DiffListView.SelectedIndex = i;
                DiffListView.ScrollIntoView(_displayItems[i]);
                return;
            }
        }
    }

    private bool CheckFileValidation(string path)
    {
        var check = FileFormatValidator.Validate(path, CompareType.TextDiff);
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

    private async void OnPickLeftFileClicked(object sender, RoutedEventArgs e)
    {
        var targetWindow = (App.GetWindowForElement(this) as MainWindow) ?? App.ActiveWindow ?? App.MainWindowInstance;
        var file = await PickerHelper.PickSingleFileAsync(new[] { "*" }, targetWindow);
        if (file != null)
        {
            if (!CheckFileValidation(file.Path)) return;

            bool hasRight = !string.IsNullOrEmpty(_rightPath) && File.Exists(_rightPath);
            if (hasRight)
            {
                await LoadFilesAsync(file.Path, _rightPath);
            }
            else
            {
                if (LeftLoadingOverlay != null) LeftLoadingOverlay.Visibility = Visibility.Visible;
                try
                {
                    _leftPath = file.Path;
                    if (LeftHeader != null) LeftHeader.Text = file.Name;
                    _leftText = await Task.Run(() => File.ReadAllText(file.Path, _currentEncoding));
                    if (LeftEditor != null) LeftEditor.Text = _leftText;
                    UpdateWorkspaceVisibility();
                    UpdateTabHeader();
                    await RecomputeDiffAsync(false);
                }
                finally
                {
                    if (LeftLoadingOverlay != null) LeftLoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }
    }

    private async void OnPickRightFileClicked(object sender, RoutedEventArgs e)
    {
        var targetWindow = (App.GetWindowForElement(this) as MainWindow) ?? App.ActiveWindow ?? App.MainWindowInstance;
        var file = await PickerHelper.PickSingleFileAsync(new[] { "*" }, targetWindow);
        if (file != null)
        {
            if (!CheckFileValidation(file.Path)) return;

            bool hasLeft = !string.IsNullOrEmpty(_leftPath) && File.Exists(_leftPath);
            if (hasLeft)
            {
                await LoadFilesAsync(_leftPath, file.Path);
            }
            else
            {
                if (RightLoadingOverlay != null) RightLoadingOverlay.Visibility = Visibility.Visible;
                try
                {
                    _rightPath = file.Path;
                    if (RightHeader != null) RightHeader.Text = file.Name;
                    _rightText = await Task.Run(() => File.ReadAllText(file.Path, _currentEncoding));
                    if (RightEditor != null) RightEditor.Text = _rightText;
                    UpdateWorkspaceVisibility();
                    UpdateTabHeader();
                    await RecomputeDiffAsync(false);
                }
                finally
                {
                    if (RightLoadingOverlay != null) RightLoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }
    }

    private void OnLeftDragOver(object sender, DragEventArgs e)
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
        _ = LoadFilesAsync(leftPath, rightPath);
    }

    private async void OnLeftDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        try
        {
            App.Log("[TextDiffPage] OnLeftDrop triggered");
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                App.Log($"[TextDiffPage] OnLeftDrop items count: {items?.Count ?? 0}");
                if (items != null && items.Count > 0)
                {
                    if (items.Count >= 2)
                    {
                        HandleDualDrop(items[0].Path, items[1].Path);
                        return;
                    }

                    string targetPath = items[0].Path;
                    App.Log($"[TextDiffPage] OnLeftDrop targetPath: {targetPath}");
                    if (!CheckFileValidation(targetPath)) return;

                    bool hasRight = !string.IsNullOrEmpty(_rightPath) && File.Exists(_rightPath);
                    if (hasRight)
                    {
                        await LoadFilesAsync(targetPath, _rightPath);
                    }
                    else
                    {
                        if (LeftLoadingOverlay != null) LeftLoadingOverlay.Visibility = Visibility.Visible;
                        try
                        {
                            _leftPath = targetPath;
                            if (LeftHeader != null) LeftHeader.Text = Path.GetFileName(targetPath);
                            _leftText = await Task.Run(() => File.ReadAllText(targetPath, _currentEncoding));
                            if (LeftEditor != null) LeftEditor.Text = _leftText;
                            UpdateWorkspaceVisibility();
                            UpdateTabHeader();
                            await RecomputeDiffAsync(false);
                        }
                        finally
                        {
                            if (LeftLoadingOverlay != null) LeftLoadingOverlay.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"[TextDiffPage] OnLeftDrop error: {ex.Message}");
        }
    }

    private void OnRightDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void OnRightDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        try
        {
            App.Log("[TextDiffPage] OnRightDrop triggered");
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                App.Log($"[TextDiffPage] OnRightDrop items count: {items?.Count ?? 0}");
                if (items != null && items.Count > 0)
                {
                    if (items.Count >= 2)
                    {
                        HandleDualDrop(items[0].Path, items[1].Path);
                        return;
                    }

                    string targetPath = items[0].Path;
                    App.Log($"[TextDiffPage] OnRightDrop targetPath: {targetPath}");
                    if (!CheckFileValidation(targetPath)) return;

                    bool hasLeft = !string.IsNullOrEmpty(_leftPath) && File.Exists(_leftPath);
                    if (hasLeft)
                    {
                        await LoadFilesAsync(_leftPath, targetPath);
                    }
                    else
                    {
                        if (RightLoadingOverlay != null) RightLoadingOverlay.Visibility = Visibility.Visible;
                        try
                        {
                            _rightPath = targetPath;
                            if (RightHeader != null) RightHeader.Text = Path.GetFileName(targetPath);
                            _rightText = await Task.Run(() => File.ReadAllText(targetPath, _currentEncoding));
                            if (RightEditor != null) RightEditor.Text = _rightText;
                            UpdateWorkspaceVisibility();
                            UpdateTabHeader();
                            await RecomputeDiffAsync(false);
                        }
                        finally
                        {
                            if (RightLoadingOverlay != null) RightLoadingOverlay.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"[TextDiffPage] OnRightDrop error: {ex.Message}");
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
            }
        }
        catch (Exception ex)
        {
            App.Log($"[TextDiffPage] OnDiffContainerDrop error: {ex.Message}");
        }

        var pos = e.GetPosition(this);
        double totalWidth = this.ActualWidth > 0 ? this.ActualWidth : 900;
        if (pos.X < totalWidth / 2)
        {
            OnLeftDrop(sender, e);
        }
        else
        {
            OnRightDrop(sender, e);
        }
    }

    public void HandleDropFromWindow(string path, bool isLeft)
    {
        if (string.IsNullOrEmpty(path)) return;
        App.Log($"[TextDiffPage] HandleDropFromWindow: isLeft={isLeft}, path={path}");
        if (!CheckFileValidation(path)) return;

        if (isLeft)
        {
            if (!string.IsNullOrEmpty(_rightPath) && File.Exists(_rightPath))
            {
                _ = LoadFilesAsync(path, _rightPath);
            }
            else
            {
                _ = LoadFilesAsync(path, null);
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(_leftPath) && File.Exists(_leftPath))
            {
                _ = LoadFilesAsync(_leftPath, path);
            }
            else
            {
                _ = LoadFilesAsync(null, path);
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

    private async void OnSaveLeftFileClicked(object sender, RoutedEventArgs e)
    {
        var targetWindow = (App.GetWindowForElement(this) as MainWindow) ?? App.ActiveWindow ?? App.MainWindowInstance;
        if (!string.IsNullOrEmpty(_leftPath))
        {
            if (SettingsManager.Instance.CreateBakBackup && File.Exists(_leftPath))
            {
                File.Copy(_leftPath, _leftPath + ".bak", true);
            }
            await File.WriteAllTextAsync(_leftPath, LeftEditor.Text, _currentEncoding);
            StatusMessageText.Text = LanguageManager.Instance.Text("sourceFileSaved");
        }
        else
        {
            var choices = new Dictionary<string, IList<string>> { { LanguageManager.Instance.Text("fileTypeText"), new List<string> { ".txt" } } };
            var file = await PickerHelper.PickSaveFileAsync(".txt", choices, "file1.txt", targetWindow);
            if (file != null)
            {
                _leftPath = file.Path;
                LeftHeader.Text = file.Name;
                await File.WriteAllTextAsync(file.Path, LeftEditor.Text, _currentEncoding);
                StatusMessageText.Text = LanguageManager.Instance.Text("sourceFileSaved");
            }
        }
    }

    private async void OnSaveRightFileClicked(object sender, RoutedEventArgs e)
    {
        var targetWindow = (App.GetWindowForElement(this) as MainWindow) ?? App.ActiveWindow ?? App.MainWindowInstance;
        if (!string.IsNullOrEmpty(_rightPath))
        {
            if (SettingsManager.Instance.CreateBakBackup && File.Exists(_rightPath))
            {
                File.Copy(_rightPath, _rightPath + ".bak", true);
            }
            await File.WriteAllTextAsync(_rightPath, RightEditor.Text, _currentEncoding);
            StatusMessageText.Text = LanguageManager.Instance.Text("targetFileSaved");
        }
        else
        {
            var choices = new Dictionary<string, IList<string>> { { LanguageManager.Instance.Text("fileTypeText"), new List<string> { ".txt" } } };
            var file = await PickerHelper.PickSaveFileAsync(".txt", choices, "file2.txt", targetWindow);
            if (file != null)
            {
                _rightPath = file.Path;
                RightHeader.Text = file.Name;
                await File.WriteAllTextAsync(file.Path, RightEditor.Text, _currentEncoding);
                StatusMessageText.Text = LanguageManager.Instance.Text("targetFileSaved");
            }
        }
    }

    private async void OnAISummaryClicked(object sender, RoutedEventArgs e)
    {
        if (AICard == null) return;

        // Toggle behavior matching macOS TwoWayDiffView.toggleAISummary()
        if (AICard.Visibility == Visibility.Visible)
        {
            AICard.Visibility = Visibility.Collapsed;
            return;
        }

        AICard.Visibility = Visibility.Visible;
        if (_lastSummaryResult == null)
        {
            await RunAISummaryAsync();
        }
    }

    private async Task RunAISummaryAsync()
    {
        if (_isAiGenerating) return;
        _isAiGenerating = true;

        if (AICard != null) AICard.Visibility = Visibility.Visible;
        if (AIProgressRing != null)
        {
            AIProgressRing.Visibility = Visibility.Visible;
            AIProgressRing.IsActive = true;
        }

        if (AIOneLineIntent != null) AIOneLineIntent.Text = LanguageManager.Instance.Text("aiAnalyzing");
        if (AIModelBadge != null) AIModelBadge.Text = AIModelManager.Instance.ActiveModelName;
        if (ReanalyzeBtn != null) ReanalyzeBtn.IsEnabled = false;

        try
        {
            string left = !string.IsNullOrEmpty(LeftEditor?.Text) ? LeftEditor.Text : _leftText;
            string right = !string.IsNullOrEmpty(RightEditor?.Text) ? RightEditor.Text : _rightText;
            string leftTitle = !string.IsNullOrEmpty(_leftPath) ? Path.GetFileName(_leftPath) : LanguageManager.Instance.Text("sourceFile");
            string rightTitle = !string.IsNullOrEmpty(_rightPath) ? Path.GetFileName(_rightPath) : LanguageManager.Instance.Text("targetFile");

            var mode = SettingsManager.Instance.AiExecutionMode;
            var cloudConfig = SettingsManager.Instance.CloudConfig;

            var result = await AIService.Instance.SummarizeDiffAsync(
                left, right, leftTitle, rightTitle, mode, cloudConfig, LanguageManager.Instance.EffectiveLanguage
            );

            _lastSummaryResult = result;

            if (AIProgressRing != null)
            {
                AIProgressRing.IsActive = false;
                AIProgressRing.Visibility = Visibility.Collapsed;
            }

            string sec = result.ExecutionTimeSeconds.ToString("F2");
            string modeBadge = mode == AIExecutionMode.CloudAPI
                ? LanguageManager.Instance.Text("aiCloudBadge")
                : LanguageManager.Instance.Text("aiLocalOfflineBadge");

            if (AIModelBadge != null) AIModelBadge.Text = $"{result.ModelName} \u00B7 {sec}s \u00B7 {modeBadge}";
            if (AIOneLineIntent != null) AIOneLineIntent.Text = result.OneLineIntent;
            if (AIKeyModsList != null) AIKeyModsList.ItemsSource = result.KeyModifications;
            if (AICommitMessage != null) AICommitMessage.Text = result.ConventionalCommit;
        }
        catch (Exception ex)
        {
            if (AIProgressRing != null)
            {
                AIProgressRing.IsActive = false;
                AIProgressRing.Visibility = Visibility.Collapsed;
            }
            if (AIOneLineIntent != null) AIOneLineIntent.Text = ex.Message;
        }
        finally
        {
            _isAiGenerating = false;
            if (ReanalyzeBtn != null) ReanalyzeBtn.IsEnabled = true;
        }
    }

    private async void OnReanalyzeClicked(object sender, RoutedEventArgs e)
    {
        await RunAISummaryAsync();
    }

    private void OnCloseAICardClicked(object sender, RoutedEventArgs e)
    {
        if (AICard != null) AICard.Visibility = Visibility.Collapsed;
    }

    private async void OnCopyCommitClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(AICommitMessage?.Text)) return;
        var dataPackage = new DataPackage();
        dataPackage.SetText(AICommitMessage.Text);
        Clipboard.SetContent(dataPackage);

        if (AICopyCommitBtnText != null)
        {
            string original = AICopyCommitBtnText.Text;
            AICopyCommitBtnText.Text = LanguageManager.Instance.Text("copied");
            await Task.Delay(2000);
            AICopyCommitBtnText.Text = original;
        }
    }

    private async void OnCopyTextClicked(object sender, RoutedEventArgs e)
    {
        var sb = new System.Text.StringBuilder();
        if (AIOneLineIntent != null) sb.AppendLine(AIOneLineIntent.Text);
        if (_lastSummaryResult?.KeyModifications != null)
        {
            sb.AppendLine();
            foreach (var mod in _lastSummaryResult.KeyModifications)
            {
                sb.AppendLine($"\u2022 {mod}");
            }
        }
        if (AICommitMessage != null && !string.IsNullOrEmpty(AICommitMessage.Text))
        {
            sb.AppendLine();
            sb.AppendLine($"Commit: {AICommitMessage.Text}");
        }

        var dataPackage = new DataPackage();
        dataPackage.SetText(sb.ToString());
        Clipboard.SetContent(dataPackage);

        if (AICopyTextBtnText != null)
        {
            string original = AICopyTextBtnText.Text;
            AICopyTextBtnText.Text = LanguageManager.Instance.Text("copied");
            await Task.Delay(2000);
            AICopyTextBtnText.Text = original;
        }
    }

    public void Dispose()
    {
        try
        {
            LanguageManager.Instance.LanguageChanged -= UpdateLocalization;
            _displayItems.Clear();
            _leftText = string.Empty;
            _rightText = string.Empty;
            if (LeftEditor != null)
            {
                LeftEditor.Text = string.Empty;
            }
            if (RightEditor != null)
            {
                RightEditor.Text = string.Empty;
            }
        }
        catch
        {
        }
    }
}

