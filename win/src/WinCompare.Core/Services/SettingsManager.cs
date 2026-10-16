using System.Text;
using System.Text.Json;
using WinCompare.Core.Models;

namespace WinCompare.Core.Services;

public enum DiffEngineMode
{
    Auto = 0,
    RustOnly = 1,
    CSharpOnly = 2
}

public sealed class UserSettingsData
{
    public string Theme { get; set; } = "Default"; // "Default", "Dark", "Light"
    public string Language { get; set; } = "Auto"; // "Auto", "ZhHans", "En", "Ja"
    public string DefaultEncoding { get; set; } = "UTF-8";
    public int DiffEngineMode { get; set; } = 0; // 0 = Auto, 1 = Rust, 2 = CSharp
    public bool DefaultIgnoreWhitespace { get; set; } = false;
    public bool DefaultIgnoreCase { get; set; } = false;
    public bool CreateBakBackup { get; set; } = true;
    public int DefaultFolderDiffMode { get; set; } = 0; // 0 = Quick, 1 = DeepHash
    public string ExcludedPatterns { get; set; } = ".git, node_modules, bin, obj, target, .vs, .idea, .DS_Store, *.bak";
    public int AiExecutionMode { get; set; } = 0; // 0 = Local, 1 = Cloud
    public int AiCloudProvider { get; set; } = 0; // 0 = DeepSeek, 1 = OpenAI, 2 = Ollama, 3 = Custom
    public string AiCloudEndpoint { get; set; } = "https://api.deepseek.com/v1";
    public string AiCloudApiKey { get; set; } = "";
    public string AiCloudModel { get; set; } = "deepseek-chat";
    public int SelectedLocalModel { get; set; } = 0; // 0 = Gemma3_1B, 1 = Gemma3_4B
    public bool AutoReleaseMemory { get; set; } = true;
    public bool AutoCheckUpdatesOnLaunch { get; set; } = true;
    public string SkippedUpdateVersion { get; set; } = "";
    public long LastUpdateCheckTimestamp { get; set; } = 0;
}

public sealed class SettingsManager
{
    private static readonly Lazy<SettingsManager> _instance = new(() => new SettingsManager());
    public static SettingsManager Instance => _instance.Value;

    private UserSettingsData _data = new();
    private readonly string _settingsFilePath;

    public event Action? SettingsChanged;

