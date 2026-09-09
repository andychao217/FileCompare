using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinCompare.Core.Models;
using WinCompare.Core.Services;
using WinCompare.UI.Helpers;

namespace WinCompare.UI.Views;

public sealed partial class ThreeWayMergePage : Page
{
    private string? _localPath;
    private string? _basePath;
    private string? _remotePath;
    private int _currentConflictIndex = -1;

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

        if (LocalEmptyTitle != null) LocalEmptyTitle.Text = LanguageManager.Instance.Text("noFileSelected");
        if (BaseEmptyTitle != null) BaseEmptyTitle.Text = LanguageManager.Instance.Text("noFileSelected");
        if (RemoteEmptyTitle != null) RemoteEmptyTitle.Text = LanguageManager.Instance.Text("noFileSelected");

        if (AutoResolveText != null) AutoResolveText.Text = LanguageManager.Instance.Text("autoResolveNonConflicts");
        if (ClearAllText != null) ClearAllText.Text = LanguageManager.Instance.Text("clearAll");
        if (SaveMergedText != null) SaveMergedText.Text = LanguageManager.Instance.Text("saveAndCompleteMerge");
        if (SaveMergedBottomText != null) SaveMergedBottomText.Text = LanguageManager.Instance.Text("saveAndCompleteMerge");
        if (OutputHeader != null) OutputHeader.Text = LanguageManager.Instance.Text("mergedOutputResult");

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
            ? (string.IsNullOrEmpty(displayPath) ? "合并工作区" : System.IO.Path.GetFileName(displayPath))
            : LanguageManager.Instance.Text("noFilesSelected");

        if (TopFilePathText != null) TopFilePathText.Text = title;
        if (OutputSubHeader != null) OutputSubHeader.Text = title;

        if (LocalSubText != null) LocalSubText.Text = string.IsNullOrEmpty(_localPath) ? "Local (Current)" : System.IO.Path.GetFileName(_localPath);
        if (BaseSubText != null) BaseSubText.Text = string.IsNullOrEmpty(_basePath) ? "Base (Ancestor)" : System.IO.Path.GetFileName(_basePath);
        if (RemoteSubText != null) RemoteSubText.Text = string.IsNullOrEmpty(_remotePath) ? "Remote (Incoming)" : System.IO.Path.GetFileName(_remotePath);
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

        if (result.HasConflicts)
        {
            if (MergeStatusDetail != null) MergeStatusDetail.Text = $"• {totalConflicts} 个冲突待解决";
        }
        else
        {
            if (MergeStatusDetail != null) MergeStatusDetail.Text = "• 所有冲突已解决";
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
    }

    private async void OnPickLocalClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { "*" }, App.MainWindowInstance);
        if (file != null)
        {
            _localPath = file.Path;
            if (LocalEditor != null) LocalEditor.Text = await System.IO.File.ReadAllTextAsync(file.Path, SettingsManager.Instance.GetEncoding());
            UpdateTitlesAndVisibility();
            ComputeMerge();
        }
    }

    private async void OnPickBaseClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { "*" }, App.MainWindowInstance);
        if (file != null)
        {
            _basePath = file.Path;
            if (BaseEditor != null) BaseEditor.Text = await System.IO.File.ReadAllTextAsync(file.Path, SettingsManager.Instance.GetEncoding());
            UpdateTitlesAndVisibility();
            ComputeMerge();
        }
    }

    private async void OnPickRemoteClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { "*" }, App.MainWindowInstance);
        if (file != null)
        {
            _remotePath = file.Path;
            if (RemoteEditor != null) RemoteEditor.Text = await System.IO.File.ReadAllTextAsync(file.Path, SettingsManager.Instance.GetEncoding());
            UpdateTitlesAndVisibility();
            ComputeMerge();
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
    private void OnLocalDragOver(object sender, DragEventArgs e) => e.AcceptedOperation = DataPackageOperation.Copy;
    private void OnBaseDragOver(object sender, DragEventArgs e) => e.AcceptedOperation = DataPackageOperation.Copy;
    private void OnRemoteDragOver(object sender, DragEventArgs e) => e.AcceptedOperation = DataPackageOperation.Copy;

    private async void OnLocalDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                _localPath = file.Path;
                if (LocalEditor != null) LocalEditor.Text = await System.IO.File.ReadAllTextAsync(file.Path, SettingsManager.Instance.GetEncoding());
                UpdateTitlesAndVisibility();
                ComputeMerge();
            }
        }
    }

    private async void OnBaseDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                _basePath = file.Path;
                if (BaseEditor != null) BaseEditor.Text = await System.IO.File.ReadAllTextAsync(file.Path, SettingsManager.Instance.GetEncoding());
                UpdateTitlesAndVisibility();
                ComputeMerge();
            }
        }
    }

    private async void OnRemoteDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                _remotePath = file.Path;
                if (RemoteEditor != null) RemoteEditor.Text = await System.IO.File.ReadAllTextAsync(file.Path, SettingsManager.Instance.GetEncoding());
                UpdateTitlesAndVisibility();
                ComputeMerge();
            }
        }
    }
}
