using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using WinCompare.Core.Models;
using WinCompare.Core.Services;

namespace WinCompare.UI.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        App.ActiveWindows.Add(this);
        App.ActiveWindow = this;

        this.Activated += (sender, args) =>
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated)
            {
                App.ActiveWindow = this;
            }
        };

        this.Closed += (sender, args) =>
        {
            TerminateIfLastWindow();
        };

        // Extend into title bar and integrate with Windows 11 Snap Layouts
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(CustomDragRegion);

        // Store strong reference to AppWindow to prevent GC collected event handlers
        _nativeAppWindow = this.AppWindow;
        if (_nativeAppWindow != null)
        {
            _nativeAppWindow.Closing += (sender, args) =>
            {
                TerminateIfLastWindow();
            };
            _nativeAppWindow.Destroying += (sender, args) =>
            {
                TerminateIfLastWindow();
            };
        }

        // Set native Windows 11 taskbar & window icon
        try
        {
            string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (System.IO.File.Exists(iconPath))
            {
                _nativeAppWindow?.SetIcon(iconPath);
            }
        }
        catch
        {
            // Ignore icon failure on headless/virtualized environments
        }

        // 底层 Win32 消息钩子：拦截 WM_CLOSE (0x0010) 与 WM_DESTROY (0x0002)
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (hwnd != IntPtr.Zero)
            {
                _subclassProc ??= SubclassWindowProc;
                SetWindowSubclass(hwnd, _subclassProc, (UIntPtr)(ulong)hwnd.ToInt64(), IntPtr.Zero);
            }
        }
        catch
        {
        }

        // Initialize language
        UpdateLanguageStrings();
        LanguageManager.Instance.LanguageChanged += UpdateLanguageStrings;

        // Initialize Theme from Settings
        var savedTheme = SettingsManager.Instance.Theme switch
        {
            "Dark" => ElementTheme.Dark,
            "Light" => ElementTheme.Light,
            _ => ElementTheme.Default
        };
        if (this.Content is FrameworkElement root)
        {
            root.RequestedTheme = savedTheme;
        }

        // Initialize Welcome tab with zero-flicker cached viewport
        EnsureWelcomePage();
        ShowContent(_welcomePage);

        MainTabView.SelectionChanged += OnTabSelectionChanged;
        MainTabView.TabTearOutWindowRequested += OnTabTearOutWindowRequested;
        MainTabView.TabTearOutRequested += OnTabTearOutRequested;
        MainTabView.ExternalTornOutTabsDropping += OnExternalTornOutTabsDropping;
        MainTabView.ExternalTornOutTabsDropped += OnExternalTornOutTabsDropped;

        // Handle Ctrl+W to close active tab cleanly without rendering accelerator keytip tooltip
        if (this.Content is FrameworkElement rootElement)
        {
            rootElement.KeyDown += (s, args) =>
            {
                if (args.Key == VirtualKey.W &&
                    Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
                {
                    CloseCurrentTab();
                    args.Handled = true;
                }
            };
        }

        if (DialogOverlay != null)
        {
            DialogOverlay.PointerPressed += OnDialogOverlayPointerPressed;
        }
    }

    private void UpdateLanguageStrings()
    {
        if (WelcomeTabItem != null) WelcomeTabItem.Header = "WinCompare";
        if (TabMenuBtn != null) ToolTipService.SetToolTip(TabMenuBtn, LanguageManager.Instance.Text("general"));

        if (MenuTextDiff != null) MenuTextDiff.Text = LanguageManager.Instance.Text("textDiffTitle");
        if (MenuExcelDiff != null) MenuExcelDiff.Text = LanguageManager.Instance.Text("excelDiffTitle");
        if (MenuWordDiff != null) MenuWordDiff.Text = LanguageManager.Instance.Text("wordDiffTitle");
        if (MenuFolderDiff != null) MenuFolderDiff.Text = LanguageManager.Instance.Text("folderDiffTitle");
        if (MenuThreeWayMerge != null) MenuThreeWayMerge.Text = LanguageManager.Instance.Text("threeWayMerge");
        if (MenuSettings != null) MenuSettings.Text = LanguageManager.Instance.Text("settingsTitle");
        if (MenuHelp != null) MenuHelp.Text = LanguageManager.Instance.Text("helpTitle");
    }

    private void OnHelpClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var helpControl = new HelpControl();
            helpControl.CloseRequested += CloseInAppDialog;
            ShowInAppDialog(helpControl);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnHelpClicked exception: {ex}");
        }
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var settingsControl = new SettingsControl();
            settingsControl.CloseRequested += CloseInAppDialog;
            settingsControl.ThemeChanged += (theme) =>
            {
                foreach (var window in App.ActiveWindows)
                {
                    if (window.Content is FrameworkElement root)
                    {
                        root.RequestedTheme = theme;
                    }
                }
            };

            ShowInAppDialog(settingsControl);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnSettingsClicked exception: {ex}");
        }
    }

    private void ShowInAppDialog(UIElement content)
    {
        DialogContentPresenter.Content = content;
        DialogOverlay.Visibility = Visibility.Visible;
    }

    public void CloseInAppDialog()
    {
        DialogOverlay.Visibility = Visibility.Collapsed;
        DialogContentPresenter.Content = null;
    }

    private void OnDialogOverlayPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, DialogOverlay))
        {
            CloseInAppDialog();
            e.Handled = true;
        }
    }

    private void OnAddTabClicked(object sender, RoutedEventArgs args)
    {
        OpenTextDiffTab();
    }

    private void OnNewTextDiffMenuClicked(object sender, RoutedEventArgs e)
    {
        OpenTextDiffTab();
    }

    private void OnNewExcelDiffMenuClicked(object sender, RoutedEventArgs e)
    {
        OpenExcelDiffTab();
    }

    private void OnNewWordDiffMenuClicked(object sender, RoutedEventArgs e)
    {
        OpenWordDiffTab();
    }

    private void OnNewFolderDiffMenuClicked(object sender, RoutedEventArgs e)
    {
        OpenFolderDiffTab();
    }

    private void OnNewThreeWayMergeMenuClicked(object sender, RoutedEventArgs e)
    {
        OpenThreeWayMergeTab();
    }

    private void OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        CloseTab(args.Tab);
    }

    public WelcomePage? _welcomePage;

    public void EnsureWelcomePage()
    {
        if (_welcomePage == null)
        {
            _welcomePage = new WelcomePage();
            if (!TabContentGrid.Children.Contains(_welcomePage))
            {
                TabContentGrid.Children.Add(_welcomePage);
            }
        }
    }

    public void ShowContent(UIElement? element)
    {
        if (element == null) return;

        if (!TabContentGrid.Children.Contains(element))
        {
            TabContentGrid.Children.Add(element);
        }

        // 使用 GPU 级 Opacity + ZIndex 瞬切机制：
        // 保持页面常驻于视觉树（不脱离排版生命周期），彻底消除从 Collapsed 恢复时的 Measure/Arrange 重排空白帧
        foreach (UIElement child in TabContentGrid.Children)
        {
            bool isCurrent = ReferenceEquals(child, element);
            child.Opacity = isCurrent ? 1.0 : 0.0;
            child.IsHitTestVisible = isCurrent;
            Canvas.SetZIndex(child, isCurrent ? 1 : 0);
            child.Visibility = Visibility.Visible;
        }
    }

    public void CloseTab(TabViewItem tab)
    {
        if (tab == WelcomeTabItem) return;

        if (tab.Tag is UIElement element)
        {
            TabContentGrid.Children.Remove(element);
            if (element is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        MainTabView.TabItems.Remove(tab);
        EnsureValidTabSelection();
    }

    public void CloseCurrentTab()
    {
        if (MainTabView.SelectedItem is TabViewItem currentTab && currentTab != WelcomeTabItem)
        {
            CloseTab(currentTab);
        }
    }

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (MainTabView.SelectedItem is TabViewItem selectedTab)
            {
                if (selectedTab == WelcomeTabItem)
                {
                    EnsureWelcomePage();
                    ShowContent(_welcomePage);
                }
                else if (selectedTab.Tag is UIElement element)
                {
                    ShowContent(element);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnTabSelectionChanged exception: {ex}");
        }
    }

    public void EnsureValidTabSelection()
    {
        if (MainTabView.TabItems.Count == 0)
        {
            ShowWelcomeTab();
        }
        else
        {
            if (MainTabView.SelectedItem == null)
            {
                MainTabView.SelectedIndex = MainTabView.TabItems.Count - 1;
            }
            if (MainTabView.SelectedItem is TabViewItem selectedTab)
            {
                if (selectedTab == WelcomeTabItem)
                {
                    EnsureWelcomePage();
                    ShowContent(_welcomePage);
                }
                else if (selectedTab.Tag is UIElement element)
                {
                    ShowContent(element);
                }
            }
        }
    }

    public void ShowWelcomeTab()
    {
        if (!MainTabView.TabItems.Contains(WelcomeTabItem))
        {
            MainTabView.TabItems.Insert(0, WelcomeTabItem);
        }
        MainTabView.SelectedItem = WelcomeTabItem;
        EnsureWelcomePage();
        ShowContent(_welcomePage);
        SetWindowTitle("WinCompare");
    }

    public void RemoveWelcomeTab()
    {
        if (MainTabView.TabItems.Contains(WelcomeTabItem))
        {
            MainTabView.TabItems.Remove(WelcomeTabItem);
        }
    }

    private void SetWindowTitle(string title)
    {
        try
        {
            if (this.Title != title)
            {
                this.Title = title;
            }
        }
        catch { }
    }

    public void AddTab(TabViewItem tab)
    {
        tab.Width = 150;
        tab.MinWidth = 150;
        tab.MaxWidth = 150;
        RemoveWelcomeTab();
        MainTabView.TabItems.Add(tab);
        MainTabView.SelectedItem = tab;
        if (tab.Tag is UIElement element)
        {
            ShowContent(element);
        }
        if (tab.Header is string titleText)
        {
            SetWindowTitle($"{titleText} - WinCompare");
        }
    }

    private Microsoft.UI.Windowing.AppWindow? _nativeAppWindow;
    private MainWindow? _tabTearOutWindow;

    private void OnTabTearOutWindowRequested(TabView sender, TabViewTabTearOutWindowRequestedEventArgs args)
    {
        // Consistent with Windows 11 File Explorer: only tear out when there are multiple tabs
        if (sender.TabItems.Count <= 1)
        {
            return;
        }

        // WelcomeTabItem represents the app hub and cannot be torn out
        if (ReferenceEquals(sender.SelectedItem, WelcomeTabItem))
        {
            return;
        }

        if (args.Items != null)
        {
            foreach (var item in args.Items)
            {
                if (ReferenceEquals(item, WelcomeTabItem))
                {
                    return;
                }
            }
        }

        _tabTearOutWindow = new MainWindow();
        _tabTearOutWindow.RemoveWelcomeTab();
        args.NewWindowId = _tabTearOutWindow.AppWindow.Id;
    }

    private void OnTabTearOutRequested(TabView sender, TabViewTabTearOutRequestedEventArgs args)
    {
        if (_tabTearOutWindow != null)
        {
            var targetWindow = _tabTearOutWindow;
            _tabTearOutWindow = null;

            var tabsToMove = new List<TabViewItem>();
            if (args.Tabs != null)
            {
                foreach (var element in args.Tabs)
                {
                    if (element is TabViewItem tvi && !ReferenceEquals(tvi, WelcomeTabItem))
                    {
                        tabsToMove.Add(tvi);
                    }
                }
            }

            if (tabsToMove.Count == 0 && sender.SelectedItem is TabViewItem sel && !ReferenceEquals(sel, WelcomeTabItem))
            {
                tabsToMove.Add(sel);
            }

            foreach (var tab in tabsToMove)
            {
                sender.TabItems.Remove(tab);
                targetWindow.AddTab(tab);
            }

            EnsureValidTabSelection();
            targetWindow.Activate();
        }
    }

    private void OnExternalTornOutTabsDropping(TabView sender, TabViewExternalTornOutTabsDroppingEventArgs args)
    {
        args.AllowDrop = true;
    }

    private void OnExternalTornOutTabsDropped(TabView sender, TabViewExternalTornOutTabsDroppedEventArgs args)
    {
        int insertIndex = args.DropIndex;
        if (insertIndex < 0 || insertIndex > sender.TabItems.Count)
        {
            insertIndex = sender.TabItems.Count;
        }

        if (args.Tabs != null)
        {
            RemoveWelcomeTab();
            foreach (var element in args.Tabs)
            {
                if (element is TabViewItem tab)
                {
                    var sourceTabView = FindSourceTabView(tab);
                    if (sourceTabView != null)
                    {
                        sourceTabView.TabItems.Remove(tab);
                        if (App.GetWindowForElement(sourceTabView) is MainWindow sourceWindow)
                        {
                            sourceWindow.EnsureValidTabSelection();
                        }
                    }

                    if (!sender.TabItems.Contains(tab))
                    {
                        sender.TabItems.Insert(insertIndex, tab);
                        sender.SelectedItem = tab;
                        if (tab.Tag is UIElement elementTag)
                        {
                            if (sourceTabView != null && App.GetWindowForElement(sourceTabView) is MainWindow sourceWindow)
                            {
                                sourceWindow.TabContentGrid.Children.Remove(elementTag);
                            }
                            ShowContent(elementTag);
                        }
                        if (tab.Header is string titleText)
                        {
                            SetWindowTitle($"{titleText} - WinCompare");
                        }
                        insertIndex++;
                    }
                }
            }
        }
    }

    private static TabView? FindSourceTabView(TabViewItem tab)
    {
        DependencyObject current = tab;
        while (current != null)
        {
            if (current is TabView tabView)
            {
                return tabView;
            }
            current = VisualTreeHelper.GetParent(current);
        }

        foreach (var window in App.ActiveWindows)
        {
            if (window.MainTabView.TabItems.Contains(tab))
            {
                return window.MainTabView;
            }
        }

        return null;
    }

    public void OpenTextDiffTab(string? leftPath = null, string? rightPath = null)
    {
        var page = new TextDiffPage();
        if (!string.IsNullOrEmpty(leftPath) || !string.IsNullOrEmpty(rightPath))
        {
            page.LoadFiles(leftPath, rightPath);
        }
        int num = MainTabView.TabItems.Count(t => !ReferenceEquals(t, WelcomeTabItem)) + 1;
        string defaultHeader = LanguageManager.Instance.Text("textDiffTitle") + " " + num;
        string header = !string.IsNullOrEmpty(leftPath) ? System.IO.Path.GetFileName(leftPath) : defaultHeader;
        var tab = new TabViewItem
        {
            Header = header,
            IconSource = new FontIconSource { Glyph = "\uE8A5" },
            Tag = page
        };
        AddTab(tab);
    }

    public void OpenExcelDiffTab(string? leftPath = null, string? rightPath = null)
    {
        var page = new ExcelDiffPage();
        if (!string.IsNullOrEmpty(leftPath) || !string.IsNullOrEmpty(rightPath))
        {
            page.LoadFiles(leftPath, rightPath);
        }
        int num = MainTabView.TabItems.Count(t => !ReferenceEquals(t, WelcomeTabItem)) + 1;
        string defaultHeader = LanguageManager.Instance.Text("excelDiffTitle") + " " + num;
        string header = !string.IsNullOrEmpty(leftPath) ? System.IO.Path.GetFileName(leftPath) : defaultHeader;
        var tab = new TabViewItem
        {
            Header = header,
            IconSource = new FontIconSource { Glyph = "\uF0EC" },
            Tag = page
        };
        AddTab(tab);
    }

    public void OpenWordDiffTab(string? leftPath = null, string? rightPath = null)
    {
        var page = new WordDiffPage();
        if (!string.IsNullOrEmpty(leftPath) || !string.IsNullOrEmpty(rightPath))
        {
            page.LoadFiles(leftPath, rightPath);
        }
        int num = MainTabView.TabItems.Count(t => !ReferenceEquals(t, WelcomeTabItem)) + 1;
        string defaultHeader = LanguageManager.Instance.Text("wordDiffTitle") + " " + num;
        string header = !string.IsNullOrEmpty(leftPath) ? System.IO.Path.GetFileName(leftPath) : defaultHeader;
        var tab = new TabViewItem
        {
            Header = header,
            IconSource = new FontIconSource { Glyph = "\uE8A5" },
            Tag = page
        };
        AddTab(tab);
    }

    public void OpenFolderDiffTab(string? leftPath = null, string? rightPath = null)
    {
        var page = new FolderDiffPage();
        if (!string.IsNullOrEmpty(leftPath) || !string.IsNullOrEmpty(rightPath))
        {
            page.LoadFolders(leftPath, rightPath);
        }
        int num = MainTabView.TabItems.Count(t => !ReferenceEquals(t, WelcomeTabItem)) + 1;
        string defaultHeader = LanguageManager.Instance.Text("folderDiffTitle") + " " + num;
        string header = !string.IsNullOrEmpty(leftPath) ? System.IO.Path.GetFileName(leftPath) : defaultHeader;
        var tab = new TabViewItem
        {
            Header = header,
            IconSource = new FontIconSource { Glyph = "\uED25" },
            Tag = page
        };
        AddTab(tab);
    }

    public void OpenThreeWayMergeTab(string? localPath = null, string? basePath = null, string? remotePath = null)
    {
        var page = new ThreeWayMergePage();
        if (!string.IsNullOrEmpty(localPath) || !string.IsNullOrEmpty(basePath) || !string.IsNullOrEmpty(remotePath))
        {
            page.LoadFiles(localPath, basePath, remotePath);
        }
        int num = MainTabView.TabItems.Count(t => !ReferenceEquals(t, WelcomeTabItem)) + 1;
        string header = LanguageManager.Instance.Text("threeWayMerge") + " " + num;
        var tab = new TabViewItem
        {
            Header = header,
            IconSource = new FontIconSource { Glyph = "\uE8D7" },
            Tag = page
        };
        AddTab(tab);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass, IntPtr dwRefData);

    [System.Runtime.InteropServices.DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);
    private static SubclassProc? _subclassProc;

    private static IntPtr SubclassWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == 0x0010 /* WM_CLOSE */ || uMsg == 0x0002 /* WM_DESTROY */)
        {
            foreach (var win in App.ActiveWindows.ToList())
            {
                try
                {
                    if (WinRT.Interop.WindowNative.GetWindowHandle(win) == hWnd)
                    {
                        win.TerminateIfLastWindow();
                        break;
                    }
                }
                catch { }
            }
        }
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private void TerminateIfLastWindow()
    {
        LanguageManager.Instance.LanguageChanged -= UpdateLanguageStrings;
        App.ActiveWindows.Remove(this);
        if (App.ActiveWindow == this)
        {
            App.ActiveWindow = App.ActiveWindows.LastOrDefault();
        }

        // 检查是否还有其它仍然处于可见 (Visible) 状态的真实窗口
        bool hasOtherVisibleWindows = false;
        foreach (var win in App.ActiveWindows.ToList())
        {
            if (win != this)
            {
                try
                {
                    var h = WinRT.Interop.WindowNative.GetWindowHandle(win);
                    if (h != IntPtr.Zero && IsWindowVisible(h))
                    {
                        hasOtherVisibleWindows = true;
                        break;
                    }
                }
                catch { }
            }
        }

        // 如果没有其它可见窗口，立即强杀进程彻底退出，绝不给控制台留下任何挂起僵尸
        if (!hasOtherVisibleWindows)
        {
            App.TerminateProcessImmediately();
        }
    }
}
