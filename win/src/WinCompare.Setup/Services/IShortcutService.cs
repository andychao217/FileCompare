namespace WinCompare.Setup.Services;

public interface IShortcutService
{
    void CreateShortcuts(string targetExePath, string installDir, string iconPath, bool desktop, bool startMenu);
    void RemoveShortcuts(bool desktop, bool startMenu);
}
