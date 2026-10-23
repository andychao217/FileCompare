namespace WinCompare.Setup.Services;

public interface IRegistryService
{
    void RegisterUninstall(string installPath, string exePath, string version, bool isPerUser, long estimatedSizeKb);
    void UnregisterUninstall(bool isPerUser);
    string? DetectExistingInstallPath();
}
