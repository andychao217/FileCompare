using System;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32;

namespace WinCompare.Setup.Services;

public sealed class RegistryService : IRegistryService
{
    private const string UninstallSubKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\WinCompare";

    public void RegisterUninstall(string installPath, string exePath, string version, bool isPerUser, long estimatedSizeKb)
    {
        // 智能安全降级：只有在确实需要全局安装且当前具有管理员权限时才写入 HKLM
        if (!isPerUser && IsAdministrator())
        {
            try
            {
                WriteUninstallInfo(Registry.LocalMachine, installPath, exePath, version, estimatedSizeKb);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                // HKLM 权限不足时自动降级写入 HKCU
            }
            catch
            {
                // 其他异常时回退到 HKCU
            }
        }

        // 默认与安全兜底：写入当前用户注册表 (免提权，安全稳定)
        try
        {
            WriteUninstallInfo(Registry.CurrentUser, installPath, exePath, version, estimatedSizeKb);
        }
        catch
        {
            // 避免极端情况下因注册表故障中断主安装流程
        }
    }

    private static void WriteUninstallInfo(RegistryKey root, string installPath, string exePath, string version, long estimatedSizeKb)
    {
        using var key = root.CreateSubKey(UninstallSubKey, true);
        if (key == null) return;

        var uninstallExePath = Path.Combine(installPath, "Uninstall.exe");
        var uninstallCommand = $"\"{uninstallExePath}\"";

        key.SetValue("DisplayName", "WinCompare", RegistryValueKind.String);
        key.SetValue("DisplayVersion", version, RegistryValueKind.String);
        key.SetValue("Publisher", "WinCompare Team", RegistryValueKind.String);
        key.SetValue("DisplayIcon", exePath, RegistryValueKind.String);
        key.SetValue("InstallLocation", installPath, RegistryValueKind.String);
        key.SetValue("UninstallString", uninstallCommand, RegistryValueKind.String);
        key.SetValue("QuietUninstallString", $"{uninstallCommand} --silent", RegistryValueKind.String);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", (int)estimatedSizeKb, RegistryValueKind.DWord);
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"), RegistryValueKind.String);
    }

    public void UnregisterUninstall(bool isPerUser)
    {
        // 双向清理：优先清理 HKCU，若有管理员权限顺带清理 HKLM
        TryDeleteSubKey(Registry.CurrentUser);
        if (IsAdministrator())
        {
            TryDeleteSubKey(Registry.LocalMachine);
        }
    }

    private static void TryDeleteSubKey(RegistryKey root)
    {
        try
        {
            root.DeleteSubKeyTree(UninstallSubKey, false);
        }
        catch
        {
            // 忽略未找到或已清理的键
        }
    }

    public string? DetectExistingInstallPath()
    {
        // 1. 扫描当前用户和本地机器的 32/64 位卸载注册表项
        var roots = new[] { Registry.CurrentUser, Registry.LocalMachine };
        var subKeys = new[]
        {
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall",
            @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var root in roots)
        {
            foreach (var subKeyPath in subKeys)
            {
                try
                {
                    using var parentKey = root.OpenSubKey(subKeyPath, false);
                    if (parentKey == null) continue;

                    // A. 直接检查 WinCompare 键
                    using var directKey = parentKey.OpenSubKey("WinCompare", false);
                    if (directKey != null)
                    {
                        var path = ExtractValidInstallPath(directKey);
                        if (!string.IsNullOrEmpty(path)) return path;
                    }

                    // B. 遍历所有子项（涵盖先前老版 MSI 随机 GUID 生成的键名）
                    foreach (var name in parentKey.GetSubKeyNames())
                    {
                        using var childKey = parentKey.OpenSubKey(name, false);
                        if (childKey == null) continue;

                        var displayName = childKey.GetValue("DisplayName") as string;
                        if (!string.IsNullOrEmpty(displayName) && displayName.Contains("WinCompare", StringComparison.OrdinalIgnoreCase))
                        {
                            var path = ExtractValidInstallPath(childKey);
                            if (!string.IsNullOrEmpty(path)) return path;
                        }
                    }
                }
                catch
                {
                    // 权限受限或键缺失时平滑跳过
                }
            }
        }

        // 2. 常规已知物理路径探测兜底
        var candidatePaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WinCompare"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinCompare"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WinCompare")
        };

        foreach (var dir in candidatePaths)
        {
            if (File.Exists(Path.Combine(dir, "WinCompare.UI.exe")))
            {
                return dir;
            }
        }

        return null;
    }

    private static string? ExtractValidInstallPath(RegistryKey key)
    {
        // 优先从 InstallLocation 获取
        var installLocation = key.GetValue("InstallLocation") as string;
        if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
        {
            if (File.Exists(Path.Combine(installLocation, "WinCompare.UI.exe")))
            {
                return Path.GetFullPath(installLocation);
            }
        }

        // 其次从 DisplayIcon 解析
        var displayIcon = key.GetValue("DisplayIcon") as string;
        if (!string.IsNullOrWhiteSpace(displayIcon))
        {
            var dir = Path.GetDirectoryName(displayIcon.Trim('"'));
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && File.Exists(Path.Combine(dir, "WinCompare.UI.exe")))
            {
                return Path.GetFullPath(dir);
            }
        }

        return null;
    }

    private static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
