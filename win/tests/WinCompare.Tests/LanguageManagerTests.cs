using WinCompare.Core.Models;
using WinCompare.Core.Services;
using Xunit;

namespace WinCompare.Tests;

public class LanguageManagerTests
{
    [Fact]
    public void TestLanguageSwitching()
    {
        var lm = LanguageManager.Instance;

        lm.SetLanguage(AppLanguage.ZhHans);
        Assert.Equal("文本对比", lm.Text("textDiffTitle"));

        lm.SetLanguage(AppLanguage.En);
        Assert.Equal("Text Diff", lm.Text("textDiffTitle"));

        lm.SetLanguage(AppLanguage.Ja);
        Assert.Equal("テキスト比較", lm.Text("textDiffTitle"));
    }
}
