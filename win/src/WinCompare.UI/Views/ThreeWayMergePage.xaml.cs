using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinCompare.Core.Models;
using WinCompare.Core.Services;
using WinCompare.UI.Helpers;
using WinCompare.UI.ViewModels;

namespace WinCompare.UI.Views;

public sealed partial class ThreeWayMergePage : Page, IDisposable
{
    private string? _localPath;
    private string? _basePath;
    private string? _remotePath;
    private string _localContent = string.Empty;
    private string _baseContent = string.Empty;
    private string _remoteContent = string.Empty;

    private int _currentConflictIndex = -1;
    private ThreeWayMergeResult? _lastResult;
    private List<ThreeWayMergeLineDisplayItem> _displayItems = new();
    private readonly List<int> _conflictLineIndices = new();

    private static Brush AddBg => App.GetThemeBrush("DiffAddedBgBrush");
    private static Brush AddFg => App.GetThemeBrush("DiffAddedFgBrush");
    private static Brush DelBg => App.GetThemeBrush("DiffDeletedBgBrush");
    private static Brush DelFg => App.GetThemeBrush("DiffDeletedFgBrush");
    private static Brush ModBg => App.GetThemeBrush("DiffModifiedBgBrush");
    private static Brush ModFg => App.GetThemeBrush("DiffModifiedFgBrush");
    private static Brush PhantomBg => App.GetThemeBrush("DiffPhantomBgBrush");
    private static Brush DimFg => App.GetThemeBrush("TextMutedBrush");
    private static Brush ConflictWarningBrush => App.GetThemeBrush("StatusModTextBrush");
    private static Brush ConflictSuccessBrush => App.GetThemeBrush("StatusAddTextBrush");

    public ThreeWayMergePage()
    {
        this.InitializeComponent();
        UpdateLocalization();
        LanguageManager.Instance.LanguageChanged += UpdateLocalization;

        UpdateTitlesAndVisibility();
        ComputeMerge();
    }

    private void UpdateLocalization()
    {
        if (LocalTitleText != null) LocalTitleText.Text = LanguageManager.Instance.Text("localBranch");
        if (BaseTitleText != null) BaseTitleText.Text = LanguageManager.Instance.Text("baseBranch");
        if (RemoteTitleText != null) RemoteTitleText.Text = LanguageManager.Instance.Text("remoteBranch");

        if (LocalBrowseBtn != null) LocalBrowseBtn.Content = LanguageManager.Instance.Text("chooseButton");
        if (BaseBrowseBtn != null) BaseBrowseBtn.Content = LanguageManager.Instance.Text("chooseButton");
        if (RemoteBrowseBtn != null) RemoteBrowseBtn.Content = LanguageManager.Instance.Text("chooseButton");
        if (LocalEmptyPickBtn != null) LocalEmptyPickBtn.Content = LanguageManager.Instance.Text("chooseButton");
        if (BaseEmptyPickBtn != null) BaseEmptyPickBtn.Content = LanguageManager.Instance.Text("chooseButton");
        if (RemoteEmptyPickBtn != null) RemoteEmptyPickBtn.Content = LanguageManager.Instance.Text("chooseButton");

        if (TakeLocalBtn != null) TakeLocalBtn.Content = LanguageManager.Instance.Text("acceptLocal");
        if (TakeBothBtn != null) TakeBothBtn.Content = LanguageManager.Instance.Text("takeBoth");
        if (TakeRemoteBtn != null) TakeRemoteBtn.Content = LanguageManager.Instance.Text("acceptRemote");
        if (TakeLocalRightBtn != null) TakeLocalRightBtn.Content = LanguageManager.Instance.Text("acceptLocal");
        if (TakeBothRightBtn != null) TakeBothRightBtn.Content = LanguageManager.Instance.Text("takeBoth");
        if (TakeRemoteRightBtn != null) TakeRemoteRightBtn.Content = LanguageManager.Instance.Text("acceptRemote");

        if (LocalEmptyTitle != null) LocalEmptyTitle.Text = LanguageManager.Instance.Text("noFileSelected");
        if (BaseEmptyTitle != null) BaseEmptyTitle.Text = LanguageManager.Instance.Text("noFileSelected");
        if (RemoteEmptyTitle != null) RemoteEmptyTitle.Text = LanguageManager.Instance.Text("noFileSelected");

        if (AutoResolveText != null) AutoResolveText.Text = LanguageManager.Instance.Text("autoResolveNonConflicts");
        if (ClearAllText != null) ClearAllText.Text = LanguageManager.Instance.Text("clearAll");
        if (SaveMergedText != null) SaveMergedText.Text = LanguageManager.Instance.Text("saveAndCompleteMerge");
        if (SaveMergedBottomText != null) SaveMergedBottomText.Text = LanguageManager.Instance.Text("saveAndCompleteMerge");
        if (OutputHeader != null) OutputHeader.Text = LanguageManager.Instance.Text("mergedOutputResult");

        if (PrevConflictBtn != null) ToolTipService.SetToolTip(PrevConflictBtn, LanguageManager.Instance.Text("prevConflict"));
        if (NextConflictBtn != null) ToolTipService.SetToolTip(NextConflictBtn, LanguageManager.Instance.Text("nextConflict"));
        if (AutoResolveBtn != null) ToolTipService.SetToolTip(AutoResolveBtn, LanguageManager.Instance.Text("autoResolveTooltip"));
        if (ClearAllBtn != null) ToolTipService.SetToolTip(ClearAllBtn, LanguageManager.Instance.Text("clearAllMergeTooltip"));
        if (SaveMergedBtn != null) ToolTipService.SetToolTip(SaveMergedBtn, LanguageManager.Instance.Text("saveMergedTooltip"));

        UpdateTitlesAndVisibility();
        UpdateConflictUI();
    }

