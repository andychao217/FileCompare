using Microsoft.UI.Xaml;
using WinCompare.UI.Views;

namespace WinCompare.UI;

public partial class App : Application
{
    private Window? _window;
    public static MainWindow? MainWindowInstance { get; private set; }

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += (sender, e) =>
        {
            System.Console.Error.WriteLine($"[FATAL ERROR] {e.Message}");
            System.Console.Error.WriteLine(e.Exception?.ToString());
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var window = new MainWindow();
            MainWindowInstance = window;
            _window = window;
            _window.Activate();
        }
        catch (System.Exception ex)
        {
            System.Console.Error.WriteLine($"[LAUNCH ERROR] {ex}");
            throw;
        }
    }
}
