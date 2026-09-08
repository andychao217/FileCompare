namespace WinCompare.Core.Models;

public enum AppLanguage
{
    Auto,
    ZhHans,
    En,
    Ja
}

public static class AppLanguageExtensions
{
    public static string ToIsoCode(this AppLanguage lang) => lang switch
    {
        AppLanguage.ZhHans => "zh-CN",
        AppLanguage.Ja => "ja",
        AppLanguage.En => "en",
        _ => "zh-CN"
    };

    public static AppLanguage FromIsoCode(string code) => code?.ToLowerInvariant() switch
    {
        "zh" or "zh-cn" or "zh-hans" => AppLanguage.ZhHans,
        "ja" or "ja-jp" => AppLanguage.Ja,
        "en" or "en-us" => AppLanguage.En,
        _ => AppLanguage.ZhHans
    };
}
