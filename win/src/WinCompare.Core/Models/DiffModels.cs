using System.Text.Json.Serialization;

namespace WinCompare.Core.Models;

public enum DiffChangeType
{
    Unchanged,
    Added,
    Deleted,
    Modified
}

public sealed class DiffToken
{
    [JsonPropertyName("start_offset")]
    public uint StartOffset { get; set; }

    [JsonPropertyName("length")]
    public uint Length { get; set; }

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
}

public sealed class DiffHunk
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("start_line_index")]
    public int StartLineIndex { get; set; }

    [JsonPropertyName("line_count")]
    public int LineCount { get; set; }

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
}

public sealed class DiffLine
{
    [JsonPropertyName("left_line_number")]
    public int? LeftLineNumber { get; set; }

    [JsonPropertyName("right_line_number")]
    public int? RightLineNumber { get; set; }

    [JsonPropertyName("content_left")]
    public string ContentLeft { get; set; } = string.Empty;

    [JsonPropertyName("content_right")]
    public string ContentRight { get; set; } = string.Empty;

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

    [JsonPropertyName("tokens_left")]
    public List<DiffToken> TokensLeft { get; set; } = new();

    [JsonPropertyName("tokens_right")]
    public List<DiffToken> TokensRight { get; set; } = new();

    [JsonPropertyName("hunk_index")]
    public int? HunkIndex { get; set; }
}

public sealed class DiffResult
{
    [JsonPropertyName("lines")]
    public List<DiffLine> Lines { get; set; } = new();

    [JsonPropertyName("total_additions")]
    public int TotalAdditions { get; set; }

    [JsonPropertyName("total_deletions")]
    public int TotalDeletions { get; set; }

    [JsonPropertyName("total_modifications")]
    public int TotalModifications { get; set; }

    [JsonPropertyName("hunks")]
    public List<DiffHunk> Hunks { get; set; } = new();

    [JsonIgnore]
    public DiffStats Stats => new DiffStats
    {
        Additions = TotalAdditions,
        Deletions = TotalDeletions,
        Modifications = TotalModifications
    };

    [JsonPropertyName("chunks")]
    public List<DiffChunk> Chunks { get; set; } = new();
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

public sealed class MergeLine
{
    [JsonPropertyName("local_line_number")]
    public int? LocalLineNumber { get; set; }

    [JsonPropertyName("base_line_number")]
    public int? BaseLineNumber { get; set; }

    [JsonPropertyName("remote_line_number")]
    public int? RemoteLineNumber { get; set; }

    [JsonPropertyName("content_local")]
    public string ContentLocal { get; set; } = string.Empty;

    [JsonPropertyName("content_base")]
    public string ContentBase { get; set; } = string.Empty;

    [JsonPropertyName("content_remote")]
    public string ContentRemote { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // CleanLocal, CleanRemote, Conflict, Unchanged

    [JsonPropertyName("resolved_content")]
    public string? ResolvedContent { get; set; }

    [JsonPropertyName("conflict_index")]
    public int? ConflictIndex { get; set; }
}

public sealed class ThreeWayMergeResult
{
    [JsonPropertyName("merged_text")]
    public string MergedText { get; set; } = string.Empty;

    [JsonPropertyName("conflict_count")]
    public int ConflictCount { get; set; }

    [JsonPropertyName("auto_resolved_count")]
    public int AutoResolvedCount { get; set; }

    [JsonPropertyName("lines")]
    public List<MergeLine> Lines { get; set; } = new();

    [JsonPropertyName("conflicts")]
    public List<MergeConflict> Conflicts { get; set; } = new();

    [JsonIgnore]
    public bool HasConflicts => ConflictCount > 0 || Conflicts.Count > 0;
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
