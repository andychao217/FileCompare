using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinCompare.UI.ViewModels;
using WinCompare.UI.Views;

namespace WinCompare.UI.Helpers;

public static class TabDragManager
{
    public static MainWindow? SourceWindow { get; set; }
    public static TabViewItem? DraggedTabItem { get; set; }
    public static CompareTabViewModel? DraggedViewModel { get; set; }
    public static bool IsTracking { get; set; }
    public static bool IsDragging { get; set; }
    public static MainWindow.POINT DragStartPoint { get; set; }
    private static DispatcherTimer? _dragTimer;

    public static void StartTracking(MainWindow sourceWin, TabViewItem tabItem, MainWindow.POINT startPt)
    {
        Reset();
        SourceWindow = sourceWin;
        DraggedTabItem = tabItem;
        DraggedViewModel = sourceWin.GetOrCreateViewModel(tabItem);
        DragStartPoint = startPt;
        IsTracking = true;
        IsDragging = false;

        if (_dragTimer == null)
        {
            _dragTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _dragTimer.Tick += OnDragTimerTick;
        }
        _dragTimer.Start();
    }

    private static void OnDragTimerTick(object? sender, object e)
    {
        if (!IsTracking)
        {
            _dragTimer?.Stop();
            return;
        }

        bool isLeftDown = (MainWindow.GetAsyncKeyState(MainWindow.VK_LBUTTON) & 0x8000) != 0;
        MainWindow.GetCursorPos(out MainWindow.POINT currentPt);

        if (!isLeftDown)
        {
            _dragTimer?.Stop();
            CompleteDrag(currentPt);
            return;
        }

        if (!IsDragging)
        {
            int startX = DragStartPoint.X;
            int startY = DragStartPoint.Y;
            int curX = currentPt.X;
            int curY = currentPt.Y;
            int dx = Math.Abs(curX - startX);
            int dy = Math.Abs(curY - startY);
            if (dx >= 6 || dy >= 6)
            {
                IsDragging = true;
                if (SourceWindow != null && DraggedTabItem != null)
                {
                    SourceWindow.SetTabPlaceholderState(DraggedTabItem, true);
                }
            }
        }

        if (IsDragging)
        {
            MainWindow.UpdateDragState(currentPt);
        }
    }

    public static void CompleteDrag(MainWindow.POINT releasePt)
    {
        bool wasDragging = IsDragging;
        var tabItem = DraggedTabItem;
        var tabVm = DraggedViewModel;
        var sourceWin = SourceWindow;

        MainWindow.HideAllInsertionIndicators();

        if (sourceWin == null || tabItem == null || tabVm == null)
        {
            MainWindow.HideAllGhosts();
            Reset();
            return;
        }

        if (!wasDragging)
        {
            MainWindow.HideAllGhosts();
            sourceWin.SetTabPlaceholderState(tabItem, false);
            if (sourceWin.MainTabView.TabItems.Contains(tabItem))
            {
                sourceWin.SelectTab(tabItem);
            }
            Reset();
            return;
        }

        Reset();
        sourceWin.HandleDragRelease(tabVm, releasePt);
    }

    public static void Reset()
    {
        _dragTimer?.Stop();
        SourceWindow = null;
        DraggedTabItem = null;
        DraggedViewModel = null;
        IsTracking = false;
        IsDragging = false;
        DragStartPoint = default;
    }
}
