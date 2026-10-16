namespace WinCompare.Core.Models;

public enum AIExecutionMode
{
    LocalOffline = 0,
    CloudAPI = 1
}

public enum AiMode
{
    LocalSLM = 0,
    CloudLLM = 1
}

public enum AIModelId
{
    Gemma3_1B = 0,
    Gemma3_4B = 1
}

public static class AIModelExtensions
{
    public static string GetIdString(this AIModelId model) => model switch
    {
        AIModelId.Gemma3_1B => "gemma-3-1b-it-q4_k_m",
        AIModelId.Gemma3_4B => "gemma-3-4b-it-q4_k_m",
        _ => "gemma-3-1b-it-q4_k_m"
    };

    public static string GetShortName(this AIModelId model) => model switch
    {
        AIModelId.Gemma3_1B => "Gemma 3 1B",
        AIModelId.Gemma3_4B => "Gemma 3 4B",
        _ => "Gemma 3"
    };

    public static string GetDisplayName(this AIModelId model) => model switch
    {
        AIModelId.Gemma3_1B => "Google Gemma 3 1B",
        AIModelId.Gemma3_4B => "Google Gemma 3 4B",
        _ => "Google Gemma 3"
    };

    public static string GetFilename(this AIModelId model) => $"{model.GetIdString()}.gguf";

    public static int GetEstimatedSizeMB(this AIModelId model) => model switch
    {
        AIModelId.Gemma3_1B => 780,
        AIModelId.Gemma3_4B => 2560,
        _ => 780
    };

    public static string GetPrimaryDownloadUrl(this AIModelId model) => model switch
    {
        AIModelId.Gemma3_1B => "https://modelscope.cn/models/lmstudio-community/gemma-3-1b-it-GGUF/resolve/master/gemma-3-1b-it-Q4_K_M.gguf",
        AIModelId.Gemma3_4B => "https://modelscope.cn/models/lmstudio-community/gemma-3-4b-it-GGUF/resolve/master/gemma-3-4b-it-Q4_K_M.gguf",
        _ => "https://modelscope.cn/models/lmstudio-community/gemma-3-1b-it-GGUF/resolve/master/gemma-3-1b-it-Q4_K_M.gguf"
    };

    public static string GetFallbackDownloadUrl(this AIModelId model) => model switch
    {
        AIModelId.Gemma3_1B => "https://huggingface.co/lmstudio-community/gemma-3-1b-it-GGUF/resolve/main/gemma-3-1b-it-Q4_K_M.gguf",
        AIModelId.Gemma3_4B => "https://huggingface.co/lmstudio-community/gemma-3-4b-it-GGUF/resolve/main/gemma-3-4b-it-Q4_K_M.gguf",
        _ => "https://huggingface.co/lmstudio-community/gemma-3-1b-it-GGUF/resolve/main/gemma-3-1b-it-Q4_K_M.gguf"
    };
}

public enum AIModelDownloadStatus
{
    NotDownloaded = 0,
    Downloading = 1,
    Ready = 2,
    Error = 3
}

public sealed class AIModelDownloadState
{
    public AIModelDownloadStatus Status { get; set; } = AIModelDownloadStatus.NotDownloaded;
    public double Progress { get; set; } = 0.0;
    public string SpeedText { get; set; } = string.Empty;
    public string RemainingText { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }

    public bool IsDownloading => Status == AIModelDownloadStatus.Downloading;
    public bool IsReady => Status == AIModelDownloadStatus.Ready;

    public static AIModelDownloadState NotDownloaded => new() { Status = AIModelDownloadStatus.NotDownloaded };
    public static AIModelDownloadState Ready => new() { Status = AIModelDownloadStatus.Ready, Progress = 1.0 };
}

public sealed class CloudAPIConfig
{
    public string Endpoint { get; set; } = "https://api.deepseek.com/v1";
    public string BaseUrl
    {
        get => Endpoint;
        set => Endpoint = value;
    }
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = "deepseek-chat";
    public string Model
    {
        get => ModelName;
        set => ModelName = value;
    }
    public double Temperature { get; set; } = 0.2;
}

public sealed class AISummaryResult
{
    public string ModelName { get; set; } = string.Empty;
    public string OneLineIntent { get; set; } = string.Empty;
    public List<string> KeyModifications { get; set; } = new();
    public string ConventionalCommit { get; set; } = string.Empty;
    public string RawMarkdown { get; set; } = string.Empty;
    public double ExecutionTimeSeconds { get; set; }
    public bool IsSuccess { get; set; } = true;
}
