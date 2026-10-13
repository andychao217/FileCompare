using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using WinCompare.Core.Models;
using WinCompare.Core.Services;
using WinCompare.UI.Helpers;
using WinCompare.UI.ViewModels;

namespace WinCompare.UI.Views;

public sealed partial class WordDiffPage : Page, IDisposable
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
    private bool _tableDiffNeedsBuild = true;

    private const double MinSidebarWidth = 220.0;
    private const double MaxSidebarWidth = 440.0;
    private double _currentSidebarWidth = 220.0;
    private bool _isDraggingSidebar = false;
    private double _dragStartPointerX = 0;
    private double _dragStartSidebarWidth = 220.0;

    private static Brush ModBg => App.GetThemeBrush("DiffModifiedBgBrush");
    private static Brush ModFg => App.GetThemeBrush("DiffModifiedFgBrush");
    private static Brush AddBg => App.GetThemeBrush("DiffAddedBgBrush");
    private static Brush AddFg => App.GetThemeBrush("DiffAddedFgBrush");
    private static Brush DelBg => App.GetThemeBrush("DiffDeletedBgBrush");
    private static Brush DelFg => App.GetThemeBrush("DiffDeletedFgBrush");

    private static Brush ActiveTabBg => App.GetThemeBrush("BrandPrimaryBrush");
    private static readonly Brush InactiveTabBg = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    private static Brush ActiveTabFg => App.GetThemeBrush("TextOnAccentBrush");

    public WordDiffPage()
    {
        try
        {
            this.InitializeComponent();
            this.Loaded += (s, e) => App.Log("[WordDiffPage] Page.Loaded event fired.");
            if (ParagraphsList != null)
            {
                ParagraphsList.Loaded += (s, e) => App.Log("[WordDiffPage] ParagraphsList.Loaded event fired.");
            }
            UpdateLocalization();
            LanguageManager.Instance.LanguageChanged += UpdateLocalization;

            this.ActualThemeChanged += (s, e) =>
            {
                try
                {
                    SetModeButtonsVisual(_viewMode);
                    _tableDiffNeedsBuild = true;
                    if (_viewMode == 2 && (_leftDoc != null || _rightDoc != null))
                    {
                        this.DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                        {
                            if (_viewMode == 2)
                            {
                                BuildTableDiff();
                                _tableDiffNeedsBuild = false;
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ActualThemeChanged error: {ex.Message}");
                }
            };

            _isInitialized = true;
            UpdateWorkspaceVisibility();
        }
        catch (Exception ex)
        {
            App.LogFatalError("WordDiffPage.Constructor", ex);
        }
    }

    private void UpdateLocalization()
    {
        if (ModeStructText != null) ModeStructText.Text = LanguageManager.Instance.Text("wordModeStruct");
        if (ModeStyleText != null) ModeStyleText.Text = LanguageManager.Instance.Text("wordModeStyle");
        if (ModeTableText != null) ModeTableText.Text = LanguageManager.Instance.Text("wordModeTable");
        if (ModeMetaText != null) ModeMetaText.Text = LanguageManager.Instance.Text("wordModeMeta");
        if (WordSearchBox != null) WordSearchBox.PlaceholderText = LanguageManager.Instance.Text("searchParagraphsPlaceholder");
        if (ExportReportText != null) ExportReportText.Text = LanguageManager.Instance.Text("exportDiff");

        if (LeftBrowseBtn != null) LeftBrowseBtn.Content = LanguageManager.Instance.Text("selectEllipsis");
        if (RightBrowseBtn != null) RightBrowseBtn.Content = LanguageManager.Instance.Text("selectEllipsis");
        if (LeftPickBtn != null) LeftPickBtn.Content = LanguageManager.Instance.Text("pickSourceFile");
        if (RightPickBtn != null) RightPickBtn.Content = LanguageManager.Instance.Text("pickTargetFile");

        if (MetadataHeader != null) MetadataHeader.Text = LanguageManager.Instance.Text("metaTitle");

        if (OutlineHeaderTitle != null) OutlineHeaderTitle.Text = LanguageManager.Instance.Text("docOutline");
        if (OutlineEmptyTitle != null) OutlineEmptyTitle.Text = LanguageManager.Instance.Text("noOutlineDetected");
        if (OutlineEmptyDesc != null) OutlineEmptyDesc.Text = LanguageManager.Instance.Text("outlineExtractHint");

        try
        {
            if (ToggleSidebarBtn != null) ToolTipService.SetToolTip(ToggleSidebarBtn, LanguageManager.Instance.Text(_isSidebarVisible ? "collapseOutline" : "expandOutline"));
            if (ExportReportBtn != null) ToolTipService.SetToolTip(ExportReportBtn, LanguageManager.Instance.Text("exportDiff"));
            if (ClearAllBtn != null) ToolTipService.SetToolTip(ClearAllBtn, LanguageManager.Instance.Text("clearAllTooltip"));
        }
        catch { }

        if (LeftEmptyTitle != null) LeftEmptyTitle.Text = LanguageManager.Instance.Text("noSourceFile");
        if (RightEmptyTitle != null) RightEmptyTitle.Text = LanguageManager.Instance.Text("noTargetFile");
        if (LeftEmptyDesc != null) LeftEmptyDesc.Text = LanguageManager.Instance.Text("dropWordPrompt");
        if (RightEmptyDesc != null) RightEmptyDesc.Text = LanguageManager.Instance.Text("dropWordPrompt");

        if (LeftLoadingText != null) LeftLoadingText.Text = LanguageManager.Instance.Text("loadingSourceDoc");
        if (RightLoadingText != null) RightLoadingText.Text = LanguageManager.Instance.Text("loadingTargetDoc");
        if (DualLeftLoadingText != null) DualLeftLoadingText.Text = LanguageManager.Instance.Text("loadingSourceDoc");
        if (DualRightLoadingText != null) DualRightLoadingText.Text = LanguageManager.Instance.Text("loadingTargetDoc");

        if (string.IsNullOrEmpty(_leftPath))
        {
            if (LeftDocTitle != null) LeftDocTitle.Text = LanguageManager.Instance.Text("pickSourceFile");
            if (LeftDocSubtitle != null) LeftDocSubtitle.Text = LanguageManager.Instance.Text("noFileSelected");
        }
        if (string.IsNullOrEmpty(_rightPath))
        {
            if (RightDocTitle != null) RightDocTitle.Text = LanguageManager.Instance.Text("pickTargetFile");
            if (RightDocSubtitle != null) RightDocSubtitle.Text = LanguageManager.Instance.Text("noFileSelected");
        }

        if (MetadataDetailsText != null)
        {
            MetadataDetailsText.Text = (_leftDoc == null && _rightDoc == null)
                ? LanguageManager.Instance.Text("metaNotLoaded")
                : LanguageManager.Instance.Text("metaNotLoaded");
        }

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

        UpdateWordStatusBar();
    }

    private void UpdateWordStatusBar()
    {
        if (_leftDoc != null && _rightDoc != null)
        {
            int adds = _displayParagraphs.Count(p => p.StatusBgBrush == AddBg);
            int dels = _displayParagraphs.Count(p => p.StatusBgBrush == DelBg);
            int mods = _displayParagraphs.Count(p => p.StatusBgBrush == ModBg);

            if (WordStatsBadges != null) WordStatsBadges.Visibility = Visibility.Visible;
            if (StatusBarText != null) StatusBarText.Visibility = Visibility.Collapsed;

            string addLabel = LanguageManager.Instance.Text("statusAdded");
            string delLabel = LanguageManager.Instance.Text("statusDeleted");
            string modLabel = LanguageManager.Instance.Text("statusModified");

            if (WordAdditionsText != null)
                WordAdditionsText.Text = $"+{adds} {addLabel}";
            if (WordDeletionsText != null)
                WordDeletionsText.Text = $"-{dels} {delLabel}";
            if (WordModificationsText != null)
                WordModificationsText.Text = $"~{mods} {modLabel}";

            int leftWords = _leftDoc.Metadata.WordCount;
            int rightWords = _rightDoc.Metadata.WordCount;
            if (WordCountsText != null)
                WordCountsText.Text = $"Words: {leftWords} vs {rightWords}";
        }
        else if (_leftDoc != null || _rightDoc != null)
        {
            var doc = _leftDoc ?? _rightDoc;
            if (WordStatsBadges != null) WordStatsBadges.Visibility = Visibility.Collapsed;
            if (StatusBarText != null)
            {
                StatusBarText.Visibility = Visibility.Visible;
                StatusBarText.Text = $"{doc!.FileName} ({doc.Metadata.FileSizeFormatted} \u2022 {doc.Metadata.WordCount} words)";
            }
            if (WordCountsText != null) WordCountsText.Text = "";
        }
        else
        {
            if (WordStatsBadges != null) WordStatsBadges.Visibility = Visibility.Collapsed;
            if (StatusBarText != null)
            {
                StatusBarText.Visibility = Visibility.Visible;
                StatusBarText.Text = LanguageManager.Instance.Text("notCompared");
            }
            if (WordCountsText != null) WordCountsText.Text = "";
        }
    }

    private void UpdateWorkspaceVisibility()
    {
        if (!_isInitialized) return;

        bool hasLeft = !string.IsNullOrEmpty(_leftPath);
        bool hasRight = !string.IsNullOrEmpty(_rightPath);

        if (!hasLeft && !hasRight)
        {
            if (ActiveDiffContainer != null) ActiveDiffContainer.Visibility = Visibility.Collapsed;
            if (LeftEmptyZone != null) LeftEmptyZone.Visibility = Visibility.Visible;
            if (LeftSingleListView != null) LeftSingleListView.Visibility = Visibility.Collapsed;
            if (RightEmptyZone != null) RightEmptyZone.Visibility = Visibility.Visible;
            if (RightSingleListView != null) RightSingleListView.Visibility = Visibility.Collapsed;

            if (OutlineEmptyState != null) OutlineEmptyState.Visibility = Visibility.Visible;
            if (OutlineTree != null) OutlineTree.Visibility = Visibility.Collapsed;
            if (OutlineCountBadge != null) OutlineCountBadge.Text = "0";

            if (StatusBarText != null) StatusBarText.Text = LanguageManager.Instance.Text("notCompared");
            if (DiffBadge != null) DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountDocBadge"), 0);

            if (ExportReportBtn != null) ExportReportBtn.IsEnabled = false;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = false;
        }
        else if (hasLeft && !hasRight)
        {
            if (ActiveDiffContainer != null) ActiveDiffContainer.Visibility = Visibility.Collapsed;
            if (LeftEmptyZone != null) LeftEmptyZone.Visibility = Visibility.Collapsed;
            if (RightEmptyZone != null) RightEmptyZone.Visibility = Visibility.Visible;
            if (RightSingleListView != null) RightSingleListView.Visibility = Visibility.Collapsed;

            if (_leftDoc != null)
            {
                if (LeftSingleListView != null)
                {
                    LeftSingleListView.ItemsSource = _leftDoc.Paragraphs
                        .Select(p => new WordSingleParagraphDisplayItem
                        {
                            IndexText = $"{p.Index}",
                            Text = p.Text,
                            IsTableBlock = p.IsTableBlock,
                            TableTitle = p.Table != null ? $"Table ({p.Table.Rows.Count} \u00D7 {p.Table.ColumnCount})" : "",
                            TableSummary = p.Table != null ? $"{p.Table.Rows.Count} rows \u00D7 {p.Table.ColumnCount} cols" : "",
                            TableRows = p.Table != null ? CreateTableRowsDisplayItems(p.Table, null, isLeft: false) : new(),
                            MediaItems = CreateMediaDisplayItems(p.MediaItems, DiffChangeType.Unchanged)
                        })
                        .ToList();
                    LeftSingleListView.Visibility = Visibility.Visible;
                }
                BuildOutlineTree();
            }
            else if (!string.IsNullOrEmpty(_leftPath) && File.Exists(_leftPath))
            {
                _ = LoadSingleDocPreviewAsync(_leftPath, isLeft: true);
            }

            if (StatusBarText != null) StatusBarText.Text = LanguageManager.Instance.Text("loadedSourceDocPleaseSelectTarget");
            if (DiffBadge != null) DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountDocBadge"), 0);

            if (ExportReportBtn != null) ExportReportBtn.IsEnabled = false;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = true;
        }
        else if (!hasLeft && hasRight)
        {
            if (ActiveDiffContainer != null) ActiveDiffContainer.Visibility = Visibility.Collapsed;
            if (LeftEmptyZone != null) LeftEmptyZone.Visibility = Visibility.Visible;
            if (LeftSingleListView != null) LeftSingleListView.Visibility = Visibility.Collapsed;
            if (RightEmptyZone != null) RightEmptyZone.Visibility = Visibility.Collapsed;

            if (_rightDoc != null)
            {
                if (RightSingleListView != null)
                {
                    RightSingleListView.ItemsSource = _rightDoc.Paragraphs
                        .Select(p => new WordSingleParagraphDisplayItem
                        {
                            IndexText = $"{p.Index}",
                            Text = p.Text,
                            IsTableBlock = p.IsTableBlock,
                            TableTitle = p.Table != null ? $"Table ({p.Table.Rows.Count} \u00D7 {p.Table.ColumnCount})" : "",
                            TableSummary = p.Table != null ? $"{p.Table.Rows.Count} rows \u00D7 {p.Table.ColumnCount} cols" : "",
                            TableRows = p.Table != null ? CreateTableRowsDisplayItems(p.Table, null, isLeft: false) : new(),
                            MediaItems = CreateMediaDisplayItems(p.MediaItems, DiffChangeType.Unchanged)
                        })
                        .ToList();
                    RightSingleListView.Visibility = Visibility.Visible;
                }
                BuildOutlineTree();
            }
            else if (!string.IsNullOrEmpty(_rightPath) && File.Exists(_rightPath))
            {
                _ = LoadSingleDocPreviewAsync(_rightPath, isLeft: false);
            }

            if (StatusBarText != null) StatusBarText.Text = LanguageManager.Instance.Text("loadedTargetDocPleaseSelectSource");
            if (DiffBadge != null) DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountDocBadge"), 0);

            if (ExportReportBtn != null) ExportReportBtn.IsEnabled = false;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = true;
        }
        else
        {
            if (ActiveDiffContainer != null) ActiveDiffContainer.Visibility = Visibility.Visible;
            if (LeftEmptyZone != null) LeftEmptyZone.Visibility = Visibility.Collapsed;
            if (LeftSingleListView != null) LeftSingleListView.Visibility = Visibility.Collapsed;
            if (RightEmptyZone != null) RightEmptyZone.Visibility = Visibility.Collapsed;
            if (RightSingleListView != null) RightSingleListView.Visibility = Visibility.Collapsed;

            SwitchViewModeDisplay();

            if (ExportReportBtn != null) ExportReportBtn.IsEnabled = true;
            if (ClearAllBtn != null) ClearAllBtn.IsEnabled = true;
        }
    }

    private async Task LoadSingleDocPreviewAsync(string path, bool isLeft)
    {
        if (isLeft)
        {
            if (LeftLoadingOverlay != null) LeftLoadingOverlay.Visibility = Visibility.Visible;
            if (LeftSingleListView != null) LeftSingleListView.Visibility = Visibility.Collapsed;
        }
        else
        {
            if (RightLoadingOverlay != null) RightLoadingOverlay.Visibility = Visibility.Visible;
            if (RightSingleListView != null) RightSingleListView.Visibility = Visibility.Collapsed;
        }

        try
        {
            var doc = await Task.Run(() => WordDocumentParser.Parse(path));
            if (!string.IsNullOrEmpty(_leftPath) && !string.IsNullOrEmpty(_rightPath)) return;

            if (isLeft)
            {
                _leftDoc = doc;
                if (LeftSingleListView != null)
                {
                    LeftSingleListView.ItemsSource = doc.Paragraphs
                        .Select(p => new WordSingleParagraphDisplayItem
                        {
                            IndexText = $"{p.Index}",
                            Text = p.Text,
                            IsTableBlock = p.IsTableBlock,
                            TableTitle = p.Table != null ? $"Table ({p.Table.Rows.Count} \u00D7 {p.Table.ColumnCount})" : "",
                            TableSummary = p.Table != null ? $"{p.Table.Rows.Count} rows \u00D7 {p.Table.ColumnCount} cols" : "",
                            TableRows = p.Table != null ? CreateTableRowsDisplayItems(p.Table, null, isLeft: true) : new(),
                            MediaItems = CreateMediaDisplayItems(p.MediaItems, DiffChangeType.Unchanged)
                        })
                        .ToList();
                    LeftSingleListView.Visibility = Visibility.Visible;
                }
            }
            else
            {
                _rightDoc = doc;
                if (RightSingleListView != null)
                {
                    RightSingleListView.ItemsSource = doc.Paragraphs
                        .Select(p => new WordSingleParagraphDisplayItem
                        {
                            IndexText = $"{p.Index}",
                            Text = p.Text,
                            IsTableBlock = p.IsTableBlock,
                            TableTitle = p.Table != null ? $"Table ({p.Table.Rows.Count} \u00D7 {p.Table.ColumnCount})" : "",
                            TableSummary = p.Table != null ? $"{p.Table.Rows.Count} rows \u00D7 {p.Table.ColumnCount} cols" : "",
                            TableRows = p.Table != null ? CreateTableRowsDisplayItems(p.Table, null, isLeft: false) : new(),
                            MediaItems = CreateMediaDisplayItems(p.MediaItems, DiffChangeType.Unchanged)
                        })
                        .ToList();
                    RightSingleListView.Visibility = Visibility.Visible;
                }
            }
            BuildOutlineTree();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadSingleDocPreviewAsync error: {ex.Message}");
        }
        finally
        {
            if (isLeft && LeftLoadingOverlay != null) LeftLoadingOverlay.Visibility = Visibility.Collapsed;
            if (!isLeft && RightLoadingOverlay != null) RightLoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    public void LoadFiles(string? leftPath, string? rightPath)
    {
        if (!string.IsNullOrEmpty(leftPath) && File.Exists(leftPath))
        {
            _leftPath = leftPath;
            _leftDoc = null;
            if (LeftDocTitle != null) LeftDocTitle.Text = Path.GetFileName(leftPath);
            if (LeftDocSubtitle != null) LeftDocSubtitle.Text = leftPath;
        }

        if (!string.IsNullOrEmpty(rightPath) && File.Exists(rightPath))
        {
            _rightPath = rightPath;
            _rightDoc = null;
            if (RightDocTitle != null) RightDocTitle.Text = Path.GetFileName(rightPath);
            if (RightDocSubtitle != null) RightDocSubtitle.Text = rightPath;
        }

        if (!string.IsNullOrEmpty(_leftPath) && !string.IsNullOrEmpty(_rightPath))
        {
            RecentHistoryManager.Instance.AddRecord(_leftPath!, _rightPath!, CompareType.WordDiff);
            ParseAndCompare();
        }
        else
        {
            UpdateWorkspaceVisibility();
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

    private async void ParseAndCompare()
    {
        if (string.IsNullOrEmpty(_leftPath) || string.IsNullOrEmpty(_rightPath)) return;

        App.Log($"[WordDiffPage] Starting ParseAndCompare (left={_leftPath}, right={_rightPath})");
        UpdateWorkspaceVisibility();
        if (DualCompareLoadingOverlay != null) DualCompareLoadingOverlay.Visibility = Visibility.Visible;

        string lp = _leftPath;
        string rp = _rightPath;

        try
        {
            var (leftDoc, rightDoc) = await Task.Run(() =>
            {
                App.Log("[WordDiffPage] Parsing left document in background...");
                var l = WordDocumentParser.Parse(lp);
                App.Log($"[WordDiffPage] Left doc parsed: {l.Paragraphs.Count} paragraphs, {l.Tables.Count} tables");
                App.Log("[WordDiffPage] Parsing right document in background...");
                var r = WordDocumentParser.Parse(rp);
                App.Log($"[WordDiffPage] Right doc parsed: {r.Paragraphs.Count} paragraphs, {r.Tables.Count} tables");
                return (l, r);
            });

            _leftDoc = leftDoc;
            _rightDoc = rightDoc;

            App.Log("[WordDiffPage] Building OutlineTree...");
            BuildOutlineTree();
            App.Log("[WordDiffPage] Building ParagraphDiff...");
            BuildParagraphDiff();

            _tableDiffNeedsBuild = true;
            if (_viewMode == 2)
            {
                App.Log("[WordDiffPage] Building TableDiff (mode is Table)...");
                BuildTableDiff();
                _tableDiffNeedsBuild = false;
            }

            App.Log("[WordDiffPage] Building MetadataDiff...");
            BuildMetadataDiff();

            UpdateWorkspaceVisibility();
            App.Log("[WordDiffPage] ParseAndCompare finished successfully.");

            this.DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                App.Log("[WordDiffPage] UI DispatcherQueue Idle: XAML layout and initial render completed successfully!");
            });
        }
        catch (Exception ex)
        {
            App.LogFatalError("WordDiffPage.ParseAndCompare", ex);
            var targetWindow = (App.GetWindowForElement(this) as MainWindow) ?? App.ActiveWindow ?? App.MainWindowInstance;
            targetWindow?.ShowValidationDialog(
                LanguageManager.Instance.Text("dialogNoticeTitle"),
                LanguageManager.Instance.Text("errWordInvalidFormat") + $"\n({ex.Message})"
            );
        }
        finally
        {
            if (DualCompareLoadingOverlay != null) DualCompareLoadingOverlay.Visibility = Visibility.Collapsed;
        }
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
        var leftParas = _leftDoc?.Paragraphs ?? new List<WordParagraph>();
        var rightParas = _rightDoc?.Paragraphs ?? new List<WordParagraph>();

        string leftText = string.Join("\n", leftParas.Select(GetParagraphDiffKey));
        string rightText = string.Join("\n", rightParas.Select(GetParagraphDiffKey));

        var diff = DiffEngineService.Instance.CompareText(leftText, rightText);
        var aligned = DiffEngineService.Instance.BuildAlignedLines(diff);

        _allParagraphs.Clear();
        int adds = 0, dels = 0, mods = 0;

        Brush defaultTextFg = App.GetThemeBrush("TextFillColorPrimaryBrush");

        foreach (var line in aligned)
        {
            var leftPara = line.LeftLineNumber.HasValue && line.LeftLineNumber.Value >= 1 && line.LeftLineNumber.Value <= leftParas.Count
                ? leftParas[line.LeftLineNumber.Value - 1]
                : null;

            var rightPara = line.RightLineNumber.HasValue && line.RightLineNumber.Value >= 1 && line.RightLineNumber.Value <= rightParas.Count
                ? rightParas[line.RightLineNumber.Value - 1]
                : null;

            var item = new WordDiffParagraphDisplayItem
            {
                LeftIndexText = leftPara != null ? $"{leftPara.Index}" : (line.LeftLineNumber.HasValue ? $"{line.LeftLineNumber}" : ""),
                LeftText = leftPara?.Text ?? "",
                LeftFgBrush = defaultTextFg,
                RightIndexText = rightPara != null ? $"{rightPara.Index}" : (line.RightLineNumber.HasValue ? $"{line.RightLineNumber}" : ""),
                RightText = rightPara?.Text ?? "",
                RightFgBrush = defaultTextFg,
                IsTableBlock = (leftPara?.IsTableBlock ?? false) || (rightPara?.IsTableBlock ?? false),
                LeftTableTitle = leftPara?.Table != null ? $"Table ({leftPara.Table.Rows.Count} \u00D7 {leftPara.Table.ColumnCount})" : "",
                RightTableTitle = rightPara?.Table != null ? $"Table ({rightPara.Table.Rows.Count} \u00D7 {rightPara.Table.ColumnCount})" : ""
            };

            if (line.LeftType == DiffChangeType.Deleted)
            {
                item.StatusText = LanguageManager.Instance.Text("statusDeleted");
                item.StatusBgBrush = DelBg;
                item.StatusFgBrush = DelFg;
                item.LeftBgBrush = DelBg;
                item.LeftFgBrush = DelFg;
                item.CenterIconGlyph = "\uE738";
                item.CenterIconBrush = DelFg;
                item.LeftMediaItems = CreateMediaDisplayItems(leftPara?.MediaItems, DiffChangeType.Deleted);
                if (!item.IsTableBlock && !string.IsNullOrEmpty(item.LeftText))
                {
                    item.LeftTokens.Add(new WordDiffTokenItem
                    {
                        Text = item.LeftText,
                        ForegroundBrush = DelFg,
                        IsStrikethrough = true
                    });
                }
                dels++;
            }
            else if (line.RightType == DiffChangeType.Added)
            {
                item.StatusText = LanguageManager.Instance.Text("statusAdded");
                item.StatusBgBrush = AddBg;
                item.StatusFgBrush = AddFg;
                item.RightBgBrush = AddBg;
                item.RightFgBrush = AddFg;
                item.CenterIconGlyph = "\uE710";
                item.CenterIconBrush = AddFg;
                item.RightMediaItems = CreateMediaDisplayItems(rightPara?.MediaItems, DiffChangeType.Added);
                if (!item.IsTableBlock && !string.IsNullOrEmpty(item.RightText))
                {
                    item.RightTokens.Add(new WordDiffTokenItem
                    {
                        Text = item.RightText,
                        ForegroundBrush = AddFg,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                    });
                }
                adds++;
            }
            else if (line.LeftType == DiffChangeType.Modified || line.RightType == DiffChangeType.Modified)
            {
                item.StatusText = LanguageManager.Instance.Text("statusModified");
                item.StatusBgBrush = ModBg;
                item.StatusFgBrush = ModFg;
                item.LeftBgBrush = ModBg;
                item.RightBgBrush = ModBg;
                item.LeftFgBrush = ModFg;
                item.RightFgBrush = ModFg;
                item.CenterIconGlyph = "\uE70F";
                item.CenterIconBrush = ModFg;
                item.LeftMediaItems = CreateMediaDisplayItems(leftPara?.MediaItems, DiffChangeType.Modified, rightPara?.MediaItems);
                item.RightMediaItems = CreateMediaDisplayItems(rightPara?.MediaItems, DiffChangeType.Modified, leftPara?.MediaItems);

                if (!item.IsTableBlock)
                {
                    var (leftTokens, rightTokens) = ComputeInlineDiffTokens(item.LeftText, item.RightText);
                    item.LeftTokens = leftTokens;
                    item.RightTokens = rightTokens;
                }
                mods++;
            }
            else
            {
                item.StatusText = LanguageManager.Instance.Text("statusIdentical");
                item.LeftFgBrush = defaultTextFg;
                item.RightFgBrush = defaultTextFg;
                item.LeftMediaItems = CreateMediaDisplayItems(leftPara?.MediaItems, DiffChangeType.Unchanged);
                item.RightMediaItems = CreateMediaDisplayItems(rightPara?.MediaItems, DiffChangeType.Unchanged);
                if (!item.IsTableBlock)
                {
                    if (!string.IsNullOrEmpty(item.LeftText))
                    {
                        item.LeftTokens.Add(new WordDiffTokenItem { Text = item.LeftText, ForegroundBrush = defaultTextFg });
                    }
                    if (!string.IsNullOrEmpty(item.RightText))
                    {
                        item.RightTokens.Add(new WordDiffTokenItem { Text = item.RightText, ForegroundBrush = defaultTextFg });
                    }
                }
            }

            if (leftPara?.Table != null)
            {
                var t = leftPara.Table;
                item.LeftTableRows = CreateTableRowsDisplayItems(t, rightPara?.Table, isLeft: true);
                var previewCells = t.Rows.FirstOrDefault()?.Take(4).Where(c => !string.IsNullOrWhiteSpace(c)).ToList() ?? new List<string>();
                string preview = previewCells.Count > 0 ? " (" + string.Join(" | ", previewCells) + ")" : "";
                item.LeftTableSummary = $"{t.Rows.Count} rows \u00D7 {t.ColumnCount} cols{preview}";
            }
            if (rightPara?.Table != null)
            {
                var t = rightPara.Table;
                item.RightTableRows = CreateTableRowsDisplayItems(t, leftPara?.Table, isLeft: false);
                var previewCells = t.Rows.FirstOrDefault()?.Take(4).Where(c => !string.IsNullOrWhiteSpace(c)).ToList() ?? new List<string>();
                string preview = previewCells.Count > 0 ? " (" + string.Join(" | ", previewCells) + ")" : "";
                item.RightTableSummary = $"{t.Rows.Count} rows \u00D7 {t.ColumnCount} cols{preview}";
            }

            _allParagraphs.Add(item);
        }

        App.Log($"[WordDiffPage] BuildParagraphDiff created {_allParagraphs.Count} display items (tables: {_allParagraphs.Count(p => p.IsTableBlock)}). Applying filter...");
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
                p.RightIndexText.Contains(query) ||
                p.LeftMediaItems.Any(m => m.FileName.ToLowerInvariant().Contains(query)) ||
                p.RightMediaItems.Any(m => m.FileName.ToLowerInvariant().Contains(query)));
        }

        _displayParagraphs = filtered.ToList();
        App.Log($"[WordDiffPage] ApplyParagraphFilter: Setting ParagraphsList.ItemsSource with {_displayParagraphs.Count} items");
        if (ParagraphsList != null)
        {
            ParagraphsList.ItemsSource = null;
            ParagraphsList.ItemsSource = _displayParagraphs;
            App.Log("[WordDiffPage] ParagraphsList.ItemsSource assigned successfully.");
        }

        UpdateWordStatusBar();
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
        return App.GetThemeBrush("TextFillColorSecondaryBrush");
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
                if (_tableDiffNeedsBuild && (_leftDoc != null || _rightDoc != null))
                {
                    _tableDiffNeedsBuild = false;
                    BuildTableDiff();
                }
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
        var width = _isSidebarVisible ? new GridLength(_currentSidebarWidth) : new GridLength(0);
        if (SidebarCol != null) SidebarCol.Width = width;
        if (HeaderSidebarCol != null) HeaderSidebarCol.Width = width;
        if (SidebarSplitter != null) SidebarSplitter.Visibility = _isSidebarVisible ? Visibility.Visible : Visibility.Collapsed;
        if (HeaderSidebarSplitter != null) HeaderSidebarSplitter.Visibility = _isSidebarVisible ? Visibility.Visible : Visibility.Collapsed;
        if (ToggleSidebarBtn != null) ToolTipService.SetToolTip(ToggleSidebarBtn, LanguageManager.Instance.Text(_isSidebarVisible ? "collapseOutline" : "expandOutline"));
    }

    private void OnSidebarSplitterPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        try
        {
            this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast);
        }
        catch { }
    }

    private void OnSidebarSplitterPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        try
        {
            if (!_isDraggingSidebar)
            {
                this.ProtectedCursor = null;
            }
        }
        catch { }
    }

    private void OnSidebarSplitterPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_isSidebarVisible) return;
        if (sender is UIElement element)
        {
            _isDraggingSidebar = true;
            _dragStartPointerX = e.GetCurrentPoint(this).Position.X;
            _dragStartSidebarWidth = SidebarCol != null && SidebarCol.Width.Value > 0 ? SidebarCol.Width.Value : _currentSidebarWidth;
            element.CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }

    private void OnSidebarSplitterPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_isDraggingSidebar)
        {
            double currentX = e.GetCurrentPoint(this).Position.X;
            double deltaX = currentX - _dragStartPointerX;
            double newWidth = Math.Clamp(_dragStartSidebarWidth + deltaX, MinSidebarWidth, MaxSidebarWidth);

            _currentSidebarWidth = newWidth;
            if (SidebarCol != null) SidebarCol.Width = new GridLength(newWidth);
            if (HeaderSidebarCol != null) HeaderSidebarCol.Width = new GridLength(newWidth);
            e.Handled = true;
        }
    }

    private void OnSidebarSplitterPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_isDraggingSidebar && sender is UIElement element)
        {
            _isDraggingSidebar = false;
            try { element.ReleasePointerCapture(e.Pointer); } catch { }
            try { this.ProtectedCursor = null; } catch { }
            e.Handled = true;
        }
    }

    private void OnSidebarSplitterPointerCaptureLost(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_isDraggingSidebar)
        {
            _isDraggingSidebar = false;
            try { this.ProtectedCursor = null; } catch { }
        }
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
            bool hasBoth = !string.IsNullOrEmpty(_leftPath) && !string.IsNullOrEmpty(_rightPath);
            if (hasBoth)
            {
                var target = _displayParagraphs.FirstOrDefault(p => p.LeftIndexText.Contains(item.Index.ToString()) || p.RightIndexText.Contains(item.Index.ToString()));
                if (target != null && ParagraphsList != null)
                {
                    ParagraphsList.SelectedItem = target;
                    ParagraphsList.ScrollIntoView(target);
                }
            }
            else if (!string.IsNullOrEmpty(_leftPath) && LeftSingleListView?.ItemsSource is List<WordSingleParagraphDisplayItem> leftItems)
            {
                var target = leftItems.FirstOrDefault(p => p.IndexText == $"\u00A7 {item.Index}");
                if (target != null)
                {
                    LeftSingleListView.SelectedItem = target;
                    LeftSingleListView.ScrollIntoView(target);
                }
            }
            else if (!string.IsNullOrEmpty(_rightPath) && RightSingleListView?.ItemsSource is List<WordSingleParagraphDisplayItem> rightItems)
            {
                var target = rightItems.FirstOrDefault(p => p.IndexText == $"\u00A7 {item.Index}");
                if (target != null)
                {
                    RightSingleListView.SelectedItem = target;
                    RightSingleListView.ScrollIntoView(target);
                }
            }
        }
    }

    private void OnClearAllClicked(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;

        if (string.IsNullOrEmpty(_leftPath) && string.IsNullOrEmpty(_rightPath) && _leftDoc == null && _rightDoc == null)
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
        _leftDoc = null;
        _rightDoc = null;
        _allParagraphs.Clear();
        _displayParagraphs.Clear();
        _searchQuery = string.Empty;
        if (WordSearchBox != null) WordSearchBox.Text = string.Empty;
        if (ParagraphsList != null) ParagraphsList.ItemsSource = null;
        if (LeftSingleListView != null) LeftSingleListView.ItemsSource = null;
        if (RightSingleListView != null) RightSingleListView.ItemsSource = null;
        if (OutlineTree != null) OutlineTree.RootNodes.Clear();

        if (LeftDocTitle != null) LeftDocTitle.Text = LanguageManager.Instance.Text("pickSourceFile");
        if (LeftDocSubtitle != null) LeftDocSubtitle.Text = LanguageManager.Instance.Text("noFileSelected");
        if (RightDocTitle != null) RightDocTitle.Text = LanguageManager.Instance.Text("pickTargetFile");
        if (RightDocSubtitle != null) RightDocSubtitle.Text = LanguageManager.Instance.Text("noFileSelected");

        if (DiffBadge != null) DiffBadge.Text = string.Format(LanguageManager.Instance.Text("diffCountDocBadge"), 0);
        if (MetadataDetailsText != null) MetadataDetailsText.Text = LanguageManager.Instance.Text("metaNotLoaded");
        if (StatusBarText != null) StatusBarText.Text = LanguageManager.Instance.Text("notCompared");

        UpdateWorkspaceVisibility();
        UpdateTabHeader();
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
        try
        {
            if (TableDiffContainer == null) return;
            TableDiffContainer.Children.Clear();

            if (_leftDoc == null && _rightDoc == null)
            {
                return;
            }

            int leftTableCount = _leftDoc?.Tables.Count ?? 0;
            int rightTableCount = _rightDoc?.Tables.Count ?? 0;

            int max = Math.Max(leftTableCount, rightTableCount);
            App.Log($"[WordDiffPage] BuildTableDiff: processing {max} tables (left={leftTableCount}, right={rightTableCount})");
            if (max == 0)
            {
                TableDiffContainer.Children.Add(new TextBlock
                {
                    Text = LanguageManager.Instance.Text("wordNoTables"),
                    Foreground = App.GetThemeBrush("TextMutedBrush"),
                    FontSize = 13
                });
                return;
            }

        // Theme-aware high-contrast colors
        Brush cardBg = App.GetThemeBrush("CardAcrylicBackground");
        Brush cardBorder = App.GetThemeBrush("CardBorderBrush");

        Brush textPrimary = App.GetThemeBrush("TextFillColorPrimaryBrush");
        Brush textSecondary = App.GetThemeBrush("TextFillColorSecondaryBrush");

        Brush gridBorderBrush = App.GetThemeBrush("DividerStrokeColorDefaultBrush");
        Brush cellUnchangedBg = App.GetThemeBrush("CardAcrylicBackground");

        Brush cellAddedBg = App.GetThemeBrush("DiffAddedBgBrush");
        Brush cellDeletedBg = App.GetThemeBrush("DiffDeletedBgBrush");
        Brush cellModifiedBg = App.GetThemeBrush("DiffModifiedBgBrush");

        Brush textGreen = App.GetThemeBrush("DiffAddedFgBrush");
        Brush textRed = App.GetThemeBrush("DiffDeletedFgBrush");
        Brush textOrange = App.GetThemeBrush("DiffModifiedFgBrush");
        Brush textDim = App.GetThemeBrush("TextMutedBrush");

        for (int i = 0; i < max; i++)
        {
            var leftT = i < leftTableCount ? _leftDoc!.Tables[i] : null;
            var rightT = i < rightTableCount ? _rightDoc!.Tables[i] : null;

            int maxRows = Math.Max(leftT?.RowCount ?? 0, rightT?.RowCount ?? 0);
            int maxCols = Math.Max(leftT?.ColumnCount ?? 0, rightT?.ColumnCount ?? 0);

            var card = new Border
            {
                Background = cardBg,
                BorderBrush = cardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16)
            };

            var panel = new StackPanel { Spacing = 12 };

            // Header Row: Icon + Title + Subtitle on left, Status badge on right
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { Spacing = 4 };
            var titleLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            titleLine.Children.Add(new FontIcon
            {
                Glyph = "\uEDB3",
                FontSize = 14,
                Foreground = App.GetThemeBrush("BrandPrimaryBrush")
            });
            titleLine.Children.Add(new TextBlock
            {
                Text = string.Format(LanguageManager.Instance.Text("tableIndexHeader"), i + 1),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 14,
                Foreground = textPrimary
            });
            titleStack.Children.Add(titleLine);

            string lLabel = LanguageManager.Instance.Text("wordSideLeft");
            string rLabel = LanguageManager.Instance.Text("wordSideRight");
            string lInfo = leftT != null
                ? string.Format(LanguageManager.Instance.Text("tableInfoFormat"), lLabel, leftT.Rows.Count, leftT.Rows.FirstOrDefault()?.Count ?? 0)
                : string.Format(LanguageManager.Instance.Text("tableInfoNone"), lLabel);
            string rInfo = rightT != null
                ? string.Format(LanguageManager.Instance.Text("tableInfoFormat"), rLabel, rightT.Rows.Count, rightT.Rows.FirstOrDefault()?.Count ?? 0)
                : string.Format(LanguageManager.Instance.Text("tableInfoNone"), rLabel);

            titleStack.Children.Add(new TextBlock
            {
                Text = $"{lInfo}   |   {rInfo}",
                FontSize = 12,
                Foreground = textSecondary
            });
            Grid.SetColumn(titleStack, 0);
            headerGrid.Children.Add(titleStack);

            // Compute overall change type
            bool hasAnyCellDiff = false;
            for (int r = 0; r < maxRows; r++)
            {
                for (int c = 0; c < maxCols; c++)
                {
                    string lCell = (r < (leftT?.Rows.Count ?? 0) && c < (leftT?.Rows[r].Count ?? 0)) ? leftT!.Rows[r][c] : "";
                    string rCell = (r < (rightT?.Rows.Count ?? 0) && c < (rightT?.Rows[r].Count ?? 0)) ? rightT!.Rows[r][c] : "";
                    bool lEx = r < (leftT?.Rows.Count ?? 0) && c < (leftT?.Rows[r].Count ?? 0);
                    bool rEx = r < (rightT?.Rows.Count ?? 0) && c < (rightT?.Rows[r].Count ?? 0);
                    if (lEx != rEx || lCell != rCell)
                    {
                        hasAnyCellDiff = true;
                        break;
                    }
                }
                if (hasAnyCellDiff) break;
            }

            DiffChangeType tableChange = DiffChangeType.Unchanged;
            if (leftT == null && rightT != null) tableChange = DiffChangeType.Added;
            else if (leftT != null && rightT == null) tableChange = DiffChangeType.Deleted;
            else if (hasAnyCellDiff) tableChange = DiffChangeType.Modified;

            string statusText = tableChange switch
            {
                DiffChangeType.Added => LanguageManager.Instance.Text("statusAdded"),
                DiffChangeType.Deleted => LanguageManager.Instance.Text("statusDeleted"),
                DiffChangeType.Modified => LanguageManager.Instance.Text("statusModified"),
                _ => LanguageManager.Instance.Text("statusIdentical")
            };
            Brush statusBg = tableChange switch
            {
                DiffChangeType.Added => AddBg,
                DiffChangeType.Deleted => DelBg,
                DiffChangeType.Modified => ModBg,
                _ => App.GetThemeBrush("DiffIdenticalBgBrush")
            };
            Brush statusFg = tableChange switch
            {
                DiffChangeType.Added => textGreen,
                DiffChangeType.Deleted => textRed,
                DiffChangeType.Modified => textOrange,
                _ => textSecondary
            };

            var badge = new Border
            {
                Background = statusBg,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Top
            };
            badge.Child = new TextBlock
            {
                Text = statusText,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = statusFg
            };
            Grid.SetColumn(badge, 1);
            headerGrid.Children.Add(badge);

            panel.Children.Add(headerGrid);

            // Table Grid Container (Excel-like Grid Preview matching macOS)
            if (maxRows > 0 && maxCols > 0)
            {
                var gridBorder = new Border
                {
                    BorderBrush = gridBorderBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Background = cardBg
                };

                var tableGrid = new Grid();
                for (int r = 0; r < maxRows; r++)
                {
                    tableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }
                for (int c = 0; c < maxCols; c++)
                {
                    tableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }

                for (int r = 0; r < maxRows; r++)
                {
                    for (int c = 0; c < maxCols; c++)
                    {
                        bool lEx = r < (leftT?.Rows.Count ?? 0) && c < (leftT?.Rows[r].Count ?? 0);
                        bool rEx = r < (rightT?.Rows.Count ?? 0) && c < (rightT?.Rows[r].Count ?? 0);
                        string lCellText = lEx ? leftT!.Rows[r][c] : "";
                        string rCellText = rEx ? rightT!.Rows[r][c] : "";

                        DiffChangeType cType = DiffChangeType.Unchanged;
                        if (!lEx && rEx) cType = DiffChangeType.Added;
                        else if (lEx && !rEx) cType = DiffChangeType.Deleted;
                        else if (lCellText != rCellText) cType = DiffChangeType.Modified;

                        var cellBorder = new Border
                        {
                            BorderBrush = gridBorderBrush,
                            BorderThickness = new Thickness(0, 0, c < maxCols - 1 ? 1 : 0, r < maxRows - 1 ? 1 : 0),
                            Padding = new Thickness(8, 6, 8, 6),
                            MinHeight = 36,
                            Background = cType switch
                            {
                                DiffChangeType.Added => cellAddedBg,
                                DiffChangeType.Deleted => cellDeletedBg,
                                DiffChangeType.Modified => cellModifiedBg,
                                _ => cellUnchangedBg
                            }
                        };
                        Grid.SetRow(cellBorder, r);
                        Grid.SetColumn(cellBorder, c);

                        var cellStack = new StackPanel { Spacing = 2 };

                        if (!string.IsNullOrEmpty(lCellText))
                        {
                            var tbL = new TextBlock
                            {
                                Text = $"L: {lCellText}",
                                FontSize = 11,
                                Foreground = cType == DiffChangeType.Deleted ? textRed : textSecondary,
                                TextWrapping = TextWrapping.Wrap
                            };
                            cellStack.Children.Add(tbL);
                        }

                        if (!string.IsNullOrEmpty(rCellText))
                        {
                            var tbR = new TextBlock
                            {
                                Text = $"R: {rCellText}",
                                FontSize = 11,
                                FontWeight = cType == DiffChangeType.Modified ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                                Foreground = cType == DiffChangeType.Added ? textGreen : (cType == DiffChangeType.Modified ? textOrange : textPrimary),
                                TextWrapping = TextWrapping.Wrap
                            };
                            cellStack.Children.Add(tbR);
                        }

                        if (string.IsNullOrEmpty(lCellText) && string.IsNullOrEmpty(rCellText))
                        {
                            cellStack.Children.Add(new TextBlock
                            {
                                Text = "\u2014",
                                FontSize = 11,
                                Foreground = textDim
                            });
                        }

                        cellBorder.Child = cellStack;
                        tableGrid.Children.Add(cellBorder);
                    }
                }

                gridBorder.Child = tableGrid;
                panel.Children.Add(gridBorder);
            }

            card.Child = panel;
            TableDiffContainer.Children.Add(card);
        }
        App.Log($"[WordDiffPage] BuildTableDiff finished. Added cards: {TableDiffContainer.Children.Count}");
        }
        catch (Exception ex)
        {
            App.LogFatalError("WordDiffPage.BuildTableDiff", ex);
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

        string srcDocTitle = LanguageManager.Instance.Text("sourceDocument");
        string tgtDocTitle = LanguageManager.Instance.Text("targetDocument");
        string fileSizeTitle = LanguageManager.Instance.Text("fileSize");
        string bytesLabel = LanguageManager.Instance.Text("bytes");
        string creationTimeTitle = LanguageManager.Instance.Text("creationTime");
        string modTimeTitle = LanguageManager.Instance.Text("modificationTime");
        string totalParasTitle = LanguageManager.Instance.Text("totalParagraphs");
        string totalTablesTitle = LanguageManager.Instance.Text("totalTables");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"--- [{srcDocTitle}: {Path.GetFileName(_leftPath)}] ---");
        if (_leftDoc != null && !string.IsNullOrEmpty(_leftPath) && File.Exists(_leftPath))
        {
            var fi = new FileInfo(_leftPath);
            sb.AppendLine($"{fileSizeTitle}: {fi.Length / 1024.0:F1} KB ({fi.Length} {bytesLabel})");
            sb.AppendLine($"{creationTimeTitle}: {fi.CreationTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"{modTimeTitle}: {fi.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"{totalParasTitle}: {_leftDoc.Paragraphs.Count}");
            sb.AppendLine($"{totalTablesTitle}: {_leftDoc.Tables.Count}");
        }
        else
        {
            sb.AppendLine(LanguageManager.Instance.Text("metaNotLoaded"));
        }

        sb.AppendLine();
        sb.AppendLine($"--- [{tgtDocTitle}: {Path.GetFileName(_rightPath)}] ---");
        if (_rightDoc != null && !string.IsNullOrEmpty(_rightPath) && File.Exists(_rightPath))
        {
            var fi = new FileInfo(_rightPath);
            sb.AppendLine($"{fileSizeTitle}: {fi.Length / 1024.0:F1} KB ({fi.Length} {bytesLabel})");
            sb.AppendLine($"{creationTimeTitle}: {fi.CreationTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"{modTimeTitle}: {fi.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"{totalParasTitle}: {_rightDoc.Paragraphs.Count}");
            sb.AppendLine($"{totalTablesTitle}: {_rightDoc.Tables.Count}");
        }
        else
        {
            sb.AppendLine(LanguageManager.Instance.Text("metaNotLoaded"));
        }

        if (MetadataDetailsText != null)
            MetadataDetailsText.Text = sb.ToString();
    }

    private bool CheckFileValidation(string path)
    {
        var check = FileFormatValidator.Validate(path, CompareType.WordDiff);
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

    private async void OnPickLeftDocClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { ".docx", ".doc" });
        if (file != null)
        {
            if (!CheckFileValidation(file.Path)) return;

            _leftPath = file.Path;
            _leftDoc = null;
            if (LeftDocTitle != null) LeftDocTitle.Text = file.Name;
            if (LeftDocSubtitle != null) LeftDocSubtitle.Text = file.Path;

            if (!string.IsNullOrEmpty(_rightPath))
            {
                RecentHistoryManager.Instance.AddRecord(_leftPath!, _rightPath!, CompareType.WordDiff);
                ParseAndCompare();
            }
            else
            {
                UpdateWorkspaceVisibility();
            }
            UpdateTabHeader();
        }
    }

    private async void OnPickRightDocClicked(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickSingleFileAsync(new[] { ".docx", ".doc" });
        if (file != null)
        {
            if (!CheckFileValidation(file.Path)) return;

            _rightPath = file.Path;
            _rightDoc = null;
            if (RightDocTitle != null) RightDocTitle.Text = file.Name;
            if (RightDocSubtitle != null) RightDocSubtitle.Text = file.Path;

            if (!string.IsNullOrEmpty(_leftPath))
            {
                RecentHistoryManager.Instance.AddRecord(_leftPath!, _rightPath!, CompareType.WordDiff);
                ParseAndCompare();
            }
            else
            {
                UpdateWorkspaceVisibility();
            }
            UpdateTabHeader();
        }
    }

    public void HandleDualDrop(string leftPath, string rightPath)
    {
        if (string.IsNullOrEmpty(leftPath) || string.IsNullOrEmpty(rightPath)) return;
        if (!CheckFileValidation(leftPath) || !CheckFileValidation(rightPath)) return;
        _leftPath = leftPath;
        _rightPath = rightPath;
        _leftDoc = null;
        _rightDoc = null;
        if (LeftDocTitle != null) LeftDocTitle.Text = Path.GetFileName(leftPath);
        if (LeftDocSubtitle != null) LeftDocSubtitle.Text = leftPath;
        if (RightDocTitle != null) RightDocTitle.Text = Path.GetFileName(rightPath);
        if (RightDocSubtitle != null) RightDocSubtitle.Text = rightPath;
        RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.WordDiff);
        ParseAndCompare();
        UpdateTabHeader();
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
        try
        {
            App.Log("[WordDiffPage] OnLeftDocDrop triggered");
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                App.Log($"[WordDiffPage] OnLeftDocDrop items count: {items?.Count ?? 0}");
                if (items != null && items.Count > 0)
                {
                    e.Handled = true;
                    if (items.Count >= 2)
                    {
                        HandleDualDrop(items[0].Path, items[1].Path);
                        return;
                    }

                    string targetPath = items[0].Path;
                    App.Log($"[WordDiffPage] OnLeftDocDrop targetPath: {targetPath}");
                    HandleDropFromWindow(targetPath, isLeft: true);
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"[WordDiffPage] OnLeftDocDrop error: {ex.Message}");
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
        try
        {
            App.Log("[WordDiffPage] OnRightDocDrop triggered");
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                App.Log($"[WordDiffPage] OnRightDocDrop items count: {items?.Count ?? 0}");
                if (items != null && items.Count > 0)
                {
                    e.Handled = true;
                    if (items.Count >= 2)
                    {
                        HandleDualDrop(items[0].Path, items[1].Path);
                        return;
                    }

                    string targetPath = items[0].Path;
                    App.Log($"[WordDiffPage] OnRightDocDrop targetPath: {targetPath}");
                    HandleDropFromWindow(targetPath, isLeft: false);
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"[WordDiffPage] OnRightDocDrop error: {ex.Message}");
        }
    }

    public void HandleDropFromWindow(string path, bool isLeft)
    {
        if (string.IsNullOrEmpty(path)) return;
        App.Log($"[WordDiffPage] HandleDropFromWindow: isLeft={isLeft}, path={path}");
        if (!CheckFileValidation(path)) return;
        if (isLeft)
        {
            _leftPath = path;
            _leftDoc = null;
            if (LeftDocTitle != null) LeftDocTitle.Text = Path.GetFileName(path);
            if (LeftDocSubtitle != null) LeftDocSubtitle.Text = path;
            if (!string.IsNullOrEmpty(_rightPath))
            {
                RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.WordDiff);
                ParseAndCompare();
            }
            else
            {
                UpdateWorkspaceVisibility();
            }
            UpdateTabHeader();
        }
        else
        {
            _rightPath = path;
            _rightDoc = null;
            if (RightDocTitle != null) RightDocTitle.Text = Path.GetFileName(path);
            if (RightDocSubtitle != null) RightDocSubtitle.Text = path;
            if (!string.IsNullOrEmpty(_leftPath))
            {
                RecentHistoryManager.Instance.AddRecord(_leftPath, _rightPath, CompareType.WordDiff);
                ParseAndCompare();
            }
            else
            {
                UpdateWorkspaceVisibility();
            }
            UpdateTabHeader();
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
            App.Log($"[WordDiffPage] OnDiffContainerDrop error: {ex.Message}");
        }

        var pos = e.GetPosition(this);
        double totalWidth = this.ActualWidth > 0 ? this.ActualWidth : 900;
        if (pos.X < totalWidth / 2)
        {
            OnLeftDocDrop(sender, e);
        }
        else
        {
            OnRightDocDrop(sender, e);
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

    private static (List<WordDiffTokenItem> leftTokens, List<WordDiffTokenItem> rightTokens) ComputeInlineDiffTokens(string left, string right)
    {
        var leftTokens = new List<WordDiffTokenItem>();
        var rightTokens = new List<WordDiffTokenItem>();

        if (string.IsNullOrEmpty(left) && string.IsNullOrEmpty(right))
        {
            return (leftTokens, rightTokens);
        }
        if (string.IsNullOrEmpty(left))
        {
            rightTokens.Add(new WordDiffTokenItem
            {
                Text = right,
                ForegroundBrush = AddFg,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            return (leftTokens, rightTokens);
        }
        if (string.IsNullOrEmpty(right))
        {
            leftTokens.Add(new WordDiffTokenItem
            {
                Text = left,
                ForegroundBrush = DelFg,
                IsStrikethrough = true
            });
            return (leftTokens, rightTokens);
        }
        if (left == right)
        {
            leftTokens.Add(new WordDiffTokenItem { Text = left });
            rightTokens.Add(new WordDiffTokenItem { Text = right });
            return (leftTokens, rightTokens);
        }

        int prefixLen = 0;
        while (prefixLen < left.Length && prefixLen < right.Length && left[prefixLen] == right[prefixLen])
        {
            prefixLen++;
        }

        int suffixLen = 0;
        while (suffixLen < left.Length - prefixLen && suffixLen < right.Length - prefixLen &&
               left[left.Length - 1 - suffixLen] == right[right.Length - 1 - suffixLen])
        {
            suffixLen++;
        }

        string leftMid = left.Substring(prefixLen, left.Length - prefixLen - suffixLen);
        string rightMid = right.Substring(prefixLen, right.Length - prefixLen - suffixLen);

        var (midLeftTokens, midRightTokens) = ComputeLcsTokens(leftMid, rightMid);

        if (prefixLen > 0)
        {
            leftTokens.Add(new WordDiffTokenItem { Text = left.Substring(0, prefixLen) });
            rightTokens.Add(new WordDiffTokenItem { Text = right.Substring(0, prefixLen) });
        }

        leftTokens.AddRange(midLeftTokens);
        rightTokens.AddRange(midRightTokens);

        if (suffixLen > 0)
        {
            leftTokens.Add(new WordDiffTokenItem { Text = left.Substring(left.Length - suffixLen, suffixLen) });
            rightTokens.Add(new WordDiffTokenItem { Text = right.Substring(right.Length - suffixLen, suffixLen) });
        }

        return (leftTokens, rightTokens);
    }

    private static (List<WordDiffTokenItem> leftTokens, List<WordDiffTokenItem> rightTokens) ComputeLcsTokens(string left, string right)
    {
        var leftTokens = new List<WordDiffTokenItem>();
        var rightTokens = new List<WordDiffTokenItem>();

        int n = left.Length;
        int m = right.Length;

        if (n == 0 && m == 0) return (leftTokens, rightTokens);
        if (n == 0)
        {
            rightTokens.Add(new WordDiffTokenItem
            {
                Text = right,
                ForegroundBrush = AddFg,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            return (leftTokens, rightTokens);
        }
        if (m == 0)
        {
            leftTokens.Add(new WordDiffTokenItem
            {
                Text = left,
                ForegroundBrush = DelFg,
                IsStrikethrough = true
            });
            return (leftTokens, rightTokens);
        }

        if ((long)n * m > 1_000_000)
        {
            leftTokens.Add(new WordDiffTokenItem { Text = left, ForegroundBrush = DelFg, IsStrikethrough = true });
            rightTokens.Add(new WordDiffTokenItem { Text = right, ForegroundBrush = AddFg, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            return (leftTokens, rightTokens);
        }

        int[,] dp = new int[n + 1, m + 1];
        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                if (left[i - 1] == right[j - 1])
                {
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                }
                else
                {
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }
        }

        int ci = n;
        int cj = m;
        var leftMatched = new HashSet<int>();
        var rightMatched = new HashSet<int>();

        while (ci > 0 && cj > 0)
        {
            if (left[ci - 1] == right[cj - 1])
            {
                leftMatched.Add(ci - 1);
                rightMatched.Add(cj - 1);
                ci--;
                cj--;
            }
            else if (dp[ci - 1, cj] >= dp[ci, cj - 1])
            {
                ci--;
            }
            else
            {
                cj--;
            }
        }

        int segStart = 0;
        while (segStart < n)
        {
            bool isDel = !leftMatched.Contains(segStart);
            int segEnd = segStart + 1;
            while (segEnd < n && (!leftMatched.Contains(segEnd)) == isDel)
            {
                segEnd++;
            }
            string chunk = left.Substring(segStart, segEnd - segStart);
            if (isDel)
            {
                leftTokens.Add(new WordDiffTokenItem
                {
                    Text = chunk,
                    ForegroundBrush = DelFg,
                    IsStrikethrough = true
                });
            }
            else
            {
                leftTokens.Add(new WordDiffTokenItem { Text = chunk });
            }
            segStart = segEnd;
        }

        segStart = 0;
        while (segStart < m)
        {
            bool isAdd = !rightMatched.Contains(segStart);
            int segEnd = segStart + 1;
            while (segEnd < m && (!rightMatched.Contains(segEnd)) == isAdd)
            {
                segEnd++;
            }
            string chunk = right.Substring(segStart, segEnd - segStart);
            if (isAdd)
            {
                rightTokens.Add(new WordDiffTokenItem
                {
                    Text = chunk,
                    ForegroundBrush = AddFg,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                });
            }
            else
            {
                rightTokens.Add(new WordDiffTokenItem { Text = chunk });
            }
            segStart = segEnd;
        }

        return (leftTokens, rightTokens);
    }

    private static string GetParagraphDiffKey(WordParagraph p)
    {
        if (p.IsTableBlock && p.Table != null)
        {
            return "[Table:" + string.Join("|", p.Table.Rows.Select(r => string.Join(",", r.Select(c => (c ?? "").Replace("\r", "").Replace("\n", " "))))) + "]";
        }
        if (p.MediaItems != null && p.MediaItems.Count > 0)
        {
            var mediaKeys = string.Join(";", p.MediaItems.Select(m => $"{m.MediaType}:{m.HashSHA256}:{m.ShapeType}:{m.FillColorHex}"));
            string safeText = (p.Text ?? "").Replace("\r", "").Replace("\n", " ");
            if (!string.IsNullOrWhiteSpace(safeText))
                return safeText + " [Media:" + mediaKeys + "]";
            return "[Media:" + mediaKeys + "]";
        }
        return (p.Text ?? "").Replace("\r", "").Replace("\n", " ");
    }

    private List<WordMediaDisplayItem> CreateMediaDisplayItems(
        IReadOnlyList<WordMediaItem>? items,
        DiffChangeType changeType,
        IReadOnlyList<WordMediaItem>? oppositeItems = null)
    {
        if (items == null || items.Count == 0) return new List<WordMediaDisplayItem>();

        var list = new List<WordMediaDisplayItem>();
        foreach (var m in items)
        {
            var display = new WordMediaDisplayItem
            {
                MediaItem = m,
                FileName = !string.IsNullOrEmpty(m.FileName) ? m.FileName : (m.MediaType == WordMediaType.Shape ? (m.ShapeType ?? "Shape") : "Media"),
                FormattedSize = m.FormattedSize,
                IsImage = m.MediaType == WordMediaType.Image,
                IsShape = m.MediaType == WordMediaType.Shape,
                ShapeName = m.ShapeType ?? "Shape"
            };

            string ext = (Path.GetExtension(m.LocalCachePath) ?? "").ToLowerInvariant();
            bool isSupportedRaster = ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".ico";

            if (display.IsImage && isSupportedRaster && !string.IsNullOrEmpty(m.LocalCachePath))
            {
                display.ImageSource = SafeLoadBitmapImage(m.LocalCachePath, 320);
            }
            else if (display.IsImage && !isSupportedRaster)
            {
                display.ImageSource = null;
            }
            else if (display.IsShape)
            {
                display.ShapeFillBrush = ParseColorBrush(m.FillColorHex, isFill: true);
                display.ShapeStrokeBrush = ParseColorBrush(m.StrokeColorHex, isFill: false);
            }

            if (changeType == DiffChangeType.Added)
            {
                display.StatusBadgeText = "\u25CF " + LanguageManager.Instance.Text("statusAdded");
                display.StatusBadgeBgBrush = new SolidColorBrush(Colors.Transparent);
                display.StatusBadgeFgBrush = AddFg;
            }
            else if (changeType == DiffChangeType.Deleted)
            {
                display.StatusBadgeText = "\u25CF " + LanguageManager.Instance.Text("statusDeleted");
                display.StatusBadgeBgBrush = new SolidColorBrush(Colors.Transparent);
                display.StatusBadgeFgBrush = DelFg;
            }
            else if (changeType == DiffChangeType.Modified)
            {
                bool matchedInOpposite = oppositeItems != null && oppositeItems.Any(op =>
                    !string.IsNullOrEmpty(m.HashSHA256) && op.HashSHA256 == m.HashSHA256);

                if (!matchedInOpposite)
                {
                    string modBadge = LanguageManager.Instance.Text("statusModified");
                    string mediaBadge = LanguageManager.Instance.Text("mediaContentChanged");
                    display.StatusBadgeText = $"\u25CF {modBadge} \u00B7 {mediaBadge}";
                    display.StatusBadgeBgBrush = new SolidColorBrush(Colors.Transparent);
                    display.StatusBadgeFgBrush = ModFg;
                }
            }

            list.Add(display);
        }
        return list;
    }

    private List<WordTableRowDisplayItem> CreateTableRowsDisplayItems(WordTable? currentT, WordTable? oppositeT, bool isLeft)
    {
        if (currentT == null || currentT.Rows.Count == 0) return new();

        Brush textPrimary = App.GetThemeBrush("TextFillColorPrimaryBrush");
        Brush cellBorderBrush = App.GetThemeBrush("DividerStrokeColorDefaultBrush");
        Brush headerBg = App.GetThemeBrush("CardAcrylicBackground");

        Brush addedBg = App.GetThemeBrush("DiffAddedBgBrush");
        Brush deletedBg = App.GetThemeBrush("DiffDeletedBgBrush");
        Brush modifiedBg = App.GetThemeBrush("DiffModifiedBgBrush");

        Brush textGreen = App.GetThemeBrush("DiffAddedFgBrush");
        Brush textRed = App.GetThemeBrush("DiffDeletedFgBrush");
        Brush textOrange = App.GetThemeBrush("DiffModifiedFgBrush");

        // Compute adaptive column widths for pixel-perfect alignment
        int colCount = 0;
        for (int r = 0; r < currentT.Rows.Count; r++)
        {
            colCount = Math.Max(colCount, currentT.Rows[r].Count);
        }
        double[] colWidths = new double[colCount];
        for (int c = 0; c < colCount; c++)
        {
            int maxLen = 0;
            for (int r = 0; r < currentT.Rows.Count; r++)
            {
                if (c < currentT.Rows[r].Count)
                {
                    maxLen = Math.Max(maxLen, (currentT.Rows[r][c] ?? "").Length);
                }
            }
            colWidths[c] = Math.Clamp(85.0 + maxLen * 7.5, 85.0, 240.0);
        }

        var rowsList = new List<WordTableRowDisplayItem>();
        for (int r = 0; r < currentT.Rows.Count; r++)
        {
            var row = currentT.Rows[r];
            var rowItem = new WordTableRowDisplayItem();

            for (int c = 0; c < row.Count; c++)
            {
                string cellText = row[c];
                bool oppHasCell = oppositeT != null && r < oppositeT.Rows.Count && c < oppositeT.Rows[r].Count;
                string oppText = oppHasCell ? oppositeT!.Rows[r][c] : "";

                DiffChangeType cType = DiffChangeType.Unchanged;
                if (oppositeT != null)
                {
                    if (!oppHasCell)
                    {
                        cType = isLeft ? DiffChangeType.Deleted : DiffChangeType.Added;
                    }
                    else if (cellText != oppText)
                    {
                        cType = DiffChangeType.Modified;
                    }
                }

                Brush bg = new SolidColorBrush(Colors.Transparent);
                Brush fg = textPrimary;
                if (r == 0)
                {
                    bg = headerBg;
                }

                if (cType == DiffChangeType.Added)
                {
                    bg = addedBg;
                    fg = textGreen;
                }
                else if (cType == DiffChangeType.Deleted)
                {
                    bg = deletedBg;
                    fg = textRed;
                }
                else if (cType == DiffChangeType.Modified)
                {
                    bg = modifiedBg;
                    fg = textOrange;
                }

                double cellWidth = (c < colWidths.Length) ? colWidths[c] : 90.0;
                rowItem.Cells.Add(new WordTableCellDisplayItem
                {
                    Text = string.IsNullOrEmpty(cellText) ? " " : cellText,
                    BackgroundBrush = bg,
                    ForegroundBrush = fg,
                    BorderBrush = cellBorderBrush,
                    IsHeader = r == 0,
                    Width = cellWidth
                });
            }
            rowsList.Add(rowItem);
        }
        return rowsList;
    }

    private static Brush ParseColorBrush(string? hexOrName, bool isFill)
    {
        if (string.IsNullOrEmpty(hexOrName))
        {
            return isFill
                ? App.GetThemeBrush("BrandInfoSubtleBgBrush")
                : App.GetThemeBrush("BrandInfoBrush");
        }

        try
        {
            string clean = hexOrName.TrimStart('#');
            if (clean.Length == 6 && uint.TryParse(clean, System.Globalization.NumberStyles.HexNumber, null, out uint rgb))
            {
                byte r = (byte)((rgb >> 16) & 0xFF);
                byte g = (byte)((rgb >> 8) & 0xFF);
                byte b = (byte)(rgb & 0xFF);
                byte a = isFill ? (byte)160 : (byte)255;
                return new SolidColorBrush(Windows.UI.Color.FromArgb(a, r, g, b));
            }
        }
        catch
        {
        }

        return isFill
            ? App.GetThemeBrush("BrandInfoSubtleBgBrush")
            : App.GetThemeBrush("BrandInfoBrush");
    }

    private static BitmapImage? SafeLoadBitmapImage(string? localPath, int decodeWidth = 320)
    {
        if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath)) return null;

        try
        {
            byte[] bytes = File.ReadAllBytes(localPath);
            if (bytes == null || bytes.Length == 0) return null;

            // In unpackaged WinUI 3, using MemoryStream with IRandomAccessStream prevents
            // STATUS_IN_PAGE_ERROR (0xC0000006) caused by DirectUI attempting to memory-map file URIs.
            var ms = new MemoryStream(bytes);
            var ras = ms.AsRandomAccessStream();

            var bmp = new BitmapImage();
            if (decodeWidth > 0)
            {
                bmp.DecodePixelWidth = decodeWidth;
            }
            bmp.SetSource(ras);
            return bmp;
        }
        catch (Exception ex)
        {
            App.Log($"[WordDiffPage] SafeLoadBitmapImage error: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        try
        {
            App.Log("[WordDiffPage] Disposing resources...");
            LanguageManager.Instance.LanguageChanged -= UpdateLocalization;

            // 1. Safely unbind UI controls from data source to prevent virtualization list reset
            if (ParagraphsList != null)
            {
                ParagraphsList.ItemsSource = null;
            }
            if (LeftSingleListView != null)
            {
                LeftSingleListView.ItemsSource = null;
            }
            if (RightSingleListView != null)
            {
                RightSingleListView.ItemsSource = null;
            }
            if (OutlineTree != null)
            {
                OutlineTree.RootNodes.Clear();
            }

            // 2. Clean background data collections without touching UI state
            _leftPath = null;
            _rightPath = null;
            _leftDoc = null;
            _rightDoc = null;
            _allParagraphs.Clear();
            _displayParagraphs.Clear();

            App.Log("[WordDiffPage] Dispose completed.");
        }
        catch (Exception ex)
        {
            App.Log($"[WordDiffPage] Dispose exception: {ex.Message}");
        }
    }
}
