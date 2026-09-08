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

public sealed partial class TextDiffPage : Page
{
    private string? _leftPath;
    private string? _rightPath;
    private string _leftText = "// File 1: Sample Left\nfunction calculate(a, b) {\n    return a + b;\n}\n";
    private string _rightText = "// File 2: Sample Right\nfunction calculate(a, b, multiplier = 1) {\n    return (a + b) * multiplier;\n}\n";
    private List<DiffLineDisplayItem> _displayItems = new();
    private int _currentDiffJumpIndex = -1;
    private bool _isDiffViewMode = true;

    private static readonly Brush AddBg = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 34, 197, 94));
    private static readonly Brush AddFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 74, 222, 128));
    private static readonly Brush DelBg = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 239, 68, 68));
    private static readonly Brush DelFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 113, 113));
    private static readonly Brush ModBg = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 234, 179, 8));
    private static readonly Brush ModFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 250, 204, 21));
    private static readonly Brush PhantomBg = new SolidColorBrush(Windows.UI.Color.FromArgb(16, 128, 128, 128));
    private static readonly Brush DefaultFg = new SolidColorBrush(Windows.UI.Color.FromArgb(230, 240, 240, 245));
    private static readonly Brush DimFg = new SolidColorBrush(Windows.UI.Color.FromArgb(140, 150, 150, 160));

    public TextDiffPage()
    {
        this.InitializeComponent();
        WhitespaceSwitch.IsOn = SettingsManager.Instance.DefaultIgnoreWhitespace;
        CaseSwitch.IsOn = SettingsManager.Instance.DefaultIgnoreCase;
        UpdateLocalization();
        LanguageManager.Instance.LanguageChanged += UpdateLocalization;
        LeftEditor.Text = _leftText;
        RightEditor.Text = _rightText;
        RecomputeDiff();
    }

    private void UpdateLocalization()
    {
        if (OpenFilesBtn != null) OpenFilesBtn.Label = LanguageManager.Instance.Text("openFile");
        if (MenuPickLeft != null) MenuPickLeft.Text = LanguageManager.Instance.Text("pickLeftFile");
        if (MenuPickRight != null) MenuPickRight.Text = LanguageManager.Instance.Text("pickRightFile");
        if (MenuSaveLeft != null) MenuSaveLeft.Text = LanguageManager.Instance.Text("saveLeftFile");
        if (MenuSaveRight != null) MenuSaveRight.Text = LanguageManager.Instance.Text("saveRightFile");
        if (ViewModeBtn != null) ViewModeBtn.Label = _isDiffViewMode ? LanguageManager.Instance.Text("viewDiffMode") : LanguageManager.Instance.Text("viewEditMode");
        if (WhitespaceSwitch != null) WhitespaceSwitch.Header = LanguageManager.Instance.Text("ignoreWhitespace");
        if (CaseSwitch != null) CaseSwitch.Header = LanguageManager.Instance.Text("ignoreCase");
        if (PrevDiffBtn != null) PrevDiffBtn.Label = LanguageManager.Instance.Text("prevDiff");
        if (NextDiffBtn != null) NextDiffBtn.Label = LanguageManager.Instance.Text("nextDiff");
        if (SwapSidesBtn != null) SwapSidesBtn.Label = LanguageManager.Instance.Text("swapSides");
        if (RefreshBtn != null) RefreshBtn.Label = LanguageManager.Instance.Text("refreshCompare");
        if (ExportReportBtn != null) ExportReportBtn.Label = LanguageManager.Instance.Text("exportDiff");
        if (ClearAllBtn != null) ClearAllBtn.Label = LanguageManager.Instance.Text("clearAll");
        if (AIBtnText != null) AIBtnText.Text = LanguageManager.Instance.Text("aiDiffSummary");
        if (LeftBrowseBtn != null) LeftBrowseBtn.Content = LanguageManager.Instance.Text("browse");
        if (RightBrowseBtn != null) RightBrowseBtn.Content = LanguageManager.Instance.Text("browse");
        if (AICardTitle != null) AICardTitle.Text = LanguageManager.Instance.Text("aiSummaryTitle");
        if (AICopyCommitBtnText != null) AICopyCommitBtnText.Text = LanguageManager.Instance.Text("copyCommitMsg");
        if (LeftHeader != null && string.IsNullOrEmpty(_leftPath)) LeftHeader.Text = LanguageManager.Instance.Text("wordSideLeft");
        if (RightHeader != null && string.IsNullOrEmpty(_rightPath)) RightHeader.Text = LanguageManager.Instance.Text("wordSideRight");
    }

    private void OnSwapSidesClicked(object sender, RoutedEventArgs e)
    {
        var tempPath = _leftPath;
        _leftPath = _rightPath;
        _rightPath = tempPath;

        LeftHeader.Text = !string.IsNullOrEmpty(_leftPath) ? Path.GetFileName(_leftPath) : LanguageManager.Instance.Text("wordSideLeft");
        RightHeader.Text = !string.IsNullOrEmpty(_rightPath) ? Path.GetFileName(_rightPath) : LanguageManager.Instance.Text("wordSideRight");

        string tempText = LeftEditor.Text;
        LeftEditor.Text = RightEditor.Text;
        RightEditor.Text = tempText;

        _leftText = LeftEditor.Text;
        _rightText = RightEditor.Text;

        RecomputeDiff();
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_leftPath) && File.Exists(_leftPath))
        {
            _leftText = File.ReadAllText(_leftPath, SettingsManager.Instance.GetEncoding());
            LeftEditor.Text = _leftText;
        }
        if (!string.IsNullOrEmpty(_rightPath) && File.Exists(_rightPath))
        {
            _rightText = File.ReadAllText(_rightPath, SettingsManager.Instance.GetEncoding());
            RightEditor.Text = _rightText;
        }
        RecomputeDiff();
    }

    private void OnClearAllClicked(object sender, RoutedEventArgs e)
    {
        _leftPath = null;
        _rightPath = null;
        LeftHeader.Text = LanguageManager.Instance.Text("wordSideLeft");
        RightHeader.Text = LanguageManager.Instance.Text("wordSideRight");
        LeftEditor.Text = string.Empty;
        RightEditor.Text = string.Empty;
        _leftText = string.Empty;
        _rightText = string.Empty;
        _displayItems.Clear();
        DiffListView.ItemsSource = null;
        _currentDiffJumpIndex = -1;
        StatsText.Text = LanguageManager.Instance.Text("textDiffSummaryZero");
        AICard.Visibility = Visibility.Collapsed;
    }

    private void OnExportDiffClicked(object sender, RoutedEventArgs e)
    {
        string left = LeftEditor?.Text ?? _leftText;
        string right = RightEditor?.Text ?? _rightText;
        bool ignoreWs = WhitespaceSwitch?.IsOn ?? false;
        bool ignoreCase = CaseSwitch?.IsOn ?? false;

        var result = DiffEngineService.Instance.CompareText(left, right, ignoreWs, ignoreCase);

        var sb = new System.Text.StringBuilder();
        string lName = !string.IsNullOrEmpty(_leftPath) ? Path.GetFileName(_leftPath) : "a/file";
        string rName = !string.IsNullOrEmpty(_rightPath) ? Path.GetFileName(_rightPath) : "b/file";
        sb.AppendLine($"--- {lName}");
        sb.AppendLine($"+++ {rName}");
        foreach (var chunk in result.Chunks)
        {
            if (chunk.ChangeType == DiffChangeType.Unchanged)
            {
                foreach (var line in chunk.LeftLines) sb.AppendLine($"  {line}");
            }
            else if (chunk.ChangeType == DiffChangeType.Added)
            {
                foreach (var line in chunk.RightLines) sb.AppendLine($"+ {line}");
            }
            else if (chunk.ChangeType == DiffChangeType.Deleted)
            {
                foreach (var line in chunk.LeftLines) sb.AppendLine($"- {line}");
            }
            else if (chunk.ChangeType == DiffChangeType.Modified)
            {
                foreach (var line in chunk.LeftLines) sb.AppendLine($"- {line}");
                foreach (var line in chunk.RightLines) sb.AppendLine($"+ {line}");
            }
        }

        var dp = new DataPackage();
        dp.SetText(sb.ToString());
        Clipboard.SetContent(dp);

        StatsText.Text = LanguageManager.Instance.Text("exportSuccess");
    }

    public void LoadFiles(string? leftPath, string? rightPath)
    {
        if (!string.IsNullOrEmpty(leftPath) && File.Exists(leftPath))
        {
            _leftPath = leftPath;
            LeftHeader.Text = Path.GetFileName(leftPath);
            _leftText = File.ReadAllText(leftPath, SettingsManager.Instance.GetEncoding());
            LeftEditor.Text = _leftText;
        }

        if (!string.IsNullOrEmpty(rightPath) && File.Exists(rightPath))
        {
            _rightPath = rightPath;
            RightHeader.Text = Path.GetFileName(rightPath);
            _rightText = File.ReadAllText(rightPath, SettingsManager.Instance.GetEncoding());
            RightEditor.Text = _rightText;
        }

        if (!string.IsNullOrEmpty(_leftPath) && !string.IsNullOrEmpty(_rightPath))
        {
            RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.TextDiff);
        }

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
        _leftText = LeftEditor.Text;
        _rightText = RightEditor.Text;
        RecomputeDiff();
    }

    private void OnDiffOptionsChanged(object sender, RoutedEventArgs e)
    {
        RecomputeDiff();
    }

    private void OnToggleViewModeClicked(object sender, RoutedEventArgs e)
    {
        _isDiffViewMode = !_isDiffViewMode;
        if (_isDiffViewMode)
        {
            DiffListContainer.Visibility = Visibility.Visible;
            EditorsContainer.Visibility = Visibility.Collapsed;
            ViewModeBtn.Label = LanguageManager.Instance.Text("viewDiffMode");
            RecomputeDiff();
        }
        else
        {
            DiffListContainer.Visibility = Visibility.Collapsed;
            EditorsContainer.Visibility = Visibility.Visible;
            ViewModeBtn.Label = LanguageManager.Instance.Text("viewEditMode");
        }
    }

    private void RecomputeDiff()
    {
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
            StatsText.Text = string.Format(LanguageManager.Instance.Text("textDiffStats"),
            result.Stats.Additions, result.Stats.Deletions, result.Stats.Modifications, alignedLines.Count);
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
        var file = await PickerHelper.PickSingleFileAsync(new[] { "*" }, App.MainWindowInstance);
        if (file != null)
        {
            _leftPath = file.Path;
            LeftHeader.Text = file.Name;
            _leftText = await File.ReadAllTextAsync(file.Path);
            LeftEditor.Text = _leftText;
            if (!string.IsNullOrEmpty(_rightPath))
            {
                RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.TextDiff);
            }
            RecomputeDiff();
        }
    }

    private async void OnPickRightFileClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { "*" }, App.MainWindowInstance);
        if (file != null)
        {
            _rightPath = file.Path;
            RightHeader.Text = file.Name;
            _rightText = await File.ReadAllTextAsync(file.Path);
            RightEditor.Text = _rightText;
            if (!string.IsNullOrEmpty(_leftPath))
            {
                RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.TextDiff);
            }
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
                _leftText = await File.ReadAllTextAsync(file.Path, SettingsManager.Instance.GetEncoding());
                LeftEditor.Text = _leftText;
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
                _rightText = await File.ReadAllTextAsync(file.Path, SettingsManager.Instance.GetEncoding());
                RightEditor.Text = _rightText;
                RecomputeDiff();
            }
        }
    }

    private async void OnSaveLeftFileClicked(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_leftPath))
        {
            if (SettingsManager.Instance.CreateBakBackup && File.Exists(_leftPath))
            {
                File.Copy(_leftPath, _leftPath + ".bak", true);
            }
            await File.WriteAllTextAsync(_leftPath, LeftEditor.Text, SettingsManager.Instance.GetEncoding());
        }
        else
        {
            var choices = new Dictionary<string, IList<string>> { { LanguageManager.Instance.Text("fileTypeText"), new List<string> { ".txt" } } };
            var file = await PickerHelper.PickSaveFileAsync(".txt", choices, "file1.txt", App.MainWindowInstance);
            if (file != null)
            {
                _leftPath = file.Path;
                LeftHeader.Text = file.Name;
                await File.WriteAllTextAsync(file.Path, LeftEditor.Text, SettingsManager.Instance.GetEncoding());
            }
        }
    }

    private async void OnSaveRightFileClicked(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_rightPath))
        {
            if (SettingsManager.Instance.CreateBakBackup && File.Exists(_rightPath))
            {
                File.Copy(_rightPath, _rightPath + ".bak", true);
            }
            await File.WriteAllTextAsync(_rightPath, RightEditor.Text, SettingsManager.Instance.GetEncoding());
        }
        else
        {
            var choices = new Dictionary<string, IList<string>> { { LanguageManager.Instance.Text("fileTypeText"), new List<string> { ".txt" } } };
            var file = await PickerHelper.PickSaveFileAsync(".txt", choices, "file2.txt", App.MainWindowInstance);
            if (file != null)
            {
                _rightPath = file.Path;
                RightHeader.Text = file.Name;
                await File.WriteAllTextAsync(file.Path, RightEditor.Text, SettingsManager.Instance.GetEncoding());
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
