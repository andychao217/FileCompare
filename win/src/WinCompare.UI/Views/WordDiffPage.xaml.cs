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

public sealed partial class WordDiffPage : Page
{
    private string? _leftPath;
    private string? _rightPath;
    private WordDocument? _leftDoc;
    private WordDocument? _rightDoc;
    private List<WordDiffParagraphDisplayItem> _allParagraphs = new();
    private List<WordDiffParagraphDisplayItem> _displayParagraphs = new();
    private int _currentDiffJumpIndex = -1;
    private string _searchQuery = string.Empty;
    private bool _isSidebarVisible = true;

    private static readonly Brush ModBg = new SolidColorBrush(Windows.UI.Color.FromArgb(35, 234, 179, 8));
    private static readonly Brush ModFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 250, 204, 21));
    private static readonly Brush AddBg = new SolidColorBrush(Windows.UI.Color.FromArgb(35, 34, 197, 94));
    private static readonly Brush AddFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 74, 222, 128));
    private static readonly Brush DelBg = new SolidColorBrush(Windows.UI.Color.FromArgb(35, 239, 68, 68));
    private static readonly Brush DelFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 113, 113));
    private static readonly Brush SameBg = new SolidColorBrush(Windows.UI.Color.FromArgb(15, 156, 163, 175));
    private static readonly Brush SameFg = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 156, 163, 175));

    public WordDiffPage()
    {
        this.InitializeComponent();
        UpdateLocalization();
        LanguageManager.Instance.LanguageChanged += UpdateLocalization;
    }

    private void UpdateLocalization()
    {
        OpenFileBtn.Label = LanguageManager.Instance.Text("openWordFiles");
        PickLeftDocItem.Text = LanguageManager.Instance.Text("pickLeftDoc");
        PickRightDocItem.Text = LanguageManager.Instance.Text("pickRightDoc");
        PrevDiffBtn.Label = LanguageManager.Instance.Text("prevDiff");
        NextDiffBtn.Label = LanguageManager.Instance.Text("nextDiff");
        ToggleSidebarBtn.Label = LanguageManager.Instance.Text("docOutline");
        ViewModeItem0.Content = LanguageManager.Instance.Text("viewStructuredContent");
        ViewModeItem1.Content = LanguageManager.Instance.Text("viewTableDiff");
        ViewModeItem2.Content = LanguageManager.Instance.Text("viewMetadataDiff");
        if (SwapSidesBtn != null) SwapSidesBtn.Label = LanguageManager.Instance.Text("swapSides");
        if (ExportReportBtn != null) ExportReportBtn.Label = LanguageManager.Instance.Text("exportDiff");
        if (ClearAllBtn != null) ClearAllBtn.Label = LanguageManager.Instance.Text("clearAll");
        if (WordSearchBox != null) WordSearchBox.PlaceholderText = LanguageManager.Instance.Text("searchOrFilter");

        LeftBrowseBtn.Content = LanguageManager.Instance.Text("browse");
        RightBrowseBtn.Content = LanguageManager.Instance.Text("browse");
        OutlineHeader.Text = LanguageManager.Instance.Text("docOutline");
        MetadataHeader.Text = LanguageManager.Instance.Text("metaTitle");
        EmptyPromptText.Text = LanguageManager.Instance.Text("wordEmptyPrompt");
        EmptyActionBtn.Content = LanguageManager.Instance.Text("wordEmptyAction");

        if (string.IsNullOrEmpty(_leftPath))
        {
            LeftDocPathText.Text = LanguageManager.Instance.Text("leftDocPrompt");
        }
        if (string.IsNullOrEmpty(_rightPath))
        {
            RightDocPathText.Text = LanguageManager.Instance.Text("rightDocPrompt");
        }
        if (_leftDoc == null)
        {
            MetadataDetailsText.Text = LanguageManager.Instance.Text("metaNotLoaded");
            StatusBarText.Text = LanguageManager.Instance.Text("notCompared");
            DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountDocBadge"), 0);
        }
        else
        {
            int adds = _displayParagraphs.Count(p => p.StatusBgBrush == AddBg);
            int dels = _displayParagraphs.Count(p => p.StatusBgBrush == DelBg);
            int mods = _displayParagraphs.Count(p => p.StatusBgBrush == ModBg);
            int totalDiffs = adds + dels + mods;

            foreach (var item in _displayParagraphs)
            {
                if (item.StatusBgBrush == AddBg)
                    item.StatusText = LanguageManager.Instance.Text("statusAdded");
                else if (item.StatusBgBrush == DelBg)
                    item.StatusText = LanguageManager.Instance.Text("statusDeleted");
                else if (item.StatusBgBrush == ModBg)
                    item.StatusText = LanguageManager.Instance.Text("statusModified");
                else
                    item.StatusText = LanguageManager.Instance.Text("statusIdentical");
            }

            DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountDocBadge"), totalDiffs);
            int outlineNodes = OutlineTree.RootNodes.Count;
            StatusBarText.Text = string.Format(LanguageManager.Instance.Text("wordSummaryStats"), _displayParagraphs.Count, outlineNodes);
        }
    }

    public void LoadFiles(string? leftPath, string? rightPath)
    {
        if (!string.IsNullOrEmpty(leftPath) && File.Exists(leftPath))
        {
            _leftPath = leftPath;
            LeftDocPathText.Text = leftPath;
        }

        if (!string.IsNullOrEmpty(rightPath) && File.Exists(rightPath))
        {
            _rightPath = rightPath;
            RightDocPathText.Text = rightPath;
        }

        if (!string.IsNullOrEmpty(_leftPath) && !string.IsNullOrEmpty(_rightPath))
        {
            RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.WordDiff);
            ParseAndCompare();
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

    private void ParseAndCompare()
    {
        if (string.IsNullOrEmpty(_leftPath) || string.IsNullOrEmpty(_rightPath)) return;

        EmptyStatePanel.Visibility = Visibility.Collapsed;

        _leftDoc = WordDocumentParser.Parse(_leftPath);
        _rightDoc = WordDocumentParser.Parse(_rightPath);

        // 1. Build Outline Tree from headings
        BuildOutlineTree();

        // 2. Aligned Paragraph Diff
        BuildParagraphDiff();

        // 3. Table Diff
        BuildTableDiff();

        // 4. Metadata Diff
        BuildMetadataDiff();
    }

    private void BuildOutlineTree()
    {
        OutlineTree.RootNodes.Clear();

        var headings = _leftDoc?.Paragraphs.Where(p => p.IsHeading).ToList() ?? new();
        if (headings.Count == 0 && _rightDoc != null)
        {
            headings = _rightDoc.Paragraphs.Where(p => p.IsHeading).ToList();
        }

        if (headings.Count == 0)
        {
            OutlineTree.RootNodes.Add(new TreeViewNode { Content = LanguageManager.Instance.Text("wordNoOutline") });
            return;
        }

        foreach (var h in headings)
        {
            var node = new TreeViewNode { Content = new OutlineHeadingItem { Title = h.Text, Index = h.Index } };
            OutlineTree.RootNodes.Add(node);
        }
    }

    private void BuildParagraphDiff()
    {
        string leftText = string.Join("\n", _leftDoc?.Paragraphs.Select(p => p.Text) ?? Array.Empty<string>());
        string rightText = string.Join("\n", _rightDoc?.Paragraphs.Select(p => p.Text) ?? Array.Empty<string>());

        var diff = DiffEngineService.Instance.CompareText(leftText, rightText);
        var aligned = DiffEngineService.Instance.BuildAlignedLines(diff);

        _allParagraphs.Clear();
        int adds = 0, dels = 0, mods = 0;

        foreach (var line in aligned)
        {
            var item = new WordDiffParagraphDisplayItem
            {
                LeftIndexText = line.LeftLineNumber.HasValue ? $"§ {line.LeftLineNumber}" : "",
                LeftText = line.LeftContent,
                RightIndexText = line.RightLineNumber.HasValue ? $"§ {line.RightLineNumber}" : "",
                RightText = line.RightContent
            };

            if (line.LeftType == DiffChangeType.Deleted)
            {
                item.StatusText = LanguageManager.Instance.Text("statusDeleted");
                item.StatusBgBrush = DelBg;
                item.StatusFgBrush = DelFg;
                item.LeftBgBrush = DelBg;
                item.LeftBadgeVisibility = Visibility.Visible;
                dels++;
            }
            else if (line.RightType == DiffChangeType.Added)
            {
                item.StatusText = LanguageManager.Instance.Text("statusAdded");
                item.StatusBgBrush = AddBg;
                item.StatusFgBrush = AddFg;
                item.RightBgBrush = AddBg;
                item.RightBadgeVisibility = Visibility.Visible;
                adds++;
            }
            else if (line.LeftType == DiffChangeType.Modified || line.RightType == DiffChangeType.Modified)
            {
                item.StatusText = LanguageManager.Instance.Text("statusModified");
                item.StatusBgBrush = ModBg;
                item.StatusFgBrush = ModFg;
                item.LeftBgBrush = ModBg;
                item.RightBgBrush = ModBg;
                item.LeftBadgeVisibility = Visibility.Visible;
                item.RightBadgeVisibility = Visibility.Visible;
                mods++;
            }
            else
            {
                item.StatusText = LanguageManager.Instance.Text("statusIdentical");
            }

            _allParagraphs.Add(item);
        }

        ApplyParagraphFilter();
    }

    private void ApplyParagraphFilter()
    {
        string query = _searchQuery.Trim().ToLowerInvariant();
        IEnumerable<WordDiffParagraphDisplayItem> filtered = _allParagraphs;

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(p =>
                p.LeftText.ToLowerInvariant().Contains(query) ||
                p.RightText.ToLowerInvariant().Contains(query) ||
                p.LeftIndexText.Contains(query) ||
                p.RightIndexText.Contains(query));
        }

        _displayParagraphs = filtered.ToList();
        ParagraphsList.ItemsSource = null;
        ParagraphsList.ItemsSource = _displayParagraphs;

        int totalDiffs = _allParagraphs.Count(p => p.LeftBadgeVisibility == Visibility.Visible || p.RightBadgeVisibility == Visibility.Visible);
        DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountDocBadge"), totalDiffs);
        int outlineNodes = OutlineTree.RootNodes.Count;
        StatusBarText.Text = string.Format(LanguageManager.Instance.Text("wordSummaryStats"), _displayParagraphs.Count, outlineNodes);
    }

    private void OnPrevDiffClicked(object sender, RoutedEventArgs e)
    {
        if (_displayParagraphs.Count == 0) return;

        int startIndex = _currentDiffJumpIndex <= 0 ? _displayParagraphs.Count - 1 : _currentDiffJumpIndex - 1;
        for (int i = startIndex; i >= 0; i--)
        {
            if (_displayParagraphs[i].LeftBadgeVisibility == Visibility.Visible || _displayParagraphs[i].RightBadgeVisibility == Visibility.Visible)
            {
                _currentDiffJumpIndex = i;
                ParagraphsList.SelectedIndex = i;
                ParagraphsList.ScrollIntoView(_displayParagraphs[i]);
                return;
            }
        }
        for (int i = _displayParagraphs.Count - 1; i > startIndex; i--)
        {
            if (_displayParagraphs[i].LeftBadgeVisibility == Visibility.Visible || _displayParagraphs[i].RightBadgeVisibility == Visibility.Visible)
            {
                _currentDiffJumpIndex = i;
                ParagraphsList.SelectedIndex = i;
                ParagraphsList.ScrollIntoView(_displayParagraphs[i]);
                return;
            }
        }
    }

    private void OnNextDiffClicked(object sender, RoutedEventArgs e)
    {
        if (_displayParagraphs.Count == 0) return;

        int startIndex = _currentDiffJumpIndex >= _displayParagraphs.Count - 1 ? 0 : _currentDiffJumpIndex + 1;
        for (int i = startIndex; i < _displayParagraphs.Count; i++)
        {
            if (_displayParagraphs[i].LeftBadgeVisibility == Visibility.Visible || _displayParagraphs[i].RightBadgeVisibility == Visibility.Visible)
            {
                _currentDiffJumpIndex = i;
                ParagraphsList.SelectedIndex = i;
                ParagraphsList.ScrollIntoView(_displayParagraphs[i]);
                return;
            }
        }
        for (int i = 0; i < startIndex; i++)
        {
            if (_displayParagraphs[i].LeftBadgeVisibility == Visibility.Visible || _displayParagraphs[i].RightBadgeVisibility == Visibility.Visible)
            {
                _currentDiffJumpIndex = i;
                ParagraphsList.SelectedIndex = i;
                ParagraphsList.ScrollIntoView(_displayParagraphs[i]);
                return;
            }
        }
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _searchQuery = sender.Text ?? string.Empty;
        ApplyParagraphFilter();
    }

    private void OnSwapSidesClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_leftPath) && string.IsNullOrEmpty(_rightPath)) return;
        var temp = _leftPath;
        _leftPath = _rightPath;
        _rightPath = temp;

        LeftDocPathText.Text = _leftPath ?? LanguageManager.Instance.Text("leftDocPrompt");
        RightDocPathText.Text = _rightPath ?? LanguageManager.Instance.Text("rightDocPrompt");

        if (!string.IsNullOrEmpty(_leftPath) && !string.IsNullOrEmpty(_rightPath))
        {
            ParseAndCompare();
        }
    }

    private void OnClearAllClicked(object sender, RoutedEventArgs e)
    {
        _leftPath = null;
        _rightPath = null;
        _leftDoc = null;
        _rightDoc = null;
        _allParagraphs.Clear();
        _displayParagraphs.Clear();
        _currentDiffJumpIndex = -1;
        _searchQuery = string.Empty;
        if (WordSearchBox != null) WordSearchBox.Text = string.Empty;
        ParagraphsList.ItemsSource = null;
        OutlineTree.RootNodes.Clear();
        LeftDocPathText.Text = LanguageManager.Instance.Text("leftDocPrompt");
        RightDocPathText.Text = LanguageManager.Instance.Text("rightDocPrompt");
        EmptyStatePanel.Visibility = Visibility.Visible;
        DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountDocBadge"), 0);
        MetadataDetailsText.Text = LanguageManager.Instance.Text("metaNotLoaded");
        StatusBarText.Text = LanguageManager.Instance.Text("notCompared");
    }

    private void OnExportDiffClicked(object sender, RoutedEventArgs e)
    {
        if (_allParagraphs.Count == 0) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Format(LanguageManager.Instance.Text("wordDiffReportTitle"), Path.GetFileName(_leftPath), Path.GetFileName(_rightPath)));
        sb.AppendLine(string.Format(LanguageManager.Instance.Text("exportTimeLabel"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        sb.AppendLine();
        string hSection = LanguageManager.Instance.Text("reportHeaderSection");
        string hStatus = LanguageManager.Instance.Text("reportHeaderStatus");
        string hLeft = LanguageManager.Instance.Text("reportHeaderLeft");
        string hRight = LanguageManager.Instance.Text("reportHeaderRight");
        sb.AppendLine($"| {hSection} | {hStatus} | {hLeft} | {hRight} |");
        sb.AppendLine("|---|---|---|---|");

        foreach (var p in _allParagraphs.Where(p => p.LeftBadgeVisibility == Visibility.Visible || p.RightBadgeVisibility == Visibility.Visible))
        {
            string sec = !string.IsNullOrEmpty(p.LeftIndexText) ? p.LeftIndexText : p.RightIndexText;
            string cleanLeft = p.LeftText.Replace("\n", " ");
            string cleanRight = p.RightText.Replace("\n", " ");
            sb.AppendLine($"| {sec} | {p.StatusText} | {cleanLeft} | {cleanRight} |");
        }

        var dp = new DataPackage();
        dp.SetText(sb.ToString());
        Clipboard.SetContent(dp);

        StatusBarText.Text = LanguageManager.Instance.Text("exportSuccess");
    }

    private void BuildTableDiff()
    {
        TableDiffContainer.Children.Clear();
        int leftTableCount = _leftDoc?.Tables.Count ?? 0;
        int rightTableCount = _rightDoc?.Tables.Count ?? 0;

        int max = Math.Max(leftTableCount, rightTableCount);
        if (max == 0)
        {
            TableDiffContainer.Children.Add(new TextBlock
            {
                Text = LanguageManager.Instance.Text("wordNoTables"),
                Foreground = new SolidColorBrush(Colors.Gray),
                FontSize = 13
            });
            return;
        }

        for (int i = 0; i < max; i++)
        {
            var card = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14)
            };

            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock
            {
                Text = string.Format(LanguageManager.Instance.Text("tableIndexHeader"), i + 1),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 13
            });

            var leftT = i < leftTableCount ? _leftDoc!.Tables[i] : null;
            var rightT = i < rightTableCount ? _rightDoc!.Tables[i] : null;

            string lLabel = LanguageManager.Instance.Text("wordSideLeft");
            string rLabel = LanguageManager.Instance.Text("wordSideRight");

            string lInfo = leftT != null 
                ? string.Format(LanguageManager.Instance.Text("tableInfoFormat"), lLabel, leftT.Rows.Count, leftT.Rows.FirstOrDefault()?.Count ?? 0) 
                : string.Format(LanguageManager.Instance.Text("tableInfoNone"), lLabel);
            string rInfo = rightT != null 
                ? string.Format(LanguageManager.Instance.Text("tableInfoFormat"), rLabel, rightT.Rows.Count, rightT.Rows.FirstOrDefault()?.Count ?? 0) 
                : string.Format(LanguageManager.Instance.Text("tableInfoNone"), rLabel);

            panel.Children.Add(new TextBlock { Text = $"{lInfo}   |   {rInfo}", FontSize = 12, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });

            card.Child = panel;
            TableDiffContainer.Children.Add(card);
        }
    }

    private void BuildMetadataDiff()
    {
        if (_leftPath == null || _rightPath == null) return;

        var fiLeft = new FileInfo(_leftPath);
        var fiRight = new FileInfo(_rightPath);

        var sbMeta = new System.Text.StringBuilder();
        sbMeta.AppendLine(LanguageManager.Instance.Text("wordMetaLeftDoc"));
        sbMeta.AppendLine(string.Format(LanguageManager.Instance.Text("wordMetaFilePath"), _leftPath));
        sbMeta.AppendLine(string.Format(LanguageManager.Instance.Text("wordMetaFileSize"), FormatSize(fiLeft.Length)));
        sbMeta.AppendLine(string.Format(LanguageManager.Instance.Text("wordMetaLastMod"), fiLeft.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")));
        sbMeta.AppendLine(string.Format(LanguageManager.Instance.Text("wordMetaParagraphs"), _leftDoc?.Paragraphs.Count ?? 0));
        sbMeta.AppendLine(string.Format(LanguageManager.Instance.Text("wordMetaTables"), _leftDoc?.Tables.Count ?? 0));
        sbMeta.AppendLine();
        sbMeta.AppendLine(LanguageManager.Instance.Text("wordMetaRightDoc"));
        sbMeta.AppendLine(string.Format(LanguageManager.Instance.Text("wordMetaFilePath"), _rightPath));
        sbMeta.AppendLine(string.Format(LanguageManager.Instance.Text("wordMetaFileSize"), FormatSize(fiRight.Length)));
        sbMeta.AppendLine(string.Format(LanguageManager.Instance.Text("wordMetaLastMod"), fiRight.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")));
        sbMeta.AppendLine(string.Format(LanguageManager.Instance.Text("wordMetaParagraphs"), _rightDoc?.Paragraphs.Count ?? 0));
        sbMeta.AppendLine(string.Format(LanguageManager.Instance.Text("wordMetaTables"), _rightDoc?.Tables.Count ?? 0));

        MetadataDetailsText.Text = sbMeta.ToString();
    }

    private void OnViewModeChanged(object sender, SelectionChangedEventArgs e)
    {
        int idx = ViewModeBox?.SelectedIndex ?? 0;
        if (ParagraphsList != null) ParagraphsList.Visibility = idx == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (TableDiffView != null) TableDiffView.Visibility = idx == 1 ? Visibility.Visible : Visibility.Collapsed;
        if (MetadataDiffView != null) MetadataDiffView.Visibility = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnToggleSidebarClicked(object sender, RoutedEventArgs e)
    {
        _isSidebarVisible = !_isSidebarVisible;
        SidebarCol.Width = _isSidebarVisible ? new GridLength(220) : new GridLength(0);
    }

    private void OnOutlineItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode node && node.Content is OutlineHeadingItem item)
        {
            var match = _displayParagraphs.FirstOrDefault(p => p.LeftIndexText.Contains(item.Index.ToString()));
            if (match != null)
            {
                ParagraphsList.ScrollIntoView(match);
                ParagraphsList.SelectedItem = match;
            }
        }
    }

    private async void OnPickLeftDocClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { ".docx", ".doc" }, App.MainWindowInstance);
        if (file != null)
        {
            _leftPath = file.Path;
            LeftDocPathText.Text = file.Path;
            if (!string.IsNullOrEmpty(_rightPath))
            {
                RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.WordDiff);
                ParseAndCompare();
            }
        }
    }

    private async void OnPickRightDocClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { ".docx", ".doc" }, App.MainWindowInstance);
        if (file != null)
        {
            _rightPath = file.Path;
            RightDocPathText.Text = file.Path;
            if (!string.IsNullOrEmpty(_leftPath))
            {
                RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.WordDiff);
                ParseAndCompare();
            }
        }
    }

    private void OnLeftDocDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void OnLeftDocDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                _leftPath = file.Path;
                LeftDocPathText.Text = file.Path;
                if (!string.IsNullOrEmpty(_rightPath)) ParseAndCompare();
            }
        }
    }

    private void OnRightDocDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void OnRightDocDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                _rightPath = file.Path;
                RightDocPathText.Text = file.Path;
                if (!string.IsNullOrEmpty(_leftPath)) ParseAndCompare();
            }
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024)
        {
            string kb = (bytes / 1024.0).ToString("F1");
            return $"{kb} KB";
        }
        string mb = (bytes / (1024.0 * 1024.0)).ToString("F1");
        return $"{mb} MB";
    }

    private sealed class OutlineHeadingItem
    {
        public string Title { get; set; } = string.Empty;
        public int Index { get; set; }
        public override string ToString() => Title;
    }
}
