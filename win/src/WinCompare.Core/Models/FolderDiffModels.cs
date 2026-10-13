using System.Text.Json.Serialization;

namespace WinCompare.Core.Models;

public enum FolderDiffMode
{
    Quick = 0,
    DeepHash = 1,
    FullByte = 2
}

public enum FolderEntryStatus
{
    Identical,
    Modified,
    Added,
    Deleted
}

public sealed class FolderDiffEntry
{
    [JsonPropertyName("relative_path")]
    public string RelativePath { get; set; } = string.Empty;

    [JsonPropertyName("is_directory")]
    public bool IsDirectory { get; set; }

    [JsonPropertyName("status")]
    public string StatusString { get; set; } = string.Empty;

    [JsonIgnore]
    public FolderEntryStatus Status => StatusString switch
    {
        "RightOnly" or "Added" => FolderEntryStatus.Added,
        "LeftOnly" or "Deleted" => FolderEntryStatus.Deleted,
        "ContentDifferent" or "MetadataDifferent" or "Modified" => FolderEntryStatus.Modified,
        _ => FolderEntryStatus.Identical
    };

    [JsonIgnore]
    public string StatusText => Status switch
    {
        FolderEntryStatus.Added => WinCompare.Core.Services.LanguageManager.Instance.Text("statusAdded"),
        FolderEntryStatus.Deleted => WinCompare.Core.Services.LanguageManager.Instance.Text("statusDeleted"),
        FolderEntryStatus.Modified => WinCompare.Core.Services.LanguageManager.Instance.Text("statusModified"),
        _ => WinCompare.Core.Services.LanguageManager.Instance.Text("statusIdentical")
    };

    [JsonPropertyName("left_size")]
    public long? LeftSize { get; set; }

    [JsonPropertyName("right_size")]
    public long? RightSize { get; set; }

    [JsonPropertyName("left_modified_timestamp")]
    public long? LeftModifiedTimestamp { get; set; }

    [JsonPropertyName("right_modified_timestamp")]
    public long? RightModifiedTimestamp { get; set; }

    [JsonPropertyName("left_modified")]
    public long? LeftModified
    {
        get => LeftModifiedTimestamp;
        set => LeftModifiedTimestamp ??= value;
    }

    [JsonPropertyName("right_modified")]
    public long? RightModified
    {
        get => RightModifiedTimestamp;
        set => RightModifiedTimestamp ??= value;
    }

    [JsonPropertyName("left_hash")]
    public string? LeftHash { get; set; }

    [JsonPropertyName("right_hash")]
    public string? RightHash { get; set; }
}