    private void UpdateTitlesAndVisibility()
    {
        bool hasLocal = !string.IsNullOrEmpty(_localPath) || !string.IsNullOrEmpty(_localContent);
        bool hasBase = !string.IsNullOrEmpty(_basePath) || !string.IsNullOrEmpty(_baseContent);
        bool hasRemote = !string.IsNullOrEmpty(_remotePath) || !string.IsNullOrEmpty(_remoteContent);
        bool hasAnyFile = hasLocal || hasBase || hasRemote;

        if (MergeListContainer != null) MergeListContainer.Visibility = hasAnyFile ? Visibility.Visible : Visibility.Collapsed;

        if (LocalEmptyZone != null) LocalEmptyZone.Visibility = hasAnyFile ? Visibility.Collapsed : Visibility.Visible;
        if (BaseEmptyZone != null) BaseEmptyZone.Visibility = hasAnyFile ? Visibility.Collapsed : Visibility.Visible;
        if (RemoteEmptyZone != null) RemoteEmptyZone.Visibility = hasAnyFile ? Visibility.Collapsed : Visibility.Visible;
        if (EmptyDivider1 != null) EmptyDivider1.Visibility = hasAnyFile ? Visibility.Collapsed : Visibility.Visible;
        if (EmptyDivider2 != null) EmptyDivider2.Visibility = hasAnyFile ? Visibility.Collapsed : Visibility.Visible;

        if (LocalActionsPanel != null) LocalActionsPanel.Visibility = hasAnyFile ? Visibility.Visible : Visibility.Collapsed;
        if (RemoteActionsPanel != null) RemoteActionsPanel.Visibility = hasAnyFile ? Visibility.Visible : Visibility.Collapsed;
        if (BaseActionsPlaceholder != null) BaseActionsPlaceholder.Visibility = hasAnyFile ? Visibility.Visible : Visibility.Collapsed;

        string displayPath = _localPath ?? _remotePath ?? _basePath ?? "";
        string title = hasAnyFile
            ? (string.IsNullOrEmpty(displayPath) ? LanguageManager.Instance.Text("mergeWorkspace") : System.IO.Path.GetFileName(displayPath))
            : LanguageManager.Instance.Text("noFilesSelected");

        if (TopFilePathText != null) TopFilePathText.Text = title;
        if (OutputSubHeader != null) OutputSubHeader.Text = title;

        if (LocalSubText != null) LocalSubText.Text = string.IsNullOrEmpty(_localPath) ? LanguageManager.Instance.Text("localBranchCurrent") : System.IO.Path.GetFileName(_localPath);
        if (BaseSubText != null) BaseSubText.Text = string.IsNullOrEmpty(_basePath) ? LanguageManager.Instance.Text("baseBranchAncestor") : System.IO.Path.GetFileName(_basePath);
        if (RemoteSubText != null) RemoteSubText.Text = string.IsNullOrEmpty(_remotePath) ? LanguageManager.Instance.Text("remoteBranchIncoming") : System.IO.Path.GetFileName(_remotePath);

        if (ClearAllBtn != null) ClearAllBtn.IsEnabled = hasAnyFile;
        if (AutoResolveBtn != null) AutoResolveBtn.IsEnabled = hasAnyFile;
        if (SaveMergedBtn != null) SaveMergedBtn.IsEnabled = hasAnyFile;
        if (SaveMergedBottomBtn != null) SaveMergedBottomBtn.IsEnabled = hasAnyFile;
    }

