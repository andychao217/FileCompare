using System.Text.Json;
using System.Text.RegularExpressions;
using WinCompare.Core.Models;

namespace WinCompare.Core.Services;

public enum UpdateCheckStatus
{
    Idle,
    Checking,
    UpdateAvailable,
    UpToDate,
    Failed
}

public sealed class GitHubAssetInfo
{
    public string Name { get; set; } = "";
    public string BrowserDownloadUrl { get; set; } = "";
    public long Size { get; set; } = 0;
}

public sealed class GitHubReleaseInfo
{
    public string TagName { get; set; } = "";
    public string? Name { get; set; }
    public string? Body { get; set; }
    public string HtmlUrl { get; set; } = "";
    public string? PublishedAt { get; set; }
    public List<GitHubAssetInfo>? Assets { get; set; }
}

public sealed class UpdateCheckerService
{
    private static readonly Lazy<UpdateCheckerService> _instance = new(() => new UpdateCheckerService());
    public static UpdateCheckerService Instance => _instance.Value;

    public const string CurrentVersion = "0.6.0";
    public const string Repository = "andychao217/FileCompare";

    public UpdateCheckStatus Status { get; private set; } = UpdateCheckStatus.Idle;
    public string LatestReleaseVersion { get; private set; } = "";
    public string LatestReleaseNotes { get; private set; } = "";
    public string LatestReleaseUrl { get; private set; } = $"https://github.com/{Repository}/releases";
    public string DownloadAssetUrl { get; private set; } = "";
    public string DownloadAssetName { get; private set; } = "";
    public long DownloadAssetSize { get; private set; } = 0;
    public DateTime? LastCheckedDate { get; private set; }

    public event Action<UpdateCheckStatus>? StatusChanged;

    public UpdateCheckerService()
    {
        long timestamp = SettingsManager.Instance.LastUpdateCheckTimestamp;
        if (timestamp > 0)
        {
            LastCheckedDate = DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
        }
    }

    public async Task<(UpdateCheckStatus status, string latestVersion, string notes, string releaseUrl)> CheckForUpdatesAsync(bool isUserInitiated = false)
    {
        if (Status == UpdateCheckStatus.Checking)
        {
            return (Status, LatestReleaseVersion, LatestReleaseNotes, LatestReleaseUrl);
        }

        Status = UpdateCheckStatus.Checking;
        StatusChanged?.Invoke(Status);

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"WinCompare/{CurrentVersion}");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");

