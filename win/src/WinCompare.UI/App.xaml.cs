using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using WinCompare.Core.Services;
using WinCompare.UI.Views;

namespace WinCompare.UI;

public partial class App : Application
{
    private Window? _window;
    public static List<MainWindow> ActiveWindows { get; } = new();
    public static MainWindow? MainWindowInstance => ActiveWindows.FirstOrDefault();
    public static MainWindow? CurrentMainWindow => ActiveWindow ?? MainWindowInstance;
    public static MainWindow? ActiveWindow { get; set; }
    public static bool IsElevatedAdministrator { get; private set; }

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

    public static bool IsLightMode()
    {
        try
        {
            var themeSetting = SettingsManager.Instance?.Theme;
            if (string.Equals(themeSetting, "Light", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(themeSetting, "Dark", StringComparison.OrdinalIgnoreCase)) return false;

            var win = ActiveWindow ?? MainWindowInstance;
            if (win?.Content is FrameworkElement fe)
            {
                if (fe.ActualTheme == ElementTheme.Light) return true;
                if (fe.ActualTheme == ElementTheme.Dark) return false;
            }

            var uiSettings = new Windows.UI.ViewManagement.UISettings();
            var bg = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
            return (bg.R + bg.G + bg.B) > 384;
        }
        catch
        {
            return false;
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Microsoft.UI.Xaml.Media.Brush> _themeBrushCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Windows.UI.Color> _themeColorCache = new();

    public static void InvalidateThemeBrushCache()
    {
        _themeBrushCache.Clear();
        _themeColorCache.Clear();
    }

    public static void WarmupThemeBrushes()
    {
        try
        {
            string[] commonKeys = new[]
            {
                "TextPrimaryBrush", "TextSecondaryBrush", "TextTertiaryBrush", "TextMutedBrush",
                "TextFillColorPrimaryBrush", "TextFillColorSecondaryBrush", "TextFillColorTertiaryBrush", "TextOnAccentBrush",
                "DiffAddedBgBrush", "DiffAddedFgBrush", "DiffAddedBorderBrush",
                "DiffDeletedBgBrush", "DiffDeletedFgBrush", "DiffDeletedBorderBrush",
                "DiffModifiedBgBrush", "DiffModifiedFgBrush", "DiffModifiedBorderBrush",
                "DiffPhantomBgBrush", "DiffIdenticalBgBrush", "DiffIdenticalFgBrush",
                "DiffMissingBgBrush", "DiffMissingFgBrush", "DiffSelectedRowBgBrush",
                "StatusModTextBrush", "StatusAddTextBrush", "StatusDelTextBrush", "StatusInfoTextBrush",
                "BrandPrimaryBrush", "BrandInfoBrush", "BrandInfoSubtleBgBrush",
                "CardAcrylicBackground", "CardBorderBrush", "CardBackgroundFillColorDefaultBrush", "CardStrokeColorDefaultBrush",
                "DividerStrokeColorDefaultBrush", "SubtleFillColorSecondaryBrush", "SubtleFillColorTransparentBrush",
                "TableHeaderBgBrush", "TableRowNumberBgBrush", "NormalBorderBrush",
                "FolderIconBrush", "CodeIconBrush", "ExcelIconBrush", "WordIconBrush", "GenericFileIconBrush",
                "WelcomeFolderDiffFgBrush", "WelcomeFolderDiffBgBrush",
                "WelcomeExcelDiffFgBrush", "WelcomeExcelDiffBgBrush",
                "WelcomeWordDiffFgBrush", "WelcomeWordDiffBgBrush",
                "WelcomeThreeWayMergeFgBrush", "WelcomeThreeWayMergeBgBrush",
                "WelcomeTextDiffFgBrush", "WelcomeTextDiffBgBrush"
            };

            foreach (var k in commonKeys)
            {
                var lightBrush = ResolveThemeBrushFromResources(k, isLight: true) ?? GetSafeFallbackBrush(k, isLight: true);
                _themeBrushCache[$"Light_{k}"] = lightBrush;

                var darkBrush = ResolveThemeBrushFromResources(k, isLight: false) ?? GetSafeFallbackBrush(k, isLight: false);
                _themeBrushCache[$"Dark_{k}"] = darkBrush;
            }
        }
        catch { }
    }

    public static Microsoft.UI.Xaml.Media.Brush GetThemeBrush(string key, Microsoft.UI.Xaml.Media.Brush? fallback = null)
    {
        bool isLight = IsLightMode();
        string cacheKey = $"{(isLight ? "Light" : "Dark")}_{key}";

        if (_themeBrushCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            var brush = ResolveThemeBrushFromResources(key, isLight);
            if (brush != null)
            {
                _themeBrushCache[cacheKey] = brush;
                return brush;
            }
        }
        catch { }

        if (fallback != null)
        {
            return fallback;
        }

        var safeBrush = GetSafeFallbackBrush(key, isLight);
        _themeBrushCache[cacheKey] = safeBrush;
        return safeBrush;
    }

    private static IEnumerable<ResourceDictionary> EnumerateDictionaries(ResourceDictionary? root)
    {
        return EnumerateDictionariesInternal(root, new HashSet<ResourceDictionary>(ReferenceEqualityComparer.Instance), 0);
    }

    private static IEnumerable<ResourceDictionary> EnumerateDictionariesInternal(ResourceDictionary? root, HashSet<ResourceDictionary> visited, int depth)
    {
        if (root == null || depth > 10) yield break;
        if (!visited.Add(root)) yield break;

        yield return root;
        if (root.MergedDictionaries != null)
        {
            foreach (var md in root.MergedDictionaries)
            {
                foreach (var child in EnumerateDictionariesInternal(md, visited, depth + 1))
                {
                    yield return child;
                }
            }
        }
    }

    private static Microsoft.UI.Xaml.Media.Brush? ResolveThemeBrushFromResources(string key, bool isLight)
    {
        if (Current?.Resources == null) return null;

        string targetTheme = isLight ? "Light" : "Default";

        // 1. 递归检索所有 MergedDictionaries 中的 ThemeDictionaries (确保覆盖 Colors.xaml)
        foreach (var dict in EnumerateDictionaries(Current.Resources))
        {
            try
            {
                if (dict.ThemeDictionaries != null &&
                    dict.ThemeDictionaries.TryGetValue(targetTheme, out var tdObj) &&
                    tdObj is ResourceDictionary td)
                {
                    if (td.ContainsKey(key) && td[key] is Microsoft.UI.Xaml.Media.Brush b)
                    {
                        return b;
                    }
                }
            }
            catch { }
        }

        // 2. 语义别名映射
        string? aliasKey = null;
        if (key != "TextPrimaryBrush" && key.Contains("TextFillColorPrimary"))
        {
            aliasKey = "TextPrimaryBrush";
        }
        else if (key != "TextSecondaryBrush" && key.Contains("TextFillColorSecondary"))
        {
            aliasKey = "TextSecondaryBrush";
        }
        else if (key != "TextMutedBrush" && (key.Contains("TextFillColorTertiary") || key == "TextTertiaryBrush"))
        {
            aliasKey = "TextMutedBrush";
        }

        if (aliasKey != null)
        {
            foreach (var dict in EnumerateDictionaries(Current.Resources))
            {
                try
                {
                    if (dict.ThemeDictionaries != null &&
                        dict.ThemeDictionaries.TryGetValue(targetTheme, out var tdObj) &&
                        tdObj is ResourceDictionary td)
                    {
                        if (td.ContainsKey(aliasKey) && td[aliasKey] is Microsoft.UI.Xaml.Media.Brush b)
                        {
                            return b;
                        }
                    }
                }
                catch { }
            }
        }

        return null;
    }

    private static Microsoft.UI.Xaml.Media.Brush GetSafeFallbackBrush(string key, bool isLight)
    {
        Windows.UI.Color color = key switch
        {
            // Core Text Brushes (Fluent High Contrast)
            "TextPrimaryBrush" or "TextFillColorPrimaryBrush" => isLight ? Windows.UI.Color.FromArgb(255, 15, 23, 42) : Windows.UI.Color.FromArgb(255, 248, 250, 252),
            "TextSecondaryBrush" or "TextFillColorSecondaryBrush" => isLight ? Windows.UI.Color.FromArgb(255, 51, 65, 85) : Windows.UI.Color.FromArgb(255, 203, 213, 225),
            "TextTertiaryBrush" or "TextMutedBrush" or "TextFillColorTertiaryBrush" => isLight ? Windows.UI.Color.FromArgb(255, 100, 116, 139) : Windows.UI.Color.FromArgb(255, 148, 163, 184),
            "TextOnAccentBrush" => Windows.UI.Color.FromArgb(255, 255, 255, 255),

            // Diff Highlights
            "DiffAddedBgBrush" => isLight ? Windows.UI.Color.FromArgb(36, 16, 185, 129) : Windows.UI.Color.FromArgb(40, 6, 78, 59),
            "DiffAddedFgBrush" => isLight ? Windows.UI.Color.FromArgb(255, 4, 120, 87) : Windows.UI.Color.FromArgb(255, 74, 222, 128),
            "DiffAddedBorderBrush" => isLight ? Windows.UI.Color.FromArgb(128, 16, 185, 129) : Windows.UI.Color.FromArgb(96, 16, 185, 129),

            "DiffDeletedBgBrush" => isLight ? Windows.UI.Color.FromArgb(36, 239, 68, 68) : Windows.UI.Color.FromArgb(40, 127, 29, 29),
            "DiffDeletedFgBrush" => isLight ? Windows.UI.Color.FromArgb(255, 185, 28, 28) : Windows.UI.Color.FromArgb(255, 248, 113, 113),
            "DiffDeletedBorderBrush" => isLight ? Windows.UI.Color.FromArgb(128, 239, 68, 68) : Windows.UI.Color.FromArgb(96, 239, 68, 68),

            "DiffModifiedBgBrush" => isLight ? Windows.UI.Color.FromArgb(40, 245, 158, 11) : Windows.UI.Color.FromArgb(42, 120, 53, 15),
            "DiffModifiedFgBrush" => isLight ? Windows.UI.Color.FromArgb(255, 180, 83, 9) : Windows.UI.Color.FromArgb(255, 253, 224, 71),
            "DiffModifiedBorderBrush" => isLight ? Windows.UI.Color.FromArgb(144, 245, 158, 11) : Windows.UI.Color.FromArgb(96, 245, 158, 11),

            "DiffPhantomBgBrush" => isLight ? Windows.UI.Color.FromArgb(10, 0, 0, 0) : Windows.UI.Color.FromArgb(21, 136, 136, 136),
            "DiffIdenticalBgBrush" => isLight ? Windows.UI.Color.FromArgb(0, 255, 255, 255) : Windows.UI.Color.FromArgb(8, 255, 255, 255),
            "DiffIdenticalFgBrush" => isLight ? Windows.UI.Color.FromArgb(255, 51, 65, 85) : Windows.UI.Color.FromArgb(255, 148, 163, 184),

            "DiffMissingBgBrush" => isLight ? Windows.UI.Color.FromArgb(13, 0, 0, 0) : Windows.UI.Color.FromArgb(37, 239, 68, 68),
            "DiffMissingFgBrush" => isLight ? Windows.UI.Color.FromArgb(255, 100, 116, 139) : Windows.UI.Color.FromArgb(255, 248, 113, 113),
            "DiffSelectedRowBgBrush" => isLight ? Windows.UI.Color.FromArgb(34, 37, 99, 235) : Windows.UI.Color.FromArgb(53, 59, 130, 246),

            // Status badges
            "StatusModTextBrush" => isLight ? Windows.UI.Color.FromArgb(255, 180, 83, 9) : Windows.UI.Color.FromArgb(255, 251, 191, 36),
            "StatusAddTextBrush" => isLight ? Windows.UI.Color.FromArgb(255, 4, 120, 87) : Windows.UI.Color.FromArgb(255, 52, 211, 153),
            "StatusDelTextBrush" => isLight ? Windows.UI.Color.FromArgb(255, 185, 28, 28) : Windows.UI.Color.FromArgb(255, 248, 113, 113),
            "StatusInfoTextBrush" => isLight ? Windows.UI.Color.FromArgb(255, 29, 78, 216) : Windows.UI.Color.FromArgb(255, 96, 165, 250),

            // Brand
            "BrandPrimaryBrush" => isLight ? Windows.UI.Color.FromArgb(255, 37, 99, 235) : Windows.UI.Color.FromArgb(255, 59, 130, 246),
            "BrandInfoBrush" => isLight ? Windows.UI.Color.FromArgb(255, 37, 99, 235) : Windows.UI.Color.FromArgb(255, 59, 130, 246),
            "BrandInfoSubtleBgBrush" => isLight ? Windows.UI.Color.FromArgb(21, 37, 99, 235) : Windows.UI.Color.FromArgb(37, 59, 130, 246),

            // Cards & Surface
            "CardAcrylicBackground" => isLight ? Windows.UI.Color.FromArgb(232, 255, 255, 255) : Windows.UI.Color.FromArgb(232, 36, 36, 36),
            "CardBorderBrush" => isLight ? Windows.UI.Color.FromArgb(24, 0, 0, 0) : Windows.UI.Color.FromArgb(37, 255, 255, 255),
            "DividerStrokeColorDefaultBrush" => isLight ? Windows.UI.Color.FromArgb(255, 226, 232, 240) : Windows.UI.Color.FromArgb(31, 255, 255, 255),
            "CardBackgroundFillColorDefaultBrush" => isLight ? Windows.UI.Color.FromArgb(153, 255, 255, 255) : Windows.UI.Color.FromArgb(13, 255, 255, 255),
            "CardStrokeColorDefaultBrush" => isLight ? Windows.UI.Color.FromArgb(255, 226, 232, 240) : Windows.UI.Color.FromArgb(24, 255, 255, 255),
            "SubtleFillColorSecondaryBrush" => isLight ? Windows.UI.Color.FromArgb(255, 241, 245, 249) : Windows.UI.Color.FromArgb(13, 255, 255, 255),
            "SubtleFillColorTransparentBrush" => Windows.UI.Color.FromArgb(0, 0, 0, 0),
            "TableHeaderBgBrush" => isLight ? Windows.UI.Color.FromArgb(255, 248, 250, 252) : Windows.UI.Color.FromArgb(255, 30, 30, 30),
            "TableRowNumberBgBrush" => isLight ? Windows.UI.Color.FromArgb(255, 241, 245, 249) : Windows.UI.Color.FromArgb(255, 36, 36, 36),
            "NormalBorderBrush" => isLight ? Windows.UI.Color.FromArgb(255, 226, 232, 240) : Windows.UI.Color.FromArgb(37, 255, 255, 255),

            // Icons
            "FolderIconBrush" or "WelcomeFolderDiffFgBrush" => isLight ? Windows.UI.Color.FromArgb(255, 217, 119, 6) : Windows.UI.Color.FromArgb(255, 245, 158, 11),
            "CodeIconBrush" or "WelcomeTextDiffFgBrush" => isLight ? Windows.UI.Color.FromArgb(255, 37, 99, 235) : Windows.UI.Color.FromArgb(255, 59, 130, 246),
            "ExcelIconBrush" or "WelcomeExcelDiffFgBrush" => isLight ? Windows.UI.Color.FromArgb(255, 5, 150, 105) : Windows.UI.Color.FromArgb(255, 16, 185, 129),
            "WordIconBrush" or "WelcomeWordDiffFgBrush" => isLight ? Windows.UI.Color.FromArgb(255, 67, 56, 202) : Windows.UI.Color.FromArgb(255, 79, 70, 229),
            "WelcomeThreeWayMergeFgBrush" => isLight ? Windows.UI.Color.FromArgb(255, 124, 58, 237) : Windows.UI.Color.FromArgb(255, 139, 92, 246),
            "GenericFileIconBrush" => isLight ? Windows.UI.Color.FromArgb(255, 100, 116, 139) : Windows.UI.Color.FromArgb(255, 148, 163, 184),

            _ => (key.Contains("Text") || key.Contains("Fg") || key.Contains("Foreground"))
                ? (isLight ? Windows.UI.Color.FromArgb(255, 15, 23, 42) : Windows.UI.Color.FromArgb(255, 248, 250, 252))
                : Windows.UI.Color.FromArgb(0, 0, 0, 0)
        };

        return new Microsoft.UI.Xaml.Media.SolidColorBrush(color);
    }

    public static Windows.UI.Color GetThemeColor(string key, Windows.UI.Color? fallback = null)
    {
        bool isLight = IsLightMode();
        string cacheKey = $"{(isLight ? "Light" : "Dark")}_{key}";

        if (_themeColorCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var brush = GetThemeBrush(key);
        if (brush is Microsoft.UI.Xaml.Media.SolidColorBrush scb)
        {
            _themeColorCache[cacheKey] = scb.Color;
            return scb.Color;
        }
        return fallback ?? Microsoft.UI.Colors.Transparent;
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate? handlerRoutine, bool add);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetStdHandle(int nStdHandle, IntPtr hHandle);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int UnhandledExceptionFilterDelegate(IntPtr exceptionPointers);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr SetUnhandledExceptionFilter(UnhandledExceptionFilterDelegate lpTopLevelExceptionFilter);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int VectoredExceptionHandlerDelegate(IntPtr exceptionPointers);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr AddVectoredExceptionHandler(uint First, VectoredExceptionHandlerDelegate Handler);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetModuleHandleEx(uint dwFlags, IntPtr lpModuleName, out IntPtr phModule);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint GetModuleFileName(IntPtr hModule, StringBuilder lpFilename, int nSize);

    [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint GetMappedFileName(IntPtr hProcess, IntPtr lpv, StringBuilder lpFilename, uint nSize);


    private const uint GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS = 0x00000004;
    private const uint GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct EXCEPTION_RECORD
    {
        public uint ExceptionCode;
        public uint ExceptionFlags;
        public IntPtr ExceptionRecord;
        public IntPtr ExceptionAddress;
        public uint NumberParameters;
        public uint __padding;
        public IntPtr ExceptionInformation0;
        public IntPtr ExceptionInformation1;
        public IntPtr ExceptionInformation2;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EXCEPTION_POINTERS
    {
        public IntPtr ExceptionRecord;
        public IntPtr ContextRecord;
    }

    private const int ATTACH_PARENT_PROCESS = -1;
    private const int STD_OUTPUT_HANDLE = -11;
    private const int STD_ERROR_HANDLE = -12;
    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;

    private delegate bool ConsoleCtrlDelegate(int ctrlType);
    private static ConsoleCtrlDelegate? _consoleCtrlHandler;
    private static UnhandledExceptionFilterDelegate? _nativeFilterDelegate;

    private static readonly object _logLock = new();

    public static void SafeAppendLog(string filename, string content)
    {
        try
        {
            lock (_logLock)
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinCompare", "logs");
                Directory.CreateDirectory(logDir);
                string logPath = Path.Combine(logDir, filename);
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        using var fs = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.WriteThrough);
                        using var writer = new StreamWriter(fs, Encoding.UTF8);
                        writer.Write(content);
                        writer.Flush();
                        break;
                    }
                    catch (IOException)
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                }
            }
        }
        catch { }
    }

    public static void Log(string message)
    {
        try
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            try
            {
                Console.WriteLine(line);
                Console.Out.Flush();
            }
            catch { }

            SafeAppendLog("app.log", line + Environment.NewLine);
        }
        catch { }
    }

    public static void LogFatalError(string source, Exception? ex, string? extra = null)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string msgText = extra ?? ex?.Message ?? "Unknown";
            string message = $"\n========================================\n" +
                             $"[FATAL ERROR / CRASH] {timestamp}\n" +
                             $"Source: {source}\n" +
                             $"Message: {msgText}\n" +
                             $"Exception Type: {ex?.GetType().FullName}\n" +
                             $"Details:\n{ex}\n" +
                             $"========================================\n";

            try
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(message);
                Console.ResetColor();
                Console.Error.Flush();
            }
            catch { }

