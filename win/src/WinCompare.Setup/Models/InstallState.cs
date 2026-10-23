namespace WinCompare.Setup.Models;

/// <summary>
/// Represents the current phase of the installer state machine.
/// </summary>
public enum InstallState
{
    Ready,
    Installing,
    Finished,
    Error,
    Uninstalling,
    UninstallFinished
}
