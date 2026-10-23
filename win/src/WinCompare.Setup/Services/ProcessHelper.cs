using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace WinCompare.Setup.Services;

public sealed class ProcessHelper : IProcessHelper
{
    private const string MutexName = @"Global\WinCompare_SingleInstance_Mutex";
    private const string ProcessName = "WinCompare.UI";

    public bool IsAppRunning()
    {
        // 1. Check named mutex
        if (Mutex.TryOpenExisting(MutexName, out var existingMutex))
        {
            existingMutex.Dispose();
            return true;
        }

        // 2. Check process list fallback
        var processes = Process.GetProcessesByName(ProcessName);
        return processes.Length > 0;
    }

    public bool TryCloseApp(int timeoutMs = 3000)
    {
        var processes = Process.GetProcessesByName(ProcessName);
        if (processes.Length == 0) return true;

        foreach (var p in processes)
        {
            try
            {
                p.CloseMainWindow();
            }
            catch
            {
                // Ignore
            }
        }

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (!IsAppRunning()) return true;
            Thread.Sleep(200);
        }

        // Force kill if graceful close timed out
        foreach (var p in Process.GetProcessesByName(ProcessName))
        {
            try { p.Kill(); } catch { }
        }

        return !IsAppRunning();
    }

    public void StartInstalledApp(string exePath)
    {
        if (File.Exists(exePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty,
                UseShellExecute = true
            });
        }
    }

    public void ScheduleSelfDeletion(string workingDir)
    {
        try
        {
            var currentPid = Environment.ProcessId;
            var script = $"@echo off\r\n:loop\r\ntasklist /FI \"PID eq {currentPid}\" 2>NUL | find \"{currentPid}\" >NUL\r\nif %ERRORLEVEL%==0 (\r\n  timeout /t 1 /nobreak >nul\r\n  goto loop\r\n)\r\nrmdir /s /q \"{workingDir}\"\r\ndel \"%~f0\"\r\n";
            var tempBat = Path.Combine(Path.GetTempPath(), $"wincompare_cleanup_{Guid.NewGuid():N}.bat");
            File.WriteAllText(tempBat, script);

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{tempBat}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi);
        }
        catch
        {
            // Ignore cleanup failure
        }
    }
}
