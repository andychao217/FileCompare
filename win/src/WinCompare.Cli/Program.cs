using System.Diagnostics;
using WinCompare.Core.Services;

namespace WinCompare.Cli;

internal class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return 0;
        }

        if (args.Contains("--version") || args.Contains("-v"))
        {
            Console.WriteLine($"WinCompare CLI (wincmp) v{UpdateCheckerService.CurrentVersion}");
            Console.WriteLine($"Rust Engine: {DiffEngineService.Instance.GetVersion()}");
            return 0;
        }

        string command = args[0].ToLowerInvariant();

        if (command == "diff" && args.Length >= 3)
        {
            return LaunchGuiOrDiff(args[1], args[2]);
        }
        else if (command == "merge" && args.Length >= 5)
        {
            return LaunchGuiOrMerge(args[1], args[2], args[3], args[4]);
        }
        else if (args.Length == 2 && !args[0].StartsWith("-") && !args[1].StartsWith("-"))
        {
            return LaunchGuiOrDiff(args[0], args[1]);
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Invalid arguments!");
            Console.ResetColor();
            PrintUsage();
            return 1;
        }
    }

    static int LaunchGuiOrDiff(string leftPath, string rightPath)
    {
        string fullLeft = Path.GetFullPath(leftPath);
        string fullRight = Path.GetFullPath(rightPath);

        // Try to launch WinCompare.exe if present
        string guiExe = Path.Combine(AppContext.BaseDirectory, "WinCompare.exe");
        if (File.Exists(guiExe))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = guiExe,
                Arguments = $"diff \"{fullLeft}\" \"{fullRight}\"",
                UseShellExecute = true
            });
            return 0;
        }

        // Otherwise print console diff summary
        Console.WriteLine($"Comparing: {fullLeft} <-> {fullRight}");
        if (File.Exists(fullLeft) && File.Exists(fullRight))
        {
            string left = File.ReadAllText(fullLeft);
            string right = File.ReadAllText(fullRight);
            var result = DiffEngineService.Instance.CompareText(left, right);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  + Additions: {result.Stats.Additions}");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  - Deletions: {result.Stats.Deletions}");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ~ Modifications: {result.Stats.Modifications}");
            Console.ResetColor();
            return result.Chunks.Any(c => c.ChangeType != WinCompare.Core.Models.DiffChangeType.Unchanged) ? 1 : 0;
        }

        Console.WriteLine("One or both target files do not exist.");
        return 1;
    }

    static int LaunchGuiOrMerge(string local, string @base, string remote, string result)
    {
        string guiExe = Path.Combine(AppContext.BaseDirectory, "WinCompare.exe");
        if (File.Exists(guiExe))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = guiExe,
                Arguments = $"merge \"{Path.GetFullPath(local)}\" \"{Path.GetFullPath(@base)}\" \"{Path.GetFullPath(remote)}\" \"{Path.GetFullPath(result)}\"",
                UseShellExecute = true
            });
            return 0;
        }

        Console.WriteLine($"Merging 3-Way: Local: {local}, Base: {@base}, Remote: {remote} -> Result: {result}");
        return 0;
    }

    static void PrintUsage()
    {
        Console.WriteLine("WinCompare CLI (wincmp) - High-Performance Git Diff & Merge Tool");
        Console.WriteLine("Usage:");
        Console.WriteLine("  wincmp <file1> <file2>                           Compare two files");
        Console.WriteLine("  wincmp diff <file1> <file2>                      Compare two files");
        Console.WriteLine("  wincmp merge <local> <base> <remote> <result>    Three-way merge");
        Console.WriteLine("  wincmp --version                                 Show version information");
        Console.WriteLine("  wincmp --help                                    Show this help");
        Console.WriteLine();
        Console.WriteLine("Git Configuration Examples:");
        Console.WriteLine("  git config --global diff.tool wincmp");
        Console.WriteLine("  git config --global difftool.wincmp.cmd 'wincmp diff \"$LOCAL\" \"$REMOTE\"'");
        Console.WriteLine("  git config --global merge.tool wincmp");
        Console.WriteLine("  git config --global mergetool.wincmp.cmd 'wincmp merge \"$LOCAL\" \"$BASE\" \"$REMOTE\" \"$MERGED\"'");
    }
}
