using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinCompare.Core.Models;
using WinCompare.Core.Services;

namespace WinCompare.UI.Views;

public sealed partial class MainWindow : Window
{
    private readonly WelcomePage _welcomePage = new();

    public MainWindow()
    {
        this.InitializeComponent();

        // Extend into title bar and integrate with Windows 11 Snap Layouts
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Set native Windows 11 taskbar & window icon
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (System.IO.File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }
        }
        catch
        {
            // Ignore icon failure on headless/virtualized environments
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

        // Initialize Welcome tab with cached page
        WelcomeTabItem.Tag = _welcomePage;
        MainContentPresenter.Content = _welcomePage;

        MainTabView.SelectionChanged += OnTabSelectionChanged;

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
                if (this.Content is FrameworkElement root)
                {
                    root.RequestedTheme = theme;
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
        if (args.Tab == WelcomeTabItem) return;

        if (args.Tab.Tag is IDisposable disposable)
        {
            disposable.Dispose();
        }

        sender.TabItems.Remove(args.Tab);
        if (sender.TabItems.Count == 0)
        {
            sender.TabItems.Add(WelcomeTabItem);
            sender.SelectedItem = WelcomeTabItem;
            MainContentPresenter.Content = _welcomePage;
        }
    }

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MainTabView.SelectedItem is TabViewItem selectedTab && selectedTab.Tag is UIElement element)
        {
            MainContentPresenter.Content = element;
        }
    }

    public void OpenTextDiffTab(string? leftPath = null, string? rightPath = null)
    {
        var page = new TextDiffPage();
        if (!string.IsNullOrEmpty(leftPath) || !string.IsNullOrEmpty(rightPath))
        {
            page.LoadFiles(leftPath, rightPath);
        }
        string defaultHeader = LanguageManager.Instance.Text("textDiffTitle") + " " + MainTabView.TabItems.Count;
        string header = !string.IsNullOrEmpty(leftPath) ? System.IO.Path.GetFileName(leftPath) : defaultHeader;
        var tab = new TabViewItem
        {
            Header = header,
            IconSource = new FontIconSource { Glyph = "\uE8A5" },
            Tag = page
        };
        MainTabView.TabItems.Add(tab);
        MainTabView.SelectedItem = tab;
        MainContentPresenter.Content = page;
    }

    public void OpenExcelDiffTab(string? leftPath = null, string? rightPath = null)
    {
        var page = new ExcelDiffPage();
        if (!string.IsNullOrEmpty(leftPath) || !string.IsNullOrEmpty(rightPath))
        {
            page.LoadFiles(leftPath, rightPath);
        }
        string defaultHeader = LanguageManager.Instance.Text("excelDiffTitle") + " " + MainTabView.TabItems.Count;
        string header = !string.IsNullOrEmpty(leftPath) ? System.IO.Path.GetFileName(leftPath) : defaultHeader;
        var tab = new TabViewItem
        {
            Header = header,
            IconSource = new FontIconSource { Glyph = "\uF0EC" },
            Tag = page
        };
        MainTabView.TabItems.Add(tab);
        MainTabView.SelectedItem = tab;
        MainContentPresenter.Content = page;
    }

    public void OpenWordDiffTab(string? leftPath = null, string? rightPath = null)
    {
        var page = new WordDiffPage();
        if (!string.IsNullOrEmpty(leftPath) || !string.IsNullOrEmpty(rightPath))
        {
            page.LoadFiles(leftPath, rightPath);
        }
        string defaultHeader = LanguageManager.Instance.Text("wordDiffTitle") + " " + MainTabView.TabItems.Count;
        string header = !string.IsNullOrEmpty(leftPath) ? System.IO.Path.GetFileName(leftPath) : defaultHeader;
        var tab = new TabViewItem
        {
            Header = header,
            IconSource = new FontIconSource { Glyph = "\uE8A5" },
            Tag = page
        };
        MainTabView.TabItems.Add(tab);
        MainTabView.SelectedItem = tab;
        MainContentPresenter.Content = page;
    }

    public void OpenFolderDiffTab(string? leftPath = null, string? rightPath = null)
    {
        var page = new FolderDiffPage();
        if (!string.IsNullOrEmpty(leftPath) || !string.IsNullOrEmpty(rightPath))
        {
            page.LoadFolders(leftPath, rightPath);
        }
        string defaultHeader = LanguageManager.Instance.Text("folderDiffTitle") + " " + MainTabView.TabItems.Count;
        string header = !string.IsNullOrEmpty(leftPath) ? System.IO.Path.GetFileName(leftPath) : defaultHeader;
        var tab = new TabViewItem
        {
            Header = header,
            IconSource = new FontIconSource { Glyph = "\uED25" },
            Tag = page
        };
        MainTabView.TabItems.Add(tab);
        MainTabView.SelectedItem = tab;
        MainContentPresenter.Content = page;
    }

    public void OpenThreeWayMergeTab(string? localPath = null, string? basePath = null, string? remotePath = null)
    {
        var page = new ThreeWayMergePage();
        if (!string.IsNullOrEmpty(localPath) || !string.IsNullOrEmpty(basePath) || !string.IsNullOrEmpty(remotePath))
        {
            page.LoadFiles(localPath, basePath, remotePath);
        }
        var tab = new TabViewItem
        {
            Header = LanguageManager.Instance.Text("threeWayMerge") + " " + MainTabView.TabItems.Count,
            IconSource = new FontIconSource { Glyph = "\uE8D7" },
            Tag = page
        };
        MainTabView.TabItems.Add(tab);
        MainTabView.SelectedItem = tab;
        MainContentPresenter.Content = page;
    }
}
