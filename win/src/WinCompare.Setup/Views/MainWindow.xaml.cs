using WinCompare.Setup.ViewModels;
using Wpf.Ui.Controls;

namespace WinCompare.Setup.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // 挂钩系统主题与 DWM 磨砂透明管线
        Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);
    }
}
