using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using WinCompare.Core.Models;
using WinCompare.Core.Services;
using WinCompare.UI.Helpers;

// Disambiguate core model PdfDocument and WinRT Windows.Data.Pdf.PdfDocument
using CorePdfDocument = WinCompare.Core.Models.PdfDocument;
using WinDataPdfDocument = Windows.Data.Pdf.PdfDocument;

namespace WinCompare.UI.Views;

public sealed partial class PdfDiffPage : Page
{
    private CorePdfDocument? _leftDoc;
    private CorePdfDocument? _rightDoc;
    private WinDataPdfDocument? _winLeftPdfDoc;
    private WinDataPdfDocument? _winRightPdfDoc;
    private Windows.Foundation.Size _leftPageSize = new(595, 842);
    private Windows.Foundation.Size _rightPageSize = new(595, 842);

    private PdfDiffResult _diffResult = new();
    private PdfVisualDiffResult? _currentVisualDiff;
    private uint _lastLeftRenderW = 0;
    private uint _lastLeftRenderH = 0;
    private uint _lastRightRenderW = 0;
    private uint _lastRightRenderH = 0;

    private int _currentPage = 0;
    private double _zoom = 1.0;
    private bool _isSyncingScroll = false;
    private int _currentDiffIndex = 0;
    private bool _isOverlayMode = false;
    public bool IsOverlayMode => _isOverlayMode;

    private bool _isInitialized = false;

    public PdfDiffPage()
    {
        this.InitializeComponent();
        _isInitialized = true;
        this.Loaded += OnPageLoaded;
        this.Unloaded += OnPageUnloaded;
        UpdateLocalization();
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        LanguageManager.Instance.LanguageChanged += UpdateLocalization;
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        LanguageManager.Instance.LanguageChanged -= UpdateLocalization;
    }

    public void UpdateLocalization()
    {
        var lm = LanguageManager.Instance;
        SideBySideText.Text = lm.Text("pdfDiffSideBySide");
        OverlayText.Text = lm.Text("pdfDiffOverlay");
        ShowMaskText.Text = lm.Text("pdfDiffShowMask");
        SyncScrollText.Text = lm.Text("pdfDiffSyncScroll");
        IgnoreWhitespaceText.Text = lm.Text("ignoreWhitespace");
        ExportReportText.Text = lm.Text("exportReport");

        LeftEmptyPromptText.Text = lm.Text("pdfDiffDropHint");
        RightEmptyPromptText.Text = lm.Text("pdfDiffDropHint");
        LeftSelectBtnText.Text = lm.Text("pdfDiffSelectLeftFile");
        RightSelectBtnText.Text = lm.Text("pdfDiffSelectRightFile");

        ToolTipService.SetToolTip(PrevPageBtn, lm.Text("prevPage"));
        ToolTipService.SetToolTip(NextPageBtn, lm.Text("nextPage"));
        ToolTipService.SetToolTip(PrevDiffBtn, lm.Text("prevDiff"));
        ToolTipService.SetToolTip(NextDiffBtn, lm.Text("nextDiff"));
        ToolTipService.SetToolTip(ZoomOutBtn, lm.Text("zoomOut"));
        ToolTipService.SetToolTip(ZoomInBtn, lm.Text("zoomIn"));
        ToolTipService.SetToolTip(ExportReportBtn, lm.Text("exportReport"));

        UpdateStatusAndDiffCount();
    }

    public async void LoadFiles(string? leftPath, string? rightPath)
    {
        await LoadFilesAsync(leftPath, rightPath);
    }

