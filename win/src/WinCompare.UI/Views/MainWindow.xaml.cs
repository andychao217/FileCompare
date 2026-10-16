using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.System;
using WinCompare.Core.Models;
using WinCompare.Core.Services;
using WinCompare.UI.ViewModels;
using WinCompare.UI.Helpers;

namespace WinCompare.UI.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        App.ActiveWindows.Add(this);
        App.ActiveWindow = this;
        AttachTabDragHandlers(WelcomeTabItem);

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



        try
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (hWnd != IntPtr.Zero)
            {
                RegisterNativeDropTarget(hWnd);
            }
        }
        catch { }

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
            root.ActualThemeChanged += (s, e) =>
            {
                App.InvalidateThemeBrushCache();
                UpdateTitleBarColors();
            };
        }
        UpdateTitleBarColors();

        // Initialize Welcome tab with zero-flicker cached viewport
        EnsureWelcomePage();
        ShowContent(_welcomePage);
        _currentWelcomeTab = WelcomeTabItem;

        if (App.IsElevatedAdministrator && UipiWarningInfoBar != null)
        {
            UipiWarningInfoBar.IsOpen = true;
        }

        MainTabView.SelectionChanged += OnTabSelectionChanged;

        // Handle keyboard shortcuts (Ctrl+W to close tab, Escape to close in-app dialogs)
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
                else if (args.Key == VirtualKey.H &&
                    Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
                {
                    ShowWelcomeTab();
                    args.Handled = true;
                }
                else if (args.Key == VirtualKey.Escape && DialogOverlay != null && DialogOverlay.Visibility == Visibility.Visible)
                {
                    CloseInAppDialog();
                    args.Handled = true;
                }
            };
        }

        if (DialogOverlay != null)
        {
            DialogOverlay.PointerPressed += OnDialogOverlayPointerPressed;
        }

        // Automatic background update check on startup (2 seconds delay, matching macOS)
        if (SettingsManager.Instance.AutoCheckUpdatesOnLaunch)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                var (status, newVer, notes, url) = await UpdateCheckerService.Instance.CheckForUpdatesAsync(isUserInitiated: false);
                if (status == UpdateCheckStatus.UpdateAvailable)
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        ShowUpdateDialog(newVer, notes, url);
                    });
                }
            });
        }

        // Initialize recent history submenu (event-driven, pre-populated to avoid crash during flyout opening)
        PopulateRecentHistoryMenu();
        RecentHistoryManager.Instance.HistoryChanged += () =>
        {
            this.DispatcherQueue.TryEnqueue(PopulateRecentHistoryMenu);
        };
    }

    public void UpdateTitleBarColors()
    {
        try
        {
            if (Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported() && _nativeAppWindow?.TitleBar != null)
            {
                var titleBar = _nativeAppWindow.TitleBar;
                titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);

                titleBar.ButtonForegroundColor = App.GetThemeColor("TitleBarButtonFgBrush");
                titleBar.ButtonHoverForegroundColor = App.GetThemeColor("TitleBarButtonHoverFgBrush");
                titleBar.ButtonHoverBackgroundColor = App.GetThemeColor("TitleBarButtonHoverBgBrush");
                titleBar.ButtonPressedForegroundColor = App.GetThemeColor("TitleBarButtonPressedFgBrush");
                titleBar.ButtonPressedBackgroundColor = App.GetThemeColor("TitleBarButtonPressedBgBrush");
                titleBar.ButtonInactiveForegroundColor = App.GetThemeColor("TitleBarButtonInactiveFgBrush");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateTitleBarColors exception: {ex.Message}");
        }
    }

    private void UpdateLanguageStrings()
    {
        if (WelcomeTabItem != null) WelcomeTabItem.Header = "WinCompare";

        if (MenuTextDiff != null) MenuTextDiff.Text = LanguageManager.Instance.Text("textDiffTitle");
        if (MenuExcelDiff != null) MenuExcelDiff.Text = LanguageManager.Instance.Text("excelDiffTitle");
        if (MenuWordDiff != null) MenuWordDiff.Text = LanguageManager.Instance.Text("wordDiffTitle");
        if (MenuFolderDiff != null) MenuFolderDiff.Text = LanguageManager.Instance.Text("folderDiffTitle");
        if (MenuThreeWayMerge != null) MenuThreeWayMerge.Text = LanguageManager.Instance.Text("threeWayMerge");
        if (MenuRecentHistory != null) MenuRecentHistory.Text = LanguageManager.Instance.Text("recentComparisons");
        if (MenuSettings != null) MenuSettings.Text = LanguageManager.Instance.Text("settingsTitle");
        if (MenuCheckUpdate != null) MenuCheckUpdate.Text = LanguageManager.Instance.Text("checkForUpdates");
        if (MenuHelp != null) MenuHelp.Text = LanguageManager.Instance.Text("helpAndGuide");
        if (MenuAbout != null) MenuAbout.Text = LanguageManager.Instance.Text("aboutApp");

        PopulateRecentHistoryMenu();
    }

    private void OnHelpClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            App.Log("[MainWindow] Opening help window...");
            var helpControl = new HelpControl(initialTabIndex: 0);
            helpControl.CloseRequested += CloseInAppDialog;
            helpControl.UpdateRequested += ShowUpdateDialog;
            ShowInAppDialog(helpControl);
            App.Log("[MainWindow] \u5E2E\u52A9\u6307\u5357\u7A97\u53E3\u5DF2\u6210\u529F\u5448\u73B0\u3002");
        }
        catch (Exception ex)
        {
            App.LogFatalError("MainWindow.OnHelpClicked", ex);
            ShowNoticeDialog(LanguageManager.Instance.Text("helpAndGuide"), ex.Message);
        }
    }

    private void OnAboutClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            App.Log("[MainWindow] \u6B63\u5728\u6253\u5F00\u5173\u4E8E\u7A97\u53E3...");
            var helpControl = new HelpControl(initialTabIndex: 2);
            helpControl.CloseRequested += CloseInAppDialog;
            helpControl.UpdateRequested += ShowUpdateDialog;
            ShowInAppDialog(helpControl);
            App.Log("[MainWindow] \u5173\u4E8E\u7A97\u53E3\u5DF2\u6210\u529F\u5448\u73B0\u3002");
        }
        catch (Exception ex)
        {
            App.LogFatalError("MainWindow.OnAboutClicked", ex);
            ShowNoticeDialog(LanguageManager.Instance.Text("aboutApp"), "WinCompare v0.6.0\n\n" + ex.Message);
        }
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var settingsControl = new SettingsControl();
            settingsControl.CloseRequested += CloseInAppDialog;
            settingsControl.UpdateRequested += ShowUpdateDialog;
            settingsControl.ThemeChanged += (theme) =>
            {
                App.InvalidateThemeBrushCache();
                foreach (var window in App.ActiveWindows)
                {
                    if (window.Content is FrameworkElement r)
                    {
                        r.RequestedTheme = theme;
                    }
                    if (window is MainWindow mw)
                    {
                        mw.UpdateTitleBarColors();
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

    private async void OnMenuCheckUpdateClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var (status, newVer, notes, url) = await UpdateCheckerService.Instance.CheckForUpdatesAsync(isUserInitiated: true);
            if (status == UpdateCheckStatus.UpdateAvailable)
            {
                ShowUpdateDialog(newVer, notes, url);
            }
            else if (status == UpdateCheckStatus.UpToDate)
            {
                ShowNoticeDialog(LanguageManager.Instance.Text("upToDateTitle"), LanguageManager.Instance.Text("upToDateMessage"));
            }
            else
            {
                ShowNoticeDialog(LanguageManager.Instance.Text("softwareUpdateTitle"), LanguageManager.Instance.Text("checkFailed"));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnMenuCheckUpdateClicked exception: {ex}");
        }
    }

    private void ShowUpdateDialog(string newVer, string notes, string url)
    {
        try
        {
            var updateControl = new UpdateAvailableControl(newVer, notes, url);
            updateControl.CloseRequested += CloseInAppDialog;
            ShowInAppDialog(updateControl);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShowUpdateDialog exception: {ex}");
        }
    }

    public void ShowNoticeDialog(string title, string message)
    {
        ShowValidationDialog(title, message, null, null);
    }

    public void ShowValidationDialog(
        string title, 
        string message, 
        string? primaryBtnText = null, 
        Action? onPrimaryAction = null)
    {
        if (this.DispatcherQueue != null && !this.DispatcherQueue.HasThreadAccess)
        {
            this.DispatcherQueue.TryEnqueue(() => ShowValidationDialog(title, message, primaryBtnText, onPrimaryAction));
            return;
        }

        try
        {
            var dialogControl = new FileValidationDialogControl();
            dialogControl.Setup(title, message, primaryBtnText, () =>
            {
                CloseInAppDialog();
                onPrimaryAction?.Invoke();
            }, () =>
            {
                CloseInAppDialog();
            });

            ShowInAppDialog(dialogControl);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShowValidationDialog error: {ex.Message}");
        }
    }

    public void ShowConfirmDialog(
        string title, 
        string message, 
        string confirmBtnText, 
        Action onConfirm, 
        bool isDestructive = false)
    {
        if (this.DispatcherQueue != null && !this.DispatcherQueue.HasThreadAccess)
        {
            this.DispatcherQueue.TryEnqueue(() => ShowConfirmDialog(title, message, confirmBtnText, onConfirm, isDestructive));
            return;
        }

        try
        {
            var dialogControl = new FileValidationDialogControl();
            dialogControl.Setup(title, message, confirmBtnText, () =>
            {
                CloseInAppDialog();
                onConfirm?.Invoke();
            }, () =>
            {
                CloseInAppDialog();
            }, isDestructive);

            ShowInAppDialog(dialogControl);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShowConfirmDialog error: {ex.Message}");
        }
    }

    public void OpenSingleTargetTab(string path, CompareType mode)
    {
        switch (mode)
        {
            case CompareType.TextDiff:
                OpenTextDiffTab(path, null);
                break;
            case CompareType.WordDiff:
                OpenWordDiffTab(path, null);
                break;
            case CompareType.ExcelDiff:
                OpenExcelDiffTab(path, null);
                break;
            case CompareType.FolderDiff:
                OpenFolderDiffTab(path, null);
                break;
            case CompareType.ThreeWayMerge:
                OpenThreeWayMergeTab(path, null, null);
                break;
        }
    }

    public void ShowInAppDialog(UIElement content)
    {
        DialogContentPresenter.Content = content;
        DialogOverlay.Visibility = Visibility.Visible;
    }

    public void CloseInAppDialog()
    {
        DialogOverlay.Visibility = Visibility.Collapsed;
        this.DispatcherQueue.TryEnqueue(() =>
        {
            if (DialogOverlay.Visibility == Visibility.Collapsed)
            {
                DialogContentPresenter.Content = null;
            }
        });
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

    private void PopulateRecentHistoryMenu()
    {
        if (MenuRecentHistory == null) return;

        try
        {
            MenuRecentHistory.Items.Clear();

            // 1. Top action: View all history (opens Welcome page)
            var viewAllItem = new MenuFlyoutItem
            {
                Text = LanguageManager.Instance.Text("viewAllHistory")
            };
            viewAllItem.Click += (s, e) =>
            {
                try
                {
                    ShowWelcomeTab();
                }
                catch { }
            };
            MenuRecentHistory.Items.Add(viewAllItem);

            var records = RecentHistoryManager.Instance.Records;
            if (records == null || records.Count == 0)
            {
                MenuRecentHistory.Items.Add(new MenuFlyoutSeparator());
                var emptyItem = new MenuFlyoutItem
                {
                    Text = LanguageManager.Instance.Text("noRecentRecords"),
                    IsEnabled = false
                };
                MenuRecentHistory.Items.Add(emptyItem);
                return;
            }

            MenuRecentHistory.Items.Add(new MenuFlyoutSeparator());

            // 2. Up to 8 recent compare records
            int count = 0;
            foreach (var rec in records)
            {
                if (count++ >= 8) break;

                string leftName = "?";
                string rightName = "?";
                try
                {
                    if (!string.IsNullOrEmpty(rec.LeftPath))
                        leftName = Path.GetFileName(rec.LeftPath);
                }
                catch { }

                try
                {
                    if (!string.IsNullOrEmpty(rec.RightPath))
                        rightName = Path.GetFileName(rec.RightPath);
                }
                catch { }

                string typePrefix = rec.EffectiveType switch
                {
                    CompareType.FolderDiff => $"[{LanguageManager.Instance.Text("folderDiffTitle")}] ",
                    CompareType.ExcelDiff => $"[{LanguageManager.Instance.Text("excelDiffTitle")}] ",
                    CompareType.WordDiff => $"[{LanguageManager.Instance.Text("wordDiffTitle")}] ",
                    CompareType.ThreeWayMerge => $"[{LanguageManager.Instance.Text("threeWayMerge")}] ",
                    _ => $"[{LanguageManager.Instance.Text("textDiffTitle")}] "
                };

                string displayText = $"{typePrefix}{leftName} \u2194 {rightName}";

                var recordItem = new MenuFlyoutItem
                {
                    Text = displayText
                };

                var currentRec = rec;
                recordItem.Click += (s, e) =>
                {
                    try
                    {
                        OpenRecentRecord(currentRec);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"OpenRecentRecord error: {ex.Message}");
                    }
                };

                MenuRecentHistory.Items.Add(recordItem);
            }

            // 3. Bottom action: Clear all recent history
            MenuRecentHistory.Items.Add(new MenuFlyoutSeparator());
            var clearItem = new MenuFlyoutItem
            {
                Text = LanguageManager.Instance.Text("clearHistory")
            };
            clearItem.Click += (s, e) =>
            {
                try
                {
                    ShowConfirmDialog(
                        LanguageManager.Instance.Text("clearHistory"),
                        LanguageManager.Instance.Text("confirmClearHistoryMessage"),
                        LanguageManager.Instance.Text("clear"),
                        () =>
                        {
                            try
                            {
                                RecentHistoryManager.Instance.ClearAll();
                                _welcomePage?.UpdateRecentHistoryVisibility();
                            }
                            catch { }
                        },
                        isDestructive: true);
                }
                catch { }
            };
            MenuRecentHistory.Items.Add(clearItem);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PopulateRecentHistoryMenu exception: {ex.Message}");
        }
    }

    public void OpenRecentRecord(RecentCompareRecord record)
    {
        if (record == null) return;

        try
        {
            bool leftExists = File.Exists(record.LeftPath) || Directory.Exists(record.LeftPath);
            bool rightExists = File.Exists(record.RightPath) || Directory.Exists(record.RightPath);

            if (!leftExists && !rightExists)
            {
                ShowNoticeDialog(
                    LanguageManager.Instance.Text("recentComparisons"),
                    $"{LanguageManager.Instance.Text("noFileSelected")}:\n{record.LeftPath}\n{record.RightPath}"
                );
                return;
            }

            switch (record.EffectiveType)
            {
                case CompareType.FolderDiff:
                    OpenFolderDiffTab(record.LeftPath, record.RightPath);
                    break;
                case CompareType.ExcelDiff:
                    OpenExcelDiffTab(record.LeftPath, record.RightPath);
                    break;
                case CompareType.WordDiff:
                    OpenWordDiffTab(record.LeftPath, record.RightPath);
                    break;
                case CompareType.ThreeWayMerge:
                    OpenThreeWayMergeTab(record.LeftPath, null, record.RightPath);
                    break;
                default:
                    OpenTextDiffTab(record.LeftPath, record.RightPath);
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] OpenRecentRecord error: {ex}");
            ShowNoticeDialog(
                LanguageManager.Instance.Text("recentComparisons"),
                ex.Message
            );
        }
    }

    private void OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        try
        {
            var tabToClose = args.Tab ?? args.Item as TabViewItem;
            if (tabToClose == null) return;

            // 核心保护：严禁在 unmanaged TabCloseRequested 同步事件栈中直接修改 TabItems 集合
            // 必须异步排队，等待原生 CloseButton 交互循环完全退出后在下一消息周期执行
            this.DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    CloseTab(tabToClose);
                }
                catch (Exception ex)
                {
                    App.LogFatalError("MainWindow.OnTabCloseRequested.Inner", ex);
                }
            });
        }
        catch (Exception ex)
        {
            App.LogFatalError("MainWindow.OnTabCloseRequested", ex);
        }
    }

    public void ShowInsertionIndicator(double x)
    {
        try
        {
            if (TabDropCanvas != null)
            {
                TabDropCanvas.Visibility = Visibility.Visible;
            }
            if (TabInsertionIndicator != null)
            {
                Canvas.SetLeft(TabInsertionIndicator, Math.Max(0, x));
                TabInsertionIndicator.Opacity = 1.0;
            }
        }
        catch { }
    }

    public void HideInsertionIndicator()
    {
        try
        {
            if (TabInsertionIndicator != null)
            {
                TabInsertionIndicator.Opacity = 0.0;
            }
            if (TabDropCanvas != null)
            {
                TabDropCanvas.Visibility = Visibility.Collapsed;
            }
        }
        catch { }
    }

    public static void HideAllInsertionIndicators()
    {
        foreach (var win in App.ActiveWindows)
        {
            win.HideInsertionIndicator();
        }
    }

    private void EnsureWelcomeTabAtFirstPosition()
    {
        try
        {
            var welcome = MainTabView.TabItems.OfType<TabViewItem>().FirstOrDefault(IsWelcomeTab);
            if (welcome != null)
            {
                int idx = MainTabView.TabItems.IndexOf(welcome);
                if (idx > 0)
                {
                    MainTabView.TabItems.RemoveAt(idx);
                    MainTabView.TabItems.Insert(0, welcome);
                }
            }
        }
        catch { }
    }

    public WelcomePage? _welcomePage;
    private TabViewItem? _currentWelcomeTab;

    private TabViewItem GetOrCreateWelcomeTab()
    {
        var existing = MainTabView.TabItems.OfType<TabViewItem>().FirstOrDefault(IsWelcomeTab);
        if (existing != null)
        {
            _currentWelcomeTab = existing;
            return existing;
        }

        if (_currentWelcomeTab != null)
        {
            return _currentWelcomeTab;
        }

        if (WelcomeTabItem != null)
        {
            _currentWelcomeTab = WelcomeTabItem;
            return WelcomeTabItem;
        }

        _currentWelcomeTab = CreateWelcomeTabItem();
        return _currentWelcomeTab;
    }

    public bool IsWelcomeTab(object? tab)
    {
        if (tab == null) return false;
        if (ReferenceEquals(tab, WelcomeTabItem)) return true;
        if (_currentWelcomeTab != null && ReferenceEquals(tab, _currentWelcomeTab)) return true;
        if (tab is TabViewItem tvi)
        {
            if (Equals(tvi.Tag, "Welcome") || tvi.Tag is WelcomePage) return true;
            if (tvi.Header is string h && h == "WinCompare") return true;
        }
        return false;
    }

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

        foreach (UIElement child in TabContentGrid.Children)
        {
            bool isCurrent = ReferenceEquals(child, element);
            child.Visibility = isCurrent ? Visibility.Visible : Visibility.Collapsed;
            child.Opacity = isCurrent ? 1.0 : 0.0;
            child.IsHitTestVisible = isCurrent;
            Canvas.SetZIndex(child, isCurrent ? 1 : 0);
        }
    }

    private void OnTabContentGridDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
        }
    }

    private async void OnTabContentGridDrop(object sender, DragEventArgs e)
    {
        if (e.Handled) return;
        try
        {
            App.Log("[MainWindow] OnTabContentGridDrop triggered");
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items == null || items.Count == 0) return;

                e.Handled = true;
                var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
                var pos = e.GetPosition(TabContentGrid);
                double w = TabContentGrid.ActualWidth > 0 ? TabContentGrid.ActualWidth : 900;
                double normalizedX = Math.Clamp(pos.X / w, 0.0, 1.0);
                HandleGlobalDrop(paths, normalizedX);
            }
        }
        catch (Exception ex)
        {
            App.Log($"[MainWindow] OnTabContentGridDrop error: {ex.Message}");
        }
    }

    public void HandleGlobalDrop(IReadOnlyList<Windows.Storage.IStorageItem> items, Windows.Foundation.Point dropPos)
    {
        if (items == null || items.Count == 0) return;
        var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
        double w = TabContentGrid.ActualWidth > 0 ? TabContentGrid.ActualWidth : 900;
        double normalizedX = Math.Clamp(dropPos.X / w, 0.0, 1.0);
        HandleGlobalDrop(paths, normalizedX);
    }

    public void HandleGlobalDrop(IReadOnlyList<string> paths, Windows.Foundation.Point dropPos, double containerWidth = 0)
    {
        if (paths == null || paths.Count == 0) return;
        double width = containerWidth > 0 ? containerWidth : (TabContentGrid.ActualWidth > 0 ? TabContentGrid.ActualWidth : 900);
        double normalizedX = Math.Clamp(dropPos.X / width, 0.0, 1.0);
        HandleGlobalDrop(paths, normalizedX);
    }

    public void HandleGlobalDrop(IReadOnlyList<string> paths, double normalizedX)
    {
        if (paths == null || paths.Count == 0) return;

        var currentTab = MainTabView.SelectedItem as TabViewItem;
        var currentPage = currentTab?.Tag as UIElement;

        bool isLeft = normalizedX < 0.5;
        App.Log($"[MainWindow] HandleGlobalDrop: count={paths.Count}, normalizedX={normalizedX:F3}, isLeft={isLeft}, currentPage={currentPage?.GetType().Name}");

        // 1. If currently inside an active diff page, route dropped file(s) directly into that page
        if (currentPage is TextDiffPage textPage)
        {
            if (paths.Count >= 2)
            {
                textPage.HandleDualDrop(paths[0], paths[1]);
            }
            else
            {
                textPage.HandleDropFromWindow(paths[0], isLeft);
            }
            return;
        }
        else if (currentPage is ExcelDiffPage excelPage)
        {
            if (paths.Count >= 2)
            {
                excelPage.HandleDualDrop(paths[0], paths[1]);
            }
            else
            {
                excelPage.HandleDropFromWindow(paths[0], isLeft);
            }
            return;
        }
        else if (currentPage is WordDiffPage wordPage)
        {
            if (paths.Count >= 2)
            {
                wordPage.HandleDualDrop(paths[0], paths[1]);
            }
            else
            {
                wordPage.HandleDropFromWindow(paths[0], isLeft);
            }
            return;
        }
        else if (currentPage is FolderDiffPage folderPage)
        {
            if (paths.Count >= 2)
            {
                folderPage.HandleDualDrop(paths[0], paths[1]);
            }
            else
            {
                folderPage.HandleDropFromWindow(paths[0], isLeft);
            }
            return;
        }
        else if (currentPage is ThreeWayMergePage threeWayPage)
        {
            if (paths.Count >= 2)
            {
                threeWayPage.HandleMultiDrop(paths);
            }
            else
            {
                int branch = normalizedX < (1.0 / 3.0) ? 0 : (normalizedX < (2.0 / 3.0) ? 1 : 2);
                threeWayPage.HandleDropFromWindow(paths[0], branch);
            }
            return;
        }

        // 2. Otherwise (Welcome page, empty tab, etc.): Open new comparison tab according to file type
        if (paths.Count >= 2)
        {
            string path1 = paths[0];
            string path2 = paths[1];
            bool isDir1 = Directory.Exists(path1);
            bool isDir2 = Directory.Exists(path2);

            if (isDir1 && isDir2)
            {
                OpenFolderDiffTab(path1, path2);
                return;
            }

            string ext1 = (Path.GetExtension(path1) ?? "").ToLowerInvariant();
            string ext2 = (Path.GetExtension(path2) ?? "").ToLowerInvariant();

            if (ext1 is ".docx" or ".doc" && ext2 is ".docx" or ".doc")
            {
                OpenWordDiffTab(path1, path2);
                return;
            }
            if (ext1 is ".xlsx" or ".xls" or ".csv" && ext2 is ".xlsx" or ".xls" or ".csv")
            {
                OpenExcelDiffTab(path1, path2);
                return;
            }
            OpenTextDiffTab(path1, path2);
            return;
        }
        else
        {
            string singlePath = paths[0];
            bool isDir = Directory.Exists(singlePath);
            string ext = (Path.GetExtension(singlePath) ?? "").ToLowerInvariant();

            if (isDir)
            {
                OpenFolderDiffTab(singlePath, null);
            }
            else if (ext is ".docx" or ".doc")
            {
                OpenWordDiffTab(singlePath, null);
            }
            else if (ext is ".xlsx" or ".xls" or ".csv")
            {
                OpenExcelDiffTab(singlePath, null);
            }
            else
            {
                OpenTextDiffTab(singlePath, null);
            }
        }
    }

    public void CloseTab(TabViewItem tab)
    {
        if (tab == null) return;
        if (!MainTabView.TabItems.Contains(tab)) return;

        var otherTabs = MainTabView.TabItems.OfType<TabViewItem>()
            .Where(t => !ReferenceEquals(t, tab))
            .ToList();

        // 窗口中唯一标签且是欢迎标签时，不允许关闭
        if (otherTabs.Count == 0 && IsWelcomeTab(tab)) return;

        try
        {
            App.Log($"[TabManager] 收到关闭标签请求: '{tab.Header}' (Tag类型: {tab.Tag?.GetType().Name})");

            // 1. 如果关闭的是欢迎 Tab（因为存在其他对比 Tab，所以允许关闭并切回对比）
            if (IsWelcomeTab(tab))
            {
                App.Log("[TabManager] 正在关闭欢迎标签页，切回其余对比标签...");
                bool isSelected = ReferenceEquals(MainTabView.SelectedItem, tab);
                int closingIndex = MainTabView.TabItems.IndexOf(tab);

                if (isSelected && otherTabs.Count > 0)
                {
                    int targetIndex = closingIndex > 0 ? closingIndex - 1 : 0;
                    if (targetIndex >= 0 && targetIndex < otherTabs.Count)
                    {
                        MainTabView.SelectedItem = otherTabs[targetIndex];
                    }
                }

                MainTabView.TabItems.Remove(tab);
                EnsureValidTabSelection();
                return;
            }

            // 2. 关闭对比 Tab
            var otherComparisonTabs = otherTabs.Where(t => !IsWelcomeTab(t)).ToList();
            bool isLastComparisonTab = otherComparisonTabs.Count == 0;

            // 先卸载并安全清理关联的视图元素，防止在视觉树上被析构
            if (tab.Tag is UIElement element)
            {
                tab.Tag = null;
                element.Visibility = Visibility.Collapsed;
                element.Opacity = 0.0;
                element.IsHitTestVisible = false;

                if (TabContentGrid.Children.Contains(element))
                {
                    TabContentGrid.Children.Remove(element);
                }

                if (element is IDisposable disposable)
                {
                    try { disposable.Dispose(); } catch { }
                }
            }

            if (isLastComparisonTab)
            {
                App.Log("[TabManager] 当前为最后一个对比标签，准备回退至欢迎页...");
                try
                {
                    if (this.Content is FrameworkElement root)
                    {
                        root.Focus(FocusState.Programmatic);
                    }
                }
                catch { }

                // 从 TabItems 中移除被关闭的标签
                MainTabView.TabItems.Remove(tab);

                // 显示欢迎标签页
                ShowWelcomeTab();

                App.Log("[TabManager] 最后一个对比标签已关闭，已平稳回到欢迎页。");
            }
            else
            {
                App.Log($"[TabManager] 多标签状态关闭标签，当前总标签数: {MainTabView.TabItems.Count}");
                bool isSelected = ReferenceEquals(MainTabView.SelectedItem, tab);
                int closingIndex = MainTabView.TabItems.IndexOf(tab);

                if (isSelected)
                {
                    int targetIndex = closingIndex > 0 ? closingIndex - 1 : closingIndex + 1;
                    if (targetIndex >= 0 && targetIndex < MainTabView.TabItems.Count && !ReferenceEquals(MainTabView.TabItems[targetIndex], tab))
                    {
                        MainTabView.SelectedIndex = targetIndex;
                    }
                }

                MainTabView.TabItems.Remove(tab);
                EnsureValidTabSelection();
                App.Log($"[TabManager] 标签已关闭，剩余标签数: {MainTabView.TabItems.Count}");
            }
        }
        catch (Exception ex)
        {
            App.LogFatalError("MainWindow.CloseTab", ex);
        }
    }

    public void CloseCurrentTab()
    {
        if (MainTabView.SelectedItem is TabViewItem currentTab)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                CloseTab(currentTab);
            });
        }
    }

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (MainTabView.SelectedItem is TabViewItem selectedTab)
            {
                if (IsWelcomeTab(selectedTab))
                {
                    EnsureWelcomePage();
                    ShowContent(_welcomePage);
                    _welcomePage?.UpdateRecentHistoryVisibility();
                    SetWindowTitle("WinCompare");
                }
                else if (selectedTab.Tag is UIElement element)
                {
                    ShowContent(element);
                    if (selectedTab.Header is string titleText)
                    {
                        SetWindowTitle($"{titleText} - WinCompare");
                    }
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
        try
        {
            var comparisonTabs = MainTabView.TabItems.OfType<TabViewItem>().Where(t => !IsWelcomeTab(t)).ToList();
            if (comparisonTabs.Count == 0)
            {
                ShowWelcomeTab();
            }
            else
            {
                if (MainTabView.SelectedItem == null || !MainTabView.TabItems.Contains(MainTabView.SelectedItem))
                {
                    MainTabView.SelectedIndex = MainTabView.TabItems.Count - 1;
                }
                if (MainTabView.SelectedItem is TabViewItem selectedTab)
                {
                    if (IsWelcomeTab(selectedTab))
                    {
                        EnsureWelcomePage();
                        ShowContent(_welcomePage);
                        _welcomePage?.UpdateRecentHistoryVisibility();
                        SetWindowTitle("WinCompare");
                    }
                    else if (selectedTab.Tag is UIElement element)
                    {
                        ShowContent(element);
                        if (selectedTab.Header is string titleText)
                        {
                            SetWindowTitle($"{titleText} - WinCompare");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"EnsureValidTabSelection exception: {ex}");
        }
    }

    private TabViewItem CreateWelcomeTabItem()
    {
        var item = new TabViewItem
        {
            Header = "WinCompare",
            Tag = "Welcome",
            IsClosable = false,
            MaxWidth = 220,
            IconSource = new FontIconSource
            {
                Glyph = "\uE80F"
            }
        };
        AttachTabDragHandlers(item);
        return item;
    }

    public void ShowWelcomeTab()
    {
        try
        {
            // 1. 获取或清理多余欢迎 Tab，确保当前至多只有一个欢迎 Tab
            var allWelcomeTabs = MainTabView.TabItems.OfType<TabViewItem>().Where(IsWelcomeTab).ToList();
            TabViewItem welcomeTab;

            if (allWelcomeTabs.Count > 0)
            {
                welcomeTab = allWelcomeTabs[0];
                _currentWelcomeTab = welcomeTab;
                for (int i = 1; i < allWelcomeTabs.Count; i++)
                {
                    MainTabView.TabItems.Remove(allWelcomeTabs[i]);
                }
            }
            else
            {
                welcomeTab = GetOrCreateWelcomeTab();
                MainTabView.TabItems.Add(welcomeTab);
            }

            AttachTabDragHandlers(welcomeTab);

            // 2. 如果存在其他对比 Tab，则欢迎页可以被关闭（以便随时返回对比）；如果是唯一 Tab 则不可关闭
            var comparisonTabs = MainTabView.TabItems.OfType<TabViewItem>().Where(t => !IsWelcomeTab(t)).ToList();
            welcomeTab.IsClosable = comparisonTabs.Count > 0;

            MainTabView.SelectedItem = welcomeTab;
            EnsureWelcomePage();
            ShowContent(_welcomePage);
            _welcomePage?.UpdateRecentHistoryVisibility();
            SetWindowTitle("WinCompare");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShowWelcomeTab exception: {ex}");
        }
    }

    public void RemoveWelcomeTab()
    {
        try
        {
            var welcomeTabs = MainTabView.TabItems.OfType<TabViewItem>().Where(IsWelcomeTab).ToList();
            foreach (var wt in welcomeTabs)
            {
                MainTabView.TabItems.Remove(wt);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RemoveWelcomeTab exception: {ex}");
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

    public TabView TabViewControl => MainTabView;

    public void SelectTab(TabViewItem tabItem)
    {
        if (MainTabView.TabItems.Contains(tabItem))
        {
            MainTabView.SelectedItem = tabItem;
        }
    }

    public void AddTab(TabViewItem tab)
    {
        tab.MaxWidth = 220;
        AttachTabDragHandlers(tab);
        
        // 先添加新对比 Tab，确保集合项数永不降至 0
        MainTabView.TabItems.Add(tab);
        
        // 然后安全移除欢迎 Tab
        RemoveWelcomeTab();

        MainTabView.SelectedItem = tab;

        if (tab.Tag is UIElement element)
        {
            ShowContent(element);
        }
        if (tab.Header is string titleText)
        {
            SetWindowTitle($"{titleText} - WinCompare");
        }

        // 新建 Tab 平滑展开 (180ms)
        AnimateTabExpand(tab);
    }

    public void UpdateTabHeaderForPage(UIElement page, string? newHeader = null)
    {
        try
        {
            foreach (var item in MainTabView.TabItems)
            {
                if (item is TabViewItem tab && ReferenceEquals(tab.Tag, page))
                {
                    if (string.IsNullOrWhiteSpace(newHeader))
                    {
                        int idx = 1;
                        foreach (var t in MainTabView.TabItems)
                        {
                            if (ReferenceEquals(t, tab)) break;
                            if (t is TabViewItem otherTab && !IsWelcomeTab(otherTab) && otherTab.Tag?.GetType() == page.GetType())
                            {
                                idx++;
                            }
                        }

                        string key = page switch
                        {
                            ExcelDiffPage => "excelDiffTitle",
                            TextDiffPage => "textDiffTitle",
                            WordDiffPage => "wordDiffTitle",
                            FolderDiffPage => "folderDiffTitle",
                            ThreeWayMergePage => "threeWayMerge",
                            _ => "textDiffTitle"
                        };
                        string title = LanguageManager.Instance.Text(key);
                        newHeader = $"{title} {idx}";
                    }

                    tab.Header = newHeader;
                    if (ReferenceEquals(MainTabView.SelectedItem, tab))
                    {
                        SetWindowTitle($"{newHeader} - WinCompare");
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateTabHeaderForPage error: {ex.Message}");
        }
    }

    private Microsoft.UI.Windowing.AppWindow? _nativeAppWindow;

    public CompareTabViewModel GetOrCreateViewModel(TabViewItem tabItem)
    {
        if (tabItem.DataContext is CompareTabViewModel existingVm)
        {
            return existingVm;
        }

        var vm = new CompareTabViewModel
        {
            Header = tabItem.Header as string ?? "Tab",
            IsWelcome = IsWelcomeTab(tabItem),
            IsClosable = tabItem.IsClosable,
            ContentPage = tabItem.Tag as UIElement,
            TabItem = tabItem
        };

        if (tabItem.IconSource is FontIconSource fontIcon)
        {
            vm.Glyph = fontIcon.Glyph;
        }

        tabItem.DataContext = vm;
        return vm;
    }

    public void AttachTabDragHandlers(TabViewItem tabItem)
    {
        // 关键点：TabViewItem 内部会消费 PointerPressed 导致普通 += 无法接收，必须 handledEventsToo = true
        tabItem.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnTabItemPointerPressed), true);
    }

    private void OnTabItemPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not TabViewItem tabItem) return;

        // 如果点击的是关闭按钮，让底层处理关闭，不启动拖拽追踪
        if (e.OriginalSource is DependencyObject dep)
        {
            DependencyObject? cur = dep;
            while (cur != null && cur != tabItem)
            {
                if (cur is Button btn && (btn.Name == "CloseButton" || btn.Content is SymbolIcon))
                {
                    return;
                }
                cur = VisualTreeHelper.GetParent(cur);
            }
        }

        // 检查物理左键是否按下
        bool isLeftDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
        if (!isLeftDown) return;

        GetCursorPos(out POINT screenPt);
        TabDragManager.StartTracking(this, tabItem, screenPt);
    }

    public void ShowGhostPreview(string header, string glyph, double dipsX, double dipsY)
    {
        try
        {
            if (TabGhostCanvas != null)
            {
                TabGhostCanvas.Visibility = Visibility.Visible;
            }
            if (TabGhostPreview != null)
            {
                TabGhostTitle.Text = header;
                TabGhostIcon.Glyph = glyph;
                TabGhostPreview.Visibility = Visibility.Visible;
                TabGhostPreview.Opacity = 0.9;
                Canvas.SetLeft(TabGhostPreview, Math.Max(0, dipsX - 50));
                Canvas.SetTop(TabGhostPreview, Math.Max(0, dipsY + 6));
            }
        }
        catch { }
    }

    public void HideGhostPreview()
    {
        try
        {
            if (TabGhostPreview != null)
            {
                TabGhostPreview.Visibility = Visibility.Collapsed;
            }
            if (TabGhostCanvas != null)
            {
                TabGhostCanvas.Visibility = Visibility.Collapsed;
            }
        }
        catch { }
    }

    public static void HideAllGhosts()
    {
        foreach (var win in App.ActiveWindows)
        {
            win.HideGhostPreview();
        }
    }

    public void SetTabPlaceholderState(TabViewItem tabItem, bool isPlaceholder)
    {
        try
        {
            if (isPlaceholder)
            {
                tabItem.Opacity = 0.35;
                tabItem.BorderBrush = App.GetThemeBrush("AccentFillColorDefaultBrush", App.GetThemeBrush("BrandPrimaryBrush"));
                tabItem.BorderThickness = new Thickness(1.5);
            }
            else
            {
                tabItem.Opacity = 1.0;
                tabItem.ClearValue(Control.BorderBrushProperty);
                tabItem.ClearValue(Control.BorderThicknessProperty);
            }
        }
        catch { }
    }

    public void AnimateGhostFlyBack(TabViewItem targetTab, Action? onCompleted = null)
    {
        try
        {
            if (TabGhostPreview == null || TabGhostPreview.Visibility != Visibility.Visible)
            {
                HideGhostPreview();
                onCompleted?.Invoke();
                return;
            }

            double targetLeft = 0;
            double targetTop = 0;

            try
            {
                var transform = targetTab.TransformToVisual(TabGhostCanvas);
                var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, targetTab.ActualWidth, targetTab.ActualHeight));
                targetLeft = bounds.Left;
                targetTop = bounds.Top;
            }
            catch
            {
                HideGhostPreview();
                onCompleted?.Invoke();
                return;
            }

            double curLeft = Canvas.GetLeft(TabGhostPreview);
            double curTop = Canvas.GetTop(TabGhostPreview);

            var leftAnim = new DoubleAnimation
            {
                From = curLeft,
                To = targetLeft,
                Duration = new Duration(TimeSpan.FromMilliseconds(160)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var topAnim = new DoubleAnimation
            {
                From = curTop,
                To = targetTop,
                Duration = new Duration(TimeSpan.FromMilliseconds(160)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var opacityAnim = new DoubleAnimation
            {
                From = 0.9,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(160)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var sb = new Storyboard();
            Storyboard.SetTarget(leftAnim, TabGhostPreview);
            Storyboard.SetTargetProperty(leftAnim, "(Canvas.Left)");
            Storyboard.SetTarget(topAnim, TabGhostPreview);
            Storyboard.SetTargetProperty(topAnim, "(Canvas.Top)");
            Storyboard.SetTarget(opacityAnim, TabGhostPreview);
            Storyboard.SetTargetProperty(opacityAnim, "Opacity");

            sb.Children.Add(leftAnim);
            sb.Children.Add(topAnim);
            sb.Children.Add(opacityAnim);

            sb.Completed += (s, e) =>
            {
                HideGhostPreview();
                onCompleted?.Invoke();
            };
            sb.Begin();
        }
        catch
        {
            HideGhostPreview();
            onCompleted?.Invoke();
        }
    }

    public static void AnimateTabExpand(TabViewItem tab)
    {
        try
        {
            tab.Opacity = 0.0;
            var opacityAnim = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(180)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var sb = new Storyboard();
            Storyboard.SetTarget(opacityAnim, tab);
            Storyboard.SetTargetProperty(opacityAnim, "Opacity");
            sb.Children.Add(opacityAnim);
            sb.Begin();
        }
        catch
        {
            tab.Opacity = 1.0;
        }
    }

    public static void AnimateTabCollapse(TabViewItem tab, Action onCompleted)
    {
        onCompleted?.Invoke();
    }

    public void AnimateWindowFadeIn()
    {
        try
        {
            if (this.Content is FrameworkElement root)
            {
                root.Opacity = 0.0;
                var anim = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(180)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var sb = new Storyboard();
                Storyboard.SetTarget(anim, root);
                Storyboard.SetTargetProperty(anim, "Opacity");
                sb.Children.Add(anim);
                sb.Begin();
            }
        }
        catch { }
    }

    public static void UpdateDragState(POINT screenPt)
    {
        MainWindow? hitWindow = null;
        double indicatorX = 0;

        foreach (var win in App.ActiveWindows)
        {
            try
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(win);
                if (hWnd == IntPtr.Zero || !IsWindowVisible(hWnd)) continue;
                if (!GetWindowRect(hWnd, out RECT rc)) continue;

                if (screenPt.X >= rc.Left && screenPt.X <= rc.Right &&
                    screenPt.Y >= rc.Top && screenPt.Y <= rc.Bottom)
                {
                    var localPt = screenPt;
                    ScreenToClient(hWnd, ref localPt);
                    double scale = win.Content?.XamlRoot?.RasterizationScale ?? 1.0;
                    double localDipsX = localPt.X / scale;
                    double localDipsY = localPt.Y / scale;
                    double winDipsWidth = (rc.Right - rc.Left) / scale;

                    // 落在该窗口内部时，更新浮动预览 Ghost 位置
                    if (TabDragManager.DraggedViewModel != null)
                    {
                        win.ShowGhostPreview(
                            TabDragManager.DraggedViewModel.Header,
                            TabDragManager.DraggedViewModel.Glyph,
                            localDipsX, localDipsY);
                    }

                    // 检查是否命中顶部 TabBar
                    if (localDipsY >= 0 && localDipsY <= 52 && localDipsX < (winDipsWidth - 140))
                    {
                        hitWindow = win;
                        indicatorX = CalculateInsertionIndicatorX(win, localDipsX);
                    }
                    break;
                }
            }
            catch { }
        }

        if (hitWindow != null)
        {
            hitWindow.ShowInsertionIndicator(indicatorX);
            foreach (var w in App.ActiveWindows)
            {
                if (w != hitWindow) w.HideInsertionIndicator();
            }
        }
        else
        {
            HideAllInsertionIndicators();
        }
    }

    public static double CalculateInsertionIndicatorX(MainWindow win, double localDipsX)
    {
        var tabs = win.MainTabView.TabItems.OfType<TabViewItem>().ToList();
        if (tabs.Count == 0) return 4;

        for (int i = 0; i < tabs.Count; i++)
        {
            var t = tabs[i];
            try
            {
                var transform = t.TransformToVisual(win.TabDropCanvas);
                var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, t.ActualWidth, t.ActualHeight));
                if (localDipsX < bounds.Left + (bounds.Width / 2.0))
                {
                    return bounds.Left;
                }
                if (i == tabs.Count - 1)
                {
                    return bounds.Right;
                }
            }
            catch { }
        }
        return localDipsX;
    }

    public static int CalculateInsertionIndex(MainWindow win, double localDipsX)
    {
        var tabs = win.MainTabView.TabItems.OfType<TabViewItem>().ToList();
        if (tabs.Count == 0) return 0;

        for (int i = 0; i < tabs.Count; i++)
        {
            var t = tabs[i];
            try
            {
                var transform = t.TransformToVisual(win.TabDropCanvas);
                var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, t.ActualWidth, t.ActualHeight));
                if (localDipsX < bounds.Left + (bounds.Width / 2.0))
                {
                    return i;
                }
            }
            catch { }
        }
        return tabs.Count;
    }

    public void HandleDragRelease(CompareTabViewModel tabVm, POINT releasePt)
    {
        MainWindow? targetTabBarWindow = null;
        int insertIndex = 0;

        foreach (var win in App.ActiveWindows)
        {
            try
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(win);
                if (hWnd == IntPtr.Zero || !IsWindowVisible(hWnd)) continue;
                if (!GetWindowRect(hWnd, out RECT rc)) continue;

                if (releasePt.X >= rc.Left && releasePt.X <= rc.Right &&
                    releasePt.Y >= rc.Top && releasePt.Y <= rc.Bottom)
                {
                    var localPt = releasePt;
                    ScreenToClient(hWnd, ref localPt);
                    double scale = win.Content?.XamlRoot?.RasterizationScale ?? 1.0;
                    double localDipsX = localPt.X / scale;
                    double localDipsY = localPt.Y / scale;
                    double winDipsWidth = (rc.Right - rc.Left) / scale;

                    if (localDipsY >= 0 && localDipsY <= 52 && localDipsX < (winDipsWidth - 140))
                    {
                        targetTabBarWindow = win;
                        insertIndex = CalculateInsertionIndex(win, localDipsX);
                        break;
                    }
                }
            }
            catch { }
        }

        if (targetTabBarWindow != null)
        {
            HideAllGhosts();
            if (targetTabBarWindow == this)
            {
                ReorderTab(tabVm, insertIndex);
                if (tabVm.TabItem != null) SetTabPlaceholderState(tabVm.TabItem, false);
            }
            else
            {
                PerformTabMerge(this, targetTabBarWindow, tabVm, insertIndex);
            }
            return;
        }

        // 检查是否落在源窗口内部的非 TabBar 区域（如内容面板区域）
        try
        {
            var srcHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (srcHwnd != IntPtr.Zero && GetWindowRect(srcHwnd, out RECT srcRc))
            {
                if (releasePt.X >= srcRc.Left && releasePt.X <= srcRc.Right &&
                    releasePt.Y >= srcRc.Top && releasePt.Y <= srcRc.Bottom)
                {
                    // 需求 4: 拖拽到非 TabBar 区域取消，平滑回弹到原位置
                    if (tabVm.TabItem != null)
                    {
                        AnimateGhostFlyBack(tabVm.TabItem, () =>
                        {
                            SetTabPlaceholderState(tabVm.TabItem, false);
                        });
                    }
                    else
                    {
                        HideAllGhosts();
                    }
                    return;
                }
            }
        }
        catch { }

        // 落在当前窗口外部 -> 需求 2: Tab tear-off（撕下标签），新建独立顶级顶级 Window
        HideAllGhosts();
        if (tabVm.TabItem != null) SetTabPlaceholderState(tabVm.TabItem, false);
        PerformTabTearOff(this, tabVm, releasePt);
    }

    public void ReorderTab(CompareTabViewModel tabVm, int targetIndex)
    {
        try
        {
            var tabItem = tabVm.TabItem;
            if (tabItem == null || !MainTabView.TabItems.Contains(tabItem)) return;

            int currentIndex = MainTabView.TabItems.IndexOf(tabItem);
            if (currentIndex == targetIndex || currentIndex == targetIndex - 1) return;

            MainTabView.TabItems.RemoveAt(currentIndex);
            if (targetIndex > currentIndex)
            {
                targetIndex--;
            }
            if (targetIndex < 0) targetIndex = 0;
            if (targetIndex > MainTabView.TabItems.Count) targetIndex = MainTabView.TabItems.Count;

            MainTabView.TabItems.Insert(targetIndex, tabItem);
            MainTabView.SelectedItem = tabItem;
            EnsureWelcomeTabAtFirstPosition();
        }
        catch (Exception ex)
        {
            App.Log($"[TabManager] ReorderTab error: {ex.Message}");
        }
    }

    public static void PerformTabMerge(MainWindow sourceWin, MainWindow targetWin, CompareTabViewModel tabVm, int targetIndex)
    {
        try
        {
            var tabItem = tabVm.TabItem;
            var content = tabVm.ContentPage;
            if (tabItem == null) return;

            // 1. 从源窗口分离
            sourceWin.MainTabView.TabItems.Remove(tabItem);
            if (content != null && sourceWin.TabContentGrid.Children.Contains(content))
            {
                sourceWin.TabContentGrid.Children.Remove(content);
            }

            // 2. 将内容加入目标窗口
            targetWin.RemoveWelcomeTab();
            if (content != null && !targetWin.TabContentGrid.Children.Contains(content))
            {
                targetWin.TabContentGrid.Children.Add(content);
            }

            // 3. 将 Tab 加入目标窗口 TabBar
            if (targetIndex < 0 || targetIndex > targetWin.MainTabView.TabItems.Count)
            {
                targetIndex = targetWin.MainTabView.TabItems.Count;
            }
            targetWin.MainTabView.TabItems.Insert(targetIndex, tabItem);
            targetWin.AttachTabDragHandlers(tabItem);
            targetWin.MainTabView.SelectedItem = tabItem;

            // 需求 3: 跨窗口合并 Tab 平滑展开插入 (180ms)
            AnimateTabExpand(tabItem);

            if (content != null)
            {
                targetWin.ShowContent(content);
            }
            if (!string.IsNullOrEmpty(tabVm.Header))
            {
                targetWin.SetWindowTitle($"{tabVm.Header} - WinCompare");
            }
            targetWin.Activate();

            // 4. 检查源窗口：若原窗口标签全部移除后，自动关闭原窗口
            var srcRemainingTabs = sourceWin.MainTabView.TabItems.OfType<TabViewItem>().Where(t => !sourceWin.IsWelcomeTab(t)).ToList();
            if (srcRemainingTabs.Count == 0)
            {
                App.Log("[TabManager] 源窗口所有标签已全部迁移，自动关闭源窗口。");
                sourceWin.Close();
            }
            else
            {
                sourceWin.EnsureValidTabSelection();
            }
        }
        catch (Exception ex)
        {
            App.Log($"[TabManager] PerformTabMerge error: {ex.Message}");
        }
    }

    public static void PerformTabTearOff(MainWindow sourceWin, CompareTabViewModel tabVm, POINT releasePt)
    {
        try
        {
            var tabItem = tabVm.TabItem;
            var content = tabVm.ContentPage;
            if (tabItem == null) return;

            // 1. 从源窗口分离
            sourceWin.MainTabView.TabItems.Remove(tabItem);
            if (content != null && sourceWin.TabContentGrid.Children.Contains(content))
            {
                sourceWin.TabContentGrid.Children.Remove(content);
            }

            // 2. 创建独立顶级新窗口
            var newWin = new MainWindow();
            newWin.RemoveWelcomeTab();

            // 3. 将内容和 Tab 挂载到新窗口
            if (content != null)
            {
                newWin.TabContentGrid.Children.Add(content);
            }
            newWin.MainTabView.TabItems.Add(tabItem);
            newWin.AttachTabDragHandlers(tabItem);
            newWin.MainTabView.SelectedItem = tabItem;

            if (content != null)
            {
                newWin.ShowContent(content);
            }
            if (!string.IsNullOrEmpty(tabVm.Header))
            {
                newWin.SetWindowTitle($"{tabVm.Header} - WinCompare");
            }

            // 4. 定位新窗口位置：放置在鼠标释放位置
            int newX = Math.Max(0, releasePt.X - 120);
            int newY = Math.Max(0, releasePt.Y - 20);
            newWin.AppWindow.Move(new Windows.Graphics.PointInt32(newX, newY));

            newWin.Activate();
            // 需求 2: 新建顶级窗口淡入显示 (180ms)
            newWin.AnimateWindowFadeIn();

            // 5. 检查源窗口：若原窗口标签全部移除后，自动关闭原窗口
            var srcRemainingTabs = sourceWin.MainTabView.TabItems.OfType<TabViewItem>().Where(t => !sourceWin.IsWelcomeTab(t)).ToList();
            if (srcRemainingTabs.Count == 0)
            {
                App.Log("[TabManager] 源窗口所有标签已被撕出，自动关闭源窗口。");
                sourceWin.Close();
            }
            else
            {
                sourceWin.EnsureValidTabSelection();
            }
        }
        catch (Exception ex)
        {
            App.Log($"[TabManager] PerformTabTearOff error: {ex.Message}");
        }
    }


    public void OpenTextDiffTab(string? leftPath = null, string? rightPath = null)
    {
        try
        {
            var page = new TextDiffPage();
            if (!string.IsNullOrEmpty(leftPath) || !string.IsNullOrEmpty(rightPath))
            {
                page.LoadFiles(leftPath, rightPath);
            }
            int num = MainTabView.TabItems.Count(t => !IsWelcomeTab(t)) + 1;
            string title = LanguageManager.Instance.Text("textDiffTitle");
            string defaultHeader = $"{title} {num}";
            string header = !string.IsNullOrEmpty(leftPath) ? System.IO.Path.GetFileName(leftPath) : defaultHeader;
            var tab = new TabViewItem
            {
                Header = header,
                IconSource = new FontIconSource { Glyph = "\uE8A5" },
                Tag = page
            };
            AddTab(tab);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OpenTextDiffTab error: {ex.Message}");
        }
    }

    public void OpenExcelDiffTab(string? leftPath = null, string? rightPath = null)
    {
        try
        {
            var page = new ExcelDiffPage();
            if (!string.IsNullOrEmpty(leftPath) || !string.IsNullOrEmpty(rightPath))
            {
                page.LoadFiles(leftPath, rightPath);
            }
            int num = MainTabView.TabItems.Count(t => !IsWelcomeTab(t)) + 1;
            string title = LanguageManager.Instance.Text("excelDiffTitle");
            string defaultHeader = $"{title} {num}";
            string header = !string.IsNullOrEmpty(leftPath) ? System.IO.Path.GetFileName(leftPath) : defaultHeader;
            var tab = new TabViewItem
            {
                Header = header,
                IconSource = new FontIconSource { Glyph = "\uF0EC" },
                Tag = page
            };
            AddTab(tab);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OpenExcelDiffTab error: {ex.Message}");
        }
    }

    public void OpenWordDiffTab(string? leftPath = null, string? rightPath = null)
    {
        try
        {
            App.Log($"[MainWindow] OpenWordDiffTab starting (left={leftPath}, right={rightPath})");
            var page = new WordDiffPage();
            if (!string.IsNullOrEmpty(leftPath) || !string.IsNullOrEmpty(rightPath))
            {
                page.LoadFiles(leftPath, rightPath);
            }
            int num = MainTabView.TabItems.Count(t => !IsWelcomeTab(t)) + 1;
            string title = LanguageManager.Instance.Text("wordDiffTitle");
            string defaultHeader = $"{title} {num}";
            string header = !string.IsNullOrEmpty(leftPath) ? System.IO.Path.GetFileName(leftPath) : defaultHeader;
            var tab = new TabViewItem
            {
                Header = header,
                IconSource = new FontIconSource { Glyph = "\uE8A5" },
                Tag = page
            };
            AddTab(tab);
            App.Log("[MainWindow] OpenWordDiffTab completed successfully.");
        }
        catch (Exception ex)
        {
            App.LogFatalError("OpenWordDiffTab", ex);
        }
    }

    public void OpenFolderDiffTab(string? leftPath = null, string? rightPath = null)
    {
        try
        {
            var page = new FolderDiffPage();
            if (!string.IsNullOrEmpty(leftPath) || !string.IsNullOrEmpty(rightPath))
            {
                page.LoadFolders(leftPath, rightPath);
            }
            int num = MainTabView.TabItems.Count(t => !IsWelcomeTab(t)) + 1;
            string title = LanguageManager.Instance.Text("folderDiffTitle");
            string defaultHeader = $"{title} {num}";
            string header = !string.IsNullOrEmpty(leftPath) ? System.IO.Path.GetFileName(leftPath) : defaultHeader;
            var tab = new TabViewItem
            {
                Header = header,
                IconSource = new FontIconSource { Glyph = "\uED25" },
                Tag = page
            };
            AddTab(tab);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OpenFolderDiffTab error: {ex.Message}");
        }
    }

    public void OpenThreeWayMergeTab(string? localPath = null, string? basePath = null, string? remotePath = null)
    {
        try
        {
            var page = new ThreeWayMergePage();
            if (!string.IsNullOrEmpty(localPath) || !string.IsNullOrEmpty(basePath) || !string.IsNullOrEmpty(remotePath))
            {
                page.LoadFiles(localPath, basePath, remotePath);
            }
            int num = MainTabView.TabItems.Count(t => !IsWelcomeTab(t)) + 1;
            string title = LanguageManager.Instance.Text("threeWayMerge");
            string header = $"{title} {num}";
            var tab = new TabViewItem
            {
                Header = header,
                IconSource = new FontIconSource { Glyph = "\uE8D7" },
                Tag = page
            };
            AddTab(tab);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OpenThreeWayMergeTab error: {ex.Message}");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public const int VK_LBUTTON = 0x01;

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr SubclassProcDelegate(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProcDelegate pfnSubclass, UIntPtr uIdSubclass, UIntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll")]
    private static extern void DragAcceptFiles(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool fAccept);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder? lpszFile, uint cch);

    [DllImport("shell32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DragQueryPoint(IntPtr hDrop, out POINT lppt);

    [DllImport("shell32.dll")]
    private static extern void DragFinish(IntPtr hDrop);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint message, uint action, IntPtr pChangeFilterStruct);

    private const uint WM_DROPFILES = 0x0233;
    private const uint WM_COPYDATA = 0x004A;
    private const uint WM_COPYGLOBALDATA = 0x0049;
    private const uint MSGFLT_ALLOW = 1;

    private SubclassProcDelegate? _nativeDropSubclassDelegate;

    private void RegisterNativeDropTarget(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;
        try
        {
            ChangeWindowMessageFilterEx(hWnd, WM_DROPFILES, MSGFLT_ALLOW, IntPtr.Zero);
            ChangeWindowMessageFilterEx(hWnd, WM_COPYDATA, MSGFLT_ALLOW, IntPtr.Zero);
            ChangeWindowMessageFilterEx(hWnd, WM_COPYGLOBALDATA, MSGFLT_ALLOW, IntPtr.Zero);

            DragAcceptFiles(hWnd, true);

            _nativeDropSubclassDelegate = new SubclassProcDelegate(NativeDropSubclassProc);
            SetWindowSubclass(hWnd, _nativeDropSubclassDelegate, (UIntPtr)101, UIntPtr.Zero);
        }
        catch (Exception ex)
        {
            App.Log($"[MainWindow] RegisterNativeDropTarget exception: {ex.Message}");
        }
    }

    private IntPtr NativeDropSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        if (uMsg == WM_DROPFILES)
        {
            try
            {
                IntPtr hDrop = wParam;
                uint count = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
                var paths = new List<string>();
                for (uint i = 0; i < count; i++)
                {
                    uint len = DragQueryFile(hDrop, i, null, 0);
                    var sb = new StringBuilder((int)len + 2);
                    DragQueryFile(hDrop, i, sb, (uint)sb.Capacity);
                    paths.Add(sb.ToString());
                }
                DragFinish(hDrop);

                GetCursorPos(out POINT screenPt);
                ScreenToClient(hWnd, ref screenPt);
                GetClientRect(hWnd, out RECT clientRect);
                int clientWidth = clientRect.Right - clientRect.Left;
                double normalizedX = 0.5;
                if (clientWidth > 0)
                {
                    normalizedX = (double)screenPt.X / clientWidth;
                    if (normalizedX < 0) normalizedX = 0;
                    if (normalizedX > 1) normalizedX = 1;
                }
                App.Log($"[MainWindow] Native WM_DROPFILES: paths={paths.Count}, clientPos=({screenPt.X},{screenPt.Y}), clientWidth={clientWidth}, normalizedX={normalizedX:F3}");

                if (paths.Count > 0)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        HandleGlobalDrop(paths, normalizedX);
                    });
                }
            }
            catch (Exception ex)
            {
                App.Log($"[MainWindow] Native WM_DROPFILES error: {ex.Message}");
            }
            return IntPtr.Zero;
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
