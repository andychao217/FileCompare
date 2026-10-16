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
    private enum CSharpBaseActionType { Kept, Modified, Deleted }
    private struct CSharpBaseAction
    {
        public CSharpBaseActionType Type;
        public int BranchIndex;
    }

    private static (List<int>[] insertions, CSharpBaseAction[] actions) AlignBranchToBase(string[] baseLines, string[] branchLines)
    {
        int n = baseLines.Length;
        int m = branchLines.Length;

        int[,] dp = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
        {
            for (int j = m - 1; j >= 0; j--)
            {
                if (baseLines[i] == branchLines[j])
                    dp[i, j] = 1 + dp[i + 1, j + 1];
                else
                    dp[i, j] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
            }
        }

        var insertions = new List<int>[n + 1];
        for (int i = 0; i <= n; i++) insertions[i] = new List<int>();

        var actions = new CSharpBaseAction[n];
        for (int i = 0; i < n; i++) actions[i] = new CSharpBaseAction { Type = CSharpBaseActionType.Deleted, BranchIndex = -1 };

        int bi = 0;
        int bj = 0;

        while (bi < n || bj < m)
        {
            if (bi < n && bj < m && baseLines[bi] == branchLines[bj])
            {
                actions[bi] = new CSharpBaseAction { Type = CSharpBaseActionType.Kept, BranchIndex = bj };
                bi++;
                bj++;
            }
            else
            {
                int delCount = 0;
                int insCount = 0;
                int ti = bi;
                int tj = bj;

                while (ti < n || tj < m)
                {
                    if (ti < n && tj < m && baseLines[ti] == branchLines[tj])
                        break;
                    if (tj < m && (ti >= n || dp[ti, tj + 1] >= dp[ti + 1, tj]))
                    {
                        insCount++;
                        tj++;
                    }
                    else if (ti < n)
                    {
                        delCount++;
                        ti++;
                    }
                }

                int common = Math.Min(delCount, insCount);
                for (int k = 0; k < common; k++)
                {
                    actions[bi + k] = new CSharpBaseAction { Type = CSharpBaseActionType.Modified, BranchIndex = bj + k };
                }
                for (int k = common; k < delCount; k++)
                {
                    actions[bi + k] = new CSharpBaseAction { Type = CSharpBaseActionType.Deleted, BranchIndex = -1 };
                }
                for (int k = common; k < insCount; k++)
                {
                    insertions[bi + common].Add(bj + k);
                }

                bi = ti;
                bj = tj;
            }
        }

        return (insertions, actions);
    }

    private static string[] SplitLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<string>();
        }
        return text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    }

    private static ThreeWayMergeResult MergeThreeWayCSharp(string local, string @base, string remote)
    {
        var result = new ThreeWayMergeResult();
        string[] localLines = SplitLines(local);
        string[] baseLines = SplitLines(@base);
        string[] remoteLines = SplitLines(remote);

        var (locIns, locActs) = AlignBranchToBase(baseLines, localLines);
        var (remIns, remActs) = AlignBranchToBase(baseLines, remoteLines);

        var mergedLines = new List<string>();
        int n = baseLines.Length;

        for (int b = 0; b <= n; b++)
        {
            var lIns = locIns[b];
            var rIns = remIns[b];
            int maxIns = Math.Max(lIns.Count, rIns.Count);

            for (int k = 0; k < maxIns; k++)
            {
                bool hasLoc = k < lIns.Count;
                bool hasRem = k < rIns.Count;
                int? lIdx = hasLoc ? lIns[k] : null;
                int? rIdx = hasRem ? rIns[k] : null;

                string locLine = lIdx.HasValue ? localLines[lIdx.Value] : string.Empty;
                string remLine = rIdx.HasValue ? remoteLines[rIdx.Value] : string.Empty;

                string status;
                string? resolved = null;
                int? conflictIdx = null;

                if (hasLoc && hasRem)
                {
                    if (locLine == remLine)
                    {
                        status = "CleanLocal";
                        result.AutoResolvedCount++;
                        resolved = locLine;
                    }
                    else
                    {
                        status = "Conflict";
                        conflictIdx = result.ConflictCount;
                        result.ConflictCount++;
                        resolved = $"<<<<<<< LOCAL{Environment.NewLine}{locLine}{Environment.NewLine}======={Environment.NewLine}{remLine}{Environment.NewLine}>>>>>>> REMOTE";
                        result.Conflicts.Add(new MergeConflict
                        {
                            StartLine = (lIdx ?? rIdx ?? 0) + 1,
                            LocalContent = locLine,
                            BaseContent = string.Empty,
                            RemoteContent = remLine
                        });
                    }
                }
                else if (hasLoc)
                {
                    status = "CleanLocal";
                    result.AutoResolvedCount++;
                    resolved = locLine;
                }
                else
                {
                    status = "CleanRemote";
                    result.AutoResolvedCount++;
                    resolved = remLine;
                }

                if (resolved != null)
                {
                    mergedLines.Add(resolved);
                }

                result.Lines.Add(new MergeLine
                {
                    LocalLineNumber = lIdx.HasValue ? lIdx.Value + 1 : null,
                    BaseLineNumber = null,
                    RemoteLineNumber = rIdx.HasValue ? rIdx.Value + 1 : null,
                    ContentLocal = locLine,
                    ContentBase = string.Empty,
                    ContentRemote = remLine,
                    Status = status,
                    ResolvedContent = resolved,
                    ConflictIndex = conflictIdx
                });
            }

            if (b < n)
            {
                string basLine = baseLines[b];
                var locAct = locActs[b];
                var remAct = remActs[b];

                int? lIdx = locAct.Type != CSharpBaseActionType.Deleted ? locAct.BranchIndex : null;
                string locLine = lIdx.HasValue ? localLines[lIdx.Value] : string.Empty;

                int? rIdx = remAct.Type != CSharpBaseActionType.Deleted ? remAct.BranchIndex : null;
                string remLine = rIdx.HasValue ? remoteLines[rIdx.Value] : string.Empty;

                string status;
                string? resolved = null;
                int? conflictIdx = null;

                switch (locAct.Type, remAct.Type)
                {
                    case (CSharpBaseActionType.Kept, CSharpBaseActionType.Kept):
                        status = "Unchanged";
                        resolved = basLine;
                        break;
                    case (CSharpBaseActionType.Modified, CSharpBaseActionType.Kept):
                        status = "CleanLocal";
                        result.AutoResolvedCount++;
                        resolved = locLine;
                        break;
                    case (CSharpBaseActionType.Kept, CSharpBaseActionType.Modified):
                        status = "CleanRemote";
                        result.AutoResolvedCount++;
                        resolved = remLine;
                        break;
                    case (CSharpBaseActionType.Deleted, CSharpBaseActionType.Kept):
                        status = "CleanLocal";
                        result.AutoResolvedCount++;
                        resolved = null;
                        break;
                    case (CSharpBaseActionType.Kept, CSharpBaseActionType.Deleted):
                        status = "CleanRemote";
                        result.AutoResolvedCount++;
                        resolved = null;
                        break;
                    case (CSharpBaseActionType.Deleted, CSharpBaseActionType.Deleted):
                        status = "CleanLocal";
                        result.AutoResolvedCount++;
                        resolved = null;
                        break;
                    case (CSharpBaseActionType.Modified, CSharpBaseActionType.Modified):
                        if (locLine == remLine)
                        {
                            status = "CleanLocal";
                            result.AutoResolvedCount++;
                            resolved = locLine;
                        }
                        else
                        {
                            status = "Conflict";
                            conflictIdx = result.ConflictCount;
                            result.ConflictCount++;
                            resolved = $"<<<<<<< LOCAL{Environment.NewLine}{locLine}{Environment.NewLine}======={Environment.NewLine}{remLine}{Environment.NewLine}>>>>>>> REMOTE";
                            result.Conflicts.Add(new MergeConflict
                            {
                                StartLine = b + 1,
                                LocalContent = locLine,
                                BaseContent = basLine,
                                RemoteContent = remLine
                            });
                        }
                        break;
                    case (CSharpBaseActionType.Modified, CSharpBaseActionType.Deleted):
                        status = "Conflict";
                        conflictIdx = result.ConflictCount;
                        result.ConflictCount++;
                        resolved = $"<<<<<<< LOCAL{Environment.NewLine}{locLine}{Environment.NewLine}======={Environment.NewLine}{Environment.NewLine}>>>>>>> REMOTE";
                        result.Conflicts.Add(new MergeConflict
                        {
                            StartLine = b + 1,
                            LocalContent = locLine,
                            BaseContent = basLine,
                            RemoteContent = string.Empty
                        });
                        break;
                    case (CSharpBaseActionType.Deleted, CSharpBaseActionType.Modified):
                        status = "Conflict";
                        conflictIdx = result.ConflictCount;
                        result.ConflictCount++;
                        resolved = $"<<<<<<< LOCAL{Environment.NewLine}{Environment.NewLine}======={Environment.NewLine}{remLine}{Environment.NewLine}>>>>>>> REMOTE";
                        result.Conflicts.Add(new MergeConflict
                        {
                            StartLine = b + 1,
                            LocalContent = string.Empty,
                            BaseContent = basLine,
                            RemoteContent = remLine
                        });
                        break;
                    default:
                        status = "Unchanged";
                        resolved = basLine;
                        break;
                }

                if (resolved != null)
                {
                    mergedLines.Add(resolved);
                }

                result.Lines.Add(new MergeLine
                {
                    LocalLineNumber = lIdx.HasValue ? lIdx.Value + 1 : null,
                    BaseLineNumber = b + 1,
                    RemoteLineNumber = rIdx.HasValue ? rIdx.Value + 1 : null,
                    ContentLocal = locLine,
                    ContentBase = basLine,
                    ContentRemote = remLine,
                    Status = status,
                    ResolvedContent = resolved,
                    ConflictIndex = conflictIdx
                });
            }
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
