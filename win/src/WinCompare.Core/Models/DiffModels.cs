using System.Text.Json.Serialization;

namespace WinCompare.Core.Models;

public enum DiffChangeType
{
    Unchanged,
    Added,
    Deleted,
    Modified
}

public sealed class DiffResult
{
    [JsonPropertyName("chunks")]
    public List<DiffChunk> Chunks { get; set; } = new();

    [JsonPropertyName("stats")]
    public DiffStats Stats { get; set; } = new();
}

public sealed class DiffChunk
{
    [JsonPropertyName("change_type")]
    public string ChangeTypeString { get; set; } = string.Empty;

    [JsonIgnore]
    public DiffChangeType ChangeType => ChangeTypeString switch
    {
        "Added" => DiffChangeType.Added,
        "Deleted" => DiffChangeType.Deleted,
        "Modified" => DiffChangeType.Modified,
        _ => DiffChangeType.Unchanged
    };

    [JsonPropertyName("left_lines")]
    public List<string> LeftLines { get; set; } = new();

    [JsonPropertyName("right_lines")]
    public List<string> RightLines { get; set; } = new();

    [JsonPropertyName("left_start_line")]
    public int LeftStartLine { get; set; }

    [JsonPropertyName("right_start_line")]
    public int RightStartLine { get; set; }
}

public sealed class DiffStats
{
    [JsonPropertyName("additions")]
    public int Additions { get; set; }

    [JsonPropertyName("deletions")]
    public int Deletions { get; set; }

    [JsonPropertyName("modifications")]
    public int Modifications { get; set; }

    [JsonPropertyName("total_lines_left")]
    public int TotalLinesLeft { get; set; }

    [JsonPropertyName("total_lines_right")]
    public int TotalLinesRight { get; set; }
}

/// <summary>
/// Side-by-side aligned line pair for UI rendering.
/// </summary>
public sealed class AlignedDiffLine
{
    public int? LeftLineNumber { get; set; }
    public string LeftContent { get; set; } = string.Empty;
    public DiffChangeType LeftType { get; set; } = DiffChangeType.Unchanged;

    public int? RightLineNumber { get; set; }
    public string RightContent { get; set; } = string.Empty;
    public DiffChangeType RightType { get; set; } = DiffChangeType.Unchanged;

    public bool IsDifference => LeftType != DiffChangeType.Unchanged || RightType != DiffChangeType.Unchanged;
}

public sealed class ThreeWayMergeResult
{
    [JsonPropertyName("merged_text")]
    public string MergedText { get; set; } = string.Empty;

    [JsonPropertyName("conflicts")]
    public List<MergeConflict> Conflicts { get; set; } = new();

    [JsonPropertyName("has_conflicts")]
    public bool HasConflicts => Conflicts.Count > 0;
}

public sealed class MergeConflict
{
    [JsonPropertyName("start_line")]
    public int StartLine { get; set; }

    [JsonPropertyName("local_content")]
    public string LocalContent { get; set; } = string.Empty;

    [JsonPropertyName("base_content")]
    public string BaseContent { get; set; } = string.Empty;

    [JsonPropertyName("remote_content")]
    public string RemoteContent { get; set; } = string.Empty;
}
