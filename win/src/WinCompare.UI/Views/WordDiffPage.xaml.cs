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
    private string _searchQuery = string.Empty;
    private bool _isSidebarVisible = true;
    private int _viewMode = 0; // 0: Struct, 1: Style, 2: Table, 3: Meta
    private bool _isInitialized = false;

    private static readonly Brush ModBg = new SolidColorBrush(Windows.UI.Color.FromArgb(35, 234, 179, 8));
    private static readonly Brush ModFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 250, 204, 21));
    private static readonly Brush AddBg = new SolidColorBrush(Windows.UI.Color.FromArgb(35, 34, 197, 94));
    private static readonly Brush AddFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 74, 222, 128));
    private static readonly Brush DelBg = new SolidColorBrush(Windows.UI.Color.FromArgb(35, 239, 68, 68));
    private static readonly Brush DelFg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 113, 113));

    private static readonly Brush ActiveTabBg = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 37, 99, 235));
    private static readonly Brush InactiveTabBg = new SolidColorBrush(Colors.Transparent);
    private static readonly Brush ActiveTabFg = new SolidColorBrush(Colors.White);

    public WordDiffPage()
    {
        this.InitializeComponent();
        UpdateLocalization();
        LanguageManager.Instance.LanguageChanged += UpdateLocalization;

        this.ActualThemeChanged += (s, e) => SetModeButtonsVisual(_viewMode);

        _isInitialized = true;
        UpdateWorkspaceVisibility();
    }

    private void UpdateLocalization()
    {
        if (ModeStructText != null) ModeStructText.Text = "结构化内容比对";
        if (ModeStyleText != null) ModeStyleText.Text = "样式格式比对";
        if (ModeTableText != null) ModeTableText.Text = "表格网格比对";
        if (ModeMetaText != null) ModeMetaText.Text = "文档元数据比对";
        if (WordSearchBox != null) WordSearchBox.PlaceholderText = "搜索与过滤段落...";
        if (ExportReportText != null) ExportReportText.Text = LanguageManager.Instance.Text("exportDiff");

        if (LeftBrowseBtn != null) LeftBrowseBtn.Content = LanguageManager.Instance.Text("selectEllipsis");
        if (RightBrowseBtn != null) RightBrowseBtn.Content = LanguageManager.Instance.Text("selectEllipsis");
        if (LeftPickBtn != null) LeftPickBtn.Content = LanguageManager.Instance.Text("pickSourceFile");
        if (RightPickBtn != null) RightPickBtn.Content = LanguageManager.Instance.Text("pickTargetFile");

        if (MetadataHeader != null) MetadataHeader.Text = LanguageManager.Instance.Text("metaTitle");

        if (string.IsNullOrEmpty(_leftPath))
        {
            if (LeftDocTitle != null) LeftDocTitle.Text = "选择源文件...";
            if (LeftDocSubtitle != null) LeftDocSubtitle.Text = "未选择文件";
        }
        if (string.IsNullOrEmpty(_rightPath))
        {
            if (RightDocTitle != null) RightDocTitle.Text = "选择目标文件...";
            if (RightDocSubtitle != null) RightDocSubtitle.Text = "未选择文件";
        }

        if (_leftDoc == null)
        {
            if (MetadataDetailsText != null) MetadataDetailsText.Text = LanguageManager.Instance.Text("metaNotLoaded");
            if (StatusBarText != null) StatusBarText.Text = LanguageManager.Instance.Text("notCompared");
            if (DiffBadge != null) DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountDocBadge"), 0);
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

            if (DiffBadge != null)
                DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountDocBadge"), totalDiffs);
            int outlineNodes = OutlineTree?.RootNodes.Count ?? 0;
            if (StatusBarText != null)
                StatusBarText.Text = string.Format(LanguageManager.Instance.Text("wordSummaryStats"), _displayParagraphs.Count, outlineNodes);
        }
    }

    private void UpdateWorkspaceVisibility()
    {
        if (!_isInitialized) return;

        bool hasLeft = !string.IsNullOrEmpty(_leftPath);
        bool hasRight = !string.IsNullOrEmpty(_rightPath);

        if (!hasLeft && !hasRight)
        {
            if (EmptyStateContainer != null) EmptyStateContainer.Visibility = Visibility.Visible;
            if (ParagraphsList != null) ParagraphsList.Visibility = Visibility.Collapsed;
            if (TableDiffView != null) TableDiffView.Visibility = Visibility.Collapsed;
            if (MetadataDiffView != null) MetadataDiffView.Visibility = Visibility.Collapsed;
            if (OutlineEmptyState != null) OutlineEmptyState.Visibility = Visibility.Visible;
            if (OutlineTree != null) OutlineTree.Visibility = Visibility.Collapsed;
            if (OutlineCountBadge != null) OutlineCountBadge.Text = "0";

            if (ExportReportBtn != null) ExportReportBtn.IsEnabled = false;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = false;
        }
        else
        {
            if (EmptyStateContainer != null) EmptyStateContainer.Visibility = Visibility.Collapsed;
            SwitchViewModeDisplay();

            bool hasBoth = hasLeft && hasRight;
            if (ExportReportBtn != null) ExportReportBtn.IsEnabled = hasBoth;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = true;
        }
    }

    public void LoadFiles(string? leftPath, string? rightPath)
    {
        if (!string.IsNullOrEmpty(leftPath) && File.Exists(leftPath))
        {
            _leftPath = leftPath;
            if (LeftDocTitle != null) LeftDocTitle.Text = Path.GetFileName(leftPath);
            if (LeftDocSubtitle != null) LeftDocSubtitle.Text = leftPath;
        }

        if (!string.IsNullOrEmpty(rightPath) && File.Exists(rightPath))
        {
            _rightPath = rightPath;
            if (RightDocTitle != null) RightDocTitle.Text = Path.GetFileName(rightPath);
            if (RightDocSubtitle != null) RightDocSubtitle.Text = rightPath;
        }

        UpdateWorkspaceVisibility();

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

        UpdateWorkspaceVisibility();

        _leftDoc = WordDocumentParser.Parse(_leftPath);
        _rightDoc = WordDocumentParser.Parse(_rightPath);

        BuildOutlineTree();
        BuildParagraphDiff();
        BuildTableDiff();
        BuildMetadataDiff();
    }

    private void BuildOutlineTree()
    {
        if (OutlineTree == null) return;
        OutlineTree.RootNodes.Clear();

        var headings = _leftDoc?.Paragraphs.Where(p => p.IsHeading).ToList() ?? new();
        if (headings.Count == 0 && _rightDoc != null)
        {
            headings = _rightDoc.Paragraphs.Where(p => p.IsHeading).ToList();
        }

        if (headings.Count == 0)
        {
            if (OutlineEmptyState != null) OutlineEmptyState.Visibility = Visibility.Visible;
            if (OutlineTree != null) OutlineTree.Visibility = Visibility.Collapsed;
            if (OutlineCountBadge != null) OutlineCountBadge.Text = "0";
            return;
        }

        if (OutlineEmptyState != null) OutlineEmptyState.Visibility = Visibility.Collapsed;
        if (OutlineTree != null) OutlineTree.Visibility = Visibility.Visible;
        if (OutlineCountBadge != null) OutlineCountBadge.Text = headings.Count.ToString();

        foreach (var h in headings)
        {
            var node = new TreeViewNode { Content = new OutlineHeadingItem { Title = h.Text, Index = h.Index } };
            OutlineTree?.RootNodes.Add(node);
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
        if (ParagraphsList != null)
        {
            ParagraphsList.ItemsSource = null;
            ParagraphsList.ItemsSource = _displayParagraphs;
        }

        int totalDiffs = _allParagraphs.Count(p => p.LeftBadgeVisibility == Visibility.Visible || p.RightBadgeVisibility == Visibility.Visible);
        if (DiffBadge != null)
            DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountDocBadge"), totalDiffs);

        int outlineNodes = OutlineTree?.RootNodes.Count ?? 0;
        if (StatusBarText != null)
            StatusBarText.Text = string.Format(LanguageManager.Instance.Text("wordSummaryStats"), _displayParagraphs.Count, outlineNodes);
    }

    private void OnModeStructClicked(object sender, RoutedEventArgs e)
    {
        _viewMode = 0;
        SetModeButtonsVisual(0);
        SwitchViewModeDisplay();
    }

    private void OnModeStyleClicked(object sender, RoutedEventArgs e)
    {
        _viewMode = 1;
        SetModeButtonsVisual(1);
        SwitchViewModeDisplay();
    }

    private void OnModeTableClicked(object sender, RoutedEventArgs e)
    {
        _viewMode = 2;
        SetModeButtonsVisual(2);
        SwitchViewModeDisplay();
    }

    private void OnModeMetaClicked(object sender, RoutedEventArgs e)
    {
        _viewMode = 3;
        SetModeButtonsVisual(3);
        SwitchViewModeDisplay();
    }

    private Brush GetInactiveTabForeground()
    {
        bool isDark = this.ActualTheme == ElementTheme.Dark;
        return isDark 
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 203, 213, 225)) // #CBD5E1 Slate 300 for dark mode
            : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 71, 85, 105));   // #475569 Slate 600 for light mode
    }

    private void SetModeButtonsVisual(int activeIndex)
    {
        var inactiveFg = GetInactiveTabForeground();

        if (ModeStructBtn != null)
        {
            ModeStructBtn.Background = activeIndex == 0 ? ActiveTabBg : InactiveTabBg;
            ModeStructBtn.Foreground = activeIndex == 0 ? ActiveTabFg : inactiveFg;
            ModeStructBtn.FontWeight = activeIndex == 0 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }
        if (ModeStyleBtn != null)
        {
            ModeStyleBtn.Background = activeIndex == 1 ? ActiveTabBg : InactiveTabBg;
            ModeStyleBtn.Foreground = activeIndex == 1 ? ActiveTabFg : inactiveFg;
            ModeStyleBtn.FontWeight = activeIndex == 1 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }
        if (ModeTableBtn != null)
        {
            ModeTableBtn.Background = activeIndex == 2 ? ActiveTabBg : InactiveTabBg;
            ModeTableBtn.Foreground = activeIndex == 2 ? ActiveTabFg : inactiveFg;
            ModeTableBtn.FontWeight = activeIndex == 2 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }
        if (ModeMetaBtn != null)
        {
            ModeMetaBtn.Background = activeIndex == 3 ? ActiveTabBg : InactiveTabBg;
            ModeMetaBtn.Foreground = activeIndex == 3 ? ActiveTabFg : inactiveFg;
            ModeMetaBtn.FontWeight = activeIndex == 3 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }
    }

    private void SwitchViewModeDisplay()
    {
        bool hasFiles = !string.IsNullOrEmpty(_leftPath) || !string.IsNullOrEmpty(_rightPath);
        if (!hasFiles) return;

        if (ParagraphsList != null) ParagraphsList.Visibility = Visibility.Collapsed;
        if (TableDiffView != null) TableDiffView.Visibility = Visibility.Collapsed;
        if (MetadataDiffView != null) MetadataDiffView.Visibility = Visibility.Collapsed;

        switch (_viewMode)
        {
            case 0:
            case 1:
                if (ParagraphsList != null) ParagraphsList.Visibility = Visibility.Visible;
                break;
            case 2:
                if (TableDiffView != null) TableDiffView.Visibility = Visibility.Visible;
                break;
            case 3:
                if (MetadataDiffView != null) MetadataDiffView.Visibility = Visibility.Visible;
                break;
        }
    }

    private void OnToggleSidebarClicked(object sender, RoutedEventArgs e)
    {
        _isSidebarVisible = !_isSidebarVisible;
        var width = _isSidebarVisible ? new GridLength(220) : new GridLength(0);
        if (SidebarCol != null) SidebarCol.Width = width;
        if (HeaderSidebarCol != null) HeaderSidebarCol.Width = width;
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (!_isInitialized) return;
        _searchQuery = sender.Text ?? string.Empty;
        ApplyParagraphFilter();
    }

    private void OnOutlineItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode node && node.Content is OutlineHeadingItem item)
        {
            var target = _displayParagraphs.FirstOrDefault(p => p.LeftIndexText.Contains(item.Index.ToString()) || p.RightIndexText.Contains(item.Index.ToString()));
            if (target != null && ParagraphsList != null)
            {
                ParagraphsList.SelectedItem = target;
                ParagraphsList.ScrollIntoView(target);
            }
        }
    }

    private void OnClearAllClicked(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        _leftPath = null;
        _rightPath = null;
        _leftDoc = null;
        _rightDoc = null;
        _allParagraphs.Clear();
        _displayParagraphs.Clear();
        _searchQuery = string.Empty;
        if (WordSearchBox != null) WordSearchBox.Text = string.Empty;
        if (ParagraphsList != null) ParagraphsList.ItemsSource = null;
        if (OutlineTree != null) OutlineTree.RootNodes.Clear();

        if (LeftDocTitle != null) LeftDocTitle.Text = "选择源文件...";
        if (LeftDocSubtitle != null) LeftDocSubtitle.Text = "未选择文件";
        if (RightDocTitle != null) RightDocTitle.Text = "选择目标文件...";
        if (RightDocSubtitle != null) RightDocSubtitle.Text = "未选择文件";

        if (DiffBadge != null) DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountDocBadge"), 0);
        if (MetadataDetailsText != null) MetadataDetailsText.Text = LanguageManager.Instance.Text("metaNotLoaded");
        if (StatusBarText != null) StatusBarText.Text = LanguageManager.Instance.Text("notCompared");

        UpdateWorkspaceVisibility();
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

        if (StatusBarText != null)
            StatusBarText.Text = LanguageManager.Instance.Text("exportSuccess");
    }

    private void BuildTableDiff()
    {
        if (TableDiffContainer == null) return;
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
        if (_leftDoc == null && _rightDoc == null)
        {
            if (MetadataDetailsText != null)
                MetadataDetailsText.Text = LanguageManager.Instance.Text("metaNotLoaded");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"--- [左侧原文档: {Path.GetFileName(_leftPath)}] ---");
        if (_leftDoc != null && !string.IsNullOrEmpty(_leftPath) && File.Exists(_leftPath))
        {
            var fi = new FileInfo(_leftPath);
            sb.AppendLine($"文件大小: {fi.Length / 1024.0:F1} KB ({fi.Length} 字节)");
            sb.AppendLine($"创建时间: {fi.CreationTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"修改时间: {fi.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"段落总数: {_leftDoc.Paragraphs.Count}");
            sb.AppendLine($"表格总数: {_leftDoc.Tables.Count}");
        }
        else
        {
            sb.AppendLine("未加载文档");
        }

        sb.AppendLine();
        sb.AppendLine($"--- [右侧修订文档: {Path.GetFileName(_rightPath)}] ---");
        if (_rightDoc != null && !string.IsNullOrEmpty(_rightPath) && File.Exists(_rightPath))
        {
            var fi = new FileInfo(_rightPath);
            sb.AppendLine($"文件大小: {fi.Length / 1024.0:F1} KB ({fi.Length} 字节)");
            sb.AppendLine($"创建时间: {fi.CreationTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"修改时间: {fi.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"段落总数: {_rightDoc.Paragraphs.Count}");
            sb.AppendLine($"表格总数: {_rightDoc.Tables.Count}");
        }
        else
        {
            sb.AppendLine("未加载文档");
        }

        if (MetadataDetailsText != null)
            MetadataDetailsText.Text = sb.ToString();
    }

    private async void OnPickLeftDocClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { ".docx", ".doc" });
        if (file != null)
        {
            _leftPath = file.Path;
            if (LeftDocTitle != null) LeftDocTitle.Text = file.Name;
            if (LeftDocSubtitle != null) LeftDocSubtitle.Text = file.Path;
            UpdateWorkspaceVisibility();

            if (!string.IsNullOrEmpty(_rightPath))
            {
                RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.WordDiff);
                ParseAndCompare();
            }
        }
    }

    private async void OnPickRightDocClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { ".docx", ".doc" });
        if (file != null)
        {
            _rightPath = file.Path;
            if (RightDocTitle != null) RightDocTitle.Text = file.Name;
            if (RightDocSubtitle != null) RightDocSubtitle.Text = file.Path;
            UpdateWorkspaceVisibility();

            if (!string.IsNullOrEmpty(_leftPath))
            {
                RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.WordDiff);
                ParseAndCompare();
            }
        }
    }

    private void OnLeftDocDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void OnLeftDocDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                _leftPath = file.Path;
                if (LeftDocTitle != null) LeftDocTitle.Text = file.Name;
                if (LeftDocSubtitle != null) LeftDocSubtitle.Text = file.Path;
                UpdateWorkspaceVisibility();

                if (!string.IsNullOrEmpty(_rightPath))
                {
                    RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.WordDiff);
                    ParseAndCompare();
                }
            }
        }
    }

    private void OnRightDocDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void OnRightDocDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                _rightPath = file.Path;
                if (RightDocTitle != null) RightDocTitle.Text = file.Name;
                if (RightDocSubtitle != null) RightDocSubtitle.Text = file.Path;
                UpdateWorkspaceVisibility();

                if (!string.IsNullOrEmpty(_leftPath))
                {
                    RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.WordDiff);
                    ParseAndCompare();
                }
            }
        }
    }
}

public sealed class OutlineHeadingItem
{
    public string Title { get; set; } = string.Empty;
    public int Index { get; set; }
    public override string ToString() => Title;
}
