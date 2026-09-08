using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

        // Initialize empty state without mock data
        LocalEditor.Text = string.Empty;
        BaseEditor.Text = string.Empty;
        RemoteEditor.Text = string.Empty;
        if (MergedOutputEditor != null) MergedOutputEditor.Text = string.Empty;
        if (ConflictBadge != null) ConflictBadge.Visibility = Visibility.Collapsed;
        if (ResolvedBadge != null) ResolvedBadge.Visibility = Visibility.Visible;
        if (ResolvedText != null) ResolvedText.Text = LanguageManager.Instance.Text("threeWayReady");
    }

    private void UpdateLocalization()
    {
        OpenFilesBtn.Label = LanguageManager.Instance.Text("openThreeWayFiles");
        RecomputeMergeBtn.Label = LanguageManager.Instance.Text("recomputeMerge");
        SaveMergedBtn.Label = LanguageManager.Instance.Text("saveMergeResult");
        AcceptAllLocalBtn.Label = LanguageManager.Instance.Text("acceptAllLocal");
        AcceptAllRemoteBtn.Label = LanguageManager.Instance.Text("acceptAllRemote");
        if (PrevConflictBtn != null) PrevConflictBtn.Label = LanguageManager.Instance.Text("prevConflict");
        if (NextConflictBtn != null) NextConflictBtn.Label = LanguageManager.Instance.Text("nextConflict");
        if (AutoResolveBtn != null) AutoResolveBtn.Label = LanguageManager.Instance.Text("autoResolveNonConflicts");
        if (TakeBothBtn != null) TakeBothBtn.Label = LanguageManager.Instance.Text("takeBoth");
        if (ClearAllBtn != null) ClearAllBtn.Label = LanguageManager.Instance.Text("clearAll");

        LocalBrowseBtn.Content = LanguageManager.Instance.Text("browse");
        BaseBrowseBtn.Content = LanguageManager.Instance.Text("browse");
        RemoteBrowseBtn.Content = LanguageManager.Instance.Text("browse");
        OutputHeader.Text = LanguageManager.Instance.Text("mergeOutputHeader");
        SaveToFileBtn.Content = LanguageManager.Instance.Text("saveToFile");

        if (string.IsNullOrEmpty(_localPath))
        {
            LocalHeader.Text = LanguageManager.Instance.Text("mergeLocalHeader");
        }
        if (string.IsNullOrEmpty(_basePath))
        {
            BaseHeader.Text = LanguageManager.Instance.Text("mergeBaseHeader");
        }
        if (string.IsNullOrEmpty(_remotePath))
        {
            RemoteHeader.Text = LanguageManager.Instance.Text("mergeRemoteHeader");
        }

        ComputeMerge();
    }

    public void LoadFiles(string? localPath, string? basePath, string? remotePath)
    {
        var encoding = SettingsManager.Instance.GetEncoding();
        if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
        {
            _localPath = localPath;
            LocalHeader.Text = Path.GetFileName(localPath);
            LocalEditor.Text = File.ReadAllText(localPath, encoding);
        }
        if (!string.IsNullOrEmpty(basePath) && File.Exists(basePath))
        {
            _basePath = basePath;
            BaseHeader.Text = Path.GetFileName(basePath);
            BaseEditor.Text = File.ReadAllText(basePath, encoding);
        }
        if (!string.IsNullOrEmpty(remotePath) && File.Exists(remotePath))
        {
            _remotePath = remotePath;
            RemoteHeader.Text = Path.GetFileName(remotePath);
            RemoteEditor.Text = File.ReadAllText(remotePath, encoding);
        }
        ComputeMerge();
    }

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        ComputeMerge();
    }

    private void OnRecomputeMergeClicked(object sender, RoutedEventArgs e)
    {
        ComputeMerge();
    }

    private void ComputeMerge()
    {
        string local = LocalEditor?.Text ?? "";
        string @base = BaseEditor?.Text ?? "";
        string remote = RemoteEditor?.Text ?? "";

        var result = DiffEngineService.Instance.MergeThreeWay(local, @base, remote);

        if (MergedOutputEditor != null)
        {
            MergedOutputEditor.Text = result.MergedText;
        }

        if (result.HasConflicts)
        {
            if (ConflictBadge != null) ConflictBadge.Visibility = Visibility.Visible;
            if (ResolvedBadge != null) ResolvedBadge.Visibility = Visibility.Collapsed;
            if (ConflictCountText != null) ConflictCountText.Text = string.Format(LanguageManager.Instance.Text("conflictCountBadge"), result.Conflicts.Count);
            if (MergeStatusDetail != null) MergeStatusDetail.Text = $"({result.Conflicts.Count})";
        }
        else
        {
            if (ConflictBadge != null) ConflictBadge.Visibility = Visibility.Collapsed;
            if (ResolvedBadge != null) ResolvedBadge.Visibility = Visibility.Visible;
            if (ResolvedText != null) ResolvedText.Text = LanguageManager.Instance.Text("threeWayReady");
            if (MergeStatusDetail != null) MergeStatusDetail.Text = "";
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
            string label = LanguageManager.Instance.Text("prevConflict");
            ConflictCountText.Text = $"{label}: {_currentConflictIndex + 1} / {list.Count}";
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

        if (!result.HasConflicts)
        {
            if (ConflictBadge != null) ConflictBadge.Visibility = Visibility.Collapsed;
            if (ResolvedBadge != null) ResolvedBadge.Visibility = Visibility.Visible;
            if (ResolvedText != null) ResolvedText.Text = LanguageManager.Instance.Text("threeWayReady");
        }
    }

    private void OnTakeBothClicked(object sender, RoutedEventArgs e)
    {
        string text = MergedOutputEditor?.Text ?? "";
        var regex = new System.Text.RegularExpressions.Regex(@"<<<<<<< LOCAL\r?\n(.*?)\r?\n=======\r?\n(.*?)\r?\n>>>>>>> REMOTE", System.Text.RegularExpressions.RegexOptions.Singleline);
        string resolved = regex.Replace(text, match =>
        {
            string loc = match.Groups[1].Value;
            string rem = match.Groups[2].Value;
            return $"{loc}\n{rem}";
        });

        if (MergedOutputEditor != null) MergedOutputEditor.Text = resolved;
        if (ConflictBadge != null) ConflictBadge.Visibility = Visibility.Collapsed;
        if (ResolvedBadge != null) ResolvedBadge.Visibility = Visibility.Visible;
        if (ResolvedText != null) ResolvedText.Text = LanguageManager.Instance.Text("takeBoth");
    }

    private void OnClearAllClicked(object sender, RoutedEventArgs e)
    {
        _localPath = null;
        _basePath = null;
        _remotePath = null;
        _currentConflictIndex = -1;
        if (LocalHeader != null) LocalHeader.Text = LanguageManager.Instance.Text("mergeLocalHeader");
        if (BaseHeader != null) BaseHeader.Text = LanguageManager.Instance.Text("mergeBaseHeader");
        if (RemoteHeader != null) RemoteHeader.Text = LanguageManager.Instance.Text("mergeRemoteHeader");
        if (LocalEditor != null) LocalEditor.Text = string.Empty;
        if (BaseEditor != null) BaseEditor.Text = string.Empty;
        if (RemoteEditor != null) RemoteEditor.Text = string.Empty;
        if (MergedOutputEditor != null) MergedOutputEditor.Text = string.Empty;
        if (ConflictBadge != null) ConflictBadge.Visibility = Visibility.Collapsed;
        if (ResolvedBadge != null) ResolvedBadge.Visibility = Visibility.Visible;
        if (ResolvedText != null) ResolvedText.Text = LanguageManager.Instance.Text("threeWayReady");
    }

    private void OnAcceptAllLocalClicked(object sender, RoutedEventArgs e)
    {
        if (MergedOutputEditor != null && LocalEditor != null)
        {
            MergedOutputEditor.Text = LocalEditor.Text;
            if (ConflictBadge != null) ConflictBadge.Visibility = Visibility.Collapsed;
            if (ResolvedBadge != null) ResolvedBadge.Visibility = Visibility.Visible;
            if (ResolvedText != null) ResolvedText.Text = LanguageManager.Instance.Text("acceptAllLocal");
        }
    }

    private void OnAcceptAllRemoteClicked(object sender, RoutedEventArgs e)
    {
        if (MergedOutputEditor != null && RemoteEditor != null)
        {
            MergedOutputEditor.Text = RemoteEditor.Text;
            if (ConflictBadge != null) ConflictBadge.Visibility = Visibility.Collapsed;
            if (ResolvedBadge != null) ResolvedBadge.Visibility = Visibility.Visible;
            if (ResolvedText != null) ResolvedText.Text = LanguageManager.Instance.Text("acceptAllRemote");
        }
    }

    private async void OnPickLocalClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { "*" }, App.MainWindowInstance);
        if (file != null)
        {
            _localPath = file.Path;
            LocalHeader.Text = file.Name;
            LocalEditor.Text = await File.ReadAllTextAsync(file.Path, SettingsManager.Instance.GetEncoding());
            ComputeMerge();
        }
    }

    private async void OnPickBaseClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { "*" }, App.MainWindowInstance);
        if (file != null)
        {
            _basePath = file.Path;
            BaseHeader.Text = file.Name;
            BaseEditor.Text = await File.ReadAllTextAsync(file.Path, SettingsManager.Instance.GetEncoding());
            ComputeMerge();
        }
    }

    private async void OnPickRemoteClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { "*" }, App.MainWindowInstance);
        if (file != null)
        {
            _remotePath = file.Path;
            RemoteHeader.Text = file.Name;
            RemoteEditor.Text = await File.ReadAllTextAsync(file.Path, SettingsManager.Instance.GetEncoding());
            ComputeMerge();
        }
    }

    private async void OnOpenFilesClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { "*" }, App.MainWindowInstance);
        if (file != null)
        {
            _localPath = file.Path;
            LocalHeader.Text = file.Name;
            LocalEditor.Text = await File.ReadAllTextAsync(file.Path, SettingsManager.Instance.GetEncoding());
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

        var file = await PickerHelper.PickSaveFileAsync(".txt", choices, "merged_result.txt", App.MainWindowInstance);
        if (file != null)
        {
            if (SettingsManager.Instance.CreateBakBackup && File.Exists(file.Path))
            {
                try { File.Copy(file.Path, file.Path + ".bak", true); } catch { }
            }
            await File.WriteAllTextAsync(file.Path, MergedOutputEditor.Text, SettingsManager.Instance.GetEncoding());
            if (ResolvedText != null)
            {
                ResolvedText.Text = LanguageManager.Instance.Text("saveMergeSuccess");
            }
        }
    }
}
