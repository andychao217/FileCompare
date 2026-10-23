namespace WinCompare.Setup.Models;

/// <summary>
/// Granular installation & uninstallation stages for multi-language status reporting.
/// </summary>
public enum InstallStage
{
    Ready,
    CheckingRunning,
    PreparingDirectory,
    ReadingPayload,
    Extracting,
    DeployingMaintenance,
    CreatingShortcuts,
    RegisteringSystem,
    InstallCompleted,
    UninstallChecking,
    UninstallShortcuts,
    UninstallRegistry,
    UninstallFiles,
    UninstallCleanup,
    UninstallCompleted,
    Error
}
