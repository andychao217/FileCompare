using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using WinCompare.Setup.Models;
using WinCompare.Setup.Services;
using WinCompare.Setup.ViewModels;
using WinCompare.Setup.Views;
using Wpf.Ui.Appearance;

namespace WinCompare.Setup;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    private const uint ATTACH_PARENT_PROCESS = unchecked((uint)-1);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Resolve Services
        IShortcutService shortcutService = new ShortcutService();
        IRegistryService registryService = new RegistryService();
        IProcessHelper processHelper = new ProcessHelper();
        IInstallService installService = new InstallService(shortcutService, registryService, processHelper);

        // 2. Parse command-line parameters
        var cmdOptions = CommandLineOptions.Parse(e.Args, Environment.ProcessPath);

        // 3. Handle Help flag
        if (cmdOptions.ShowHelp)
        {
            var attached = AttachToParentConsole();
            PrintHelp();
            if (attached) DetachFromParentConsole();
            Shutdown(0);
            Environment.Exit(0);
            return;
        }

        // 4. Handle Silent Mode (Unattended CI/CD or Enterprise Batch Install)
        if (cmdOptions.IsSilent)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var exitCode = Task.Run(() => RunSilentAsync(cmdOptions, installService, registryService, processHelper)).GetAwaiter().GetResult();
            Shutdown(exitCode);
            Environment.Exit(exitCode);
            return;
        }

        // 5. GUI Mode: Initialize Fluent Theme & System Accent
        ApplicationThemeManager.ApplySystemTheme();
        try
        {
            ApplicationAccentColorManager.ApplySystemAccent();
        }
        catch
        {
            ApplicationAccentColorManager.Apply(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4));
        }

        // Ensure text on accent background is always pure white
        Current.Resources["TextOnAccentFillColorPrimaryBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);

        // 6. Instantiate ViewModel & MainWindow
        var viewModel = new MainViewModel(
            installService, 
            registryService, 
            processHelper, 
            isUninstallMode: cmdOptions.IsUninstall, 
            customInstallPath: cmdOptions.InstallDir);

        var mainWindow = new MainWindow(viewModel);
        MainWindow = mainWindow;
        mainWindow.Show();

        if (cmdOptions.IsUninstall)
        {
            // Auto start uninstallation in UI mode
            viewModel.StartUninstallCommand.Execute(null);
        }
    }

    private static async Task<int> RunSilentAsync(
        CommandLineOptions options,
        IInstallService installService,
        IRegistryService registryService,
        IProcessHelper processHelper)
    {
        var attachedConsole = AttachToParentConsole();

        try
        {
            if (options.IsUninstall)
            {
                Console.WriteLine("[WinCompare Setup] Running silent uninstallation...");

                // 1. Resolve install directory
                var targetDir = options.InstallDir;
                if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
                {
                    var currentExeDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                    if (File.Exists(Path.Combine(currentExeDir, "WinCompare.UI.exe")))
                    {
                        targetDir = currentExeDir;
                    }
                    else
                    {
                        targetDir = registryService.DetectExistingInstallPath() ?? InstallOptions.GetDefaultInstallPath();
                    }
                }

                Console.WriteLine($"[WinCompare Setup] Uninstalling from: {targetDir}");

                var installOptions = new InstallOptions { InstallPath = targetDir };
                var lastReported = -1;
                var progress = new Progress<InstallProgressReport>(report =>
                {
                    var pct = (int)report.Percentage;
                    if (pct != lastReported)
                    {
                        lastReported = pct;
                        var detailMsg = !string.IsNullOrEmpty(report.Detail) ? $" - {report.Detail}" : string.Empty;
                        Console.WriteLine($"[WinCompare Setup] Progress: {pct}% ({report.Stage}){detailMsg}");
                    }
                });

                await installService.UninstallAsync(targetDir, installOptions.IsPerUser, progress);
                Console.WriteLine("[WinCompare Setup] Uninstallation completed successfully.");
                return 0;
            }
            else
            {
                Console.WriteLine("[WinCompare Setup] Running silent installation...");

                // 1. Resolve install directory
                var targetDir = options.InstallDir;
                if (string.IsNullOrEmpty(targetDir))
                {
                    // Auto-detect existing installation to upgrade in-place, or fallback to default
                    targetDir = registryService.DetectExistingInstallPath() ?? InstallOptions.GetDefaultInstallPath();
                }

                Console.WriteLine($"[WinCompare Setup] Target directory: {targetDir}");

                var installOptions = new InstallOptions
                {
                    InstallPath = targetDir,
                    CreateDesktopShortcut = options.CreateDesktopShortcut,
                    CreateStartMenuShortcut = options.CreateStartMenuShortcut,
                    LaunchAfterInstall = false // Never auto-launch in silent/unattended mode
                };

                var lastReported = -1;
                var progress = new Progress<InstallProgressReport>(report =>
                {
                    var pct = (int)report.Percentage;
                    if (pct != lastReported && (pct % 20 == 0 || pct == 100))
                    {
                        lastReported = pct;
                        Console.WriteLine($"[WinCompare Setup] Progress: {pct}% ({report.Stage})");
                    }
                });

                await installService.InstallAsync(installOptions, progress);
                Console.WriteLine("[WinCompare Setup] Installation completed successfully.");
                return 0;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WinCompare Setup] ERROR: {ex.Message}");
            return 1;
        }
        finally
        {
            if (attachedConsole)
            {
                Console.Out.Flush();
                Console.Error.Flush();
                DetachFromParentConsole();
            }
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"WinCompare Setup Installer
Usage: Setup.exe [options]

Options:
  --silent, /S               Run installer in background without UI
  --uninstall                Uninstall WinCompare
  --dir=<path>, /D=<path>    Specify custom installation directory
  --no-desktop-shortcut      Do not create desktop shortcut
  --no-start-menu-shortcut   Do not create start menu shortcut
  --no-shortcuts             Do not create any shortcuts
  --help, /?, -h             Display this help information

Exit Codes:
  0                          Success
  1                          Failure or error occurred");
    }

    private static bool AttachToParentConsole()
    {
        try
        {
            if (AttachConsole(ATTACH_PARENT_PROCESS))
            {
                var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                var stderr = new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
                Console.SetOut(stdout);
                Console.SetError(stderr);
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    private static void DetachFromParentConsole()
    {
        try
        {
            FreeConsole();
        }
        catch
        {
        }
    }
}
