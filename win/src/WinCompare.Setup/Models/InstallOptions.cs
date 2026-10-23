using System;
using System.IO;

namespace WinCompare.Setup.Models;

/// <summary>
/// Configuration options selected by the user for installation.
/// </summary>
public sealed class InstallOptions
{
    public string InstallPath { get; set; } = GetDefaultInstallPath();
    public bool CreateDesktopShortcut { get; set; } = true;
    public bool CreateStartMenuShortcut { get; set; } = true;
    public bool LaunchAfterInstall { get; set; } = true;

    /// <summary>
    /// Determines whether the target directory resides inside user profile (免 UAC 极速安装).
    /// </summary>
    public bool IsPerUser
    {
        get
        {
            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var fullInstall = Path.GetFullPath(InstallPath).TrimEnd('\\', '/');
                var fullLocal = Path.GetFullPath(localAppData).TrimEnd('\\', '/');
                return fullInstall.StartsWith(fullLocal, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return true;
            }
        }
    }

    public static string GetDefaultInstallPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Programs", "WinCompare");
    }
}
