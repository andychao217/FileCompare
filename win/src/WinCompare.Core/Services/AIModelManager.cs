using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using WinCompare.Core.Models;

namespace WinCompare.Core.Services;

public sealed class AIModelManager
{
    private static readonly Lazy<AIModelManager> _instance = new(() => new AIModelManager());
    public static AIModelManager Instance => _instance.Value;

    private readonly ConcurrentDictionary<AIModelId, AIModelDownloadState> _modelStates = new();
    private readonly ConcurrentDictionary<AIModelId, CancellationTokenSource> _downloadCts = new();
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromHours(4) };

    public event Action<AIModelId, AIModelDownloadState>? ModelStateChanged;

    public string ModelsDirectory { get; }

    private AIModelManager()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        ModelsDirectory = Path.Combine(appData, "WinCompare", "models");
        try
        {
            if (!Directory.Exists(ModelsDirectory))
            {
                Directory.CreateDirectory(ModelsDirectory);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to create models directory: {ex.Message}");
        }

        RefreshAllStatuses();
    }

    public AIModelId SelectedModel
    {
        get => SettingsManager.Instance.SelectedLocalModel;
        set => SettingsManager.Instance.SelectedLocalModel = value;
    }

    public AIModelId EffectiveModel
    {
        get
        {
            if (IsModelDownloaded(SelectedModel)) return SelectedModel;
            if (IsModelDownloaded(AIModelId.Gemma3_1B)) return AIModelId.Gemma3_1B;
            if (IsModelDownloaded(AIModelId.Gemma3_4B)) return AIModelId.Gemma3_4B;
            return SelectedModel;
        }
    }

    public string ActiveModelName
    {
        get
        {
            if ((AIExecutionMode)SettingsManager.Instance.AiExecutionMode == AIExecutionMode.CloudAPI &&
                !string.IsNullOrWhiteSpace(SettingsManager.Instance.AiCloudModel))
            {
                return SettingsManager.Instance.AiCloudModel.Trim();
            }
            return EffectiveModel.GetShortName();
        }
    }

    public string GetLocalFilePath(AIModelId model)
    {
        return Path.Combine(ModelsDirectory, model.GetFilename());
    }

    public bool IsModelDownloaded(AIModelId model)
    {
        string path = GetLocalFilePath(model);
        if (!File.Exists(path)) return false;
        try
        {
            var fi = new FileInfo(path);
            return fi.Length > 10 * 1024 * 1024; // > 10MB
        }
        catch
        {
            return false;
        }
    }

    public AIModelDownloadState GetModelState(AIModelId model)
    {
        if (_modelStates.TryGetValue(model, out var state))
        {
            return state;
        }

        if (IsModelDownloaded(model))
        {
            return AIModelDownloadState.Ready;
        }

        return AIModelDownloadState.NotDownloaded;
    }

    public void RefreshAllStatuses()
    {
        foreach (AIModelId model in Enum.GetValues<AIModelId>())
        {
            if (_downloadCts.ContainsKey(model)) continue;

            if (IsModelDownloaded(model))
            {
                _modelStates[model] = AIModelDownloadState.Ready;
            }
            else
            {
                _modelStates[model] = AIModelDownloadState.NotDownloaded;
            }
        }
    }

    public void StartDownload(AIModelId model)
    {
        if (_downloadCts.ContainsKey(model)) return;

        var cts = new CancellationTokenSource();
        _downloadCts[model] = cts;

        var initialState = new AIModelDownloadState
        {
            Status = AIModelDownloadStatus.Downloading,
            Progress = 0.0,
            SpeedText = LanguageManager.Instance.Text("aiPreparing"),
            RemainingText = LanguageManager.Instance.Text("aiCalculating")
        };
        _modelStates[model] = initialState;
        ModelStateChanged?.Invoke(model, initialState);

        _ = Task.Run(() => ExecuteDownloadAsync(model, cts.Token));
    }

    public void CancelDownload(AIModelId model)
    {
        if (_downloadCts.TryRemove(model, out var cts))
        {
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch { }
        }

        string tempPath = GetLocalFilePath(model) + ".downloading";
        try
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
        catch { }

        var state = AIModelDownloadState.NotDownloaded;
        _modelStates[model] = state;
        ModelStateChanged?.Invoke(model, state);
    }

    public void DeleteModel(AIModelId model)
    {
        CancelDownload(model);

        string path = GetLocalFilePath(model);
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }

        var state = AIModelDownloadState.NotDownloaded;
        _modelStates[model] = state;

        if (SelectedModel == model)
        {
            foreach (AIModelId m in Enum.GetValues<AIModelId>())
            {
                if (m != model && IsModelDownloaded(m))
                {
                    SelectedModel = m;
                    break;
                }
            }
        }

        ModelStateChanged?.Invoke(model, state);
    }

    private async Task ExecuteDownloadAsync(AIModelId model, CancellationToken ct)
    {
        string finalPath = GetLocalFilePath(model);
        string tempPath = finalPath + ".downloading";

        string primaryUrl = model.GetPrimaryDownloadUrl();
        string fallbackUrl = model.GetFallbackDownloadUrl();

        try
        {
            bool downloaded = await TryDownloadFromUrlAsync(primaryUrl, tempPath, model, ct);
            if (!downloaded && !ct.IsCancellationRequested)
            {
                downloaded = await TryDownloadFromUrlAsync(fallbackUrl, tempPath, model, ct);
            }

            if (!downloaded)
            {
                throw new InvalidOperationException(LanguageManager.Instance.Text("aiDownloadFailed"));
            }

            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }
            File.Move(tempPath, finalPath);

            var readyState = AIModelDownloadState.Ready;
            _modelStates[model] = readyState;
            _downloadCts.TryRemove(model, out _);

            if (!IsModelDownloaded(SelectedModel))
            {
                SelectedModel = model;
            }

            ModelStateChanged?.Invoke(model, readyState);
        }
        catch (OperationCanceledException)
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            var state = AIModelDownloadState.NotDownloaded;
            _modelStates[model] = state;
            _downloadCts.TryRemove(model, out _);
            ModelStateChanged?.Invoke(model, state);
        }
        catch (Exception ex)
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            var errorState = new AIModelDownloadState
            {
                Status = AIModelDownloadStatus.Error,
                ErrorMessage = ex.Message
            };
            _modelStates[model] = errorState;
            _downloadCts.TryRemove(model, out _);
            ModelStateChanged?.Invoke(model, errorState);
        }
    }

    private async Task<bool> TryDownloadFromUrlAsync(string url, string tempPath, AIModelId model, CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) return false;

            long totalBytes = response.Content.Headers.ContentLength ?? (long)model.GetEstimatedSizeMB() * 1024 * 1024;

            using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, true);

            byte[] buffer = new byte[128 * 1024];
            long totalRead = 0;
            int read;

            var sw = Stopwatch.StartNew();
            long lastReportBytes = 0;
            var lastReportTime = sw.Elapsed;

            while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                totalRead += read;

                var elapsed = sw.Elapsed;
                if ((elapsed - lastReportTime).TotalMilliseconds >= 350)
                {
                    double timeDiffSec = (elapsed - lastReportTime).TotalSeconds;
                    long bytesDiff = totalRead - lastReportBytes;
                    double bytesPerSec = bytesDiff / Math.Max(0.01, timeDiffSec);
                    double mbPerSec = bytesPerSec / (1024.0 * 1024.0);

                    double progress = totalBytes > 0 ? (double)totalRead / totalBytes : 0.0;
                    progress = Math.Clamp(progress, 0.0, 0.99);

                    string speedText = $"{mbPerSec:F1} MB/s";
                    string remainingText;

                    if (mbPerSec > 0.05 && totalBytes > totalRead)
                    {
                        int remSecs = (int)((totalBytes - totalRead) / bytesPerSec);
                        if (remSecs < 60)
                        {
                            remainingText = string.Format(LanguageManager.Instance.Text("aiRemainingSeconds"), Math.Max(1, remSecs));
                        }
                        else
                        {
                            remainingText = string.Format(LanguageManager.Instance.Text("aiRemainingMinutes"), Math.Max(1, remSecs / 60));
                        }
                    }
                    else
                    {
                        remainingText = LanguageManager.Instance.Text("aiCalculating");
                    }

                    lastReportBytes = totalRead;
                    lastReportTime = elapsed;

                    var state = new AIModelDownloadState
                    {
                        Status = AIModelDownloadStatus.Downloading,
                        Progress = progress,
                        SpeedText = speedText,
                        RemainingText = remainingText
                    };
                    _modelStates[model] = state;
                    ModelStateChanged?.Invoke(model, state);
                }
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
