namespace WinCompare.Setup.Services;

public interface IProcessHelper
{
    bool IsAppRunning();
    bool TryCloseApp(int timeoutMs = 3000);
    void StartInstalledApp(string exePath);
    void ScheduleSelfDeletion(string workingDir);
}
