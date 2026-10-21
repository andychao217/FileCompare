﻿﻿using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using WinCompare.UI.ViewModels;

namespace WinCompare.UI.Controls;

public sealed partial class MinimapControl : UserControl
{
    public event Action<int>? LineSelected;

    private IReadOnlyList<DiffLineDisplayItem>? _items;
    private bool _isDragging = false;
    private double _viewportRatio = 0.0;
    private double _visibleRatio = 0.1;

    private static Brush AddedBrush => App.GetThemeBrush("DiffAddedBgBrush");
    private static Brush DeletedBrush => App.GetThemeBrush("DiffDeletedBgBrush");
    private static Brush ModifiedBrush => App.GetThemeBrush("DiffModifiedBgBrush");
    private static Brush UnchangedBrush => new SolidColorBrush(Windows.UI.Color.FromArgb(46, 128, 128, 128));

    public MinimapControl()
    {
        this.InitializeComponent();
    }

    public void SetItems(IReadOnlyList<DiffLineDisplayItem>? items)
    {
        _items = items;
        RenderMinimap();
    }

    public void UpdateViewport(double scrollRatio, double visibleRatio = 0.0)
    {
        // 关键防护：当用户正在手动拖拽 Minimap 时，绝不允许 ScrollViewer 异步滚动事件反向覆写，彻底杜绝上下抖动
        if (_isDragging) return;

        _viewportRatio = Math.Clamp(scrollRatio, 0.0, 1.0);
        UpdateThumbPosition();
    }

    private void OnRootGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderMinimap();
        UpdateThumbPosition();
    }

    private void RenderMinimap()
    {
        if (ColorCanvas == null) return;
        ColorCanvas.Children.Clear();

        if (_items == null || _items.Count == 0)
        {
            if (ViewportThumb != null) ViewportThumb.Visibility = Visibility.Collapsed;
            return;
        }

        if (ViewportThumb != null) ViewportThumb.Visibility = Visibility.Visible;

        double canvasWidth = Math.Max(10, ActualWidth - 8);
        double canvasHeight = Math.Max(20, ActualHeight - 12);
        int totalLines = _items.Count;
        double step = canvasHeight / totalLines;

        // 采用步长合并或绘制
        for (int i = 0; i < totalLines; i++)
        {
            var item = _items[i];
            Brush brush;

            if (item.LeftSign == "-" || item.RightSign == "-")
            {
                brush = DeletedBrush;
            }
            else if (item.LeftSign == "+" || item.RightSign == "+")
            {
                brush = AddedBrush;
            }
            else if (item.IsDifference)
            {
                brush = ModifiedBrush;
            }
            else
            {
                brush = UnchangedBrush;
            }

            var rect = new Rectangle
            {
                Width = canvasWidth,
                Height = Math.Max(step, 1.5),
                Fill = brush
            };

            Canvas.SetLeft(rect, 0);
            Canvas.SetTop(rect, i * step);
            ColorCanvas.Children.Add(rect);
        }

        UpdateThumbPosition();
    }

    private void UpdateThumbPosition()
    {
        if (ViewportThumb == null || ActualHeight <= 20) return;

        double totalHeight = ActualHeight;
        int totalLines = Math.Max(_items?.Count ?? 1, 1);

        // 与 macOS 版算法完全一致：基于总行数稳定计算视口滑块高度，彻底解决拖拽滚动时因虚拟化跳动导致的忽大忽小
        double ratio = Math.Min(25.0 / totalLines, 0.35);
        double thumbHeight = Math.Max(totalHeight * ratio, 36.0);
        ViewportThumb.Height = thumbHeight;

        double maxOffset = Math.Max(0, totalHeight - thumbHeight);
        double top = _viewportRatio * maxOffset;

        ViewportThumb.Margin = new Thickness(3, top, 3, 0);
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_items == null || _items.Count == 0) return;

        _isDragging = true;
        RootGrid.CapturePointer(e.Pointer);
        HandlePointerJump(e);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging) return;
        HandlePointerJump(e);
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            RootGrid.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
    }

    private void HandlePointerJump(PointerRoutedEventArgs e)
    {
        if (_items == null || _items.Count == 0) return;

        var point = e.GetCurrentPoint(RootGrid);
        double totalHeight = ActualHeight;
        if (totalHeight <= 20) return;

        int totalLines = Math.Max(_items.Count, 1);
        double ratioHeight = Math.Min(25.0 / totalLines, 0.35);
        double thumbHeight = Math.Max(totalHeight * ratioHeight, 36.0);
        double maxOffset = Math.Max(1.0, totalHeight - thumbHeight);

        // 让视口滑块中心跟随鼠标位置，拖拽平滑自然、绝不跳变
        double top = Math.Clamp(point.Position.Y - (thumbHeight / 2.0), 0.0, maxOffset);
        _viewportRatio = top / maxOffset;
        UpdateThumbPosition();

        int targetLine = (int)(_viewportRatio * (_items.Count - 1));
        targetLine = Math.Clamp(targetLine, 0, _items.Count - 1);
        LineSelected?.Invoke(targetLine);
    }
}