    public async Task LoadFilesAsync(string? leftPath, string? rightPath)
    {
        if (!string.IsNullOrEmpty(leftPath) && File.Exists(leftPath))
        {
            _leftDoc = PdfDocumentParser.Parse(leftPath);
            LeftHeaderTitle.Text = _leftDoc.FileName;
            try
            {
                var sf = await StorageFile.GetFileFromPathAsync(leftPath);
                _winLeftPdfDoc = await WinDataPdfDocument.LoadFromFileAsync(sf);
                if (_winLeftPdfDoc.PageCount > 0)
                {
                    _leftDoc.PageCount = (int)_winLeftPdfDoc.PageCount;
                }
            }
            catch (Exception ex)
            {
                App.Log($"[PdfDiffPage] Load winLeftPdfDoc error: {ex.Message}");
            }
        }

        if (!string.IsNullOrEmpty(rightPath) && File.Exists(rightPath))
        {
            _rightDoc = PdfDocumentParser.Parse(rightPath);
            RightHeaderTitle.Text = _rightDoc.FileName;
            try
            {
                var sf = await StorageFile.GetFileFromPathAsync(rightPath);
                _winRightPdfDoc = await WinDataPdfDocument.LoadFromFileAsync(sf);
                if (_winRightPdfDoc.PageCount > 0)
                {
                    _rightDoc.PageCount = (int)_winRightPdfDoc.PageCount;
                }
            }
            catch (Exception ex)
            {
                App.Log($"[PdfDiffPage] Load winRightPdfDoc error: {ex.Message}");
            }
        }

        _currentPage = 0;
        _currentDiffIndex = 0;
        ComputeDiffAndRender();
        UpdateTabHeader();
    }

    private void UpdateTabHeader()
    {
        try
        {
            string header = !string.IsNullOrEmpty(_leftDoc?.FileName) ? _leftDoc.FileName : LanguageManager.Instance.Text("pdfDiffTitle");
            App.CurrentMainWindow?.UpdateTabHeaderForPage(this, header);
        }
        catch { }
    }

    public void HandleDropFromWindow(string path, bool isLeft)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return;