    public SettingsManager()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        catch { }

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "WinCompare");
        _settingsFilePath = Path.Combine(folder, "settings.json");

        Load();
    }

    public UserSettingsData Data => _data;

    public string Theme
    {
        get => _data.Theme;
        set { _data.Theme = value; Save(); }
    }

    public AppLanguage Language
    {
        get => _data.Language switch
        {
            "ZhHans" => AppLanguage.ZhHans,
            "En" => AppLanguage.En,
            "Ja" => AppLanguage.Ja,
            _ => AppLanguage.Auto
        };
        set
        {
            _data.Language = value.ToString();
            Save();
        }
    }

    public string DefaultEncoding
    {
        get => _data.DefaultEncoding;
        set { _data.DefaultEncoding = value; Save(); }
    }

    public DiffEngineMode EngineMode
    {
        get => (DiffEngineMode)_data.DiffEngineMode;
        set { _data.DiffEngineMode = (int)value; Save(); }
    }

    public bool DefaultIgnoreWhitespace
    {
        get => _data.DefaultIgnoreWhitespace;
        set { _data.DefaultIgnoreWhitespace = value; Save(); }
    }

    public bool DefaultIgnoreCase
    {
        get => _data.DefaultIgnoreCase;
        set { _data.DefaultIgnoreCase = value; Save(); }
    }

    public bool CreateBakBackup
    {
        get => _data.CreateBakBackup;
        set { _data.CreateBakBackup = value; Save(); }
    }

    public FolderDiffMode DefaultFolderDiffMode
    {
        get => _data.DefaultFolderDiffMode == 1 ? FolderDiffMode.DeepHash : FolderDiffMode.Quick;
        set { _data.DefaultFolderDiffMode = (int)value; Save(); }
    }

    public string ExcludedPatterns
    {
        get => _data.ExcludedPatterns;
        set { _data.ExcludedPatterns = value; Save(); }
    }

    public AIExecutionMode AiExecutionMode
    {
        get => (AIExecutionMode)_data.AiExecutionMode;
        set { _data.AiExecutionMode = (int)value; Save(); }
    }

    public int AiCloudProvider
    {
        get => _data.AiCloudProvider;
        set { _data.AiCloudProvider = value; Save(); }
    }

    public string AiCloudEndpoint
    {
        get => _data.AiCloudEndpoint;
        set { _data.AiCloudEndpoint = value; Save(); }
    }

    public string AiCloudApiKey
    {
        get => _data.AiCloudApiKey;
        set { _data.AiCloudApiKey = value; Save(); }
    }

    public string AiCloudModel
    {
        get => _data.AiCloudModel;
        set { _data.AiCloudModel = value; Save(); }
    }

    public CloudAPIConfig CloudConfig
    {
        get
        {
            return new CloudAPIConfig
            {
                Endpoint = string.IsNullOrWhiteSpace(AiCloudEndpoint) ? "https://api.deepseek.com/v1" : AiCloudEndpoint,
                ApiKey = AiCloudApiKey,
                ModelName = string.IsNullOrWhiteSpace(AiCloudModel) ? "deepseek-chat" : AiCloudModel
            };
        }
        set
        {
            if (value != null)
            {
                AiCloudEndpoint = value.Endpoint;
                AiCloudApiKey = value.ApiKey;
                AiCloudModel = value.ModelName;
            }
        }
    }

    public AIModelId SelectedLocalModel
    {
        get => (AIModelId)_data.SelectedLocalModel;
        set { _data.SelectedLocalModel = (int)value; Save(); }
    }

    public bool AutoReleaseMemory
    {
        get => _data.AutoReleaseMemory;
        set { _data.AutoReleaseMemory = value; Save(); }
    }

    public bool AutoCheckUpdatesOnLaunch
    {
        get => _data.AutoCheckUpdatesOnLaunch;
        set { _data.AutoCheckUpdatesOnLaunch = value; Save(); }
    }

    public string SkippedUpdateVersion
    {
        get => _data.SkippedUpdateVersion;
        set { _data.SkippedUpdateVersion = value; Save(); }
    }

    public long LastUpdateCheckTimestamp
    {
        get => _data.LastUpdateCheckTimestamp;
        set { _data.LastUpdateCheckTimestamp = value; Save(); }
    }

    public Encoding GetEncoding()
    {
        try
        {
            return _data.DefaultEncoding switch
            {
                "GBK / GB18030" or "GBK" or "GB18030" => Encoding.GetEncoding("GB18030"),
                "UTF-16 LE" or "UTF-16LE" => Encoding.Unicode,
                "Shift-JIS" => Encoding.GetEncoding("shift_jis"),
                "ISO-8859-1" => Encoding.GetEncoding("iso-8859-1"),
                _ => new UTF8Encoding(false)
            };
        }
        catch
        {
            return new UTF8Encoding(false);
        }
    }

    public bool IsPathExcluded(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(_data.ExcludedPatterns)) return false;

        var patterns = _data.ExcludedPatterns.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string normalized = relativePath.Replace('\\', '/').TrimStart('/');
        string[] parts = normalized.Split('/');

        foreach (var p in patterns)
        {
            string clean = p.TrimStart('*', '.').Trim();
            if (string.IsNullOrEmpty(clean)) continue;

            if (p.StartsWith("*."))
            {
                string ext = p.Substring(1);
                if (normalized.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) return true;
            }
            else
            {
                if (parts.Any(part => part.Equals(clean, StringComparison.OrdinalIgnoreCase))) return true;
            }
        }
        return false;
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<UserSettingsData>(json);
                if (loaded != null)
                {
                    _data = loaded;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsManager.Load failed: {ex.Message}");
        }
    }

    public void Save()
    {
        try
        {
            string dir = Path.GetDirectoryName(_settingsFilePath)!;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_data, options);
            File.WriteAllText(_settingsFilePath, json);
            SettingsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsManager.Save failed: {ex.Message}");
        }
    }
}
