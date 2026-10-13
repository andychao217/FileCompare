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
        if (left == null) left = string.Empty;
        if (right == null) right = string.Empty;

        if (left.Contains('\r')) left = left.Replace("\r\n", "\n").Replace('\r', '\n');
        if (right.Contains('\r')) right = right.Replace("\r\n", "\n").Replace('\r', '\n');

        if (IsRustAvailable())
        {
            try
            {
                IntPtr jsonPtr = NativeMethods.maccompare_compare_text_json(left, right, ignoreWhitespace, ignoreCase);
                var handle = new SafeRustStringHandle(jsonPtr);
                string json = handle.ToManagedStringAndDispose();

                if (!string.IsNullOrEmpty(json))
                {
                    var result = JsonSerializer.Deserialize<DiffResult>(json);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DiffEngineService] Rust text diff failed, falling back to C#: {ex.Message}");
                return CompareTextCSharp(left, right, ignoreWhitespace, ignoreCase);
            }
        }

        return CompareTextCSharp(left, right, ignoreWhitespace, ignoreCase);
    }

    public ThreeWayMergeResult MergeThreeWay(string local, string @base, string remote)
    {
        if (local == null) local = string.Empty;
        if (@base == null) @base = string.Empty;
        if (remote == null) remote = string.Empty;

        if (local.Contains('\r')) local = local.Replace("\r\n", "\n").Replace('\r', '\n');
        if (@base.Contains('\r')) @base = @base.Replace("\r\n", "\n").Replace('\r', '\n');
        if (remote.Contains('\r')) remote = remote.Replace("\r\n", "\n").Replace('\r', '\n');
        if (IsRustAvailable())
        {
            try
            {
                IntPtr jsonPtr = NativeMethods.maccompare_merge_three_way_json(local, @base, remote);
                var handle = new SafeRustStringHandle(jsonPtr);
                string json = handle.ToManagedStringAndDispose();

                if (!string.IsNullOrEmpty(json))
                {
                    var result = JsonSerializer.Deserialize<ThreeWayMergeResult>(json);
                    if (result != null)
                    {
                        if (result.ConflictCount > 0 && result.Conflicts.Count == 0)
                        {
                            foreach (var line in result.Lines.Where(l => l.Status == "Conflict"))
                            {
                                result.Conflicts.Add(new MergeConflict
                                {
                                    StartLine = line.BaseLineNumber ?? line.LocalLineNumber ?? line.RemoteLineNumber ?? 0,
                                    LocalContent = line.ContentLocal,
                                    BaseContent = line.ContentBase,
                                    RemoteContent = line.ContentRemote
                                });
                            }
                        }
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DiffEngineService] Rust 3-way merge failed, falling back to C#: {ex.Message}");
                return MergeThreeWayCSharp(local, @base, remote);
            }
        }

        return MergeThreeWayCSharp(local, @base, remote);
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiffEngineService] Rust folder diff failed, falling back to C#: {ex.Message}");
            return CompareFoldersCSharp(leftPath, rightPath, mode, excludePatterns);
        }

        return CompareFoldersCSharp(leftPath, rightPath, mode, excludePatterns);
    }

    private static List<FolderDiffEntry> CompareFoldersCSharp(string leftPath, string rightPath, FolderDiffMode mode, List<string>? excludePatterns)
    {
        var result = new List<FolderDiffEntry>();
        if (!Directory.Exists(leftPath) && !Directory.Exists(rightPath)) return result;

        var enumOptions = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = System.IO.FileAttributes.ReparsePoint
        };

        var leftFiles = Directory.Exists(leftPath)
            ? Directory.EnumerateFiles(leftPath, "*", enumOptions)
                .Select(p => Path.GetRelativePath(leftPath, p).Replace('\\', '/'))
                .ToHashSet()
            : new HashSet<string>();

        var rightFiles = Directory.Exists(rightPath)
            ? Directory.EnumerateFiles(rightPath, "*", enumOptions)
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
                try
                {
                    var fi = new FileInfo(leftFull);
                    entry.LeftSize = fi.Length;
                    entry.LeftModified = new DateTimeOffset(fi.LastWriteTimeUtc).ToUnixTimeSeconds();
                }
                catch
                {
                    // Ignore inaccessible metadata
                }
            }

            if (hasRight)
            {
                try
                {
                    var fi = new FileInfo(rightFull);
                    entry.RightSize = fi.Length;
                    entry.RightModified = new DateTimeOffset(fi.LastWriteTimeUtc).ToUnixTimeSeconds();
                }
                catch
                {
                    // Ignore inaccessible metadata
                }
            }

            if (hasLeft && !hasRight)
            {
                entry.StatusString = "LeftOnly";
            }
            else if (!hasLeft && hasRight)
            {
                entry.StatusString = "RightOnly";
            }
            else
            {
                if (entry.LeftSize != entry.RightSize)
                {
                    entry.StatusString = "ContentDifferent";
                }
                else if (mode == FolderDiffMode.Quick)
                {
                    entry.StatusString = (entry.LeftModified != entry.RightModified) ? "ContentDifferent" : "Equal";
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
                    entry.StatusString = identical ? "Equal" : "ContentDifferent";
                }
            }

            result.Add(entry);
        }

        return result;
    }

    /// <summary>
    /// Pure C# Myers diff fallback when Rust core is disabled or unavailable.
    /// </summary>
    private static DiffResult CompareTextCSharp(string left, string right, bool ignoreWhitespace, bool ignoreCase)
    {
        var result = new DiffResult();
        string[] leftLines = left.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        string[] rightLines = right.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        int max = Math.Max(leftLines.Length, rightLines.Length);
        int leftLineNum = 1;
        int rightLineNum = 1;

        for (int i = 0; i < max; i++)
        {
            bool hasLeft = i < leftLines.Length;
            bool hasRight = i < rightLines.Length;

            string lStr = hasLeft ? leftLines[i] : string.Empty;
            string rStr = hasRight ? rightLines[i] : string.Empty;

            string compL = ignoreWhitespace ? System.Text.RegularExpressions.Regex.Replace(lStr, @"\s+", "") : lStr;
            string compR = ignoreWhitespace ? System.Text.RegularExpressions.Regex.Replace(rStr, @"\s+", "") : rStr;
            var compType = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            var diffLine = new DiffLine
            {
                LeftLineNumber = hasLeft ? leftLineNum++ : null,
                RightLineNumber = hasRight ? rightLineNum++ : null,
                ContentLeft = lStr,
                ContentRight = rStr
            };

            if (hasLeft && hasRight)
            {
                if (string.Equals(compL, compR, compType))
                {
                    diffLine.ChangeTypeString = "Unchanged";
                }
                else
                {
                    diffLine.ChangeTypeString = "Modified";
                    result.TotalModifications++;
                }
            }
            else if (hasLeft)
            {
                diffLine.ChangeTypeString = "Deleted";
                result.TotalDeletions++;
            }
            else
            {
                diffLine.ChangeTypeString = "Added";
                result.TotalAdditions++;
            }

            result.Lines.Add(diffLine);
        }

        return result;
    }

    /// <summary>
    /// Pure C# 3-Way merge fallback when Rust core is disabled or unavailable.
    /// </summary>
    private static ThreeWayMergeResult MergeThreeWayCSharp(string local, string @base, string remote)
    {
        var result = new ThreeWayMergeResult();
        string[] localLines = local.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        string[] baseLines = @base.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        string[] remoteLines = remote.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        var mergedLines = new List<string>();
        int max = Math.Max(baseLines.Length, Math.Max(localLines.Length, remoteLines.Length));

        for (int i = 0; i < max; i++)
        {
            string b = i < baseLines.Length ? baseLines[i] : "";
            string l = i < localLines.Length ? localLines[i] : "";
            string r = i < remoteLines.Length ? remoteLines[i] : "";

            var mergeLine = new MergeLine
            {
                LocalLineNumber = i < localLines.Length ? i + 1 : null,
                BaseLineNumber = i < baseLines.Length ? i + 1 : null,
                RemoteLineNumber = i < remoteLines.Length ? i + 1 : null,
                ContentLocal = l,
                ContentBase = b,
                ContentRemote = r
            };

            if (l == r)
            {
                mergeLine.Status = "Unchanged";
                mergeLine.ResolvedContent = l;
                mergedLines.Add(l);
            }
            else if (l == b)
            {
                mergeLine.Status = "CleanRemote";
                mergeLine.ResolvedContent = r;
                mergedLines.Add(r);
                result.AutoResolvedCount++;
            }
            else if (r == b)
            {
                mergeLine.Status = "CleanLocal";
                mergeLine.ResolvedContent = l;
                mergedLines.Add(l);
                result.AutoResolvedCount++;
            }
            else
            {
                mergeLine.Status = "Conflict";
                result.ConflictCount++;
                mergedLines.Add("<<<<<<< LOCAL");
                mergedLines.Add(l);
                mergedLines.Add("=======");
                mergedLines.Add(r);
                mergedLines.Add(">>>>>>> REMOTE");

                result.Conflicts.Add(new MergeConflict
                {
                    StartLine = i + 1,
                    LocalContent = l,
                    BaseContent = b,
                    RemoteContent = r
                });
            }

            result.Lines.Add(mergeLine);
        }

        result.MergedText = string.Join(Environment.NewLine, mergedLines);
        return result;
    }

    /// <summary>
    /// Align diff lines into side-by-side line pairs for dual-pane synchronized scrolling UI.
    /// </summary>
    public List<AlignedDiffLine> BuildAlignedLines(DiffResult diffResult)
    {
        if (diffResult.Lines.Count > 0)
        {
            var result = new List<AlignedDiffLine>(diffResult.Lines.Count);
            foreach (var line in diffResult.Lines)
            {
                var changeType = line.ChangeType;
                result.Add(new AlignedDiffLine
                {
                    LeftLineNumber = line.LeftLineNumber,
                    LeftContent = line.ContentLeft,
                    LeftType = line.LeftLineNumber == null ? DiffChangeType.Unchanged : (changeType == DiffChangeType.Deleted || changeType == DiffChangeType.Modified ? changeType : DiffChangeType.Unchanged),
                    RightLineNumber = line.RightLineNumber,
                    RightContent = line.ContentRight,
                    RightType = line.RightLineNumber == null ? DiffChangeType.Unchanged : (changeType == DiffChangeType.Added || changeType == DiffChangeType.Modified ? changeType : DiffChangeType.Unchanged)
                });
            }
            return result;
        }

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
