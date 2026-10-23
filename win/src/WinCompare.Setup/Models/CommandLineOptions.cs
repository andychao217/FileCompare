using System;
using System.IO;

namespace WinCompare.Setup.Models;

/// <summary>
/// Command line options parser for silent and unattended installation/uninstallation.
/// </summary>
public sealed class CommandLineOptions
{
    public bool IsSilent { get; set; }
    public bool IsUninstall { get; set; }
    public bool ShowHelp { get; set; }
    public string? InstallDir { get; set; }
    public bool CreateDesktopShortcut { get; set; } = true;
    public bool CreateStartMenuShortcut { get; set; } = true;

    public static CommandLineOptions Parse(string[] args, string? processPath)
    {
        var options = new CommandLineOptions();

        var processName = Path.GetFileNameWithoutExtension(processPath ?? string.Empty);
        if (processName.Contains("uninstall", StringComparison.OrdinalIgnoreCase))
        {
            options.IsUninstall = true;
        }

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i].Trim();
            if (string.IsNullOrEmpty(arg)) continue;

            // Help flags
            if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-help", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "/help", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "/h", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "/?", StringComparison.OrdinalIgnoreCase))
            {
                options.ShowHelp = true;
                continue;
            }

            // Silent / Quiet flags
            if (string.Equals(arg, "--silent", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-silent", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "/silent", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "/s", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-s", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--quiet", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-quiet", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "/quiet", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "/q", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-q", StringComparison.OrdinalIgnoreCase))
            {
                options.IsSilent = true;
                continue;
            }

            // Uninstall flags
            if (string.Equals(arg, "--uninstall", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-uninstall", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "/uninstall", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-u", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "/u", StringComparison.OrdinalIgnoreCase))
            {
                options.IsUninstall = true;
                continue;
            }

            // Directory flags
            if (arg.StartsWith("--dir=", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("/dir=", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-dir=", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("--install-dir=", StringComparison.OrdinalIgnoreCase))
            {
                var idx = arg.IndexOf('=');
                options.InstallDir = CleanPath(arg[(idx + 1)..]);
                continue;
            }

            // Inno/NSIS style: /D=path
            if (arg.StartsWith("/D=", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-D=", StringComparison.OrdinalIgnoreCase))
            {
                options.InstallDir = CleanPath(arg[3..]);
                continue;
            }

            if ((string.Equals(arg, "--dir", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(arg, "-dir", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(arg, "/dir", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(arg, "-d", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(arg, "/d", StringComparison.OrdinalIgnoreCase)) &&
                i + 1 < args.Length)
            {
                options.InstallDir = CleanPath(args[++i]);
                continue;
            }

            // Shortcut flags
            if (string.Equals(arg, "--no-desktop-shortcut", StringComparison.OrdinalIgnoreCase))
            {
                options.CreateDesktopShortcut = false;
                continue;
            }

            if (string.Equals(arg, "--no-start-menu-shortcut", StringComparison.OrdinalIgnoreCase))
            {
                options.CreateStartMenuShortcut = false;
                continue;
            }

            if (string.Equals(arg, "--no-shortcuts", StringComparison.OrdinalIgnoreCase))
            {
                options.CreateDesktopShortcut = false;
                options.CreateStartMenuShortcut = false;
                continue;
            }
        }

        return options;
    }

    private static string CleanPath(string raw)
    {
        return raw.Trim('"', '\'').Trim();
    }
}
