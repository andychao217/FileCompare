using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinCompare.Core.Models;
using WinCompare.Core.Services;
using WinCompare.UI.Helpers;
using Microsoft.UI.Xaml.Media;

namespace WinCompare.UI.Views;

public sealed partial class ThreeWayMergePage : Page, IDisposable
{
    private string? _localPath;
    private string? _basePath;
    private string? _remotePath;
    private int _currentConflictIndex = -1;

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
        ComputeMerge();
    }

    private void UpdateTitlesAndVisibility()
    {
        bool hasLocal = !string.IsNullOrEmpty(_localPath) || !string.IsNullOrEmpty(LocalEditor?.Text);
        bool hasBase = !string.IsNullOrEmpty(_basePath) || !string.IsNullOrEmpty(BaseEditor?.Text);
        bool hasRemote = !string.IsNullOrEmpty(_remotePath) || !string.IsNullOrEmpty(RemoteEditor?.Text);

        if (LocalEmptyZone != null) LocalEmptyZone.Visibility = hasLocal ? Visibility.Collapsed : Visibility.Visible;
        if (LocalEditor != null) LocalEditor.Visibility = hasLocal ? Visibility.Visible : Visibility.Collapsed;

        if (BaseEmptyZone != null) BaseEmptyZone.Visibility = hasBase ? Visibility.Collapsed : Visibility.Visible;
        if (BaseEditor != null) BaseEditor.Visibility = hasBase ? Visibility.Visible : Visibility.Collapsed;

        if (RemoteEmptyZone != null) RemoteEmptyZone.Visibility = hasRemote ? Visibility.Collapsed : Visibility.Visible;
        if (RemoteEditor != null) RemoteEditor.Visibility = hasRemote ? Visibility.Visible : Visibility.Collapsed;

        string displayPath = _localPath ?? _remotePath ?? _basePath ?? "";
        bool hasAnyFile = hasLocal || hasBase || hasRemote;
        string title = hasAnyFile
            ? (string.IsNullOrEmpty(displayPath) ? LanguageManager.Instance.Text("mergeWorkspace") : System.IO.Path.GetFileName(displayPath))
            : LanguageManager.Instance.Text("noFilesSelected");

        if (TopFilePathText != null) TopFilePathText.Text = title;
        if (OutputSubHeader != null) OutputSubHeader.Text = title;

        if (LocalSubText != null) LocalSubText.Text = string.IsNullOrEmpty(_localPath) ? LanguageManager.Instance.Text("localBranchCurrent") : System.IO.Path.GetFileName(_localPath);
        if (BaseSubText != null) BaseSubText.Text = string.IsNullOrEmpty(_basePath) ? LanguageManager.Instance.Text("baseBranchAncestor") : System.IO.Path.GetFileName(_basePath);
        if (RemoteSubText != null) RemoteSubText.Text = string.IsNullOrEmpty(_remotePath) ? LanguageManager.Instance.Text("remoteBranchIncoming") : System.IO.Path.GetFileName(_remotePath);

        if (ClearAllBtn != null) ClearAllBtn.IsEnabled = hasAnyFile;
    }

    public void LoadFiles(string? localPath, string? basePath, string? remotePath)
    {
        var encoding = SettingsManager.Instance.GetEncoding();
        if (!string.IsNullOrEmpty(localPath) && System.IO.File.Exists(localPath))
        {
            _localPath = localPath;
            if (LocalEditor != null) LocalEditor.Text = System.IO.File.ReadAllText(localPath, encoding);
        }
        if (!string.IsNullOrEmpty(basePath) && System.IO.File.Exists(basePath))
        {
            _basePath = basePath;
            if (BaseEditor != null) BaseEditor.Text = System.IO.File.ReadAllText(basePath, encoding);
        }
        if (!string.IsNullOrEmpty(remotePath) && System.IO.File.Exists(remotePath))
        {
            _remotePath = remotePath;
            if (RemoteEditor != null) RemoteEditor.Text = System.IO.File.ReadAllText(remotePath, encoding);
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
        string local = LocalEditor?.Text ?? "";
        string @base = BaseEditor?.Text ?? "";
        string remote = RemoteEditor?.Text ?? "";

        if (string.IsNullOrEmpty(local) && string.IsNullOrEmpty(@base) && string.IsNullOrEmpty(remote))
        {
            if (MergedOutputEditor != null) MergedOutputEditor.Text = "";
            if (ConflictCountText != null) ConflictCountText.Text = $"{LanguageManager.Instance.Text("conflictCountFormat")} (0 / 0)";
            if (PrevConflictBtn != null) PrevConflictBtn.IsEnabled = false;
            if (NextConflictBtn != null) NextConflictBtn.IsEnabled = false;
            if (ConflictStatusBadge != null) ConflictStatusBadge.Visibility = Visibility.Collapsed;
            if (MergeStatusDetail != null) MergeStatusDetail.Text = "";
            return;
        }

        var result = DiffEngineService.Instance.MergeThreeWay(local, @base, remote);
        if (MergedOutputEditor != null)
        {
            MergedOutputEditor.Text = result.MergedText;
        }

        int totalConflicts = result.Conflicts.Count;
        int currentNum = totalConflicts > 0 ? (_currentConflictIndex >= 0 ? _currentConflictIndex + 1 : 1) : 0;
        if (ConflictCountText != null)
        {
            ConflictCountText.Text = $"{LanguageManager.Instance.Text("conflictCountFormat")} ({currentNum} / {totalConflicts})";
        }

        if (PrevConflictBtn != null) PrevConflictBtn.IsEnabled = totalConflicts > 0;
        if (NextConflictBtn != null) NextConflictBtn.IsEnabled = totalConflicts > 0;

        if (ConflictStatusBadge != null) ConflictStatusBadge.Visibility = Visibility.Visible;
        if (result.HasConflicts)
        {
            if (ConflictStatusIcon != null)
            {
                ConflictStatusIcon.Glyph = "\uE7BA";
                ConflictStatusIcon.Foreground = ConflictWarningBrush;
            }
            if (ConflictStatusText != null)
            {
                ConflictStatusText.Text = $"{totalConflicts} {LanguageManager.Instance.Text("conflictsRemaining")}";
                ConflictStatusText.Foreground = ConflictWarningBrush;
            }
        }
        else
        {
            if (ConflictStatusIcon != null)
            {
                ConflictStatusIcon.Glyph = "\uE73E";
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
        var list = FindConflictIndices();
        if (list.Count == 0 || targetIndex < 0 || targetIndex >= list.Count) return;

        _currentConflictIndex = targetIndex;
        int charPos = list[_currentConflictIndex];
        MergedOutputEditor?.Focus(FocusState.Programmatic);
        MergedOutputEditor?.Select(charPos, 7);

        if (ConflictCountText != null)
        {
            ConflictCountText.Text = $"{LanguageManager.Instance.Text("conflictCountFormat")} ({_currentConflictIndex + 1} / {list.Count})";
        }
    }

    private void OnPrevConflictClicked(object sender, RoutedEventArgs e)
    {
        var list = FindConflictIndices();
        if (list.Count == 0) return;
        int next = _currentConflictIndex <= 0 ? list.Count - 1 : _currentConflictIndex - 1;
        JumpToConflict(next);
    }

    private void OnNextConflictClicked(object sender, RoutedEventArgs e)
    {
        var list = FindConflictIndices();
        if (list.Count == 0) return;
        int next = _currentConflictIndex >= list.Count - 1 ? 0 : _currentConflictIndex + 1;
        JumpToConflict(next);
    }

    private void OnAutoResolveClicked(object sender, RoutedEventArgs e)
    {
        string local = LocalEditor?.Text ?? "";
        string @base = BaseEditor?.Text ?? "";
        string remote = RemoteEditor?.Text ?? "";

        var result = DiffEngineService.Instance.MergeThreeWay(local, @base, remote);
        if (MergedOutputEditor != null)
        {
            MergedOutputEditor.Text = result.MergedText;
        }

        ComputeMerge();
    }

    private void OnClearAllClicked(object sender, RoutedEventArgs e)
    {
        bool hasLocal = !string.IsNullOrEmpty(_localPath) || !string.IsNullOrEmpty(LocalEditor?.Text);
        bool hasBase = !string.IsNullOrEmpty(_basePath) || !string.IsNullOrEmpty(BaseEditor?.Text);
        bool hasRemote = !string.IsNullOrEmpty(_remotePath) || !string.IsNullOrEmpty(RemoteEditor?.Text);
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
        _currentConflictIndex = -1;

        if (LocalEditor != null) LocalEditor.Text = string.Empty;
        if (BaseEditor != null) BaseEditor.Text = string.Empty;
        if (RemoteEditor != null) RemoteEditor.Text = string.Empty;
        if (MergedOutputEditor != null) MergedOutputEditor.Text = string.Empty;

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
            if (LocalEditor != null) LocalEditor.Text = await System.IO.File.ReadAllTextAsync(file.Path, SettingsManager.Instance.GetEncoding());
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
            if (BaseEditor != null) BaseEditor.Text = await System.IO.File.ReadAllTextAsync(file.Path, SettingsManager.Instance.GetEncoding());
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
            if (RemoteEditor != null) RemoteEditor.Text = await System.IO.File.ReadAllTextAsync(file.Path, SettingsManager.Instance.GetEncoding());
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
                MergeStatusDetail.Text = "\u2022 " + LanguageManager.Instance.Text("saveMergeSuccess");
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
                    if (LocalEditor != null) LocalEditor.Text = await System.IO.File.ReadAllTextAsync(targetPath, SettingsManager.Instance.GetEncoding());
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
                    if (BaseEditor != null) BaseEditor.Text = await System.IO.File.ReadAllTextAsync(targetPath, SettingsManager.Instance.GetEncoding());
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
                    if (RemoteEditor != null) RemoteEditor.Text = await System.IO.File.ReadAllTextAsync(targetPath, SettingsManager.Instance.GetEncoding());
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

    public async void HandleMultiDrop(IReadOnlyList<string> paths)
    {
        if (paths == null || paths.Count == 0) return;
        try
        {
            var enc = SettingsManager.Instance.GetEncoding();
            if (paths.Count >= 1 && !string.IsNullOrEmpty(paths[0]) && CheckFileValidation(paths[0]))
            {
                _localPath = paths[0];
                if (LocalEditor != null) LocalEditor.Text = await System.IO.File.ReadAllTextAsync(paths[0], enc);
            }
            if (paths.Count >= 2 && !string.IsNullOrEmpty(paths[1]) && CheckFileValidation(paths[1]))
            {
                _basePath = paths[1];
                if (BaseEditor != null) BaseEditor.Text = await System.IO.File.ReadAllTextAsync(paths[1], enc);
            }
            if (paths.Count >= 3 && !string.IsNullOrEmpty(paths[2]) && CheckFileValidation(paths[2]))
            {
                _remotePath = paths[2];
                if (RemoteEditor != null) RemoteEditor.Text = await System.IO.File.ReadAllTextAsync(paths[2], enc);
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
                if (LocalEditor != null) LocalEditor.Text = await System.IO.File.ReadAllTextAsync(path, enc);
            }
            else if (branch == 1)
            {
                _basePath = path;
                if (BaseEditor != null) BaseEditor.Text = await System.IO.File.ReadAllTextAsync(path, enc);
            }
            else if (branch == 2)
            {
                _remotePath = path;
                if (RemoteEditor != null) RemoteEditor.Text = await System.IO.File.ReadAllTextAsync(path, enc);
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
