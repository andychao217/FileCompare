using WinCompare.Core.Models;
using WinCompare.Core.Services;
using Xunit;

namespace WinCompare.Tests;

public class DiffEngineTests
{
    [Fact]
    public void TestAlignedLinesBuilding()
    {
        var diff = new DiffResult
        {
            Chunks = new List<DiffChunk>
            {
                new()
                {
                    ChangeTypeString = "Modified",
                    LeftLines = new List<string> { "Line 1 old", "Line 2 old" },
                    RightLines = new List<string> { "Line 1 new" }
                }
            }
        };

        var aligned = DiffEngineService.Instance.BuildAlignedLines(diff);

        Assert.Equal(2, aligned.Count);
        Assert.Equal("Line 1 old", aligned[0].LeftContent);
        Assert.Equal("Line 1 new", aligned[0].RightContent);
        Assert.Equal("Line 2 old", aligned[1].LeftContent);
        Assert.Empty(aligned[1].RightContent);
        Assert.Null(aligned[1].RightLineNumber);
    }

    [Fact]
    public void TestAISummaryFallback()
    {
        var result = AIService.Instance.SummarizeDiffAsync(
            "old code",
            "new code",
            "a.txt",
            "b.txt",
            AIExecutionMode.LocalOffline,
            null,
            AppLanguage.ZhHans
        ).GetAwaiter().GetResult();

        Assert.True(result.IsSuccess);
        Assert.Contains("比对完成", result.OneLineIntent);
        Assert.NotEmpty(result.ConventionalCommit);
    }
}
