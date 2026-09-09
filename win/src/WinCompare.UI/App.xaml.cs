using Microsoft.UI.Xaml;
using WinCompare.UI.Views;

namespace WinCompare.UI;

public partial class App : Application
{
    private Window? _window;
    public static List<MainWindow> ActiveWindows { get; } = new();
    public static MainWindow? MainWindowInstance => ActiveWindows.FirstOrDefault();
    public static MainWindow? ActiveWindow { get; set; }

    public static Window? GetWindowForElement(UIElement? element)
    {
        if (element?.XamlRoot != null)
        {
            foreach (var window in ActiveWindows)
            {
                if (window.Content?.XamlRoot == element.XamlRoot)
                {
                    return window;
                }
            }
        }
        return ActiveWindow ?? MainWindowInstance;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern IntPtr GetCurrentProcess();

    [System.Runtime.InteropServices.DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate? handlerRoutine, bool add);

    private delegate bool ConsoleCtrlDelegate(int ctrlType);
    private static ConsoleCtrlDelegate? _consoleCtrlHandler;

    public static void TerminateProcessImmediately()
    {
        try
        {
            Application.Current?.Exit();
        }
        catch { }

        try
        {
            TerminateProcess(GetCurrentProcess(), 0);
        }
        catch { }

        try
        {
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }
        catch { }

        try
        {
            System.Environment.Exit(0);
        }
        catch { }
    }

    public App()
    {
        this.InitializeComponent();

        try
        {
            _consoleCtrlHandler = (ctrlType) =>
            {
                TerminateProcessImmediately();
                return false;
            };
            SetConsoleCtrlHandler(_consoleCtrlHandler, true);
        }
        catch
        {
            // Ignore in non-console environments
        }

        this.UnhandledException += (sender, e) =>
        {
            System.Console.Error.WriteLine($"[FATAL ERROR] {e.Message}");
            System.Console.Error.WriteLine(e.Exception?.ToString());
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var window = new MainWindow();
            _window = window;
            _window.Closed += (s, e) =>
            {
                TerminateProcessImmediately();
            };
            _window.Activate();
        }
        catch (System.Exception ex)
        {
            System.Console.Error.WriteLine($"[LAUNCH ERROR] {ex}");
            throw;
        }
    }
}
