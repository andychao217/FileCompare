using System.Text.Json.Serialization;
using WinCompare.Core.Services;

namespace WinCompare.Core.Models;

public enum CompareType
{
    TextDiff,
    ExcelDiff,
    WordDiff,
    FolderDiff,
    ThreeWayMerge
}

public sealed class RecentCompareRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string LeftPath { get; set; } = string.Empty;
    public string RightPath { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CompareType Type { get; set; } = CompareType.TextDiff;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    [JsonIgnore]
    public CompareType EffectiveType
    {
        get
        {
            // If explicit type is FolderDiff or either path is an existing directory
            if (Type == CompareType.FolderDiff ||
                Directory.Exists(LeftPath) || Directory.Exists(RightPath))
            {
                return CompareType.FolderDiff;
            }

            // Check file extensions
            string leftExt = Path.GetExtension(LeftPath).ToLowerInvariant();
            string rightExt = Path.GetExtension(RightPath).ToLowerInvariant();

            if (leftExt is ".xlsx" or ".xls" or ".csv" || rightExt is ".xlsx" or ".xls" or ".csv" || Type == CompareType.ExcelDiff)
            {
                return CompareType.ExcelDiff;
            }

            if (leftExt is ".docx" or ".doc" || rightExt is ".docx" or ".doc" || Type == CompareType.WordDiff)
            {
                return CompareType.WordDiff;
            }

            if (Type == CompareType.ThreeWayMerge)
            {
                return CompareType.ThreeWayMerge;
            }

            // If neither path has a file extension and contains directory separators, treat as FolderDiff
            if (string.IsNullOrEmpty(leftExt) && string.IsNullOrEmpty(rightExt) &&
                (LeftPath.Contains('\\') || LeftPath.Contains('/') || RightPath.Contains('\\') || RightPath.Contains('/')))
            {
                return CompareType.FolderDiff;
            }

            return CompareType.TextDiff;
        }
    }

    [JsonIgnore]
    public string FormattedDate
    {
        get
        {
            var span = DateTime.Now - Timestamp;
            var lang = LanguageManager.Instance.EffectiveLanguage;

            if (span.TotalMinutes < 1)
            {
                return lang switch
                {
                    AppLanguage.ZhHans => "刚刚",
                    AppLanguage.En => "Just now",
                    AppLanguage.Ja => "たった今",
                    _ => "Just now"
                };
            }

            if (span.TotalMinutes < 60)
            {
                int m = (int)span.TotalMinutes;
                return lang switch
                {
                    AppLanguage.ZhHans => $"{m} 分钟前",
                    AppLanguage.En => $"{m}m ago",
                    AppLanguage.Ja => $"{m}分前",
                    _ => $"{m}m ago"
                };
            }

            if (span.TotalHours < 24)
            {
                int h = (int)span.TotalHours;
                return lang switch
                {
                    AppLanguage.ZhHans => $"{h} 小时前",
                    AppLanguage.En => $"{h}h ago",
                    AppLanguage.Ja => $"{h}時間前",
                    _ => $"{h}h ago"
                };
            }

            if (span.TotalDays < 7)
            {
                int d = (int)span.TotalDays;
                return lang switch
                {
                    AppLanguage.ZhHans => $"{d} 天前",
                    AppLanguage.En => $"{d}d ago",
                    AppLanguage.Ja => $"{d}日前",
                    _ => $"{d}d ago"
                };
            }

            return Timestamp.ToString("MM-dd HH:mm");
        }
    }

    [JsonIgnore]
    public string LeftFileName => System.IO.Path.GetFileName(LeftPath);

    [JsonIgnore]
    public string RightFileName => System.IO.Path.GetFileName(RightPath);

    [JsonIgnore]
    public string DisplayTitle => string.IsNullOrEmpty(Title) 
        ? $"{LeftFileName} ↔ {RightFileName}" 
        : Title;
}
