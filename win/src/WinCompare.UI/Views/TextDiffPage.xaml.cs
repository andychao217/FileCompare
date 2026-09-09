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

public sealed partial class TextDiffPage : Page
{
    private string? _leftPath;
    private string? _rightPath;
    private string _leftText = string.Empty;
    private string _rightText = string.Empty;
    private List<DiffLineDisplayItem> _displayItems = new();
    private int _currentDiffJumpIndex = -1;
    private bool _isDiffViewMode = true;
    private System.Text.Encoding _currentEncoding = System.Text.Encoding.UTF8;

    private static readonly Brush AddBg = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 34, 197, 94));
    private static readonly Brush AddFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 74, 222, 128));
    private static readonly Brush DelBg = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 239, 68, 68));
    private static readonly Brush DelFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 113, 113));
    private static readonly Brush ModBg = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 234, 179, 8));
    private static readonly Brush ModFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 250, 204, 21));
    private static readonly Brush PhantomBg = new SolidColorBrush(Windows.UI.Color.FromArgb(16, 128, 128, 128));
    private static readonly Brush DefaultFg = new SolidColorBrush(Windows.UI.Color.FromArgb(230, 240, 240, 245));
    private static readonly Brush DimFg = new SolidColorBrush(Windows.UI.Color.FromArgb(140, 150, 150, 160));

    private bool _isInitialized = false;

    public TextDiffPage()
    {
        this.InitializeComponent();
        
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
    }

    private void UpdateWorkspaceVisibility()
    {
        if (!_isInitialized) return;

        bool hasLeft = !string.IsNullOrEmpty(_leftPath) || !string.IsNullOrEmpty(_leftText);
        bool hasRight = !string.IsNullOrEmpty(_rightPath) || !string.IsNullOrEmpty(_rightText);

        if (!hasLeft && !hasRight)
        {
            if (EmptyStateContainer != null) EmptyStateContainer.Visibility = Visibility.Visible;
            if (DiffListContainer != null) DiffListContainer.Visibility = Visibility.Collapsed;
            if (EditorsContainer != null) EditorsContainer.Visibility = Visibility.Collapsed;

            if (PrevDiffBtn != null) PrevDiffBtn.IsEnabled = false;
            if (NextDiffBtn != null) NextDiffBtn.IsEnabled = false;
            if (TakeLeftBtn != null) TakeLeftBtn.IsEnabled = false;
            if (TakeRightBtn != null) TakeRightBtn.IsEnabled = false;
            if (AISummaryButton != null) AISummaryButton.IsEnabled = false;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = false;
            if (LeftSaveBtn != null) LeftSaveBtn.IsEnabled = false;
            if (RightSaveBtn != null) RightSaveBtn.IsEnabled = false;
            if (StatsText != null) StatsText.Text = string.Format(LanguageManager.Instance.Text("diffItemCount"), 0);
        }
        else
        {
            if (EmptyStateContainer != null) EmptyStateContainer.Visibility = Visibility.Collapsed;
            if (_isDiffViewMode)
            {
                if (DiffListContainer != null) DiffListContainer.Visibility = Visibility.Visible;
                if (EditorsContainer != null) EditorsContainer.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (DiffListContainer != null) DiffListContainer.Visibility = Visibility.Collapsed;
                if (EditorsContainer != null) EditorsContainer.Visibility = Visibility.Visible;
            }

            bool hasBoth = hasLeft && hasRight;
            if (PrevDiffBtn != null) PrevDiffBtn.IsEnabled = hasBoth;
            if (NextDiffBtn != null) NextDiffBtn.IsEnabled = hasBoth;
            if (TakeLeftBtn != null) TakeLeftBtn.IsEnabled = hasBoth;
            if (TakeRightBtn != null) TakeRightBtn.IsEnabled = hasBoth;
            if (AISummaryButton != null) AISummaryButton.IsEnabled = hasBoth;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = true;
            if (LeftSaveBtn != null) LeftSaveBtn.IsEnabled = hasLeft;
            if (RightSaveBtn != null) RightSaveBtn.IsEnabled = hasRight;
        }
    }

    private void OnTakeLeftClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_leftText) && string.IsNullOrEmpty(LeftEditor.Text)) return;

        _rightText = LeftEditor?.Text ?? _leftText;
        if (RightEditor != null) RightEditor.Text = _rightText;
        RecomputeDiff();
        StatusMessageText.Text = "已采纳左侧内容至右侧";
    }

    private void OnTakeRightClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_rightText) && string.IsNullOrEmpty(RightEditor.Text)) return;

        _leftText = RightEditor?.Text ?? _rightText;
        if (LeftEditor != null) LeftEditor.Text = _leftText;
        RecomputeDiff();
        StatusMessageText.Text = "已采纳右侧内容至左侧";
    }

    private void OnEncodingChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;

        if (EncodingComboBox?.SelectedItem is ComboBoxItem item && item.Content is string encName)
        {
            _currentEncoding = GetEncodingByName(encName);
            if (EncodingStatusText != null) EncodingStatusText.Text = encName;

            if (!string.IsNullOrEmpty(_leftPath) && File.Exists(_leftPath))
            {
                _leftText = File.ReadAllText(_leftPath, _currentEncoding);
                if (LeftEditor != null) LeftEditor.Text = _leftText;
            }
            if (!string.IsNullOrEmpty(_rightPath) && File.Exists(_rightPath))
            {
                _rightText = File.ReadAllText(_rightPath, _currentEncoding);
                if (RightEditor != null) RightEditor.Text = _rightText;
            }
            RecomputeDiff();
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
        _currentDiffJumpIndex = -1;
        if (StatusMessageText != null) StatusMessageText.Text = string.Empty;
        if (CursorInfoText != null) CursorInfoText.Text = "Ln 1, Col 1";
        if (AICard != null) AICard.Visibility = Visibility.Collapsed;
        UpdateWorkspaceVisibility();
    }

    public void LoadFiles(string? leftPath, string? rightPath)
    {
        if (!string.IsNullOrEmpty(leftPath) && File.Exists(leftPath))
        {
            _leftPath = leftPath;
            if (LeftHeader != null) LeftHeader.Text = Path.GetFileName(leftPath);
            _leftText = File.ReadAllText(leftPath, _currentEncoding);
            if (LeftEditor != null) LeftEditor.Text = _leftText;
        }

        if (!string.IsNullOrEmpty(rightPath) && File.Exists(rightPath))
        {
            _rightPath = rightPath;
            if (RightHeader != null) RightHeader.Text = Path.GetFileName(rightPath);
            _rightText = File.ReadAllText(rightPath, _currentEncoding);
            if (RightEditor != null) RightEditor.Text = _rightText;
        }

        if (!string.IsNullOrEmpty(_leftPath) && !string.IsNullOrEmpty(_rightPath))
        {
            RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.TextDiff);
        }

        UpdateWorkspaceVisibility();
        RecomputeDiff();
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
        if (!_isInitialized) return;
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
        if (!_isInitialized) return;
        UpdateWorkspaceVisibility();

        bool hasLeft = !string.IsNullOrEmpty(_leftPath) || !string.IsNullOrEmpty(_leftText);
        bool hasRight = !string.IsNullOrEmpty(_rightPath) || !string.IsNullOrEmpty(_rightText);

        if (!hasLeft && !hasRight)
        {
            return;
        }

        bool ignoreWs = WhitespaceSwitch?.IsOn ?? false;
        bool ignoreCase = CaseSwitch?.IsOn ?? false;

        string left = LeftEditor?.Text ?? _leftText;
        string right = RightEditor?.Text ?? _rightText;

        var result = DiffEngineService.Instance.CompareText(left, right, ignoreWs, ignoreCase);
        var alignedLines = DiffEngineService.Instance.BuildAlignedLines(result);

        _displayItems.Clear();
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
                    else
                    {
                        item.LeftFgBrush = DefaultFg;
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
                    else
                    {
                        item.RightFgBrush = DefaultFg;
                    }
                    break;
            }

            _displayItems.Add(item);
        }

        if (DiffListView != null)
        {
            DiffListView.ItemsSource = null;
            DiffListView.ItemsSource = _displayItems;
        }

        if (StatsText != null)
        {
            int totalChanges = result.Stats.Additions + result.Stats.Deletions + result.Stats.Modifications;
            StatsText.Text = string.Format(LanguageManager.Instance.Text("diffItemCount"), totalChanges);
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

    private async void OnPickLeftFileClicked(object sender, RoutedEventArgs e)
    {
        var targetWindow = (App.GetWindowForElement(this) as MainWindow) ?? App.ActiveWindow ?? App.MainWindowInstance;
        var file = await PickerHelper.PickSingleFileAsync(new[] { "*" }, targetWindow);
        if (file != null)
        {
            _leftPath = file.Path;
            LeftHeader.Text = file.Name;
            _leftText = await File.ReadAllTextAsync(file.Path, _currentEncoding);
            LeftEditor.Text = _leftText;
            if (!string.IsNullOrEmpty(_rightPath))
            {
                RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.TextDiff);
            }
            UpdateWorkspaceVisibility();
            RecomputeDiff();
        }
    }

    private async void OnPickRightFileClicked(object sender, RoutedEventArgs e)
    {
        var targetWindow = (App.GetWindowForElement(this) as MainWindow) ?? App.ActiveWindow ?? App.MainWindowInstance;
        var file = await PickerHelper.PickSingleFileAsync(new[] { "*" }, targetWindow);
        if (file != null)
        {
            _rightPath = file.Path;
            RightHeader.Text = file.Name;
            _rightText = await File.ReadAllTextAsync(file.Path, _currentEncoding);
            RightEditor.Text = _rightText;
            if (!string.IsNullOrEmpty(_leftPath))
            {
                RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.TextDiff);
            }
            UpdateWorkspaceVisibility();
            RecomputeDiff();
        }
    }

    private void OnLeftDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void OnLeftDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                _leftPath = file.Path;
                LeftHeader.Text = file.Name;
                _leftText = await File.ReadAllTextAsync(file.Path, _currentEncoding);
                LeftEditor.Text = _leftText;
                UpdateWorkspaceVisibility();
                RecomputeDiff();
            }
        }
    }

    private void OnRightDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void OnRightDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                _rightPath = file.Path;
                RightHeader.Text = file.Name;
                _rightText = await File.ReadAllTextAsync(file.Path, _currentEncoding);
                RightEditor.Text = _rightText;
                UpdateWorkspaceVisibility();
                RecomputeDiff();
            }
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
            StatusMessageText.Text = "源文件已保存";
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
                StatusMessageText.Text = "源文件已保存";
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
            StatusMessageText.Text = "目标文件已保存";
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
                StatusMessageText.Text = "目标文件已保存";
            }
        }
    }

    private async void OnAISummaryClicked(object sender, RoutedEventArgs e)
    {
        AICard.Visibility = Visibility.Visible;
        AIOneLineIntent.Text = LanguageManager.Instance.Text("aiAnalyzingIntent");
        AIModelBadge.Text = AIModelManager.Instance.ActiveModelName;

        string left = LeftEditor.Text;
        string right = RightEditor.Text;
        string leftTitle = LeftHeader.Text;
        string rightTitle = RightHeader.Text;

        var mode = SettingsManager.Instance.AiExecutionMode;
        var cloudConfig = SettingsManager.Instance.CloudConfig;

        var result = await AIService.Instance.SummarizeDiffAsync(
            left, right, leftTitle, rightTitle, mode, cloudConfig, LanguageManager.Instance.EffectiveLanguage
        );

        string sec = result.ExecutionTimeSeconds.ToString("F1");
        AIModelBadge.Text = $"{result.ModelName} · {sec}s";
        AIOneLineIntent.Text = result.OneLineIntent;
        AIKeyModsList.ItemsSource = result.KeyModifications;
        AICommitMessage.Text = result.ConventionalCommit;
    }

    private void OnCloseAICardClicked(object sender, RoutedEventArgs e)
    {
        AICard.Visibility = Visibility.Collapsed;
    }

    private void OnCopyCommitClicked(object sender, RoutedEventArgs e)
    {
        var dataPackage = new DataPackage();
        dataPackage.SetText(AICommitMessage.Text);
        Clipboard.SetContent(dataPackage);
    }
}
