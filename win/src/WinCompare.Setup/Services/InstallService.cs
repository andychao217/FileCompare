using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using WinCompare.Setup.Models;

namespace WinCompare.Setup.Services;

public sealed class InstallService : IInstallService
{
    private readonly IShortcutService _shortcutService;
    private readonly IRegistryService _registryService;
    private readonly IProcessHelper _processHelper;

    public InstallService(IShortcutService shortcutService, IRegistryService registryService, IProcessHelper processHelper)
    {
        _shortcutService = shortcutService;
        _registryService = registryService;
        _processHelper = processHelper;
    }

    public async Task InstallAsync(InstallOptions options, IProgress<InstallProgressReport> progress, CancellationToken ct = default)
    {
        // 1. Process lock verification
        progress.Report(new InstallProgressReport(5, InstallStage.CheckingRunning));
        if (_processHelper.IsAppRunning())
        {
            var closed = _processHelper.TryCloseApp();
            if (!closed)
            {
                throw new InvalidOperationException("WinCompare 正在运行，请先关闭正在运行的应用。");
            }
        }

        // 2. Prepare target directory
        progress.Report(new InstallProgressReport(10, InstallStage.PreparingDirectory));
        if (!Directory.Exists(options.InstallPath))
        {
            Directory.CreateDirectory(options.InstallPath);
        }

        // 3. Locate and open payload.zip stream
        progress.Report(new InstallProgressReport(15, InstallStage.ReadingPayload));
        using var zipStream = OpenPayloadStream();
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        // Calculate total uncompressed bytes
        long totalBytes = 0;
        foreach (var entry in archive.Entries)
        {
            totalBytes += entry.Length;
        }
        if (totalBytes <= 0) totalBytes = 1;

        // 4. Stream extraction with byte-level progress
        long processedBytes = 0;
        var buffer = new byte[81920]; // 80KB buffer

        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();

            var destinationPath = Path.GetFullPath(Path.Combine(options.InstallPath, entry.FullName));
            // Security check against ZipSlip
            if (!destinationPath.StartsWith(Path.GetFullPath(options.InstallPath), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            var parentDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            using var entryStream = entry.Open();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

            int bytesRead;
            while ((bytesRead = await entryStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                processedBytes += bytesRead;

                var pct = 15.0 + (processedBytes / (double)totalBytes) * 70.0;
                progress.Report(new InstallProgressReport(Math.Min(85.0, pct), InstallStage.Extracting, entry.Name, processedBytes, totalBytes));
            }
        }

        // 5. Copy self as Setup.exe and Uninstall.exe for maintenance & direct uninstallation
        progress.Report(new InstallProgressReport(88, InstallStage.DeployingMaintenance));
        var selfExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrEmpty(selfExe) && File.Exists(selfExe))
        {
            try
            {
                var setupTarget = Path.Combine(options.InstallPath, "Setup.exe");
                var uninstallTarget = Path.Combine(options.InstallPath, "Uninstall.exe");
                File.Copy(selfExe, setupTarget, true);
                File.Copy(selfExe, uninstallTarget, true);
            }
            catch
            {
                // Self might already be in destination if updating in place
            }
        }

        var mainExe = Path.Combine(options.InstallPath, "WinCompare.UI.exe");
        var appIcon = Path.Combine(options.InstallPath, "Assets", "AppIcon.ico");
        if (!File.Exists(appIcon))
        {
            appIcon = mainExe;
        }

        // 6. Create Shortcuts
        progress.Report(new InstallProgressReport(92, InstallStage.CreatingShortcuts));
        _shortcutService.CreateShortcuts(mainExe, options.InstallPath, appIcon, options.CreateDesktopShortcut, options.CreateStartMenuShortcut);

        // 7. Register Uninstall Info
        progress.Report(new InstallProgressReport(96, InstallStage.RegisteringSystem));
        var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.7.0";
        var totalSizeKb = Math.Max(1024, totalBytes / 1024);
        _registryService.RegisterUninstall(options.InstallPath, mainExe, appVersion, options.IsPerUser, totalSizeKb);

        progress.Report(new InstallProgressReport(100, InstallStage.InstallCompleted));
        // 注意：不自动启动主程序，完全交由完成界面的“立即启动”按钮触发
    }

    public async Task UninstallAsync(string installDir, bool isPerUser, IProgress<InstallProgressReport> progress, CancellationToken ct = default)
    {
        progress.Report(new InstallProgressReport(10, InstallStage.UninstallChecking));
        if (_processHelper.IsAppRunning())
        {
            _processHelper.TryCloseApp();
        }

        progress.Report(new InstallProgressReport(30, InstallStage.UninstallShortcuts));
        _shortcutService.RemoveShortcuts(desktop: true, startMenu: true);

        progress.Report(new InstallProgressReport(60, InstallStage.UninstallRegistry));
        _registryService.UnregisterUninstall(isPerUser);

        progress.Report(new InstallProgressReport(80, InstallStage.UninstallFiles));
        await Task.Run(() =>
        {
            if (Directory.Exists(installDir))
            {
                // Delete files except running files
                foreach (var file in Directory.GetFiles(installDir, "*", SearchOption.AllDirectories))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }, ct).ConfigureAwait(false);

        progress.Report(new InstallProgressReport(95, InstallStage.UninstallCleanup));
        _processHelper.ScheduleSelfDeletion(installDir);

        progress.Report(new InstallProgressReport(100, InstallStage.UninstallCompleted));
    }

    private static Stream OpenPayloadStream()
    {
        var asm = Assembly.GetExecutingAssembly();
        var resourceNames = asm.GetManifestResourceNames();

        foreach (var name in resourceNames)
        {
            if (name.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase))
            {
                var s = asm.GetManifestResourceStream(name);
                if (s != null) return s;
            }
        }

        // Fallback: look for payload.zip next to current executable
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var localPayload = Path.Combine(baseDir, "payload.zip");
        if (File.Exists(localPayload))
        {
            return File.OpenRead(localPayload);
        }

        var resPayload = Path.Combine(baseDir, "Resources", "payload.zip");
        if (File.Exists(resPayload))
        {
            return File.OpenRead(resPayload);
        }

        throw new FileNotFoundException("未找到嵌入的核心资源 payload.zip，安装程序无法继续。");
    }
}
