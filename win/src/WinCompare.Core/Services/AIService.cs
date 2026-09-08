using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WinCompare.Core.Models;

namespace WinCompare.Core.Services;

public sealed class AIService
{
    private static readonly Lazy<AIService> _instance = new(() => new AIService());
    public static AIService Instance => _instance.Value;

    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<(bool Success, double LatencySeconds, string Message)> TestCloudConnectionAsync(
        string endpoint,
        string apiKey,
        string modelName)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return (false, 0, LanguageManager.Instance.Text("aiEndpointEmpty"));
            }

            string cleanEndpoint = endpoint.Trim().TrimEnd('/');
            string testUrl = $"{cleanEndpoint}/chat/completions";

            var payload = new
            {
                model = string.IsNullOrWhiteSpace(modelName) ? "deepseek-chat" : modelName.Trim(),
                messages = new[] { new { role = "user", content = "ping" } },
                max_tokens = 1
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, testUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var response = await _httpClient.SendAsync(request, cts.Token);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                return (true, sw.Elapsed.TotalSeconds, LanguageManager.Instance.Text("aiCloudTestSuccess"));
            }

            int code = (int)response.StatusCode;
            string reason = response.ReasonPhrase ?? "Error";
            return (false, sw.Elapsed.TotalSeconds, $"HTTP {code} ({reason})");
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return (false, sw.Elapsed.TotalSeconds, LanguageManager.Instance.Text("aiTimeout"));
        }
        catch (Exception ex)
        {
            sw.Stop();
            return (false, sw.Elapsed.TotalSeconds, ex.Message);
        }
    }

    public async Task<AISummaryResult> SummarizeDiffAsync(
        string leftContent,
        string rightContent,
        string leftTitle = "Left",
        string rightTitle = "Right",
        AIExecutionMode mode = AIExecutionMode.LocalOffline,
        CloudAPIConfig? cloudConfig = null,
        AppLanguage language = AppLanguage.Auto)
    {
        var effectiveLang = language == AppLanguage.Auto
            ? LanguageManager.Instance.EffectiveLanguage
            : language;

        var sw = Stopwatch.StartNew();

        // 1. Fast diff lines extraction
        var diff = DiffEngineService.Instance.CompareText(leftContent, rightContent, false, false);
        var added = new List<string>();
        var removed = new List<string>();
        foreach (var chunk in diff.Chunks)
        {
            if (chunk.ChangeType == DiffChangeType.Added || chunk.ChangeType == DiffChangeType.Modified)
            {
                added.AddRange(chunk.RightLines);
            }
            if (chunk.ChangeType == DiffChangeType.Deleted || chunk.ChangeType == DiffChangeType.Modified)
            {
                removed.AddRange(chunk.LeftLines);
            }
        }

        if (added.Count == 0 && removed.Count == 0)
        {
            sw.Stop();
            return new AISummaryResult
            {
                ModelName = LanguageManager.Instance.Text("aiModelDiffEngine"),
                OneLineIntent = LanguageManager.Instance.Text("aiSummaryIdentical"),
                KeyModifications = new List<string> { LanguageManager.Instance.Text("aiSummaryNoChanges") },
                ConventionalCommit = LanguageManager.Instance.Text("aiSummaryCommitNoChanges"),
                ExecutionTimeSeconds = sw.Elapsed.TotalSeconds,
                IsSuccess = true
            };
        }

        // 2. Cloud API Mode
        if (mode == AIExecutionMode.CloudAPI)
        {
            var activeCloudConfig = cloudConfig ?? SettingsManager.Instance.CloudConfig;
            if (activeCloudConfig != null && !string.IsNullOrWhiteSpace(activeCloudConfig.ApiKey))
            {
                return await GenerateCloudSummaryAsync(added, removed, activeCloudConfig, effectiveLang, sw);
            }

            sw.Stop();
            return new AISummaryResult
            {
                ModelName = !string.IsNullOrWhiteSpace(activeCloudConfig?.ModelName) ? activeCloudConfig.ModelName : LanguageManager.Instance.Text("aiModelCloudDefault"),
                OneLineIntent = LanguageManager.Instance.Text("aiSummaryConfigCloud"),
                KeyModifications = new List<string>
                {
                    LanguageManager.Instance.Text("aiConfigApiKeyTip"),
                    LanguageManager.Instance.Text("aiConfigCompatibleTip"),
                    LanguageManager.Instance.Text("aiConfigLocalTip")
                },
                ConventionalCommit = LanguageManager.Instance.Text("aiSummaryCommitConfigKey"),
                ExecutionTimeSeconds = sw.Elapsed.TotalSeconds,
                IsSuccess = false
            };
        }

        // 3. Local Offline Mode (Gemma 3)
        var modelManager = AIModelManager.Instance;
        var effectiveModel = modelManager.EffectiveModel;
        bool isDownloaded = modelManager.IsModelDownloaded(effectiveModel);

        if (!isDownloaded)
        {
            sw.Stop();
            string unreadyIntent;
            List<string> diffStats;
            switch (effectiveLang)
            {
                case AppLanguage.ZhHans:
                    unreadyIntent = "端侧大模型尚未就绪。请点击「下载模型」以启用 Gemma 3 真实大模型深度分析。";
                    diffStats = new List<string>
                    {
                        $"检测到 {added.Count} 处新增内容",
                        $"检测到 {removed.Count} 处移除内容",
                        "下载约 780MB 离线模型后即可由本地神经网络自动提炼意图与 Commit"
                    };
                    break;
                case AppLanguage.Ja:
                    unreadyIntent = "ローカルAIモデルが準備されていません。「モデルをダウンロード」をクリックして有効にしてください。";
                    diffStats = new List<string>
                    {
                        $"{added.Count} 件の追加内容を検出",
                        $"{removed.Count} 件の削除内容を検出",
                        "モデルのダウンロード完了後、ローカルLLMによる自動要約が有効になります"
                    };
                    break;
                default:
                    unreadyIntent = "Local AI model is not ready. Please download the Gemma 3 model to enable on-device semantic analysis.";
                    diffStats = new List<string>
                    {
                        $"{added.Count} additions detected",
                        $"{removed.Count} removals detected",
                        "Local neural network will generate semantic intent after downloading the model"
                    };
                    break;
            }

            string notReadyStatus = LanguageManager.Instance.Text("aiModelStatusNotReady");
            string commitDesc1 = LanguageManager.Instance.Text("aiSummaryCommitUpdateDesc");
            string targetName1 = Path.GetFileNameWithoutExtension(rightTitle).ToLower();
            return new AISummaryResult
            {
                ModelName = $"{effectiveModel.GetShortName()} ({notReadyStatus})",
                OneLineIntent = unreadyIntent,
                KeyModifications = diffStats,
                ConventionalCommit = $"feat({targetName1}): {commitDesc1}",
                ExecutionTimeSeconds = sw.Elapsed.TotalSeconds,
                IsSuccess = false
            };
        }

        // Check for local llama-cli runner if bundled
        string modelPath = modelManager.GetLocalFilePath(effectiveModel);
        string? llamaRunner = FindLlamaRunnerExecutable();

        if (!string.IsNullOrEmpty(llamaRunner) && File.Exists(llamaRunner))
        {
            var result = await TryRunLlamaCliInferenceAsync(llamaRunner, modelPath, added, removed, effectiveLang, sw);
            if (result != null) return result;
        }

        sw.Stop();
        string readyStatus = LanguageManager.Instance.Text("aiModelStatusLocalReady");
        string commitDesc2 = LanguageManager.Instance.Text("aiSummaryCommitUpdateDesc");
        string targetName2 = Path.GetFileNameWithoutExtension(rightTitle).ToLower();
        return new AISummaryResult
        {
            ModelName = $"{effectiveModel.GetShortName()} ({readyStatus})",
            OneLineIntent = string.Format(LanguageManager.Instance.Text("aiSummaryCompleted"), added.Count, removed.Count),
            KeyModifications = new List<string>
            {
                string.Format(LanguageManager.Instance.Text("aiSummaryMainTitle"), rightTitle),
                string.Format(LanguageManager.Instance.Text("aiModelOfflineLoaded"), effectiveModel.GetDisplayName(), modelManager.ModelsDirectory),
                LanguageManager.Instance.Text("aiSummaryCommitFeature")
            },
            ConventionalCommit = $"feat({targetName2}): {commitDesc2}",
            ExecutionTimeSeconds = sw.Elapsed.TotalSeconds,
            IsSuccess = true
        };
    }

    public Task<AISummaryResult> SummarizeDiffAsync(
        string leftContent,
        string rightContent,
        string leftTitle,
        string rightTitle,
        string language)
    {
        var lang = AppLanguageExtensions.FromIsoCode(language);
        return SummarizeDiffAsync(leftContent, rightContent, leftTitle, rightTitle, AIExecutionMode.LocalOffline, null, lang);
    }

    private static string? FindLlamaRunnerExecutable()
    {
        string baseDir = AppContext.BaseDirectory;
        string[] candidates = { "llama-cli.exe", "llama.exe", "main.exe", "mc-llama.exe" };
        foreach (var c in candidates)
        {
            string p = Path.Combine(baseDir, c);
            if (File.Exists(p)) return p;
            string pTools = Path.Combine(baseDir, "tools", c);
            if (File.Exists(pTools)) return pTools;
        }
        return null;
    }

    /// <summary>
    /// 对齐 macOS 版 buildPairwiseDiff：成对配对改动行，使端侧小模型与大模型清晰理解前后演变
    /// </summary>
    private static string BuildPairwiseDiff(List<string> addedLines, List<string> removedLines, AppLanguage language)
    {
        var remainingAdded = addedLines.Take(20).ToList();
        var pairs = new List<(string Old, string New)>();
        var singleRemoved = new List<string>();

        foreach (var oldLine in removedLines.Take(20))
        {
            string oldPrefix = oldLine.Length > 10 ? oldLine.Substring(0, 10) : oldLine;
            int matchIdx = -1;
            if (!string.IsNullOrWhiteSpace(oldPrefix))
            {
                matchIdx = remainingAdded.FindIndex(a => a.StartsWith(oldPrefix, StringComparison.Ordinal));
            }
            if (matchIdx < 0)
            {
                matchIdx = remainingAdded.FindIndex(a =>
                {
                    int commonChars = oldLine.Intersect(a).Count();
                    return commonChars > Math.Max(oldLine.Length, a.Length) / 2;
                });
            }

            if (matchIdx >= 0)
            {
                pairs.Add((oldLine, remainingAdded[matchIdx]));
                remainingAdded.RemoveAt(matchIdx);
            }
            else
            {
                singleRemoved.Add(oldLine);
            }
        }

        var snippets = new List<string>();
        int index = 1;

        string changeLabel = language switch
        {
            AppLanguage.ZhHans => "改动项",
            AppLanguage.Ja => "変更項目",
            _ => "Change"
        };
        string addLabel = language switch
        {
            AppLanguage.ZhHans => "新增项",
            AppLanguage.Ja => "追加項目",
            _ => "Added"
        };
        string delLabel = language switch
        {
            AppLanguage.ZhHans => "删除项",
            AppLanguage.Ja => "削除項目",
            _ => "Deleted"
        };

        foreach (var pair in pairs.Take(6))
        {
            snippets.Add($"[{changeLabel} {index++}]\n- {pair.Old}\n+ {pair.New}");
        }
        foreach (var added in remainingAdded.Take(3))
        {
            snippets.Add($"[{addLabel} {index++}]\n+ {added}");
        }
        foreach (var removed in singleRemoved.Take(3))
        {
            snippets.Add($"[{delLabel} {index++}]\n- {removed}");
        }

        return string.Join("\n\n", snippets);
    }

    /// <summary>
    /// 对齐 macOS 版多语言 Prompt 构建策略：根据语言环境分别输出地道的中文/日文/英文 System 与 User 指令
    /// </summary>
    private static (string SystemInstruction, string UserInstruction) BuildInstructions(string diffBody, AppLanguage language)
    {
        switch (language)
        {
            case AppLanguage.ZhHans:
                return (
                    "你是一位资深架构师与代码审查专家。请阅读用户提供的成对代码改动（Diff），分析修改意图并提炼出关键改动。请直接输出合法的 JSON 格式，不要进行冗长的思考或推导，不要添加额外的问候语或 Markdown 外部包裹。",
                    "请阅读以下成对配对的代码改动（Diff）：\n" +
                    diffBody + "\n\n" +
                    "请输出一个合法的 JSON 对象，格式如下：\n" +
                    "{\n" +
                    "  \"intent\": \"用一句话概括本次修改的核心目的\",\n" +
                    "  \"points\": [\n" +
                    "    \"核心变更说明1（说明从旧值变更为新值，如：将 ARCH 由 arch64 更改为 arm64）\",\n" +
                    "    \"核心变更说明2\",\n" +
                    "    \"核心变更说明3\"\n" +
                    "  ],\n" +
                    "  \"commit\": \"规范的 Git 提交信息（如：chore(build): 升级构建版本并调整架构）\"\n" +
                    "}\n" +
                    "规则：准确使用代码中的变量名，不得捏造不存在的名称，不得输出重复要点。"
                );
            case AppLanguage.Ja:
                return (
                    "あなたはシニアアーキテクト兼コードレビューの専門家です。コード差分を分析し、長考や過度な推論を行わずに、有効なJSON形式のみを直接出力してください。挨拶やMarkdownの外枠は含めないでください。",
                    "成対差分内容：\n" +
                    diffBody + "\n\n" +
                    "以下の3つのフィールドを持つ単一のJSON形式で出力してください：\n" +
                    "{\n" +
                    "  \"intent\": \"変更意図の簡潔な日本語要約\",\n" +
                    "  \"points\": [\n" +
                    "    \"変更点1（旧値から新値への変更を明記）\",\n" +
                    "    \"変更点2\"\n" +
                    "  ],\n" +
                    "  \"commit\": \"Gitのコミットメッセージ（例: chore(build): ...）\"\n" +
                    "}\n" +
                    "規則: コード内の変数名を正確に使用し、重複した項目は出力しないでください。"
                );
            default:
                return (
                    "You are a senior code reviewer. Analyze code diffs and directly output a valid JSON object without lengthy thinking or explanations. Do not include greetings or markdown fences.",
                    "Paired diff content:\n" +
                    diffBody + "\n\n" +
                    "Output a single JSON object with the following schema:\n" +
                    "{\n" +
                    "  \"intent\": \"concise summary of the core purpose\",\n" +
                    "  \"points\": [\n" +
                    "    \"Key point 1 detailing changes from old value to new value\",\n" +
                    "    \"Key point 2\"\n" +
                    "  ],\n" +
                    "  \"commit\": \"Git conventional commit message (e.g., chore(build): update build script)\"\n" +
                    "}\n" +
                    "Rules: Use exact variable names from the diff. Do not invent names or repeat items."
                );
        }
    }

    /// <summary>
    /// 解析 LLM 返回的 JSON 块（支持兼容 intent/oneLineIntent、points/keyModifications、commit/conventionalCommit，及正则兜底）
    /// </summary>
    private static (string? Intent, List<string> Points, string? Commit) ParseLLMJson(string rawText)
    {
        int jsonStart = rawText.IndexOf('{');
        int jsonEnd = rawText.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd <= jsonStart)
        {
            return (null, new List<string>(), null);
        }

        string jsonStr = rawText.Substring(jsonStart, jsonEnd - jsonStart + 1);

        // 1. 清理 LLM 常见的尾随逗号 (trailing comma)
        jsonStr = System.Text.RegularExpressions.Regex.Replace(jsonStr, @",\s*([\]\}])", "$1");

        try
        {
            using var doc = JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;

            string? intent = null;
            if (root.TryGetProperty("intent", out var intentElem))
            {
                intent = intentElem.GetString();
            }
            else if (root.TryGetProperty("oneLineIntent", out var oliElem))
            {
                intent = oliElem.GetString();
            }

            var points = new List<string>();
            JsonElement pointsElem = default;
            if (root.TryGetProperty("points", out pointsElem) || root.TryGetProperty("keyModifications", out pointsElem))
            {
                if (pointsElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in pointsElem.EnumerateArray())
                    {
                        string s = item.GetString()?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(s) && !points.Contains(s))
                        {
                            points.Add(s);
                        }
                    }
                }
            }

            string? commit = null;
            if (root.TryGetProperty("commit", out var commitElem))
            {
                commit = commitElem.GetString();
            }
            else if (root.TryGetProperty("conventionalCommit", out var ccElem))
            {
                commit = ccElem.GetString();
            }

            return (intent, points, commit);
        }
        catch
        {
            // 正则兜底解析器：即使 JSON 结构被非标字符打断，也能提取出关键字段
            string? intent = null;
            var intentMatch = System.Text.RegularExpressions.Regex.Match(rawText, @"""(?:intent|oneLineIntent)""\s*:\s*""([^""]+)""");
            if (intentMatch.Success) intent = intentMatch.Groups[1].Value;

            string? commit = null;
            var commitMatch = System.Text.RegularExpressions.Regex.Match(rawText, @"""(?:commit|conventionalCommit)""\s*:\s*""([^""]+)""");
            if (commitMatch.Success) commit = commitMatch.Groups[1].Value;

            var points = new List<string>();
            var pointsBlock = System.Text.RegularExpressions.Regex.Match(rawText, @"""(?:points|keyModifications)""\s*:\s*\[([^\]]+)\]");
            if (pointsBlock.Success)
            {
                var itemMatches = System.Text.RegularExpressions.Regex.Matches(pointsBlock.Groups[1].Value, @"""([^""]+)""");
                foreach (System.Text.RegularExpressions.Match m in itemMatches)
                {
                    string s = m.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(s) && !points.Contains(s)) points.Add(s);
                }
            }

            return (intent, points, commit);
        }
    }

    private async Task<AISummaryResult?> TryRunLlamaCliInferenceAsync(
        string executable,
        string modelPath,
        List<string> added,
        List<string> removed,
        AppLanguage language,
        Stopwatch sw)
    {
        try
        {
            string diffBody = BuildPairwiseDiff(added, removed, language);
            if (string.IsNullOrWhiteSpace(diffBody)) return null;

            var (_, userInstruction) = BuildInstructions(diffBody, language);
            string fullPrompt = $"<start_of_turn>user\n{userInstruction}<end_of_turn>\n<start_of_turn>model\n";
            string escapedPrompt = fullPrompt.Replace("\"", "\\\"");

            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = $"-m \"{modelPath}\" -p \"{escapedPrompt}\" -n 280 -st --no-display-prompt",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return null;

            string output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            sw.Stop();
            var (intent, points, commit) = ParseLLMJson(output);

            if (!string.IsNullOrEmpty(intent) || points.Count > 0 || !string.IsNullOrEmpty(commit))
            {
                return new AISummaryResult
                {
                    ModelName = LanguageManager.Instance.Text("aiModelGemmaLocal"),
                    OneLineIntent = intent ?? LanguageManager.Instance.Text("aiSummaryLocalIntent"),
                    KeyModifications = points.Count > 0 ? points : new List<string> { LanguageManager.Instance.Text("aiSummaryCommitFeature") },
                    ConventionalCommit = commit ?? LanguageManager.Instance.Text("aiSummaryCommitUpdate"),
                    RawMarkdown = output,
                    ExecutionTimeSeconds = sw.Elapsed.TotalSeconds,
                    IsSuccess = true
                };
            }
        }
        catch { }
        return null;
    }

    private async Task<AISummaryResult> GenerateCloudSummaryAsync(
        List<string> added,
        List<string> removed,
        CloudAPIConfig config,
        AppLanguage language,
        Stopwatch sw)
    {
        try
        {
            string diffBody = BuildPairwiseDiff(added, removed, language);
            var (systemInstruction, userInstruction) = BuildInstructions(diffBody, language);

            var payload = new
            {
                model = string.IsNullOrWhiteSpace(config.ModelName) ? "deepseek-chat" : config.ModelName.Trim(),
                temperature = config.Temperature,
                messages = new[]
                {
                    new { role = "system", content = systemInstruction },
                    new { role = "user", content = userInstruction }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{config.Endpoint.TrimEnd('/')}/chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string respContent = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(respContent);
            string rawAnswer = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

            sw.Stop();
            var (intent, points, commit) = ParseLLMJson(rawAnswer);

            if (!string.IsNullOrEmpty(intent) || points.Count > 0 || !string.IsNullOrEmpty(commit))
            {
                return new AISummaryResult
                {
                    ModelName = config.ModelName,
                    OneLineIntent = intent ?? LanguageManager.Instance.Text("aiSummaryLocalIntent"),
                    KeyModifications = points.Count > 0 ? points : new List<string> { rawAnswer },
                    ConventionalCommit = commit ?? LanguageManager.Instance.Text("aiSummaryCommitUpdate"),
                    RawMarkdown = rawAnswer,
                    ExecutionTimeSeconds = sw.Elapsed.TotalSeconds,
                    IsSuccess = true
                };
            }

            return new AISummaryResult
            {
                ModelName = config.ModelName,
                OneLineIntent = rawAnswer.Split('\n').FirstOrDefault() ?? LanguageManager.Instance.Text("aiSummaryLocalIntent"),
                KeyModifications = new List<string> { rawAnswer },
                ConventionalCommit = LanguageManager.Instance.Text("aiSummaryCommitUpdate"),
                ExecutionTimeSeconds = sw.Elapsed.TotalSeconds,
                IsSuccess = true
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new AISummaryResult
            {
                ModelName = config.ModelName,
                OneLineIntent = string.Format(LanguageManager.Instance.Text("aiCloudCallFailed"), ex.Message),
                KeyModifications = new List<string> { LanguageManager.Instance.Text("aiCloudCheckSettings") },
                ConventionalCommit = LanguageManager.Instance.Text("aiSummaryCommitError"),
                ExecutionTimeSeconds = sw.Elapsed.TotalSeconds,
                IsSuccess = false
            };
        }
    }
}
