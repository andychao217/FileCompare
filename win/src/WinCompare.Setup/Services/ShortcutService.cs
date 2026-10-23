using System;
using System.IO;
using WinCompare.Setup.Interop;

namespace WinCompare.Setup.Services;

public sealed class ShortcutService : IShortcutService
{
    private const string AppShortcutName = "WinCompare.lnk";

    public void CreateShortcuts(string targetExePath, string installDir, string iconPath, bool desktop, bool startMenu)
    {
        if (desktop)
        {
            var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            CreateShortcutInternal(Path.Combine(desktopDir, AppShortcutName), targetExePath, installDir, iconPath, "WinCompare 文件比对工具");
        }

        if (startMenu)
        {
            var programsDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var appFolder = Path.Combine(programsDir, "WinCompare");
            Directory.CreateDirectory(appFolder);
            CreateShortcutInternal(Path.Combine(appFolder, AppShortcutName), targetExePath, installDir, iconPath, "WinCompare 文件比对工具");
        }
    }

    public void RemoveShortcuts(bool desktop, bool startMenu)
    {
        try
        {
            if (desktop)
            {
                var desktopLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppShortcutName);
                if (File.Exists(desktopLink))
                {
                    File.Delete(desktopLink);
                }
            }

            if (startMenu)
            {
                var programsDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                var appFolder = Path.Combine(programsDir, "WinCompare");
                var startMenuLink = Path.Combine(appFolder, AppShortcutName);
                if (File.Exists(startMenuLink))
                {
                    File.Delete(startMenuLink);
                }

                if (Directory.Exists(appFolder) && Directory.GetFileSystemEntries(appFolder).Length == 0)
                {
                    Directory.Delete(appFolder);
                }
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private static void CreateShortcutInternal(string lnkPath, string targetPath, string workingDir, string iconPath, string description)
    {
        var link = (IShellLinkW)new ShellLink();
        link.SetPath(targetPath);
        link.SetWorkingDirectory(workingDir);
        link.SetDescription(description);

        if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
        {
            link.SetIconLocation(iconPath, 0);
        }

        var persistFile = (IPersistFile)link;
        persistFile.Save(lnkPath, true);
    }
}
