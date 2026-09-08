using System.Text.Json;
using WinCompare.Core.Models;
using WinCompare.Core.Native;

namespace WinCompare.Core.Services;

public sealed class DiffEngineService
{
    private static readonly Lazy<DiffEngineService> _instance = new(() => new DiffEngineService());
    public static DiffEngineService Instance => _instance.Value;

    public bool IsRustAvailable()
    {
        if (SettingsManager.Instance.EngineMode == DiffEngineMode.CSharpOnly)
        {
            return false;
        }

        try
        {
            return NativeMethods.maccompare_is_available();
        }
        catch
        {
            return false;
        }
    }

    public string GetVersion()
    {
        try
        {
            IntPtr ptr = NativeMethods.maccompare_version();
            var handle = new SafeRustStringHandle(ptr);
            return handle.ToManagedStringAndDispose();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public DiffResult CompareText(string left, string right, bool ignoreWhitespace = false, bool ignoreCase = false)
    {
        IntPtr jsonPtr = NativeMethods.maccompare_compare_text_json(left, right, ignoreWhitespace, ignoreCase);
        var handle = new SafeRustStringHandle(jsonPtr);
        string json = handle.ToManagedStringAndDispose();

        if (string.IsNullOrEmpty(json))
        {
            return new DiffResult();
        }

        return JsonSerializer.Deserialize<DiffResult>(json) ?? new DiffResult();
    }

    public ThreeWayMergeResult MergeThreeWay(string local, string @base, string remote)
    {
        IntPtr jsonPtr = NativeMethods.maccompare_merge_three_way_json(local, @base, remote);
        var handle = new SafeRustStringHandle(jsonPtr);
        string json = handle.ToManagedStringAndDispose();

        if (string.IsNullOrEmpty(json))
        {
            return new ThreeWayMergeResult();
        }

        return JsonSerializer.Deserialize<ThreeWayMergeResult>(json) ?? new ThreeWayMergeResult();
    }

    public List<FolderDiffEntry> CompareFolders(string leftPath, string rightPath, FolderDiffMode mode = FolderDiffMode.Quick, List<string>? excludePatterns = null)
    {
        try
        {
            if (IsRustAvailable())
            {
                string? excludeJson = excludePatterns != null ? JsonSerializer.Serialize(excludePatterns) : null;
                IntPtr jsonPtr = NativeMethods.maccompare_compare_folders_json(leftPath, rightPath, (int)mode, excludeJson);
                var handle = new SafeRustStringHandle(jsonPtr);
                string json = handle.ToManagedStringAndDispose();

                if (!string.IsNullOrEmpty(json))
                {
                    var entries = JsonSerializer.Deserialize<List<FolderDiffEntry>>(json);
                    if (entries != null && entries.Count > 0)
                    {
                        return entries;
                    }
                }
            }
        }
        catch
        {
            // Fallback to pure C#
        }

        return CompareFoldersCSharp(leftPath, rightPath, mode, excludePatterns);
    }

    private static List<FolderDiffEntry> CompareFoldersCSharp(string leftPath, string rightPath, FolderDiffMode mode, List<string>? excludePatterns)
    {
        var result = new List<FolderDiffEntry>();
        if (!Directory.Exists(leftPath) && !Directory.Exists(rightPath)) return result;

        var leftFiles = Directory.Exists(leftPath)
            ? Directory.GetFiles(leftPath, "*", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(leftPath, p).Replace('\\', '/'))
                .ToHashSet()
            : new HashSet<string>();

        var rightFiles = Directory.Exists(rightPath)
            ? Directory.GetFiles(rightPath, "*", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(rightPath, p).Replace('\\', '/'))
                .ToHashSet()
            : new HashSet<string>();

        var allRel = new SortedSet<string>(leftFiles.Union(rightFiles));

        foreach (var rel in allRel)
        {
            if (SettingsManager.Instance.IsPathExcluded(rel))
            {
                continue;
            }
            if (excludePatterns != null && excludePatterns.Any(p => rel.Contains(p.Trim('*', '/'))))
            {
                continue;
            }

            string leftFull = Path.Combine(leftPath, rel);
            string rightFull = Path.Combine(rightPath, rel);

            bool hasLeft = File.Exists(leftFull);
            bool hasRight = File.Exists(rightFull);

            var entry = new FolderDiffEntry
            {
                RelativePath = rel,
                IsDirectory = false
            };

            if (hasLeft)
            {
                var fi = new FileInfo(leftFull);
                entry.LeftSize = fi.Length;
                entry.LeftModified = new DateTimeOffset(fi.LastWriteTimeUtc).ToUnixTimeSeconds();
            }

            if (hasRight)
            {
                var fi = new FileInfo(rightFull);
                entry.RightSize = fi.Length;
                entry.RightModified = new DateTimeOffset(fi.LastWriteTimeUtc).ToUnixTimeSeconds();
            }

            if (hasLeft && !hasRight)
            {
                entry.StatusString = "Deleted";
            }
            else if (!hasLeft && hasRight)
            {
                entry.StatusString = "Added";
            }
            else
            {
                if (entry.LeftSize != entry.RightSize)
                {
                    entry.StatusString = "Modified";
                }
                else if (mode == FolderDiffMode.Quick)
                {
                    entry.StatusString = (entry.LeftModified != entry.RightModified) ? "Modified" : "Identical";
                }
                else
                {
                    bool identical = true;
                    try
                    {
                        byte[] b1 = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(leftFull));
                        byte[] b2 = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(rightFull));
                        identical = b1.AsSpan().SequenceEqual(b2);
                    }
                    catch
                    {
                        identical = false;
                    }
                    entry.StatusString = identical ? "Identical" : "Modified";
                }
            }

            result.Add(entry);
        }

        return result;
    }

    /// <summary>
    /// Align diff chunks into side-by-side line pairs for dual-pane synchronized scrolling UI.
    /// </summary>
    public List<AlignedDiffLine> BuildAlignedLines(DiffResult diffResult)
    {
        var aligned = new List<AlignedDiffLine>();
        int leftLineNum = 1;
        int rightLineNum = 1;

        foreach (var chunk in diffResult.Chunks)
        {
            int maxCount = Math.Max(chunk.LeftLines.Count, chunk.RightLines.Count);

            for (int i = 0; i < maxCount; i++)
            {
                bool hasLeft = i < chunk.LeftLines.Count;
                bool hasRight = i < chunk.RightLines.Count;

                var line = new AlignedDiffLine
                {
                    LeftLineNumber = hasLeft ? leftLineNum++ : null,
                    LeftContent = hasLeft ? chunk.LeftLines[i] : string.Empty,
                    LeftType = hasLeft ? chunk.ChangeType : DiffChangeType.Unchanged,

                    RightLineNumber = hasRight ? rightLineNum++ : null,
                    RightContent = hasRight ? chunk.RightLines[i] : string.Empty,
                    RightType = hasRight ? chunk.ChangeType : DiffChangeType.Unchanged
                };

                aligned.Add(line);
            }
        }

        return aligned;
    }
}