    public void LoadFiles(string? localPath, string? basePath, string? remotePath)
    {
        var encoding = SettingsManager.Instance.GetEncoding();
        if (!string.IsNullOrEmpty(localPath) && System.IO.File.Exists(localPath))
        {
            _localPath = localPath;
            _localContent = System.IO.File.ReadAllText(localPath, encoding);
            if (LocalEditor != null) LocalEditor.Text = _localContent;
        }
        if (!string.IsNullOrEmpty(basePath) && System.IO.File.Exists(basePath))
        {
            _basePath = basePath;
            _baseContent = System.IO.File.ReadAllText(basePath, encoding);
            if (BaseEditor != null) BaseEditor.Text = _baseContent;
        }
        if (!string.IsNullOrEmpty(remotePath) && System.IO.File.Exists(remotePath))
        {
            _remotePath = remotePath;
            _remoteContent = System.IO.File.ReadAllText(remotePath, encoding);
            if (RemoteEditor != null) RemoteEditor.Text = _remoteContent;
        }

        UpdateTitlesAndVisibility();
        ComputeMerge();
        UpdateTabHeader();
    }

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateTitlesAndVisibility();
        ComputeMerge();
    }

    private void ComputeMerge()
    {
        string local = _localContent;
        string @base = _baseContent;
        string remote = _remoteContent;

        if (string.IsNullOrEmpty(local) && string.IsNullOrEmpty(@base) && string.IsNullOrEmpty(remote))
        {
            _lastResult = null;
            _displayItems.Clear();
            _conflictLineIndices.Clear();
            _currentConflictIndex = -1;
            if (MergeListView != null) MergeListView.ItemsSource = null;
            if (MergedOutputEditor != null) MergedOutputEditor.Text = "";
            if (ConflictCountText != null) ConflictCountText.Text = $"{LanguageManager.Instance.Text("conflictCountFormat")} (0 / 0)";
            if (PrevConflictBtn != null) PrevConflictBtn.IsEnabled = false;
            if (NextConflictBtn != null) NextConflictBtn.IsEnabled = false;
            if (ConflictStatusBadge != null) ConflictStatusBadge.Visibility = Visibility.Collapsed;
            if (MergeStatusDetail != null) MergeStatusDetail.Text = "";
            return;
        }

        _lastResult = DiffEngineService.Instance.MergeThreeWay(local, @base, remote);
        if (MergedOutputEditor != null)
        {
            MergedOutputEditor.Text = _lastResult.MergedText;
        }

        PopulateMergeListView();

        _currentConflictIndex = _conflictLineIndices.Count > 0 ? 0 : -1;
        UpdateConflictUI();
        if (_currentConflictIndex >= 0)
        {
            JumpToConflict(0);
        }
    }

    private void PopulateMergeListView()
    {
        if (_lastResult == null || _lastResult.Lines.Count == 0)
        {
            _displayItems.Clear();
            _conflictLineIndices.Clear();
            if (MergeListView != null) MergeListView.ItemsSource = null;
            return;
        }

        var items = new List<ThreeWayMergeLineDisplayItem>(_lastResult.Lines.Count);
        _conflictLineIndices.Clear();

        var transparentBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        var primaryFg = App.GetThemeBrush("TextFillColorPrimaryBrush");
        var warningBrush = App.GetThemeBrush("StatusModTextBrush");

        for (int i = 0; i < _lastResult.Lines.Count; i++)
        {
            var line = _lastResult.Lines[i];
            var item = new ThreeWayMergeLineDisplayItem
            {
                LineIndex = i,
                Status = line.Status,
                LocalLineNumText = line.LocalLineNumber.HasValue ? line.LocalLineNumber.Value.ToString() : string.Empty,
                BaseLineNumText = line.BaseLineNumber.HasValue ? line.BaseLineNumber.Value.ToString() : string.Empty,
                RemoteLineNumText = line.RemoteLineNumber.HasValue ? line.RemoteLineNumber.Value.ToString() : string.Empty,
                LocalText = line.ContentLocal,
                BaseText = line.ContentBase,
                RemoteText = line.ContentRemote
            };

            if (line.Status == "Conflict")
            {
                _conflictLineIndices.Add(i);

                // Local
                item.LocalSign = "!";
                item.LocalBgBrush = DelBg;
                item.LocalFgBrush = DelFg;

                // Base
                item.BaseSign = "!";
                item.BaseBgBrush = ModBg;
                item.BaseFgBrush = warningBrush;

                // Remote
                item.RemoteSign = "!";
                item.RemoteBgBrush = DelBg;
                item.RemoteFgBrush = DelFg;
            }
            else if (line.Status == "CleanLocal")
            {
                bool isAdd = !line.BaseLineNumber.HasValue;
                item.LocalSign = isAdd ? "+" : "M";
                item.LocalBgBrush = ModBg;
                item.LocalFgBrush = ModFg;

                item.BaseSign = " ";
                item.BaseBgBrush = line.BaseLineNumber.HasValue ? transparentBrush : PhantomBg;
                item.BaseFgBrush = line.BaseLineNumber.HasValue ? primaryFg : DimFg;

                item.RemoteSign = " ";
                item.RemoteBgBrush = line.RemoteLineNumber.HasValue ? transparentBrush : PhantomBg;
                item.RemoteFgBrush = line.RemoteLineNumber.HasValue ? primaryFg : DimFg;
            }
            else if (line.Status == "CleanRemote")
            {
                bool isAdd = !line.BaseLineNumber.HasValue;
                item.RemoteSign = isAdd ? "+" : "M";
                item.RemoteBgBrush = AddBg;
                item.RemoteFgBrush = AddFg;

                item.BaseSign = " ";
                item.BaseBgBrush = line.BaseLineNumber.HasValue ? transparentBrush : PhantomBg;
                item.BaseFgBrush = line.BaseLineNumber.HasValue ? primaryFg : DimFg;

                item.LocalSign = " ";
                item.LocalBgBrush = line.LocalLineNumber.HasValue ? transparentBrush : PhantomBg;
                item.LocalFgBrush = line.LocalLineNumber.HasValue ? primaryFg : DimFg;
            }
            else
            {
                item.LocalSign = " ";
                item.LocalBgBrush = line.LocalLineNumber.HasValue ? transparentBrush : PhantomBg;
                item.LocalFgBrush = line.LocalLineNumber.HasValue ? primaryFg : DimFg;

                item.BaseSign = " ";
                item.BaseBgBrush = line.BaseLineNumber.HasValue ? transparentBrush : PhantomBg;
                item.BaseFgBrush = line.BaseLineNumber.HasValue ? primaryFg : DimFg;

                item.RemoteSign = " ";
                item.RemoteBgBrush = line.RemoteLineNumber.HasValue ? transparentBrush : PhantomBg;
                item.RemoteFgBrush = line.RemoteLineNumber.HasValue ? primaryFg : DimFg;
            }

            items.Add(item);
        }

        _displayItems = items;
        if (MergeListView != null) MergeListView.ItemsSource = _displayItems;
    }

    private void UpdateConflictUI()
    {
        int total = _conflictLineIndices.Count;
        int currentNum = total > 0 ? (_currentConflictIndex >= 0 ? _currentConflictIndex + 1 : 1) : 0;
        if (ConflictCountText != null)
        {
            ConflictCountText.Text = $"{LanguageManager.Instance.Text("conflictCountFormat")} ({currentNum} / {total})";
        }

        if (PrevConflictBtn != null) PrevConflictBtn.IsEnabled = total > 0;
        if (NextConflictBtn != null) NextConflictBtn.IsEnabled = total > 0;

        bool hasAnyFile = !string.IsNullOrEmpty(_localPath) || !string.IsNullOrEmpty(_basePath) || !string.IsNullOrEmpty(_remotePath) ||
                          !string.IsNullOrEmpty(_localContent) || !string.IsNullOrEmpty(_baseContent) || !string.IsNullOrEmpty(_remoteContent);

        if (ConflictStatusBadge != null) ConflictStatusBadge.Visibility = hasAnyFile ? Visibility.Visible : Visibility.Collapsed;

        if (total > 0)
        {
            if (ConflictStatusIcon != null)
            {
                ConflictStatusIcon.Glyph = "";
                ConflictStatusIcon.Foreground = ConflictWarningBrush;
            }
            if (ConflictStatusText != null)
            {
                ConflictStatusText.Text = $"{total} {LanguageManager.Instance.Text("conflictsRemaining")}";
                ConflictStatusText.Foreground = ConflictWarningBrush;
            }
        }
        else if (hasAnyFile)
        {
            if (ConflictStatusIcon != null)
            {
                ConflictStatusIcon.Glyph = "";
                ConflictStatusIcon.Foreground = ConflictSuccessBrush;
            }
            if (ConflictStatusText != null)
            {
                ConflictStatusText.Text = LanguageManager.Instance.Text("allConflictsResolved");
                ConflictStatusText.Foreground = ConflictSuccessBrush;
            }
        }
    }

    private List<int> FindConflictIndices()
    {
        var indices = new List<int>();
        string text = MergedOutputEditor?.Text ?? "";
        int index = 0;
        while ((index = text.IndexOf("<<<<<<<", index, StringComparison.Ordinal)) != -1)
        {
            indices.Add(index);
            index += 7;
        }
        return indices;
    }

    private void JumpToConflict(int targetIndex)
    {
        if (_conflictLineIndices.Count == 0 || targetIndex < 0 || targetIndex >= _conflictLineIndices.Count) return;

        _currentConflictIndex = targetIndex;
        int lineIdx = _conflictLineIndices[_currentConflictIndex];

        if (MergeListView != null && lineIdx >= 0 && lineIdx < _displayItems.Count)
        {
            MergeListView.ScrollIntoView(_displayItems[lineIdx]);
            MergeListView.SelectedIndex = lineIdx;
        }

        var charPositions = FindConflictIndices();
        if (_currentConflictIndex >= 0 && _currentConflictIndex < charPositions.Count)
        {
            int charPos = charPositions[_currentConflictIndex];
            MergedOutputEditor?.Focus(FocusState.Programmatic);
            MergedOutputEditor?.Select(charPos, 7);
        }

        if (ConflictCountText != null)
        {
            ConflictCountText.Text = $"{LanguageManager.Instance.Text("conflictCountFormat")} ({_currentConflictIndex + 1} / {_conflictLineIndices.Count})";
        }
    }

    private void OnPrevConflictClicked(object sender, RoutedEventArgs e)
    {
        if (_conflictLineIndices.Count == 0) return;
        int next = _currentConflictIndex <= 0 ? _conflictLineIndices.Count - 1 : _currentConflictIndex - 1;
        JumpToConflict(next);
    }

    private void OnNextConflictClicked(object sender, RoutedEventArgs e)
    {
        if (_conflictLineIndices.Count == 0) return;
        int next = _currentConflictIndex >= _conflictLineIndices.Count - 1 ? 0 : _currentConflictIndex + 1;
        JumpToConflict(next);
    }

    private enum TakeChoice
    {
        Local,
        Both,
        Remote
    }

    private void OnTakeLocalClicked(object sender, RoutedEventArgs e)
    {
        ResolveConflict(TakeChoice.Local);
    }

    private void OnTakeBothClicked(object sender, RoutedEventArgs e)
    {
        ResolveConflict(TakeChoice.Both);
    }

    private void OnTakeRemoteClicked(object sender, RoutedEventArgs e)
    {
        ResolveConflict(TakeChoice.Remote);
    }

    private void ResolveConflict(TakeChoice choice)
    {
        if (_lastResult == null || _lastResult.Lines.Count == 0) return;

        int targetConflictIdx = -1;
        if (_currentConflictIndex >= 0 && _currentConflictIndex < _conflictLineIndices.Count)
        {
            int lineIdx = _conflictLineIndices[_currentConflictIndex];
            if (lineIdx >= 0 && lineIdx < _lastResult.Lines.Count)
            {
                targetConflictIdx = _lastResult.Lines[lineIdx].ConflictIndex ?? _currentConflictIndex;
            }
        }
        else if (MergeListView?.SelectedItem is ThreeWayMergeLineDisplayItem selItem)
        {
            if (selItem.LineIndex >= 0 && selItem.LineIndex < _lastResult.Lines.Count)
            {
                targetConflictIdx = _lastResult.Lines[selItem.LineIndex].ConflictIndex ?? -1;
            }
        }

        if (targetConflictIdx == -1)
        {
            var firstConflict = _lastResult.Lines.FirstOrDefault(l => l.Status == "Conflict");
            if (firstConflict != null)
            {
                targetConflictIdx = firstConflict.ConflictIndex ?? 0;
            }
        }

        if (targetConflictIdx == -1) return;

        for (int i = 0; i < _lastResult.Lines.Count; i++)
        {
            var line = _lastResult.Lines[i];
            if (line.ConflictIndex == targetConflictIdx)
            {
                switch (choice)
                {
                    case TakeChoice.Local:
                        line.ResolvedContent = line.ContentLocal;
                        line.Status = "CleanLocal";
                        break;
                    case TakeChoice.Both:
                        line.ResolvedContent = $"{line.ContentLocal}{Environment.NewLine}{line.ContentRemote}";
                        line.Status = "Unchanged";
                        break;
                    case TakeChoice.Remote:
                        line.ResolvedContent = line.ContentRemote;
                        line.Status = "CleanRemote";
                        break;
                }
            }
        }

        RebuildMergedText();
    }

    private void RebuildMergedText()
    {
        if (_lastResult == null) return;
        var lines = new List<string>();
        int remainingConflicts = 0;

        foreach (var line in _lastResult.Lines)
        {
            if (line.Status == "Conflict")
            {
                remainingConflicts++;
                lines.Add($"<<<<<<< Local{Environment.NewLine}{line.ContentLocal}{Environment.NewLine}======={Environment.NewLine}{line.ContentRemote}{Environment.NewLine}>>>>>>> Remote");
            }
            else if (line.ResolvedContent != null)
            {
                lines.Add(line.ResolvedContent);
            }
            else
            {
                lines.Add(line.ContentBase);
            }
        }

        _lastResult.MergedText = string.Join(Environment.NewLine, lines);
        _lastResult.ConflictCount = remainingConflicts;

        if (MergedOutputEditor != null)
        {
            MergedOutputEditor.Text = _lastResult.MergedText;
        }

        PopulateMergeListView();
        UpdateConflictUI();

        if (_conflictLineIndices.Count > 0)
        {
            if (_currentConflictIndex >= _conflictLineIndices.Count)
            {
                _currentConflictIndex = _conflictLineIndices.Count - 1;
            }
            else if (_currentConflictIndex < 0)
            {
                _currentConflictIndex = 0;
            }
            JumpToConflict(_currentConflictIndex);
        }
        else
        {
            _currentConflictIndex = -1;
        }
    }

    private void OnAutoResolveClicked(object sender, RoutedEventArgs e)
    {
        if (_lastResult == null || _lastResult.Lines.Count == 0) return;

        int resolvedCount = 0;
        foreach (var line in _lastResult.Lines)
        {
            if (line.Status == "CleanLocal")
            {
                line.ResolvedContent = line.ContentLocal;
                resolvedCount++;
            }
            else if (line.Status == "CleanRemote")
            {
                line.ResolvedContent = line.ContentRemote;
                resolvedCount++;
            }
        }

        RebuildMergedText();
        if (MergeStatusDetail != null)
        {
            MergeStatusDetail.Text = $"• Auto-resolved {resolvedCount} non-conflicting changes.";
        }
    }

    private void OnClearAllClicked(object sender, RoutedEventArgs e)
    {
        bool hasLocal = !string.IsNullOrEmpty(_localPath) || !string.IsNullOrEmpty(_localContent);
        bool hasBase = !string.IsNullOrEmpty(_basePath) || !string.IsNullOrEmpty(_baseContent);
        bool hasRemote = !string.IsNullOrEmpty(_remotePath) || !string.IsNullOrEmpty(_remoteContent);
        if (!hasLocal && !hasBase && !hasRemote)
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
        _localPath = null;
        _basePath = null;
        _remotePath = null;
        _localContent = string.Empty;
        _baseContent = string.Empty;
        _remoteContent = string.Empty;
        _currentConflictIndex = -1;
        _lastResult = null;
        _displayItems.Clear();
        _conflictLineIndices.Clear();

        if (LocalEditor != null) LocalEditor.Text = string.Empty;
        if (BaseEditor != null) BaseEditor.Text = string.Empty;
        if (RemoteEditor != null) RemoteEditor.Text = string.Empty;
        if (MergedOutputEditor != null) MergedOutputEditor.Text = string.Empty;
        if (MergeListView != null) MergeListView.ItemsSource = null;

        UpdateTitlesAndVisibility();
        ComputeMerge();
        UpdateTabHeader();
    }

    private async void OnPickLocalClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { "*" }, App.MainWindowInstance);
        if (file != null)
        {
            if (!CheckFileValidation(file.Path)) return;

            _localPath = file.Path;
            _localContent = await System.IO.File.ReadAllTextAsync(file.Path, SettingsManager.Instance.GetEncoding());
            if (LocalEditor != null) LocalEditor.Text = _localContent;
            UpdateTitlesAndVisibility();
            ComputeMerge();
            UpdateTabHeader();
        }
    }

    private async void OnPickBaseClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { "*" }, App.MainWindowInstance);
        if (file != null)
        {
            if (!CheckFileValidation(file.Path)) return;

            _basePath = file.Path;
            _baseContent = await System.IO.File.ReadAllTextAsync(file.Path, SettingsManager.Instance.GetEncoding());
            if (BaseEditor != null) BaseEditor.Text = _baseContent;
            UpdateTitlesAndVisibility();
            ComputeMerge();
            UpdateTabHeader();
        }
    }

    private async void OnPickRemoteClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { "*" }, App.MainWindowInstance);
        if (file != null)
        {
            if (!CheckFileValidation(file.Path)) return;

            _remotePath = file.Path;
            _remoteContent = await System.IO.File.ReadAllTextAsync(file.Path, SettingsManager.Instance.GetEncoding());
            if (RemoteEditor != null) RemoteEditor.Text = _remoteContent;
            UpdateTitlesAndVisibility();
            ComputeMerge();
            UpdateTabHeader();
        }
    }

    private async void OnSaveMergedClicked(object sender, RoutedEventArgs e)
    {
        var choices = new Dictionary<string, IList<string>>
        {
            { LanguageManager.Instance.Text("fileTypeTextCode"), new List<string> { ".txt", ".cs", ".rs", ".js", ".ts", ".json", ".md" } },
            { LanguageManager.Instance.Text("fileTypeAll"), new List<string> { "." } }
        };

        string suggestedFileName = !string.IsNullOrEmpty(_localPath)
            ? System.IO.Path.GetFileName(_localPath)
            : "merged_result.txt";

        var file = await PickerHelper.PickSaveFileAsync(".txt", choices, suggestedFileName, App.MainWindowInstance);
        if (file != null)
        {
            if (SettingsManager.Instance.CreateBakBackup && System.IO.File.Exists(file.Path))
            {
                try { System.IO.File.Copy(file.Path, file.Path + ".bak", true); } catch { }
            }
            await System.IO.File.WriteAllTextAsync(file.Path, MergedOutputEditor.Text, SettingsManager.Instance.GetEncoding());
            if (MergeStatusDetail != null)
            {
                MergeStatusDetail.Text = "• " + LanguageManager.Instance.Text("saveMergeSuccess");
            }
        }
    }

    // Drag & Drop Handlers
    private void OnLocalDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private void OnBaseDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private void OnRemoteDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void OnLocalDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        try
        {
            App.Log("[ThreeWayMergePage] OnLocalDrop triggered");
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                App.Log($"[ThreeWayMergePage] OnLocalDrop items count: {items?.Count ?? 0}");
                if (items != null && items.Count > 0)
                {
                    if (items.Count >= 2)
                    {
                        var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
                        HandleMultiDrop(paths);
                        return;
                    }

                    string targetPath = items[0].Path;
                    App.Log($"[ThreeWayMergePage] OnLocalDrop targetPath: {targetPath}");
                    if (!CheckFileValidation(targetPath)) return;

                    _localPath = targetPath;
                    _localContent = await System.IO.File.ReadAllTextAsync(targetPath, SettingsManager.Instance.GetEncoding());
                    if (LocalEditor != null) LocalEditor.Text = _localContent;
                    UpdateTitlesAndVisibility();
                    ComputeMerge();
                    UpdateTabHeader();
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"[ThreeWayMergePage] OnLocalDrop error: {ex.Message}");
        }
    }

    private async void OnBaseDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        try
        {
            App.Log("[ThreeWayMergePage] OnBaseDrop triggered");
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                App.Log($"[ThreeWayMergePage] OnBaseDrop items count: {items?.Count ?? 0}");
                if (items != null && items.Count > 0)
                {
                    if (items.Count >= 2)
                    {
                        var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
                        HandleMultiDrop(paths);
                        return;
                    }

                    string targetPath = items[0].Path;
                    App.Log($"[ThreeWayMergePage] OnBaseDrop targetPath: {targetPath}");
                    if (!CheckFileValidation(targetPath)) return;

                    _basePath = targetPath;
                    _baseContent = await System.IO.File.ReadAllTextAsync(targetPath, SettingsManager.Instance.GetEncoding());
                    if (BaseEditor != null) BaseEditor.Text = _baseContent;
                    UpdateTitlesAndVisibility();
                    ComputeMerge();
                    UpdateTabHeader();
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"[ThreeWayMergePage] OnBaseDrop error: {ex.Message}");
        }
    }

    private async void OnRemoteDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        try
        {
            App.Log("[ThreeWayMergePage] OnRemoteDrop triggered");
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                App.Log($"[ThreeWayMergePage] OnRemoteDrop items count: {items?.Count ?? 0}");
                if (items != null && items.Count > 0)
                {
                    if (items.Count >= 2)
                    {
                        var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
                        HandleMultiDrop(paths);
                        return;
                    }

                    string targetPath = items[0].Path;
                    App.Log($"[ThreeWayMergePage] OnRemoteDrop targetPath: {targetPath}");
                    if (!CheckFileValidation(targetPath)) return;

                    _remotePath = targetPath;
                    _remoteContent = await System.IO.File.ReadAllTextAsync(targetPath, SettingsManager.Instance.GetEncoding());
                    if (RemoteEditor != null) RemoteEditor.Text = _remoteContent;
                    UpdateTitlesAndVisibility();
                    ComputeMerge();
                    UpdateTabHeader();
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"[ThreeWayMergePage] OnRemoteDrop error: {ex.Message}");
        }
    }

    private void OnDiffContainerDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void OnDiffContainerDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        try
        {
            App.Log("[ThreeWayMergePage] OnDiffContainerDrop triggered");
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items != null && items.Count > 0)
                {
                    if (items.Count >= 2)
                    {
                        var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
                        HandleMultiDrop(paths);
                        return;
                    }

                    string targetPath = items[0].Path;
                    if (!CheckFileValidation(targetPath)) return;

                    var pos = e.GetPosition(MergeListContainer);
                    double w = MergeListContainer != null ? MergeListContainer.ActualWidth : 0;
                    int branch = 0;
                    if (w > 0)
                    {
                        if (pos.X < w / 3.0) branch = 0;
                        else if (pos.X < 2.0 * w / 3.0) branch = 1;
                        else branch = 2;
                    }
                    HandleDropFromWindow(targetPath, branch);
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"[ThreeWayMergePage] OnDiffContainerDrop error: {ex.Message}");
        }
    }

    public async void HandleMultiDrop(IReadOnlyList<string> paths)
    {
        if (paths == null || paths.Count == 0) return;
        try
        {
            var enc = SettingsManager.Instance.GetEncoding();
            if (paths.Count == 2)
            {
                if (!string.IsNullOrEmpty(paths[0]) && CheckFileValidation(paths[0]))
                {
                    _localPath = paths[0];
                    _localContent = await System.IO.File.ReadAllTextAsync(paths[0], enc);
                    if (LocalEditor != null) LocalEditor.Text = _localContent;
                }
                if (!string.IsNullOrEmpty(paths[1]) && CheckFileValidation(paths[1]))
                {
                    _remotePath = paths[1];
                    _remoteContent = await System.IO.File.ReadAllTextAsync(paths[1], enc);
                    if (RemoteEditor != null) RemoteEditor.Text = _remoteContent;
                }
            }
            else
            {
                if (paths.Count >= 1 && !string.IsNullOrEmpty(paths[0]) && CheckFileValidation(paths[0]))
                {
                    _localPath = paths[0];
                    _localContent = await System.IO.File.ReadAllTextAsync(paths[0], enc);
                    if (LocalEditor != null) LocalEditor.Text = _localContent;
                }
                if (paths.Count >= 2 && !string.IsNullOrEmpty(paths[1]) && CheckFileValidation(paths[1]))
                {
                    _basePath = paths[1];
                    _baseContent = await System.IO.File.ReadAllTextAsync(paths[1], enc);
                    if (BaseEditor != null) BaseEditor.Text = _baseContent;
                }
                if (paths.Count >= 3 && !string.IsNullOrEmpty(paths[2]) && CheckFileValidation(paths[2]))
                {
                    _remotePath = paths[2];
                    _remoteContent = await System.IO.File.ReadAllTextAsync(paths[2], enc);
                    if (RemoteEditor != null) RemoteEditor.Text = _remoteContent;
                }
            }
            UpdateTitlesAndVisibility();
            ComputeMerge();
            UpdateTabHeader();
        }
        catch (Exception ex)
        {
            App.Log($"[ThreeWayMergePage] HandleMultiDrop error: {ex.Message}");
        }
    }

    public async void HandleDropFromWindow(string path, int branch)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (!CheckFileValidation(path)) return;

        try
        {
            var enc = SettingsManager.Instance.GetEncoding();
            if (branch == 0)
            {
                _localPath = path;
                _localContent = await System.IO.File.ReadAllTextAsync(path, enc);
                if (LocalEditor != null) LocalEditor.Text = _localContent;
            }
            else if (branch == 1)
            {
                _basePath = path;
                _baseContent = await System.IO.File.ReadAllTextAsync(path, enc);
                if (BaseEditor != null) BaseEditor.Text = _baseContent;
            }
            else if (branch == 2)
            {
                _remotePath = path;
                _remoteContent = await System.IO.File.ReadAllTextAsync(path, enc);
                if (RemoteEditor != null) RemoteEditor.Text = _remoteContent;
            }
            UpdateTitlesAndVisibility();
            ComputeMerge();
            UpdateTabHeader();
        }
        catch (Exception ex)
        {
            App.Log($"[ThreeWayMergePage] HandleDropFromWindow error: {ex.Message}");
        }
    }

    private void UpdateTabHeader()
    {
        try
        {
            string? header = null;
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(_localPath)) parts.Add(System.IO.Path.GetFileName(_localPath));
            if (!string.IsNullOrEmpty(_basePath)) parts.Add(System.IO.Path.GetFileName(_basePath));
            if (!string.IsNullOrEmpty(_remotePath)) parts.Add(System.IO.Path.GetFileName(_remotePath));

            if (parts.Count > 0)
            {
                header = string.Join(" | ", parts);
            }

            var win = App.GetWindowForElement(this) as MainWindow ?? App.ActiveWindow as MainWindow ?? App.MainWindowInstance;
            win?.UpdateTabHeaderForPage(this, header);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateTabHeader error: {ex.Message}");
        }
    }

    private bool CheckFileValidation(string path)
    {
        var check = FileFormatValidator.Validate(path, CompareType.ThreeWayMerge);
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
            App.Log("[ThreeWayMergePage] Disposing resources...");
            LanguageManager.Instance.LanguageChanged -= UpdateLocalization;
            _localPath = null;
            _basePath = null;
            _remotePath = null;
            _localContent = string.Empty;
            _baseContent = string.Empty;
            _remoteContent = string.Empty;
            _lastResult = null;
            _displayItems.Clear();
            _conflictLineIndices.Clear();
            if (MergeListView != null) MergeListView.ItemsSource = null;
            if (LocalEditor != null) LocalEditor.Text = string.Empty;
            if (BaseEditor != null) BaseEditor.Text = string.Empty;
            if (RemoteEditor != null) RemoteEditor.Text = string.Empty;
            if (MergedOutputEditor != null) MergedOutputEditor.Text = string.Empty;
            App.Log("[ThreeWayMergePage] Dispose completed.");
        }
        catch (Exception ex)
        {
            App.Log($"[ThreeWayMergePage] Dispose exception: {ex.Message}");
        }
    }
}
