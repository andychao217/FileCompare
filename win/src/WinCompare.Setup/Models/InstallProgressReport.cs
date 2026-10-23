namespace WinCompare.Setup.Models;

/// <summary>
/// Progress reporting payload for installation and uninstallation tasks with localization support.
/// </summary>
public sealed class InstallProgressReport
{
    public double Percentage { get; init; }
    public InstallStage Stage { get; init; }
    public string Detail { get; init; } = string.Empty;
    public string DetailText => Detail;
    public long ProcessedBytes { get; init; }
    public long TotalBytes { get; init; }

    public InstallProgressReport(double percentage, InstallStage stage, string detail = "", long processedBytes = 0, long totalBytes = 0)
    {
        Percentage = percentage;
        Stage = stage;
        Detail = detail;
        ProcessedBytes = processedBytes;
        TotalBytes = totalBytes;
    }
}