            string apiUrl = $"https://api.github.com/repos/{Repository}/releases/latest";
            using var response = await client.GetAsync(apiUrl);

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tProp) ? (tProp.GetString() ?? "") : "";
                string body = root.TryGetProperty("body", out var bProp) ? (bProp.GetString() ?? "") : "";
                string htmlUrl = root.TryGetProperty("html_url", out var hProp) ? (hProp.GetString() ?? "") : $"https://github.com/{Repository}/releases";

                string latestVer = tagName.TrimStart('v', 'V').Trim();
                LatestReleaseVersion = latestVer;
                LatestReleaseNotes = body;
                LatestReleaseUrl = string.IsNullOrWhiteSpace(htmlUrl) ? $"https://github.com/{Repository}/releases" : htmlUrl;

                // Parse assets
                if (root.TryGetProperty("assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assetsElem.EnumerateArray())
                    {
                        string name = asset.TryGetProperty("name", out var n) ? (n.GetString() ?? "") : "";
                        string dUrl = asset.TryGetProperty("browser_download_url", out var u) ? (u.GetString() ?? "") : "";
                        long sz = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;

                        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            DownloadAssetName = name;
                            DownloadAssetUrl = dUrl;
                            DownloadAssetSize = sz;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(DownloadAssetUrl))
                {
                    DownloadAssetUrl = LatestReleaseUrl;
                }

                LastCheckedDate = DateTime.Now;
                SettingsManager.Instance.LastUpdateCheckTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();

                if (IsNewerVersion(latestVer, CurrentVersion))
                {
                    // Check if user skipped this version (only suppress on automated launch check)
                    if (!isUserInitiated && string.Equals(SettingsManager.Instance.SkippedUpdateVersion, latestVer, StringComparison.OrdinalIgnoreCase))
                    {
                        Status = UpdateCheckStatus.Idle;
                        StatusChanged?.Invoke(Status);
                        return (Status, latestVer, LatestReleaseNotes, LatestReleaseUrl);
                    }

                    Status = UpdateCheckStatus.UpdateAvailable;
                    StatusChanged?.Invoke(Status);
                    return (Status, latestVer, LatestReleaseNotes, LatestReleaseUrl);
                }
                else
                {
                    Status = UpdateCheckStatus.UpToDate;
                    StatusChanged?.Invoke(Status);
                    return (Status, latestVer, LatestReleaseNotes, LatestReleaseUrl);
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Status = UpdateCheckStatus.UpToDate;
                StatusChanged?.Invoke(Status);
                return (Status, CurrentVersion, "", LatestReleaseUrl);
            }
            else
            {
                Status = UpdateCheckStatus.Failed;
                StatusChanged?.Invoke(Status);
                return (Status, "", "", LatestReleaseUrl);
            }
        }
        catch
        {
            Status = UpdateCheckStatus.Failed;
            StatusChanged?.Invoke(Status);
            return (Status, "", "", LatestReleaseUrl);
        }
    }

    public static bool IsNewerVersion(string latest, string current)
    {
        latest = latest.TrimStart('v', 'V').Trim();
        current = current.TrimStart('v', 'V').Trim();

        if (Version.TryParse(latest, out var vLatest) && Version.TryParse(current, out var vCurrent))
        {
            return vLatest > vCurrent;
        }

        var lParts = latest.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToList();
        var cParts = current.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToList();
        int maxLen = Math.Max(lParts.Count, cParts.Count);
        for (int i = 0; i < maxLen; i++)
        {
            int l = i < lParts.Count ? lParts[i] : 0;
            int c = i < cParts.Count ? cParts[i] : 0;
            if (l > c) return true;
            if (l < c) return false;
        }
        return false;
    }

    public string LocalizedReleaseNotes(AppLanguage language)
    {
        string raw = (LatestReleaseNotes ?? "").Trim();
        if (string.IsNullOrEmpty(raw)) return "";

        string[] targetTags;
        switch (language)
        {
            case AppLanguage.ZhHans:
                targetTags = new[] { "<!-- zh-Hans -->", "<!-- zh_CN -->", "<!-- zh -->", "## 简体中文", "## 中文" };
                break;
            case AppLanguage.Ja:
                targetTags = new[] { "<!-- ja -->", "<!-- ja_JP -->", "## 日本語", "## 日本语" };
                break;
            case AppLanguage.En:
            default:
                targetTags = new[] { "<!-- en -->", "<!-- en_US -->", "## English" };
                break;
        }

        string sectionRegex = @"(<!--\s*[a-zA-Z0-9_-]+\s*-->|##\s+[^\n\r]+)";

        foreach (var tag in targetTags)
        {
            int idx = raw.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                string afterTag = raw.Substring(idx + tag.Length);
                var match = Regex.Match(afterTag, sectionRegex);
                string section = match.Success ? afterTag.Substring(0, match.Index) : afterTag;
                section = section.Trim();
                if (!string.IsNullOrEmpty(section)) return section;
            }
        }

        // Fallback to English if non-English requested but not found
        if (language != AppLanguage.En)
        {
            foreach (var tag in new[] { "<!-- en -->", "<!-- en_US -->", "## English" })
            {
                int idx = raw.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    string afterTag = raw.Substring(idx + tag.Length);
                    var match = Regex.Match(afterTag, sectionRegex);
                    string section = match.Success ? afterTag.Substring(0, match.Index) : afterTag;
                    section = section.Trim();
                    if (!string.IsNullOrEmpty(section)) return section;
                }
            }
        }

        return raw;
    }

    public void SkipVersion(string version)
    {
        SettingsManager.Instance.SkippedUpdateVersion = version.TrimStart('v', 'V').Trim();
        Status = UpdateCheckStatus.Idle;
        StatusChanged?.Invoke(Status);
    }
}