        if (isLeft)
        {
            LoadFiles(path, _rightDoc?.FilePath);
        }
        else
        {
            LoadFiles(_leftDoc?.FilePath, path);
        }
    }

    public void HandleDualDrop(string leftPath, string rightPath)
    {
        LoadFiles(leftPath, rightPath);
    }

    private void ComputeDiffAndRender()
    {
        if (_leftDoc != null && _rightDoc != null)
        {
            bool ignoreWs = IgnoreWhitespaceCheck.IsChecked ?? false;
            _diffResult = PdfDiffEngine.Compare(_leftDoc, _rightDoc, ignoreWs);

            if (!string.IsNullOrEmpty(_leftDoc.FilePath) && !string.IsNullOrEmpty(_rightDoc.FilePath))
            {
                RecentHistoryManager.Instance.AddRecord(_leftDoc.FilePath, _rightDoc.FilePath, CompareType.PdfDiff);
            }
        }
        else
        {
            _diffResult = new PdfDiffResult();
        }

        UpdateStatusAndDiffCount();
        RenderCurrentPage();
    }

    private void UpdateStatusAndDiffCount()
    {
        var lm = LanguageManager.Instance;
        int diffCount = _currentVisualDiff?.BoundingBoxes.Count ?? _diffResult.Blocks.Count(b => b.ChangeType != PdfDiffChangeType.Unchanged);

        if (diffCount == 0)
        {
            DiffCountText.Text = lm.Text("pdfDiffNoDiff");
            DiffCountText.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
            PrevDiffBtn.IsEnabled = false;
            NextDiffBtn.IsEnabled = false;
            DiffBadgeCapsule.Visibility = Visibility.Collapsed;
        }
        else
        {
            int displayIdx = Math.Min(_currentDiffIndex + 1, diffCount);
            DiffCountText.Text = string.Format(lm.Text("pdfDiffRegionNavFormat"), displayIdx, diffCount);
            DiffCountText.Foreground = new SolidColorBrush(Color.FromArgb(255, 234, 88, 12));
            PrevDiffBtn.IsEnabled = true;
            NextDiffBtn.IsEnabled = true;

            // Update floating badge
            DiffBadgeText.Text = string.Format(lm.Text("pdfDiffCountFormat"), diffCount);
            DiffBadgeCapsule.Visibility = (ShowMaskCheck.IsChecked ?? true) ? Visibility.Visible : Visibility.Collapsed;
        }

        // Status bar texts
        if (_leftDoc != null)
        {
            string pageFormat = string.Format(lm.Text("pdfPagesCountFormat"), _leftDoc.PageCount);
            LeftStatusText.Text = $"{_leftDoc.FileName}: {pageFormat} ({_leftDoc.Metadata.FileSizeFormatted})";
        }
        else
        {
            LeftStatusText.Text = string.Empty;
        }

        if (_rightDoc != null)
        {
            string pageFormat = string.Format(lm.Text("pdfPagesCountFormat"), _rightDoc.PageCount);
            string extra = diffCount > 0 ? $" · 👁 {string.Format(lm.Text("pdfDiffCountFormat"), diffCount)}" : "";
            RightStatusText.Text = $"{_rightDoc.FileName}: {pageFormat} ({_rightDoc.Metadata.FileSizeFormatted}){extra}";
        }
        else
        {
            RightStatusText.Text = string.Empty;
        }
    }

    private async void RenderCurrentPage()
    {
        int maxPages = Math.Max(_leftDoc?.PageCount ?? 0, _rightDoc?.PageCount ?? 0);
        maxPages = Math.Max(1, maxPages);

        PageIndicatorText.Text = $"{_currentPage + 1} / {maxPages}";
        PrevPageBtn.IsEnabled = _currentPage > 0;
        NextPageBtn.IsEnabled = _currentPage + 1 < maxPages;

        var leftTask = RenderPaneAsync(LeftPageImage, LeftPaperCard, LeftScrollViewer, LeftEmptyPanel, _winLeftPdfDoc, _leftDoc, isLeft: true);
        var rightTask = RenderPaneAsync(RightPageImage, RightPaperCard, RightScrollViewer, RightEmptyPanel, _winRightPdfDoc, _rightDoc, isLeft: false);

        var (leftPixels, leftW, leftH) = await leftTask;
        var (rightPixels, rightW, rightH) = await rightTask;

        _lastLeftRenderW = leftW;
        _lastLeftRenderH = leftH;
        _lastRightRenderW = rightW;
        _lastRightRenderH = rightH;

        if (leftPixels != null && rightPixels != null)
        {
            int w = (int)Math.Min(leftW, rightW);
            int h = (int)Math.Min(leftH, rightH);
            _currentVisualDiff = await Task.Run(() => PdfPixelDiffEngine.ComparePixelBuffers(leftPixels, rightPixels, w, h, _currentPage));
        }
        else
        {
            _currentVisualDiff = null;
        }

        _currentDiffIndex = 0;
        DrawDiffOverlays();
        UpdateStatusAndDiffCount();
    }

    private async Task<(byte[]? Pixels, uint Width, uint Height)> RenderPaneAsync(
        Image pageImage,
        Border paperCard,
        ScrollViewer sv,
        Grid emptyPanel,
        WinDataPdfDocument? winPdfDoc,
        CorePdfDocument? doc,
        bool isLeft)
    {
        if (pageImage == null || paperCard == null || sv == null || emptyPanel == null) return (null, 0, 0);

        if (doc == null)
        {
            sv.Visibility = Visibility.Collapsed;
            emptyPanel.Visibility = Visibility.Visible;
            return (null, 0, 0);
        }

        sv.Visibility = Visibility.Visible;
        emptyPanel.Visibility = Visibility.Collapsed;

        if (winPdfDoc != null && _currentPage < winPdfDoc.PageCount)
        {
            try
            {
                using var page = winPdfDoc.GetPage((uint)_currentPage);
                if (isLeft) _leftPageSize = page.Size;
                else _rightPageSize = page.Size;

                paperCard.Width = page.Size.Width * _zoom;
                paperCard.Height = page.Size.Height * _zoom;

                using var stream = new InMemoryRandomAccessStream();
                var renderOpts = new Windows.Data.Pdf.PdfPageRenderOptions
                {
                    DestinationWidth = (uint)(page.Size.Width * Math.Max(1.5, _zoom * 1.5)),
                    DestinationHeight = (uint)(page.Size.Height * Math.Max(1.5, _zoom * 1.5))
                };
                await page.RenderToStreamAsync(stream, renderOpts);

                stream.Seek(0);
                var bmp = new BitmapImage();
                await bmp.SetSourceAsync(stream);
                pageImage.Source = bmp;

                stream.Seek(0);
                var decoder = await BitmapDecoder.CreateAsync(stream);
                var pixelData = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Rgba8,
                    BitmapAlphaMode.Premultiplied,
                    new BitmapTransform(),
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.ColorManageToSRgb);
                byte[] pixels = pixelData.DetachPixelData();
                return (pixels, decoder.PixelWidth, decoder.PixelHeight);
            }
            catch (Exception ex)
            {
                App.Log($"[PdfDiffPage] Render page error: {ex.Message}");
            }
        }

        // Fallback dimensions
        paperCard.Width = 595 * _zoom;
        paperCard.Height = 842 * _zoom;
        return (null, 0, 0);
    }

    private void DrawDiffOverlays()
    {
        LeftDiffCanvas?.Children.Clear();
        RightDiffCanvas?.Children.Clear();

        if (!(ShowMaskCheck.IsChecked ?? true) || _currentVisualDiff == null || _currentVisualDiff.BoundingBoxes.Count == 0)
        {
            return;
        }

        double leftCardW = LeftPaperCard.Width;
        double leftCardH = LeftPaperCard.Height;
        double rightCardW = RightPaperCard.Width;
        double rightCardH = RightPaperCard.Height;

        double leftScaleX = _lastLeftRenderW > 0 ? leftCardW / _lastLeftRenderW : 1.0;
        double leftScaleY = _lastLeftRenderH > 0 ? leftCardH / _lastLeftRenderH : 1.0;
        double rightScaleX = _lastRightRenderW > 0 ? rightCardW / _lastRightRenderW : 1.0;
        double rightScaleY = _lastRightRenderH > 0 ? rightCardH / _lastRightRenderH : 1.0;

        for (int i = 0; i < _currentVisualDiff.BoundingBoxes.Count; i++)
        {
            var box = _currentVisualDiff.BoundingBoxes[i];
            bool isCurrent = (i == _currentDiffIndex);

            if (LeftDiffCanvas != null)
            {
                var leftRect = CreateDiffHighlightRect(box, leftScaleX, leftScaleY, isLeft: true, isCurrent: isCurrent);
                LeftDiffCanvas.Children.Add(leftRect);
            }

            if (RightDiffCanvas != null)
            {
                var rightRect = CreateDiffHighlightRect(box, rightScaleX, rightScaleY, isLeft: false, isCurrent: isCurrent);
                RightDiffCanvas.Children.Add(rightRect);
            }
        }
    }

    private Rectangle CreateDiffHighlightRect(PdfBoundingBox box, double scaleX, double scaleY, bool isLeft, bool isCurrent)
    {
        double pad = 2 * _zoom;
        double x = Math.Max(0, (box.X * scaleX) - pad);
        double y = Math.Max(0, (box.Y * scaleY) - pad);
        double w = (box.Width * scaleX) + (pad * 2);
        double h = (box.Height * scaleY) + (pad * 2);

        var rect = new Rectangle
        {
            Width = Math.Max(w, 8),
            Height = Math.Max(h, 8),
            RadiusX = 4,
            RadiusY = 4,
            Fill = isLeft
                ? new SolidColorBrush(Color.FromArgb((byte)(isCurrent ? 70 : 35), 239, 68, 68))
                : new SolidColorBrush(Color.FromArgb((byte)(isCurrent ? 70 : 35), 34, 197, 94)),
            Stroke = isLeft
                ? new SolidColorBrush(Color.FromArgb((byte)(isCurrent ? 255 : 180), 239, 68, 68))
                : new SolidColorBrush(Color.FromArgb((byte)(isCurrent ? 255 : 180), 34, 197, 94)),
            StrokeThickness = isCurrent ? 2.5 : 1.5
        };

        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        return rect;
    }

    // MARK: - View Mode Switching

    private void OnSideBySideModeClicked(object sender, RoutedEventArgs e)
    {
        _isOverlayMode = false;
        SideBySideModeBtn.Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        SideBySideText.Foreground = (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"];
        OverlayModeBtn.Background = new SolidColorBrush(Colors.Transparent);
        OverlayText.Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
        LeftPaperCard.Opacity = 1.0;
        RightPaperCard.Opacity = 1.0;
    }

    private void OnOverlayModeClicked(object sender, RoutedEventArgs e)
    {
        _isOverlayMode = true;
        OverlayModeBtn.Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        OverlayText.Foreground = (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"];
        SideBySideModeBtn.Background = new SolidColorBrush(Colors.Transparent);
        SideBySideText.Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
        LeftPaperCard.Opacity = 0.65;
        RightPaperCard.Opacity = 0.65;
    }

    // MARK: - Navigation

    private void OnPrevPageClicked(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 0)
        {
            _currentPage--;
            RenderCurrentPage();
        }
    }

    private void OnNextPageClicked(object sender, RoutedEventArgs e)
    {
        int maxPages = Math.Max(_leftDoc?.PageCount ?? 0, _rightDoc?.PageCount ?? 0);
        if (_currentPage + 1 < maxPages)
        {
            _currentPage++;
            RenderCurrentPage();
        }
    }

    private void OnPrevDiffClicked(object sender, RoutedEventArgs e)
    {
        if (_currentVisualDiff == null || _currentVisualDiff.BoundingBoxes.Count == 0) return;

        _currentDiffIndex = (_currentDiffIndex - 1 + _currentVisualDiff.BoundingBoxes.Count) % _currentVisualDiff.BoundingBoxes.Count;
        ScrollToCurrentDiff();
        DrawDiffOverlays();
        UpdateStatusAndDiffCount();
    }

    private void OnNextDiffClicked(object sender, RoutedEventArgs e)
    {
        if (_currentVisualDiff == null || _currentVisualDiff.BoundingBoxes.Count == 0) return;

        _currentDiffIndex = (_currentDiffIndex + 1) % _currentVisualDiff.BoundingBoxes.Count;
        ScrollToCurrentDiff();
        DrawDiffOverlays();
        UpdateStatusAndDiffCount();
    }

    private void ScrollToCurrentDiff()
    {
        if (_currentVisualDiff == null || _currentDiffIndex < 0 || _currentDiffIndex >= _currentVisualDiff.BoundingBoxes.Count) return;
        var box = _currentVisualDiff.BoundingBoxes[_currentDiffIndex];
        double scaleY = _lastLeftRenderH > 0 ? LeftPaperCard.Height / _lastLeftRenderH : 1.0;
        double targetY = Math.Max(0, (box.Y * scaleY) - 80);
        LeftScrollViewer?.ChangeView(null, targetY, null);
        RightScrollViewer?.ChangeView(null, targetY, null);
    }

    // MARK: - Zoom Controls

    private void OnZoomInClicked(object sender, RoutedEventArgs e)
    {
        _zoom = Math.Min(4.0, _zoom + 0.2);
        ApplyZoom();
    }

    private void OnZoomOutClicked(object sender, RoutedEventArgs e)
    {
        _zoom = Math.Max(0.5, _zoom - 0.2);
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        ZoomText.Text = $"{(int)(_zoom * 100)}%";
        if (LeftPaperCard != null && _leftPageSize.Width > 0)
        {
            LeftPaperCard.Width = _leftPageSize.Width * _zoom;
            LeftPaperCard.Height = _leftPageSize.Height * _zoom;
        }
        if (RightPaperCard != null && _rightPageSize.Width > 0)
        {
            RightPaperCard.Width = _rightPageSize.Width * _zoom;
            RightPaperCard.Height = _rightPageSize.Height * _zoom;
        }
        DrawDiffOverlays();
    }

    private void OnOptionChanged(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        DrawDiffOverlays();
        UpdateStatusAndDiffCount();
    }

    // MARK: - File Pickers

    private async void OnChooseLeftFileClicked(object sender, RoutedEventArgs e)
    {
        var path = await PickPdfFileAsync();
        if (!string.IsNullOrEmpty(path))
        {
            LoadFiles(path, _rightDoc?.FilePath);
        }
    }

    private async void OnChooseRightFileClicked(object sender, RoutedEventArgs e)
    {
        var path = await PickPdfFileAsync();
        if (!string.IsNullOrEmpty(path))
        {
            LoadFiles(_leftDoc?.FilePath, path);
        }
    }

    private async Task<string?> PickPdfFileAsync()
    {
        try
        {
            var file = await PickerHelper.PickSingleFileAsync(new[] { ".pdf" }, App.GetWindowForElement(this));
            return file?.Path;
        }
        catch
        {
            return null;
        }
    }

    // MARK: - Drag & Drop

    private void OnPaneDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = LanguageManager.Instance.Text("openFile");
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.IsContentVisible = true;
    }

    private async void OnLeftPaneDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var file = items.OfType<StorageFile>().FirstOrDefault(f => f.FileType.Equals(".pdf", StringComparison.OrdinalIgnoreCase));
            if (file != null)
            {
                LoadFiles(file.Path, _rightDoc?.FilePath);
            }
        }
    }

    private async void OnRightPaneDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var file = items.OfType<StorageFile>().FirstOrDefault(f => f.FileType.Equals(".pdf", StringComparison.OrdinalIgnoreCase));
            if (file != null)
            {
                LoadFiles(_leftDoc?.FilePath, file.Path);
            }
        }
    }

    // MARK: - Synchronized Scroll

    private void OnLeftScrollViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_isSyncingScroll || !(SyncScrollCheck.IsChecked ?? false)) return;
        _isSyncingScroll = true;
        RightScrollViewer.ChangeView(null, LeftScrollViewer.VerticalOffset, null, true);
        _isSyncingScroll = false;
    }

    private void OnRightScrollViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_isSyncingScroll || !(SyncScrollCheck.IsChecked ?? false)) return;
        _isSyncingScroll = true;
        LeftScrollViewer.ChangeView(null, RightScrollViewer.VerticalOffset, null, true);
        _isSyncingScroll = false;
    }

    // MARK: - Export Report

    // MARK: - Export Report

    private async void OnExportReportClicked(object sender, RoutedEventArgs e)
    {
        if (_leftDoc == null || _rightDoc == null) return;
        try
        {
            var choices = new Dictionary<string, IList<string>>
            {
                { "HTML 差异审阅报告", new List<string> { ".html" } }
            };
            var file = await PickerHelper.PickSaveFileAsync(
                ".html",
                choices,
                $"PDF_Diff_{_leftDoc.FileName}_vs_{_rightDoc.FileName}.html",
                App.GetWindowForElement(this)
            );
            if (file != null)
            {
                ExportReportBtn.IsEnabled = false;
                string originalText = ExportReportText.Text;
                var currentLang = LanguageManager.Instance.EffectiveLanguage;
                ExportReportText.Text = currentLang == AppLanguage.En ? "Exporting..." : (currentLang == AppLanguage.Ja ? "エクスポート中..." : "导出中...");

                try
                {
                    var html = PdfReportExporter.GenerateHtmlReport(
                        _leftDoc, 
                        _rightDoc, 
                        _diffResult, 
                        _currentVisualDiff, 
                        currentLang, 
                        SettingsManager.Instance.Theme);
                    await Windows.Storage.FileIO.WriteTextAsync(file, html);
                }
                finally
                {
                    ExportReportBtn.IsEnabled = true;
                    ExportReportText.Text = originalText;
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"[PdfDiffPage] Export report error: {ex.Message}");
        }
    }
}