            SafeAppendLog("crash.log", message + Environment.NewLine);
        }
        catch { }
    }

    private static int _isTerminating = 0;

    public static void TerminateProcessImmediately()
    {
        if (System.Threading.Interlocked.Exchange(ref _isTerminating, 1) != 0)
        {
            return;
        }

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

    private static void EnsureConsoleAttached()
    {
        try
        {
            // 1. \u5C1D\u8BD5\u9644\u52A0\u5230\u7236\u8FDB\u7A0B\u63A7\u5236\u53F0
            AttachConsole(ATTACH_PARENT_PROCESS);

            // 2. \u65E0\u8BBA\u9644\u52A0\u7ED3\u679C\u5982\u4F55\uFF0C\u663E\u5F0F\u6253\u5F00\u5E76\u91CD\u5B9A\u5411\u5230\u7CFB\u7EDF CONOUT$ \u8BBE\u5907\u7BA1\u9053\uFF0C\u786E\u4FDD PowerShell \u4E2D 100% \u80FD\u6253\u5370\uFF01
            try
            {
                IntPtr conoutHandle = CreateFile("CONOUT$", GENERIC_WRITE | GENERIC_READ, FILE_SHARE_WRITE | FILE_SHARE_READ, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (conoutHandle != IntPtr.Zero && conoutHandle != new IntPtr(-1))
                {
                    SetStdHandle(STD_OUTPUT_HANDLE, conoutHandle);
                    SetStdHandle(STD_ERROR_HANDLE, conoutHandle);

                    var fileStream = new FileStream(new Microsoft.Win32.SafeHandles.SafeFileHandle(conoutHandle, false), FileAccess.Write);
                    var writer = new StreamWriter(fileStream, new UTF8Encoding(false)) { AutoFlush = true };
                    Console.SetOut(writer);
                    Console.SetError(writer);
                }
                else
                {
                    var stdout = Console.OpenStandardOutput();
                    var stderr = Console.OpenStandardError();
                    Console.SetOut(new StreamWriter(stdout, new UTF8Encoding(false)) { AutoFlush = true });
                    Console.SetError(new StreamWriter(stderr, new UTF8Encoding(false)) { AutoFlush = true });
                }
            }
            catch { }

            try
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
            catch { }

            string startMsg = $"========================================\n" +
                              $"[WinCompare] \u63A7\u5236\u53F0\u8BCA\u65AD\u901A\u9053\u5DF2\u5C31\u7EEA ({DateTime.Now:yyyy-MM-dd HH:mm:ss.fff})\n" +
                              $"[WinCompare] \u6240\u6709\u64CD\u4F5C\u65E5\u5FD7\u5C06\u540C\u6B65\u8F93\u51FA\u5230\u672C PowerShell \u7A97\u53E3\u548C %LOCALAPPDATA%\\WinCompare\\logs\n" +
                              $"========================================";

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(startMsg);
            Console.ResetColor();
            Console.Out.Flush();

            SafeAppendLog("app.log", startMsg + Environment.NewLine + Environment.NewLine);

            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                IsElevatedAdministrator = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                if (IsElevatedAdministrator)
                {
                    string uipiWarn = "\n" +
                        "*******************************************************************************\n" +
                        "[UIPI 权限警告 / CRITICAL WARNING] 检测到应用当前正在以【管理员身份 (Administrator)】运行！\n" +
                        "由于 Windows 内核的 UIPI (User Interface Privilege Isolation) 权限隔离机制，\n" +
                        "以普通权限运行的 Windows 文件资源管理器 (Explorer) 无法向管理员应用拖放文件，\n" +
                        "拖拽时光标将强制显示禁止图标 🚫！\n" +
                        "【解决方案】：请使用普通非管理员权限的 PowerShell / 终端重新启动应用，即可正常拖拽！\n" +
                        "*******************************************************************************\n";
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(uipiWarn);
                    Console.ResetColor();
                    Console.Out.Flush();
                    SafeAppendLog("app.log", uipiWarn + Environment.NewLine);
                }
                else
                {
                    Log("[Security] 当前应用以标准权限 (Non-Admin) 运行，UIPI 不会拦截资源管理器拖拽。");
                }
            }
            catch { }
        }
        catch
        {
            // \u5FFD\u7565\u975E\u7EC8\u7AEF\u8FD0\u884C\u73AF\u5887
        }
    }

    private static int OnVectoredException(IntPtr exceptionPointers)
    {
        try
        {
            if (exceptionPointers == IntPtr.Zero) return 0;

            var pointers = Marshal.PtrToStructure<EXCEPTION_POINTERS>(exceptionPointers);
            if (pointers.ExceptionRecord == IntPtr.Zero) return 0;

            var record = Marshal.PtrToStructure<EXCEPTION_RECORD>(pointers.ExceptionRecord);
            uint code = record.ExceptionCode;

            if (code == 0x40010006 || code == 0x406D1388 || code == 0x000006BA)
            {
                return 0;
            }

            bool isFatal = code switch
            {
                0xC0000005 => true,
                0xC0000409 => true,
                0xC00000FD => true,
                0x80000003 => true,
                0xC000001D => true,
                0xC0000025 => true,
                0xE06D7363 => true,
                _ => (code & 0xC0000000) == 0xC0000000
            };

            if (isFatal)
            {
                string moduleName = "Unknown";
                long offset = 0;
                try
                {
                    if (GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, record.ExceptionAddress, out IntPtr hModule))
                    {
                        var sb = new StringBuilder(260);
                        if (GetModuleFileName(hModule, sb, sb.Capacity) > 0)
                        {
                            moduleName = Path.GetFileName(sb.ToString());
                            offset = record.ExceptionAddress.ToInt64() - hModule.ToInt64();
                        }
                    }
                }
                catch { }

                string codeDesc = code switch
                {
                    0xC0000005 => "ACCESS_VIOLATION (\u5185\u5B58\u8BBF\u95EE\u8D8A\u754C/\u91CE\u6307\u9488)",
                    0xC0000006 => "STATUS_IN_PAGE_ERROR (\u6620\u5C04\u9875\u5185\u5B58/\u78C1\u76D8/\u7F51\u7EDC\u8BFB\u5196\u9519\u8BEF)",
                    0xC0000409 => "STATUS_STACK_BUFFER_OVERRUN / FAST_FAIL (DirectUI FailFast)",
                    0xC00000FD => "STACK_OVERFLOW (\u6808\u6EA2\u51FA)",
                    0x80000003 => "BREAKPOINT / ASSERTION_FAILED",
                    0xC000001D => "ILLEGAL_INSTRUCTION (\u975E\u6CD5\u6307\u4EE4)",
                    0xE06D7363 => "MSVC_CPP_EXCEPTION (DirectUI C++ \u6838\u5FC3\u5F02\u5E38)",
                    _ => $"Native NTSTATUS (0x{code:X8})"
                };

                string extraInfo = "";
                if (code == 0xC0000006 && record.NumberParameters >= 3)
                {
                    string op = record.ExceptionInformation0 == IntPtr.Zero ? "Read" : "Write";
                    string mappedFile = "";
                    try
                    {
                        var sbMap = new StringBuilder(512);
                        if (GetMappedFileName(GetCurrentProcess(), record.ExceptionInformation1, sbMap, (uint)sbMap.Capacity) > 0)
                        {
                            mappedFile = $", MappedFile={sbMap}";
                        }
                    }
                    catch { }
                    extraInfo = $"Fault Details:  Op={op}, TargetAddr=0x{record.ExceptionInformation1.ToInt64():X16}, SubNTSTATUS=0x{record.ExceptionInformation2.ToInt64():X8}{mappedFile}\n";
                }
                else if (code == 0xC0000005 && record.NumberParameters >= 2)
                {
                    string op = record.ExceptionInformation0 == IntPtr.Zero ? "Read" : "Write";
                    extraInfo = $"Fault Details:  Op={op}, TargetAddr=0x{record.ExceptionInformation1.ToInt64():X16}\n";
                }

                string msg = "\n========================================\n" +
                             $"[FATAL NATIVE VEH CRASH] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n" +
                             $"Exception Code: 0x{code:X8} - {codeDesc}\n" +
                             $"Fault Address:  0x{record.ExceptionAddress:X16}\n" +
                             $"Fault Module:   {moduleName} (+0x{offset:X})\n" +
                             extraInfo +
                             "========================================\n";

                try
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine(msg);
                    Console.ResetColor();
                    Console.Error.Flush();
                }
                catch { }

                SafeAppendLog("crash.log", msg + Environment.NewLine);
            }
        }
        catch { }

        return 0;
    }

    private static int OnNativeUnhandledException(IntPtr exceptionPointers)
    {
        try
        {
            uint code = 0;
            IntPtr addr = IntPtr.Zero;
            if (exceptionPointers != IntPtr.Zero)
            {
                var pointers = Marshal.PtrToStructure<EXCEPTION_POINTERS>(exceptionPointers);
                if (pointers.ExceptionRecord != IntPtr.Zero)
                {
                    var record = Marshal.PtrToStructure<EXCEPTION_RECORD>(pointers.ExceptionRecord);
                    code = record.ExceptionCode;
                    addr = record.ExceptionAddress;
                }
            }

            string avDesc = code == 0xC0000005 ? "(ACCESS_VIOLATION \u5185\u5B58\u8BBF\u95EE\u8D8A\u754C/\u91CE\u6307\u9488)" : "";
            string crashMsg = "\n========================================\n" +
                              "[FATAL NATIVE CRASH DETECTED / \u539F\u751F\u7CFB\u7EDF\u7EA7\u5D29\u6E83]\n" +
                              $"\u65F6\u95F4: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n" +
                              $"\u5E95\u5C42\u5F02\u5E38\u4EE3\u7801 (ExceptionCode): 0x{code:X8} {avDesc}\n" +
                              $"\u5D29\u6E83\u6307\u4EE4\u7269\u7406\u5730\u5740: 0x{addr:X16}\n" +
                              "========================================\n";

            try
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(crashMsg);
                Console.ResetColor();
                Console.Error.Flush();
            }
            catch { }

            SafeAppendLog("crash.log", crashMsg + Environment.NewLine);
        }
        catch { }

        return 0; // EXCEPTION_CONTINUE_SEARCH
    }

    static App()
    {
        EnsureConsoleAttached();

        // 注释掉调试期的 VEH 第一轮异常拦截器，避免拦截并误报 DirectUI/Windows 内部已安全处理的良性 First-Chance 异常
        // 真正的致命崩溃由下方的 SetUnhandledExceptionFilter 和 AppDomain.UnhandledException 统一兜底
        // try
        // {
        //     _vectoredHandlerDelegate = OnVectoredException;
        //     AddVectoredExceptionHandler(1, _vectoredHandlerDelegate);
        // }
        // catch { }

        try
        {
            _nativeFilterDelegate = OnNativeUnhandledException;
            SetUnhandledExceptionFilter(_nativeFilterDelegate);
        }
        catch { }

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            LogFatalError("AppDomain.UnhandledException", ex, e.ExceptionObject?.ToString());
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            LogFatalError("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        // FirstChanceException 在生产环境中严禁启用，防止其内部任何轻微异常导致与 CLR 无限重入递归并引发 0xc00000fd 栈溢出崩溃
        // 真正的未捕获致命异常由上方的 SetUnhandledExceptionFilter 和 AppDomain.CurrentDomain.UnhandledException 统一接管
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
            LogFatalError("WinUI.Application.UnhandledException", e.Exception, e.Message);
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var window = new MainWindow();
            _window = window;
            _window.Activate();
        }
        catch (System.Exception ex)
        {
            LogFatalError("App.OnLaunched", ex);
            throw;
        }
    }
}
