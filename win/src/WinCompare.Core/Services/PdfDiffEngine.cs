using WinCompare.Core.Models;

namespace WinCompare.Core.Services;

public static class PdfDiffEngine
{
    public static PdfDiffResult Compare(PdfDocument left, PdfDocument right, bool ignoreWhitespace = false)
    {
        var blocks = new List<PdfPageDiffBlock>();
        int totalDiffs = 0;
        int maxPages = Math.Max(left.Pages.Count, right.Pages.Count);

        for (int i = 0; i < maxPages; i++)
        {
            var leftPage = i < left.Pages.Count ? left.Pages[i] : null;
            var rightPage = i < right.Pages.Count ? right.Pages[i] : null;

            if (leftPage != null && rightPage != null)
            {
                string lText = ignoreWhitespace ? Sanitize(leftPage.Text) : leftPage.Text;
                string rText = ignoreWhitespace ? Sanitize(rightPage.Text) : rightPage.Text;

                if (string.Equals(lText, rText, StringComparison.Ordinal))
                {
                    blocks.Add(new PdfPageDiffBlock
                    {
                        LeftPageIndex = leftPage.PageIndex,
                        RightPageIndex = rightPage.PageIndex,
                        ChangeType = PdfDiffChangeType.Unchanged,
                        LeftText = leftPage.Text,
                        RightText = rightPage.Text
                    });
                }
                else
                {
                    totalDiffs++;
                    var tokens = ComputeTokenDiff(leftPage.Text, rightPage.Text);
                    blocks.Add(new PdfPageDiffBlock
                    {
                        LeftPageIndex = leftPage.PageIndex,
                        RightPageIndex = rightPage.PageIndex,
                        ChangeType = PdfDiffChangeType.Modified,
                        LeftText = leftPage.Text,
                        RightText = rightPage.Text,
                        Tokens = tokens
                    });
                }
            }
            else if (leftPage != null)
            {
                totalDiffs++;
                blocks.Add(new PdfPageDiffBlock
                {
                    LeftPageIndex = leftPage.PageIndex,
                    RightPageIndex = null,
                    ChangeType = PdfDiffChangeType.Deleted,
                    LeftText = leftPage.Text,
                    RightText = null
                });
            }
            else if (rightPage != null)
            {
                totalDiffs++;
                blocks.Add(new PdfPageDiffBlock
                {
                    LeftPageIndex = null,
                    RightPageIndex = rightPage.PageIndex,
                    ChangeType = PdfDiffChangeType.Inserted,
                    LeftText = null,
                    RightText = rightPage.Text
                });
            }
        }

        return new PdfDiffResult { Blocks = blocks, TotalDiffCount = totalDiffs };
    }

    private static string Sanitize(string text)
    {
        return string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static List<PdfDiffToken> ComputeTokenDiff(string left, string right)
    {
        var lTokens = Tokenize(left);
        var rTokens = Tokenize(right);

        int m = lTokens.Count;
        int n = rTokens.Count;

        if (m == 0 && n == 0) return new List<PdfDiffToken>();
        if (m == 0) return MergeConsecutiveTokens(rTokens.Select(t => new PdfDiffToken { Text = t, ChangeType = PdfDiffChangeType.Inserted }).ToList());
        if (n == 0) return MergeConsecutiveTokens(lTokens.Select(t => new PdfDiffToken { Text = t, ChangeType = PdfDiffChangeType.Deleted }).ToList());

        int[,] dp = new int[m + 1, n + 1];
        for (int i = 0; i < m; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (string.Equals(lTokens[i], rTokens[j], StringComparison.Ordinal))
                {
                    dp[i + 1, j + 1] = dp[i, j] + 1;
                }
                else
                {
                    dp[i + 1, j + 1] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
                }
            }
        }

        var reversed = new List<PdfDiffToken>();
        int curI = m;
        int curJ = n;

        while (curI > 0 || curJ > 0)
        {
            if (curI > 0 && curJ > 0 && string.Equals(lTokens[curI - 1], rTokens[curJ - 1], StringComparison.Ordinal))
            {
                reversed.Add(new PdfDiffToken { Text = lTokens[curI - 1], ChangeType = PdfDiffChangeType.Unchanged });
                curI--;
                curJ--;
            }
            else if (curJ > 0 && (curI == 0 || dp[curI, curJ - 1] >= dp[curI - 1, curJ]))
            {
                reversed.Add(new PdfDiffToken { Text = rTokens[curJ - 1], ChangeType = PdfDiffChangeType.Inserted });
                curJ--;
            }
            else if (curI > 0 && (curJ == 0 || dp[curI, curJ - 1] < dp[curI - 1, curJ]))
            {
                reversed.Add(new PdfDiffToken { Text = lTokens[curI - 1], ChangeType = PdfDiffChangeType.Deleted });
                curI--;
            }
        }

        reversed.Reverse();
        return MergeConsecutiveTokens(reversed);
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var currentAscii = new System.Text.StringBuilder();

        foreach (char c in text)
        {
            if (c <= 127 && char.IsLetterOrDigit(c))
            {
                currentAscii.Append(c);
            }
            else
            {
                if (currentAscii.Length > 0)
                {
                    tokens.Add(currentAscii.ToString());
                    currentAscii.Clear();
                }
                tokens.Add(c.ToString());
            }
        }

        if (currentAscii.Length > 0)
        {
            tokens.Add(currentAscii.ToString());
        }

        return tokens;
    }

    private static List<PdfDiffToken> MergeConsecutiveTokens(List<PdfDiffToken> tokens)
    {
        if (tokens.Count == 0) return tokens;
        var merged = new List<PdfDiffToken>();
        var currentType = tokens[0].ChangeType;
        var sb = new System.Text.StringBuilder(tokens[0].Text);

        for (int i = 1; i < tokens.Count; i++)
        {
            if (tokens[i].ChangeType == currentType)
            {
                sb.Append(tokens[i].Text);
            }
            else
            {
                merged.Add(new PdfDiffToken { Text = sb.ToString(), ChangeType = currentType });
                currentType = tokens[i].ChangeType;
                sb.Clear();
                sb.Append(tokens[i].Text);
            }
        }

        merged.Add(new PdfDiffToken { Text = sb.ToString(), ChangeType = currentType });
        return merged;
    }
}
