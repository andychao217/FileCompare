using WinCompare.Core.Models;

namespace WinCompare.Core.Services;

public sealed class LanguageManager
{
    private static readonly Lazy<LanguageManager> _instance = new(() => new LanguageManager());
    public static LanguageManager Instance => _instance.Value;

    public AppLanguage CurrentLanguage
    {
        get => SettingsManager.Instance.Language;
        set => SetLanguage(value);
    }

    public AppLanguage EffectiveLanguage
    {
        get
        {
            var selected = CurrentLanguage;
            if (selected == AppLanguage.Auto)
            {
                var culture = System.Globalization.CultureInfo.CurrentUICulture.Name.ToLowerInvariant();
                if (culture.StartsWith("zh")) return AppLanguage.ZhHans;
                if (culture.StartsWith("ja")) return AppLanguage.Ja;
                return AppLanguage.En;
            }
            return selected;
        }
    }

    public event Action? LanguageChanged;

    public void SetLanguage(AppLanguage language)
    {
        if (SettingsManager.Instance.Language != language)
        {
            SettingsManager.Instance.Language = language;
            LanguageChanged?.Invoke();
        }
    }

    public string Text(string key)
    {
        var eff = EffectiveLanguage;
        if (Translations.TryGetValue(key, out var dict))
        {
            if (dict.TryGetValue(eff, out var val))
            {
                return val;
            }
            if (dict.TryGetValue(AppLanguage.En, out var fallback))
            {
                return fallback;
            }
        }
        return key;
    }

    private static readonly Dictionary<string, Dictionary<AppLanguage, string>> Translations = new()
    {
        ["appTitle"] = new()
        {
            [AppLanguage.ZhHans] = "WinCompare",
            [AppLanguage.En] = "WinCompare",
            [AppLanguage.Ja] = "WinCompare"
        },
        ["welcomeTitle"] = new()
        {
            [AppLanguage.ZhHans] = "WinCompare",
            [AppLanguage.En] = "WinCompare",
            [AppLanguage.Ja] = "WinCompare"
        },
        ["welcomeSubtitle"] = new()
        {
            [AppLanguage.ZhHans] = "基于 Rust 高吞吐引擎打造的 Windows 11 原生文件比对与三向合并利器",
            [AppLanguage.En] = "High-Performance File Comparison & 3-Way Merge for Windows 11",
            [AppLanguage.Ja] = "Rust エンジンによる Windows 11 向け高速ファイル比較＆3方向マージツール"
        },
        ["textDiffTitle"] = new()
        {
            [AppLanguage.ZhHans] = "文本对比",
            [AppLanguage.En] = "Text Diff",
            [AppLanguage.Ja] = "テキスト比較"
        },
        ["textDiffDesc"] = new()
        {
            [AppLanguage.ZhHans] = "双向代码 Myers 差异对比、字符级高亮、行内编辑与 3-Way 冲突合并",
            [AppLanguage.En] = "Two-way Myers diff, inline edit, and 3-way conflict resolution",
            [AppLanguage.Ja] = "Myers アルゴリズムによる差分比較と 3-Way 衝突解決"
        },
        ["excelDiffTitle"] = new()
        {
            [AppLanguage.ZhHans] = "表格对比",
            [AppLanguage.En] = "Excel Diff",
            [AppLanguage.Ja] = "Excel 比較"
        },
        ["excelDiffDesc"] = new()
        {
            [AppLanguage.ZhHans] = "支持 .xlsx / .csv 大数据流式分页视口与单元格级智能着色",
            [AppLanguage.En] = "High-performance streaming diff for large Excel and CSV spreadsheets",
            [AppLanguage.Ja] = "大容量 Excel/CSV スプレッドシートの高速ストリーミング比較"
        },
        ["wordDiffTitle"] = new()
        {
            [AppLanguage.ZhHans] = "Word 对比",
            [AppLanguage.En] = "Word Diff",
            [AppLanguage.Ja] = "Word 比較"
        },
        ["wordDiffDesc"] = new()
        {
            [AppLanguage.ZhHans] = "智能解析 .docx 段落、图文混排、大纲树与嵌套表格对比",
            [AppLanguage.En] = "Structured diff for Word docs, outlines, tables, and media",
            [AppLanguage.Ja] = "Word 文書の段落、アウトライン、表、メディアの構造化比較"
        },
        ["folderDiffTitle"] = new()
        {
            [AppLanguage.ZhHans] = "文件夹对比",
            [AppLanguage.En] = "Folder Diff",
            [AppLanguage.Ja] = "フォルダ比較"
        },
        ["folderDiffDesc"] = new()
        {
            [AppLanguage.ZhHans] = "多线程快速、哈希与全字节扫描，支持双向一键同步与过滤",
            [AppLanguage.En] = "Multi-threaded fast, hash, and byte scanning with sync actions",
            [AppLanguage.Ja] = "マルチスレッドによる高速・ハッシュ・完全一致フォルダ比較"
        },
        ["welcomeTab"] = new()
        {
            [AppLanguage.ZhHans] = "欢迎",
            [AppLanguage.En] = "Welcome",
            [AppLanguage.Ja] = "ようこそ"
        },
        ["recentComparisons"] = new()
        {
            [AppLanguage.ZhHans] = "最近比对记录",
            [AppLanguage.En] = "Recent Comparisons",
            [AppLanguage.Ja] = "最近の比較履歴"
        },
        ["noRecentRecords"] = new()
        {
            [AppLanguage.ZhHans] = "暂无最近比对记录，开始你的第一次比对吧",
            [AppLanguage.En] = "No recent comparisons yet. Start your first diff above!",
            [AppLanguage.Ja] = "最近の比較履歴はありません。最初の比較を開始してください。"
        },
        ["clearHistory"] = new()
        {
            [AppLanguage.ZhHans] = "一键清除",
            [AppLanguage.En] = "Clear All",
            [AppLanguage.Ja] = "すべて消去"
        },
        ["languageMenu"] = new()
        {
            [AppLanguage.ZhHans] = "语言",
            [AppLanguage.En] = "Language",
            [AppLanguage.Ja] = "言語"
        },
        ["aiSummaryTitle"] = new()
        {
            [AppLanguage.ZhHans] = "AI 改动意图总结 (WinUI 3)",
            [AppLanguage.En] = "AI Change Intent Summary",
            [AppLanguage.Ja] = "AI 変更意図の要約"
        },
        ["copyCommitMsg"] = new()
        {
            [AppLanguage.ZhHans] = "复制 Commit 信息",
            [AppLanguage.En] = "Copy Commit Message",
            [AppLanguage.Ja] = "Commit メッセージをコピー"
        },
        ["reanalyze"] = new()
        {
            [AppLanguage.ZhHans] = "重新分析",
            [AppLanguage.En] = "Reanalyze",
            [AppLanguage.Ja] = "再分析"
        },
        ["settingsTitle"] = new()
        {
            [AppLanguage.ZhHans] = "偏好设置",
            [AppLanguage.En] = "Preferences & Settings",
            [AppLanguage.Ja] = "設定"
        },
        ["done"] = new()
        {
            [AppLanguage.ZhHans] = "完成",
            [AppLanguage.En] = "Done",
            [AppLanguage.Ja] = "完了"
        },
        ["general"] = new()
        {
            [AppLanguage.ZhHans] = "常规",
            [AppLanguage.En] = "General",
            [AppLanguage.Ja] = "一般"
        },
        ["aiEngine"] = new()
        {
            [AppLanguage.ZhHans] = "AI 智能引擎",
            [AppLanguage.En] = "AI Engine",
            [AppLanguage.Ja] = "AI エンジン"
        },
        ["about"] = new()
        {
            [AppLanguage.ZhHans] = "关于",
            [AppLanguage.En] = "About",
            [AppLanguage.Ja] = "情報"
        },
        ["selectLanguage"] = new()
        {
            [AppLanguage.ZhHans] = "界面显示语言",
            [AppLanguage.En] = "App Language",
            [AppLanguage.Ja] = "言語"
        },
        ["appearance"] = new()
        {
            [AppLanguage.ZhHans] = "外观主题",
            [AppLanguage.En] = "Appearance",
            [AppLanguage.Ja] = "外観"
        },
        ["defaultDiffSettings"] = new()
        {
            [AppLanguage.ZhHans] = "默认比对偏好",
            [AppLanguage.En] = "Default Diff Settings",
            [AppLanguage.Ja] = "デフォルト比較設定"
        },
        ["defaultEncoding"] = new()
        {
            [AppLanguage.ZhHans] = "默认文件编码",
            [AppLanguage.En] = "Default File Encoding",
            [AppLanguage.Ja] = "デフォルト文字コード"
        },
        ["coreEngineSection"] = new()
        {
            [AppLanguage.ZhHans] = "核心引擎",
            [AppLanguage.En] = "Core Engine",
            [AppLanguage.Ja] = "コアエンジン"
        },
        ["diffEngine"] = new()
        {
            [AppLanguage.ZhHans] = "比对核心引擎",
            [AppLanguage.En] = "Diff Core Engine",
            [AppLanguage.Ja] = "差分コアエンジン"
        },
        ["currentEngineStatus"] = new()
        {
            [AppLanguage.ZhHans] = "当前引擎状态",
            [AppLanguage.En] = "Current Engine Status",
            [AppLanguage.Ja] = "現在のエンジン状態"
        },
        ["createBakBackupTitle"] = new()
        {
            [AppLanguage.ZhHans] = "保存时自动创建 .bak 备份文件",
            [AppLanguage.En] = "Create .bak backup files when saving",
            [AppLanguage.Ja] = "保存時に .bak バックアップを作成"
        },
        ["createBakBackupDesc"] = new()
        {
            [AppLanguage.ZhHans] = "在覆盖保存前自动复制一份 .bak 文件，防止意外数据丢失。",
            [AppLanguage.En] = "Automatically duplicate original files as .bak before overwriting to prevent accidental data loss.",
            [AppLanguage.Ja] = "上書き保存前に元のファイルを .bak として自動複製し、データの損失を防ぎます。"
        },
        ["autoCheckUpdatesOnLaunch"] = new()
        {
            [AppLanguage.ZhHans] = "启动时自动检查更新",
            [AppLanguage.En] = "Automatically check for updates on launch",
            [AppLanguage.Ja] = "起動時に自動的にアップデートを確認"
        },
        ["checkForUpdates"] = new()
        {
            [AppLanguage.ZhHans] = "检查更新",
            [AppLanguage.En] = "Check for Updates",
            [AppLanguage.Ja] = "アップデートを確認"
        },
        ["checkingForUpdates"] = new()
        {
            [AppLanguage.ZhHans] = "正在检查更新...",
            [AppLanguage.En] = "Checking for updates...",
            [AppLanguage.Ja] = "アップデートを確認中..."
        },
        ["upToDateTitle"] = new()
        {
            [AppLanguage.ZhHans] = "当前已是最新版本 (v1.0.0)",
            [AppLanguage.En] = "You're up to date (v1.0.0)",
            [AppLanguage.Ja] = "最新バージョンです (v1.0.0)"
        },
        ["lastChecked"] = new()
        {
            [AppLanguage.ZhHans] = "上次检查",
            [AppLanguage.En] = "Last checked",
            [AppLanguage.Ja] = "前回の確認"
        },
        ["aiEngineModeLocal"] = new()
        {
            [AppLanguage.ZhHans] = "本地端侧离线模型 (SLM / 零出网 / 100% 隐私安全)",
            [AppLanguage.En] = "Local SLM (100% Offline & Private)",
            [AppLanguage.Ja] = "ローカル SLM (完全オフライン・プライベート)"
        },
        ["aiEngineModeCloud"] = new()
        {
            [AppLanguage.ZhHans] = "云端大模型 API (DeepSeek / OpenAI / 本地 Ollama)",
            [AppLanguage.En] = "Cloud LLM API (DeepSeek / OpenAI / Ollama)",
            [AppLanguage.Ja] = "クラウド LLM API"
        },
        ["aiModelLibrary"] = new()
        {
            [AppLanguage.ZhHans] = "本地模型库",
            [AppLanguage.En] = "Local Model Library",
            [AppLanguage.Ja] = "ローカルモデルライブラリ"
        },
        ["aiAutoReleaseMemory"] = new()
        {
            [AppLanguage.ZhHans] = "自动释放显存/内存",
            [AppLanguage.En] = "Auto-release Memory",
            [AppLanguage.Ja] = "メモリ自動解放"
        },
        ["aiAutoReleaseMemoryDesc"] = new()
        {
            [AppLanguage.ZhHans] = "比对完成闲置 5 分钟后自动卸载模型释放显存与内存，保障系统流畅。",
            [AppLanguage.En] = "Automatically unload model after 5 minutes of inactivity to reclaim VRAM and RAM.",
            [AppLanguage.Ja] = "5分間未使用時にモデルを自動アンロードしメモリを解放します。"
        },
        ["aiCloudPreset"] = new()
        {
            [AppLanguage.ZhHans] = "服务商预设",
            [AppLanguage.En] = "Provider Preset",
            [AppLanguage.Ja] = "プロバイダプリセット"
        },
        ["aiCloudBaseURL"] = new()
        {
            [AppLanguage.ZhHans] = "API Base URL",
            [AppLanguage.En] = "API Base URL",
            [AppLanguage.Ja] = "API Base URL"
        },
        ["aiCloudAPIKey"] = new()
        {
            [AppLanguage.ZhHans] = "API Key",
            [AppLanguage.En] = "API Key",
            [AppLanguage.Ja] = "API Key"
        },
        ["aiCloudModelName"] = new()
        {
            [AppLanguage.ZhHans] = "模型名称",
            [AppLanguage.En] = "Model Name",
            [AppLanguage.Ja] = "モデル名"
        },
        ["aiCloudTestConnection"] = new()
        {
            [AppLanguage.ZhHans] = "测试接口连通性",
            [AppLanguage.En] = "Test Connection",
            [AppLanguage.Ja] = "接続テスト"
        },
        ["aiCloudTesting"] = new()
        {
            [AppLanguage.ZhHans] = "正在测试连通性...",
            [AppLanguage.En] = "Testing connection...",
            [AppLanguage.Ja] = "接続テスト中..."
        },
        ["aiCloudTestSuccess"] = new()
        {
            [AppLanguage.ZhHans] = "连接成功",
            [AppLanguage.En] = "Connection Successful",
            [AppLanguage.Ja] = "接続成功"
        },
        ["aiCloudTestFailed"] = new()
        {
            [AppLanguage.ZhHans] = "连接失败",
            [AppLanguage.En] = "Connection Failed",
            [AppLanguage.Ja] = "接続失敗"
        },
        ["aiModelInUse"] = new()
        {
            [AppLanguage.ZhHans] = "正在使用",
            [AppLanguage.En] = "In Use",
            [AppLanguage.Ja] = "使用中"
        },
        ["aiModelSetDefault"] = new()
        {
            [AppLanguage.ZhHans] = "设为默认",
            [AppLanguage.En] = "Set Default",
            [AppLanguage.Ja] = "デフォルトに設定"
        },
        ["aiDownloadModel"] = new()
        {
            [AppLanguage.ZhHans] = "下载模型",
            [AppLanguage.En] = "Download",
            [AppLanguage.Ja] = "ダウンロード"
        },
        ["aiCleanModel"] = new()
        {
            [AppLanguage.ZhHans] = "清理模型",
            [AppLanguage.En] = "Delete",
            [AppLanguage.Ja] = "削除"
        },
        ["aiCancelDownload"] = new()
        {
            [AppLanguage.ZhHans] = "取消",
            [AppLanguage.En] = "Cancel",
            [AppLanguage.Ja] = "キャンセル"
        },
        ["aiRemainingSeconds"] = new()
        {
            [AppLanguage.ZhHans] = "剩余 {0} 秒",
            [AppLanguage.En] = "{0}s remaining",
            [AppLanguage.Ja] = "残り {0} 秒"
        },
        ["aiRemainingMinutes"] = new()
        {
            [AppLanguage.ZhHans] = "剩余 {0} 分钟",
            [AppLanguage.En] = "{0}m remaining",
            [AppLanguage.Ja] = "残り {0} 分"
        },
        ["aiCalculating"] = new()
        {
            [AppLanguage.ZhHans] = "计算中...",
            [AppLanguage.En] = "Calculating...",
            [AppLanguage.Ja] = "計算中..."
        },
        ["aiPreparing"] = new()
        {
            [AppLanguage.ZhHans] = "准备中...",
            [AppLanguage.En] = "Preparing...",
            [AppLanguage.Ja] = "準備中..."
        },
        ["aiDownloadFailed"] = new()
        {
            [AppLanguage.ZhHans] = "下载失败",
            [AppLanguage.En] = "Download Failed",
            [AppLanguage.Ja] = "ダウンロード失敗"
        },
        ["aiRetry"] = new()
        {
            [AppLanguage.ZhHans] = "重试",
            [AppLanguage.En] = "Retry",
            [AppLanguage.Ja] = "再試行"
        },
        ["aiModelNotReady"] = new()
        {
            [AppLanguage.ZhHans] = "端侧大模型尚未就绪。请在「设置」中点击「下载模型」以启用 Gemma 3 真实大模型深度分析。",
            [AppLanguage.En] = "Local AI model is not ready. Please download the Gemma 3 model in Settings to enable on-device semantic analysis.",
            [AppLanguage.Ja] = "ローカルAIモデルが準備されていません。「設定」からモデルをダウンロードしてください。"
        },
        ["aiModelReady"] = new()
        {
            [AppLanguage.ZhHans] = "就绪",
            [AppLanguage.En] = "Ready",
            [AppLanguage.Ja] = "準備完了"
        },
        ["folderDiff"] = new()
        {
            [AppLanguage.ZhHans] = "文件夹对比",
            [AppLanguage.En] = "Folder Diff",
            [AppLanguage.Ja] = "フォルダ比較"
        },
        ["defaultCompareMode"] = new()
        {
            [AppLanguage.ZhHans] = "默认比对模式",
            [AppLanguage.En] = "Default Compare Mode",
            [AppLanguage.Ja] = "デフォルト比較モード"
        },
        ["quickCompareMode"] = new()
        {
            [AppLanguage.ZhHans] = "快速比对 (时间戳与文件大小)",
            [AppLanguage.En] = "Quick Compare (Timestamp/Size)",
            [AppLanguage.Ja] = "クイック比較 (タイムスタンプ/サイズ)"
        },
        ["deepHashCompareMode"] = new()
        {
            [AppLanguage.ZhHans] = "深度哈希比对 (CRC32 内容校验)",
            [AppLanguage.En] = "Deep Hash Compare (CRC32)",
            [AppLanguage.Ja] = "詳細ハッシュ比較 (CRC32)"
        },
        ["defaultExcludedPatterns"] = new()
        {
            [AppLanguage.ZhHans] = "默认排除过滤规则",
            [AppLanguage.En] = "Default Excluded Patterns",
            [AppLanguage.Ja] = "デフォルト除外パターン"
        },
        ["defaultExcludedPatternsDesc"] = new()
        {
            [AppLanguage.ZhHans] = "在文件夹对比与目录扫描时自动忽略匹配项",
            [AppLanguage.En] = "Automatically ignore matching files or directories during scan",
            [AppLanguage.Ja] = "スキャン時に一致するファイルまたはフォルダを自動的に除外します"
        },
        ["helpTitle"] = new()
        {
            [AppLanguage.ZhHans] = "WinCompare 帮助与用户指南",
            [AppLanguage.En] = "WinCompare Help & User Guide",
            [AppLanguage.Ja] = "WinCompare ヘルプ＆ユーザーガイド"
        },
        ["helpSubtitle"] = new()
        {
            [AppLanguage.ZhHans] = "Windows 11 原生高性能差分比对与合并套件",
            [AppLanguage.En] = "Windows 11 Native High-Performance Diff & Merge Suite",
            [AppLanguage.Ja] = "Windows 11 ネイティブ高速差分比較＆マージツール"
        },
        ["coreFeatures"] = new()
        {
            [AppLanguage.ZhHans] = "核心功能说明",
            [AppLanguage.En] = "Core Features",
            [AppLanguage.Ja] = "主な機能"
        },
        ["shortcuts"] = new()
        {
            [AppLanguage.ZhHans] = "快捷键清单",
            [AppLanguage.En] = "Keyboard Shortcuts",
            [AppLanguage.Ja] = "ショートカット一覧"
        },
        ["aboutArch"] = new()
        {
            [AppLanguage.ZhHans] = "架构与关于",
            [AppLanguage.En] = "Architecture & About",
            [AppLanguage.Ja] = "アーキテクチャと情報"
        },
        ["close"] = new()
        {
            [AppLanguage.ZhHans] = "关闭",
            [AppLanguage.En] = "Close",
            [AppLanguage.Ja] = "閉じる"
        },
        ["copyCommand"] = new()
        {
            [AppLanguage.ZhHans] = "复制配置命令",
            [AppLanguage.En] = "Copy Command",
            [AppLanguage.Ja] = "コマンドをコピー"
        },
        ["copied"] = new()
        {
            [AppLanguage.ZhHans] = "已复制",
            [AppLanguage.En] = "Copied",
            [AppLanguage.Ja] = "コピー完了"
        },
        ["gitMergetoolConfigGuide"] = new()
        {
            [AppLanguage.ZhHans] = "Git 全局 mergetool 配置命令（在终端中运行）：",
            [AppLanguage.En] = "Git Global mergetool Setup Commands (Run in Terminal):",
            [AppLanguage.Ja] = "Git mergetool グローバル設定コマンド（ターミナルで実行）："
        },
        ["textDiffGuideDetail"] = new()
        {
            [AppLanguage.ZhHans] = "• 文件添加：直接拖入两份文件至左右面板，或点击选择。单侧加载保持中性无色，双侧加载后自动触发比对。\n• 差异采纳：点击采纳左/右箭头快速合并差异块，按 Ctrl + S 保存修改。\n• 规则切换：支持快速切换「忽略空白字符」与「忽略大小写」，并在设置中开启自动创建 .bak 备份。",
            [AppLanguage.En] = "• Adding Files: Drag & drop two files into left/right panes, or click 'Choose...'. Single file loads neutrally; both files trigger comparison.\n• Merge & Actions: Click 'Take Left/Right' to merge hunks and Ctrl + S to save.\n• Options: Toggle 'Ignore Whitespace' / 'Ignore Case'; enable '.bak' backups in Settings.",
            [AppLanguage.Ja] = "• ファイル追加：左右のパネルにファイルをドラッグ＆ドロップ、または選択。単側は中立表示、両側で自動差分比較。\n• 差分マージ：「← / → 採用」をクリックして差分を反対側に素早くコピー、Ctrl + S で保存。\n• オプション：空白や大文字/小文字の無視切り替え、設定での「.bak バックアップ自動作成」に対応。"
        },
        ["excelDiffGuideDetail"] = new()
        {
            [AppLanguage.ZhHans] = "• 多 Sheet 对齐：自动识别并对齐工作簿（.xlsx / .xls / .csv / .tsv）中的所有工作表。\n• 高性能流式视口：针对海量大表格流式分页渲染，占用极低内存。支持主键对齐与 Myers LCS 行级智能对齐。\n• 单元格内联高亮：精准着色差异单元格，支持配置浮点数容差与底部明细检查器。",
            [AppLanguage.En] = "• Multi-Sheet Support: Auto-detects and aligns all worksheets across workbooks (.xlsx / .csv / .tsv).\n• Streaming Engine: Key-based column mapping & Myers LCS row alignment with character-level intra-cell diffing.\n• Rules & Inspector: Configurable floating-point numeric tolerance and bottom row detail inspector.",
            [AppLanguage.Ja] = "• 複数シート照合：ワークブック（.xlsx / .csv）内の全シートを自動検出して照合。\n• ストリーミングエンジン：主キーまたは Myers LCS による行揃えとセル内インライン差分表示。\n• 詳細インスペクター：数値の許容誤差設定と下部行詳細インスペクターを搭載。"
        },
        ["wordDiffGuideDetail"] = new()
        {
            [AppLanguage.ZhHans] = "• 富文本段落流对比：精准解析 .docx 文档的段落文字、文字样式（加粗/倾斜/下划线/颜色）与结构变动。\n• 多维可视化差异：支持文档大纲树比对、嵌入表格单元格网格对比、以及基于图片哈希的配图变动检测。",
            [AppLanguage.En] = "• Rich Text Comparison: Compares text paragraphs, styles (bold/italic/underline/color), and structural changes in .docx files.\n• Visual Diff: Inspect differences across document outline, embedded tables, and media images with hash matching.",
            [AppLanguage.Ja] = "• リッチテキスト段落比較：.docx の段落テキスト、書式スタイル（太字/斜体/下線/色）および構造の変更を抽出。\n• ビジュアル差分：見出しアウトライン、埋め込みテーブル、画像ハッシュによる画像差分に対応。"
        },
        ["folderDiffGuideDetail"] = new()
        {
            [AppLanguage.ZhHans] = "• 目录拖拽添加：直接拖入文件夹或在侧边栏选择。支持快速模式（大小与修改时间）与深度哈希模式（CRC32）。\n• 安全演练预演：在物理执行同步前可开启演练预览（Dry-Run），清晰查看所有文件变更计划，防止误删。",
            [AppLanguage.En] = "• Adding Folders: Drag & drop directories or choose from recent history. Supports Quick Mode (size & timestamp) and Deep Hash Mode (CRC32).\n• Safe Sync: Click 'Dry-Run Preview' to inspect all planned file actions before applying physical synchronization.",
            [AppLanguage.Ja] = "• フォルダ追加：フォルダをドラッグ＆ドロップまたは履歴から選択。「簡易比較」と「ハッシュ比較(CRC32)」に対応。\n• 安全な同期：同期を実行する前に「ドライランプレビュー」で変更予定一覧を確認可能。"
        },
        ["threeWayMergeGuideDetail"] = new()
        {
            [AppLanguage.ZhHans] = "• 区域划分：顶部左侧为「本地分支 (Local)」、中间为「基准祖先 (Base)」、右侧为「远程分支 (Remote)」，底部为最终产物。\n• 智能冲突合并：点击「自动解决无冲突项」可自动融合单侧修改，仅需人工审核真正的代码冲突块。\n• Git 终端联动：配置 git mergetool 后，在命令行执行 git mergetool 即可自动唤起并载入文件，轻松化解 rebase 冲突。",
            [AppLanguage.En] = "• Panes Overview: Top panels display Local Branch, Base Ancestor, and Remote Branch. Bottom panel displays Merged Output.\n• Auto-Resolve: Click 'Auto-Resolve Non-Conflicts' to merge clean changes automatically, only resolving true conflicts manually.\n• Git Integration: Run 'git mergetool' from terminal to auto-populate files and resolve conflicts seamlessly.",
            [AppLanguage.Ja] = "• パネル構成：上部は「ローカル」、「共通祖先(Base)」、「リモート」、下部は「マージ結果」です。\n• 自動解決：「競合なしを自動解決」で片側の変更を自動適用し、競合行のみを手動選択。\n• Git 連携：'git mergetool' を実行するだけで自動入力され、コンフリクトをスムーズに解決。"
        },
        ["aiAssistantGuideDetail"] = new()
        {
            [AppLanguage.ZhHans] = "• 本地 100% 隐私安全：集成 Google Gemma 3 端侧离线大模型，零网络请求，商业敏感代码放心分析。\n• 智能语义摘要：自动提炼代码改动背后的业务意图，一键生成规范的 Conventional Commits 提交信息。",
            [AppLanguage.En] = "• 100% Offline & Private: Integrated Google Gemma 3 local SLM with zero network requests—safe for enterprise code.\n• Semantic Summary: Automatically summarizes the intent behind changes and generates Conventional Commits messages.",
            [AppLanguage.Ja] = "• 完全オフライン＆高プライバシー：Google Gemma 3 ローカルモデルを統合。社内機密コードも安全に分析可能。\n• 変更意図の要約：差分の背景にある意図を自動解析し、Conventional Commits 準拠のメッセージを生成。"
        },
        ["threeWayMerge"] = new()
        {
            [AppLanguage.ZhHans] = "三向合并",
            [AppLanguage.En] = "3-Way Merge",
            [AppLanguage.Ja] = "3方向マージ"
        },
        ["newTextCompare"] = new()
        {
            [AppLanguage.ZhHans] = "新建文本对比",
            [AppLanguage.En] = "New Text Diff",
            [AppLanguage.Ja] = "新規テキスト比較"
        },
        ["openFile"] = new()
        {
            [AppLanguage.ZhHans] = "打开文件",
            [AppLanguage.En] = "Open Files",
            [AppLanguage.Ja] = "ファイルを開く"
        },
        ["pickLeftFile"] = new()
        {
            [AppLanguage.ZhHans] = "选择左侧文件...",
            [AppLanguage.En] = "Select Left File...",
            [AppLanguage.Ja] = "左ファイルを選択..."
        },
        ["pickRightFile"] = new()
        {
            [AppLanguage.ZhHans] = "选择右侧文件...",
            [AppLanguage.En] = "Select Right File...",
            [AppLanguage.Ja] = "右ファイルを選択..."
        },
        ["saveLeftFile"] = new()
        {
            [AppLanguage.ZhHans] = "保存左侧文件",
            [AppLanguage.En] = "Save Left File",
            [AppLanguage.Ja] = "左ファイルを保存"
        },
        ["saveRightFile"] = new()
        {
            [AppLanguage.ZhHans] = "保存右侧文件",
            [AppLanguage.En] = "Save Right File",
            [AppLanguage.Ja] = "右ファイルを保存"
        },
        ["prevDiff"] = new()
        {
            [AppLanguage.ZhHans] = "上一处差异",
            [AppLanguage.En] = "Previous Diff",
            [AppLanguage.Ja] = "前の差分"
        },
        ["nextDiff"] = new()
        {
            [AppLanguage.ZhHans] = "下一处差异",
            [AppLanguage.En] = "Next Diff",
            [AppLanguage.Ja] = "次の差分"
        },
        ["ignoreWhitespace"] = new()
        {
            [AppLanguage.ZhHans] = "忽略空格",
            [AppLanguage.En] = "Ignore Whitespace",
            [AppLanguage.Ja] = "空白を無視"
        },
        ["ignoreCase"] = new()
        {
            [AppLanguage.ZhHans] = "忽略大小写",
            [AppLanguage.En] = "Ignore Case",
            [AppLanguage.Ja] = "大文字/小文字を無視"
        },
        ["viewDiffMode"] = new()
        {
            [AppLanguage.ZhHans] = "视图: 对齐差异",
            [AppLanguage.En] = "View: Aligned Diff",
            [AppLanguage.Ja] = "表示: 差分整列"
        },
        ["viewEditMode"] = new()
        {
            [AppLanguage.ZhHans] = "视图: 自由编辑",
            [AppLanguage.En] = "View: Free Edit",
            [AppLanguage.Ja] = "表示: 編集"
        },
        ["browse"] = new()
        {
            [AppLanguage.ZhHans] = "浏览",
            [AppLanguage.En] = "Browse",
            [AppLanguage.Ja] = "参照"
        },
        ["aiDiffSummary"] = new()
        {
            [AppLanguage.ZhHans] = "AI 改动总结",
            [AppLanguage.En] = "AI Summary",
            [AppLanguage.Ja] = "AI 要約"
        },
        ["statusAdded"] = new()
        {
            [AppLanguage.ZhHans] = "新增",
            [AppLanguage.En] = "Added",
            [AppLanguage.Ja] = "追加"
        },
        ["statusDeleted"] = new()
        {
            [AppLanguage.ZhHans] = "删除",
            [AppLanguage.En] = "Deleted",
            [AppLanguage.Ja] = "削除"
        },
        ["statusModified"] = new()
        {
            [AppLanguage.ZhHans] = "修改",
            [AppLanguage.En] = "Modified",
            [AppLanguage.Ja] = "変更"
        },
        ["statusIdentical"] = new()
        {
            [AppLanguage.ZhHans] = "一致",
            [AppLanguage.En] = "Identical",
            [AppLanguage.Ja] = "一致"
        },
        ["filterAll"] = new()
        {
            [AppLanguage.ZhHans] = "全部",
            [AppLanguage.En] = "All",
            [AppLanguage.Ja] = "すべて"
        },
        ["filterDiffOnly"] = new()
        {
            [AppLanguage.ZhHans] = "仅差异",
            [AppLanguage.En] = "Diffs Only",
            [AppLanguage.Ja] = "差分のみ"
        },
        ["filterModified"] = new()
        {
            [AppLanguage.ZhHans] = "修改",
            [AppLanguage.En] = "Modified",
            [AppLanguage.Ja] = "変更"
        },
        ["filterAdded"] = new()
        {
            [AppLanguage.ZhHans] = "新增",
            [AppLanguage.En] = "Added",
            [AppLanguage.Ja] = "追加"
        },
        ["filterDeleted"] = new()
        {
            [AppLanguage.ZhHans] = "删除",
            [AppLanguage.En] = "Deleted",
            [AppLanguage.Ja] = "削除"
        },
        ["swapFolders"] = new()
        {
            [AppLanguage.ZhHans] = "交换左右目录",
            [AppLanguage.En] = "Swap Folders",
            [AppLanguage.Ja] = "左右フォルダを入れ替え"
        },
        ["swapSides"] = new()
        {
            [AppLanguage.ZhHans] = "交换左右",
            [AppLanguage.En] = "Swap Sides",
            [AppLanguage.Ja] = "左右を入れ替え"
        },
        ["clearAll"] = new()
        {
            [AppLanguage.ZhHans] = "清空比对",
            [AppLanguage.En] = "Clear All",
            [AppLanguage.Ja] = "すべてクリア"
        },
        ["exportDiff"] = new()
        {
            [AppLanguage.ZhHans] = "导出差异",
            [AppLanguage.En] = "Export Diff",
            [AppLanguage.Ja] = "差分を出力"
        },
        ["searchOrFilter"] = new()
        {
            [AppLanguage.ZhHans] = "搜索或过滤...",
            [AppLanguage.En] = "Search or filter...",
            [AppLanguage.Ja] = "検索またはフィルター..."
        },
        ["exportSuccess"] = new()
        {
            [AppLanguage.ZhHans] = "差异报告已复制到剪贴板！",
            [AppLanguage.En] = "Diff report copied to clipboard!",
            [AppLanguage.Ja] = "差分レポートをクリップボードにコピーしました！"
        },
        ["prevConflict"] = new()
        {
            [AppLanguage.ZhHans] = "上一个冲突",
            [AppLanguage.En] = "Previous Conflict",
            [AppLanguage.Ja] = "前の競合"
        },
        ["nextConflict"] = new()
        {
            [AppLanguage.ZhHans] = "下一个冲突",
            [AppLanguage.En] = "Next Conflict",
            [AppLanguage.Ja] = "次の競合"
        },
        ["autoResolveNonConflicts"] = new()
        {
            [AppLanguage.ZhHans] = "自动解决非冲突",
            [AppLanguage.En] = "Auto Resolve Non-conflicts",
            [AppLanguage.Ja] = "競合以外を自動解決"
        },
        ["takeBoth"] = new()
        {
            [AppLanguage.ZhHans] = "保留双方修改",
            [AppLanguage.En] = "Keep Both Changes",
            [AppLanguage.Ja] = "両方の変更を保持"
        },
        ["saveMergeSuccess"] = new()
        {
            [AppLanguage.ZhHans] = "合并结果已成功保存！",
            [AppLanguage.En] = "Merged file saved successfully!",
            [AppLanguage.Ja] = "マージ結果を保存しました！"
        },
        ["noConflictsToResolve"] = new()
        {
            [AppLanguage.ZhHans] = "当前无冲突需要处理",
            [AppLanguage.En] = "No conflicts to resolve",
            [AppLanguage.Ja] = "解決する競合はありません"
        },
        ["newVersionFoundFormat"] = new()
        {
            [AppLanguage.ZhHans] = "🎉 发现新版本: {0} (当前: {1})",
            [AppLanguage.En] = "🎉 New version available: {0} (Current: {1})",
            [AppLanguage.Ja] = "🎉 新しいバージョンが見つかりました: {0} (現在: {1})"
        },
        ["textDiffSummaryZero"] = new()
        {
            [AppLanguage.ZhHans] = "0 处新增 · 0 处删除 · 0 处修改",
            [AppLanguage.En] = "0 added · 0 deleted · 0 modified",
            [AppLanguage.Ja] = "0 件追加 · 0 件削除 · 0 件変更"
        },
        ["excelDiffReportTitle"] = new()
        {
            [AppLanguage.ZhHans] = "# Excel 差异比对报告 - {0} vs {1}",
            [AppLanguage.En] = "# Excel Diff Report - {0} vs {1}",
            [AppLanguage.Ja] = "# Excel 差分比較レポート - {0} vs {1}"
        },
        ["wordDiffReportTitle"] = new()
        {
            [AppLanguage.ZhHans] = "# Word 差异比对报告 - {0} vs {1}",
            [AppLanguage.En] = "# Word Diff Report - {0} vs {1}",
            [AppLanguage.Ja] = "# Word 差分比較レポート - {0} vs {1}"
        },
        ["folderDiffReportTitle"] = new()
        {
            [AppLanguage.ZhHans] = "# 文件夹差异比对报告 - {0} vs {1}",
            [AppLanguage.En] = "# Folder Diff Report - {0} vs {1}",
            [AppLanguage.Ja] = "# フォルダ差分比較レポート - {0} vs {1}"
        },
        ["exportTimeLabel"] = new()
        {
            [AppLanguage.ZhHans] = "导出时间: {0}",
            [AppLanguage.En] = "Export Time: {0}",
            [AppLanguage.Ja] = "出力日時: {0}"
        },
        ["reportHeaderRow"] = new()
        {
            [AppLanguage.ZhHans] = "行号",
            [AppLanguage.En] = "Row",
            [AppLanguage.Ja] = "行"
        },
        ["reportHeaderSection"] = new()
        {
            [AppLanguage.ZhHans] = "段落章节",
            [AppLanguage.En] = "Section",
            [AppLanguage.Ja] = "セクション"
        },
        ["reportHeaderStatus"] = new()
        {
            [AppLanguage.ZhHans] = "状态",
            [AppLanguage.En] = "Status",
            [AppLanguage.Ja] = "状態"
        },
        ["reportHeaderLeft"] = new()
        {
            [AppLanguage.ZhHans] = "左侧",
            [AppLanguage.En] = "Left",
            [AppLanguage.Ja] = "左側"
        },
        ["reportHeaderRight"] = new()
        {
            [AppLanguage.ZhHans] = "右侧",
            [AppLanguage.En] = "Right",
            [AppLanguage.Ja] = "右側"
        },
        ["reportHeaderPath"] = new()
        {
            [AppLanguage.ZhHans] = "相对路径",
            [AppLanguage.En] = "Relative Path",
            [AppLanguage.Ja] = "相対パス"
        },
        ["reportHeaderLeftSize"] = new()
        {
            [AppLanguage.ZhHans] = "左侧大小",
            [AppLanguage.En] = "Left Size",
            [AppLanguage.Ja] = "左側サイズ"
        },
        ["reportHeaderRightSize"] = new()
        {
            [AppLanguage.ZhHans] = "右侧大小",
            [AppLanguage.En] = "Right Size",
            [AppLanguage.Ja] = "右側サイズ"
        },
        ["aiEndpointEmpty"] = new()
        {
            [AppLanguage.ZhHans] = "Endpoint URL 不能为空",
            [AppLanguage.En] = "Endpoint URL cannot be empty",
            [AppLanguage.Ja] = "エンドポイントURLを空にすることはできません"
        },
        ["aiTimeout"] = new()
        {
            [AppLanguage.ZhHans] = "请求超时 (Timeout)",
            [AppLanguage.En] = "Request timeout",
            [AppLanguage.Ja] = "リクエストタイムアウト"
        },
        ["aiConfigApiKeyTip"] = new()
        {
            [AppLanguage.ZhHans] = "请在「设置」->「AI 智能引擎」中填入有效的 API Key",
            [AppLanguage.En] = "Please configure a valid API Key in Settings -> AI Engine",
            [AppLanguage.Ja] = "「設定」->「AI エンジン」で有効な API キーを入力してください"
        },
        ["aiConfigCompatibleTip"] = new()
        {
            [AppLanguage.ZhHans] = "支持 DeepSeek, OpenAI, Claude, Ollama 等兼容 OpenAI 协议的大模型",
            [AppLanguage.En] = "Compatible with DeepSeek, OpenAI, Claude, Ollama, and OpenAI-compatible models",
            [AppLanguage.Ja] = "DeepSeek、OpenAI、Claude、Ollama などの互換モデルに対応"
        },
        ["aiConfigLocalTip"] = new()
        {
            [AppLanguage.ZhHans] = "也可在设置中切换为「本地端侧离线模型」模式零成本使用",
            [AppLanguage.En] = "Or switch to Local Offline Model mode in Settings for free, on-device usage",
            [AppLanguage.Ja] = "または設定で「ローカルオフラインモデル」に切り替えてコストゼロで使用可能"
        },
        ["aiDownloadGemmaTip"] = new()
        {
            [AppLanguage.ZhHans] = "前往「设置」->「AI 智能引擎」一键下载 Gemma 3 本地端侧离线模型",
            [AppLanguage.En] = "Go to Settings -> AI Engine to download Gemma 3 local model",
            [AppLanguage.Ja] = "「設定」->「AI エンジン」で Gemma 3 ローカルモデルをワンクリックでダウンロード"
        },
        ["aiModelStatusNotReady"] = new()
        {
            [AppLanguage.ZhHans] = "未就绪",
            [AppLanguage.En] = "Not Ready",
            [AppLanguage.Ja] = "未準備"
        },
        ["aiModelStatusLocalReady"] = new()
        {
            [AppLanguage.ZhHans] = "GGUF 本地就绪",
            [AppLanguage.En] = "GGUF Local Ready",
            [AppLanguage.Ja] = "GGUF ローカル準備完了"
        },
        ["aiModelOfflineLoaded"] = new()
        {
            [AppLanguage.ZhHans] = "{0} 离线权重已加载 ({1})",
            [AppLanguage.En] = "{0} offline weights loaded ({1})",
            [AppLanguage.Ja] = "{0} オフライン重み読み込み完了 ({1})"
        },
        ["aiSummaryCommitNoChanges"] = new()
        {
            [AppLanguage.ZhHans] = "chore: 无改动检测",
            [AppLanguage.En] = "chore: no changes detected",
            [AppLanguage.Ja] = "chore: 変更は検出されませんでした"
        },
        ["aiSummaryCommitConfigKey"] = new()
        {
            [AppLanguage.ZhHans] = "chore: 请在设置中配置 API Key",
            [AppLanguage.En] = "chore: configure api key in settings",
            [AppLanguage.Ja] = "chore: 設定で API キーを設定してください"
        },
        ["aiSummaryCommitUpdate"] = new()
        {
            [AppLanguage.ZhHans] = "chore: 更新实现",
            [AppLanguage.En] = "chore: update implementation",
            [AppLanguage.Ja] = "chore: 実装を更新"
        },
        ["aiSummaryCommitError"] = new()
        {
            [AppLanguage.ZhHans] = "chore: 分析异常",
            [AppLanguage.En] = "chore: error in analysis",
            [AppLanguage.Ja] = "chore: 解析エラー"
        },
        ["aiSummaryCommitUpdateDesc"] = new()
        {
            [AppLanguage.ZhHans] = "更新代码实现",
            [AppLanguage.En] = "update implementation",
            [AppLanguage.Ja] = "コード実装を更新"
        },
        ["fileTypeTextCode"] = new()
        {
            [AppLanguage.ZhHans] = "文本与代码文件",
            [AppLanguage.En] = "Text / Code Files",
            [AppLanguage.Ja] = "テキスト・コードファイル"
        },
        ["fileTypeText"] = new()
        {
            [AppLanguage.ZhHans] = "文本文件",
            [AppLanguage.En] = "Text File",
            [AppLanguage.Ja] = "テキストファイル"
        },
        ["fileTypeAll"] = new()
        {
            [AppLanguage.ZhHans] = "所有文件",
            [AppLanguage.En] = "All Files",
            [AppLanguage.Ja] = "すべてのファイル"
        },
        ["aiModelCloudDefault"] = new()
        {
            [AppLanguage.ZhHans] = "云端大模型 API",
            [AppLanguage.En] = "Cloud API",
            [AppLanguage.Ja] = "クラウド API"
        },
        ["aiModelGemmaLocal"] = new()
        {
            [AppLanguage.ZhHans] = "Gemma 3 (端侧本地)",
            [AppLanguage.En] = "Gemma 3 (Local)",
            [AppLanguage.Ja] = "Gemma 3 (ローカル)"
        },
        ["aiModelDiffEngine"] = new()
        {
            [AppLanguage.ZhHans] = "Diff 规则引擎",
            [AppLanguage.En] = "DiffEngine",
            [AppLanguage.Ja] = "差分エンジン"
        },
        ["aiSummaryLocalIntent"] = new()
        {
            [AppLanguage.ZhHans] = "端侧 AI 意图分析完成",
            [AppLanguage.En] = "On-device AI analysis completed",
            [AppLanguage.Ja] = "ローカルAIによる意図分析完了"
        },
        ["aiCloudCallFailed"] = new()
        {
            [AppLanguage.ZhHans] = "云端大模型调用失败: {0}",
            [AppLanguage.En] = "Cloud AI call failed: {0}",
            [AppLanguage.Ja] = "クラウドAIの呼び出しに失敗しました: {0}"
        },
        ["aiCloudCheckSettings"] = new()
        {
            [AppLanguage.ZhHans] = "请在「设置」->「AI 智能引擎」中检查 API Base URL 与 API Key",
            [AppLanguage.En] = "Please check API Base URL and API Key in Settings -> AI Engine",
            [AppLanguage.Ja] = "「設定」->「AI エンジン」で API Base URL と API キーを確認してください"
        },
        ["startCompare"] = new()
        {
            [AppLanguage.ZhHans] = "开始比对",
            [AppLanguage.En] = "Compare",
            [AppLanguage.Ja] = "比較開始"
        },
        ["syncAction"] = new()
        {
            [AppLanguage.ZhHans] = "单向/双向同步",
            [AppLanguage.En] = "Sync Actions",
            [AppLanguage.Ja] = "同期"
        },
        ["syncLeftToRight"] = new()
        {
            [AppLanguage.ZhHans] = "左侧同步至右侧 (Left -> Right)",
            [AppLanguage.En] = "Sync Left to Right",
            [AppLanguage.Ja] = "左から右へ同期"
        },
        ["syncRightToLeft"] = new()
        {
            [AppLanguage.ZhHans] = "右侧同步至左侧 (Right -> Left)",
            [AppLanguage.En] = "Sync Right to Left",
            [AppLanguage.Ja] = "右から左へ同期"
        },
        ["quickMode"] = new()
        {
            [AppLanguage.ZhHans] = "快速模式 (大小+时间)",
            [AppLanguage.En] = "Quick (Size & Time)",
            [AppLanguage.Ja] = "クイック (サイズ/時刻)"
        },
        ["deepHashMode"] = new()
        {
            [AppLanguage.ZhHans] = "深度哈希 (CRC32/SHA256)",
            [AppLanguage.En] = "Deep Hash (CRC32)",
            [AppLanguage.Ja] = "詳細ハッシュ (CRC32)"
        },
        ["recomputeMerge"] = new()
        {
            [AppLanguage.ZhHans] = "重新合并",
            [AppLanguage.En] = "Re-merge",
            [AppLanguage.Ja] = "再マージ"
        },
        ["saveMergeResult"] = new()
        {
            [AppLanguage.ZhHans] = "保存合并结果",
            [AppLanguage.En] = "Save Merged",
            [AppLanguage.Ja] = "マージ結果を保存"
        },
        ["acceptAllLocal"] = new()
        {
            [AppLanguage.ZhHans] = "全部采用本地",
            [AppLanguage.En] = "Accept All Local",
            [AppLanguage.Ja] = "すべてローカル採用"
        },
        ["acceptAllRemote"] = new()
        {
            [AppLanguage.ZhHans] = "全部采用远端",
            [AppLanguage.En] = "Accept All Remote",
            [AppLanguage.Ja] = "すべてリモート採用"
        },
        ["worksheet"] = new()
        {
            [AppLanguage.ZhHans] = "工作表 (Sheet)",
            [AppLanguage.En] = "Worksheet (Sheet)",
            [AppLanguage.Ja] = "ワークシート"
        },
        ["docOutline"] = new()
        {
            [AppLanguage.ZhHans] = "文档大纲结构",
            [AppLanguage.En] = "Document Outline",
            [AppLanguage.Ja] = "文書アウトライン"
        },
        ["viewStructuredContent"] = new()
        {
            [AppLanguage.ZhHans] = "正文结构比对",
            [AppLanguage.En] = "Content Diff",
            [AppLanguage.Ja] = "本文構造比較"
        },
        ["viewTableDiff"] = new()
        {
            [AppLanguage.ZhHans] = "表格比对",
            [AppLanguage.En] = "Table Diff",
            [AppLanguage.Ja] = "表比較"
        },
        ["viewMetadataDiff"] = new()
        {
            [AppLanguage.ZhHans] = "文档元数据",
            [AppLanguage.En] = "Metadata",
            [AppLanguage.Ja] = "メタデータ"
        },
        ["pickLeftFolder"] = new()
        {
            [AppLanguage.ZhHans] = "选择左侧目录...",
            [AppLanguage.En] = "Select Left Folder...",
            [AppLanguage.Ja] = "左フォルダを選択..."
        },
        ["pickRightFolder"] = new()
        {
            [AppLanguage.ZhHans] = "选择右侧目录...",
            [AppLanguage.En] = "Select Right Folder...",
            [AppLanguage.Ja] = "右フォルダを選択..."
        },
        ["leftFolderPrompt"] = new()
        {
            [AppLanguage.ZhHans] = "拖入或选择左侧源目录",
            [AppLanguage.En] = "Drop or select left folder",
            [AppLanguage.Ja] = "左側のフォルダをドロップまたは選択"
        },
        ["rightFolderPrompt"] = new()
        {
            [AppLanguage.ZhHans] = "拖入或选择右侧目标目录",
            [AppLanguage.En] = "Drop or select right folder",
            [AppLanguage.Ja] = "右側のフォルダをドロップまたは選択"
        },
        ["folderColStatus"] = new()
        {
            [AppLanguage.ZhHans] = "状态",
            [AppLanguage.En] = "Status",
            [AppLanguage.Ja] = "状態"
        },
        ["folderColPath"] = new()
        {
            [AppLanguage.ZhHans] = "相对路径 (双击打开详细比对)",
            [AppLanguage.En] = "Relative Path (Double-click to diff)",
            [AppLanguage.Ja] = "相対パス (ダブルクリックで詳細比較)"
        },
        ["folderColLeftSize"] = new()
        {
            [AppLanguage.ZhHans] = "左侧大小",
            [AppLanguage.En] = "Left Size",
            [AppLanguage.Ja] = "左サイズ"
        },
        ["folderColRightSize"] = new()
        {
            [AppLanguage.ZhHans] = "右侧大小",
            [AppLanguage.En] = "Right Size",
            [AppLanguage.Ja] = "右サイズ"
        },
        ["folderEmptyPrompt"] = new()
        {
            [AppLanguage.ZhHans] = "请选择左侧和右侧目录以开始比对",
            [AppLanguage.En] = "Select left and right folders to compare",
            [AppLanguage.Ja] = "比較する左右のフォルダを選択してください"
        },
        ["folderEmptyAction"] = new()
        {
            [AppLanguage.ZhHans] = "选择两端目录并比对",
            [AppLanguage.En] = "Select Folders & Compare",
            [AppLanguage.Ja] = "フォルダを選択して比較"
        },
        ["folderSummaryStats"] = new()
        {
            [AppLanguage.ZhHans] = "共比对 {0} 项 · {1} 处修改 · {2} 处新增 · {3} 处删除 · {4} 处完全一致",
            [AppLanguage.En] = "Total {0} items · {1} modified · {2} added · {3} deleted · {4} identical",
            [AppLanguage.Ja] = "合計 {0} 件 · 変更 {1} · 追加 {2} · 削除 {3} · 一致 {4}"
        },
        ["syncStatusReady"] = new()
        {
            [AppLanguage.ZhHans] = "双向就绪",
            [AppLanguage.En] = "Sync Ready",
            [AppLanguage.Ja] = "同期準備完了"
        },
        ["syncedLeftToRight"] = new()
        {
            [AppLanguage.ZhHans] = "已从左侧同步 {0} 个文件至右侧",
            [AppLanguage.En] = "Synced {0} files from left to right",
            [AppLanguage.Ja] = "左から右へ {0} 件のファイルを同期しました"
        },
        ["syncedRightToLeft"] = new()
        {
            [AppLanguage.ZhHans] = "已从右侧同步 {0} 个文件至左侧",
            [AppLanguage.En] = "Synced {0} files from right to left",
            [AppLanguage.Ja] = "右から左へ {0} 件のファイルを同期しました"
        },
        ["openExcelFiles"] = new()
        {
            [AppLanguage.ZhHans] = "打开表格文件...",
            [AppLanguage.En] = "Open Spreadsheets...",
            [AppLanguage.Ja] = "スプレッドシートを開く..."
        },
        ["excelColRow"] = new()
        {
            [AppLanguage.ZhHans] = "行号",
            [AppLanguage.En] = "Row",
            [AppLanguage.Ja] = "行"
        },
        ["excelColStatus"] = new()
        {
            [AppLanguage.ZhHans] = "状态",
            [AppLanguage.En] = "Status",
            [AppLanguage.Ja] = "状態"
        },
        ["excelEmptyPrompt"] = new()
        {
            [AppLanguage.ZhHans] = "请选择两份 Excel / CSV 表格以开始智能对比",
            [AppLanguage.En] = "Select two Excel / CSV files to start comparison",
            [AppLanguage.Ja] = "比較する2つのExcel / CSVファイルを選択してください"
        },
        ["excelEmptyAction"] = new()
        {
            [AppLanguage.ZhHans] = "选择表格文件并比对",
            [AppLanguage.En] = "Select Spreadsheets & Compare",
            [AppLanguage.Ja] = "スプレッドシートを選択して比較"
        },
        ["excelSummaryStats"] = new()
        {
            [AppLanguage.ZhHans] = "工作表: {0} · 共比对 {1} 行 · 发现 {2} 行差异",
            [AppLanguage.En] = "Sheet: {0} · Total {1} rows · {2} row diffs",
            [AppLanguage.Ja] = "シート: {0} · 合計 {1} 行 · 差異 {2} 行"
        },
        ["openWordFiles"] = new()
        {
            [AppLanguage.ZhHans] = "打开 Word 文档...",
            [AppLanguage.En] = "Open Word Documents...",
            [AppLanguage.Ja] = "Word 文書を開く..."
        },
        ["wordEmptyPrompt"] = new()
        {
            [AppLanguage.ZhHans] = "请选择两份 Word 文档以开始结构化对比",
            [AppLanguage.En] = "Select two Word documents to start structured comparison",
            [AppLanguage.Ja] = "比較する2つのWord文書を選択してください"
        },
        ["wordEmptyAction"] = new()
        {
            [AppLanguage.ZhHans] = "选择 Word 文档并比对",
            [AppLanguage.En] = "Select Word Docs & Compare",
            [AppLanguage.Ja] = "Word 文書を選択して比較"
        },
        ["wordSummaryStats"] = new()
        {
            [AppLanguage.ZhHans] = "共比对 {0} 个段落 · 提取 {1} 个大纲节点",
            [AppLanguage.En] = "Total {0} paragraphs · Extracted {1} outline nodes",
            [AppLanguage.Ja] = "合計 {0} 段落 · {1} 個のアウトライン項目を抽出"
        },
        ["openThreeWayFiles"] = new()
        {
            [AppLanguage.ZhHans] = "打开三方文件...",
            [AppLanguage.En] = "Open 3-Way Files...",
            [AppLanguage.Ja] = "3方向ファイルを開く..."
        },
        ["mergeLocalHeader"] = new()
        {
            [AppLanguage.ZhHans] = "本地修改 (Local / Ours)",
            [AppLanguage.En] = "Local Changes (Ours)",
            [AppLanguage.Ja] = "ローカル変更 (Ours)"
        },
        ["mergeBaseHeader"] = new()
        {
            [AppLanguage.ZhHans] = "共同祖先 (Base)",
            [AppLanguage.En] = "Common Ancestor (Base)",
            [AppLanguage.Ja] = "共通祖先 (Base)"
        },
        ["mergeRemoteHeader"] = new()
        {
            [AppLanguage.ZhHans] = "远程分支 (Remote / Theirs)",
            [AppLanguage.En] = "Remote Changes (Theirs)",
            [AppLanguage.Ja] = "リモート変更 (Theirs)"
        },
        ["mergeResultHeader"] = new()
        {
            [AppLanguage.ZhHans] = "合并结果 (Merged Result)",
            [AppLanguage.En] = "Merged Output",
            [AppLanguage.Ja] = "マージ結果"
        },
        ["mergeConflictsSummary"] = new()
        {
            [AppLanguage.ZhHans] = "共产生 {0} 处冲突 · 当前已解决 {1} 处",
            [AppLanguage.En] = "{0} conflicts total · {1} resolved",
            [AppLanguage.Ja] = "合計 {0} 件の衝突 · {1} 件解決済み"
        },
        ["refreshCompare"] = new()
        {
            [AppLanguage.ZhHans] = "刷新比对",
            [AppLanguage.En] = "Refresh",
            [AppLanguage.Ja] = "更新"
        },
        ["diffCountBadge"] = new()
        {
            [AppLanguage.ZhHans] = "{0} 处差异",
            [AppLanguage.En] = "{0} diffs",
            [AppLanguage.Ja] = "{0} 箇所の差分"
        },
        ["diffCountDocBadge"] = new()
        {
            [AppLanguage.ZhHans] = "{0} 处段落差异",
            [AppLanguage.En] = "{0} paragraph diffs",
            [AppLanguage.Ja] = "{0} 段落の差分"
        },
        ["leftExcelPrompt"] = new()
        {
            [AppLanguage.ZhHans] = "拖入或选择左侧 Excel / CSV 文件",
            [AppLanguage.En] = "Drop or select left Excel / CSV file",
            [AppLanguage.Ja] = "左側のExcel / CSVファイルをドロップまたは選択"
        },
        ["rightExcelPrompt"] = new()
        {
            [AppLanguage.ZhHans] = "拖入或选择右侧 Excel / CSV 文件",
            [AppLanguage.En] = "Drop or select right Excel / CSV file",
            [AppLanguage.Ja] = "右側のExcel / CSVファイルをドロップまたは選択"
        },
        ["leftDocPrompt"] = new()
        {
            [AppLanguage.ZhHans] = "拖入或选择左侧原文档 (.docx)",
            [AppLanguage.En] = "Drop or select left Word doc (.docx)",
            [AppLanguage.Ja] = "左側のWord文書 (.docx) をドロップまたは選択"
        },
        ["rightDocPrompt"] = new()
        {
            [AppLanguage.ZhHans] = "拖入或选择右侧修订文档 (.docx)",
            [AppLanguage.En] = "Drop or select right Word doc (.docx)",
            [AppLanguage.Ja] = "右側のWord文書 (.docx) をドロップまたは選択"
        },
        ["leftColContent"] = new()
        {
            [AppLanguage.ZhHans] = "左侧单元格内容",
            [AppLanguage.En] = "Left Cell Content",
            [AppLanguage.Ja] = "左セル内容"
        },
        ["rightColContent"] = new()
        {
            [AppLanguage.ZhHans] = "右侧单元格内容",
            [AppLanguage.En] = "Right Cell Content",
            [AppLanguage.Ja] = "右セル内容"
        },
        ["pickLeftExcel"] = new()
        {
            [AppLanguage.ZhHans] = "选择左侧表格 (Left)...",
            [AppLanguage.En] = "Select Left Spreadsheet...",
            [AppLanguage.Ja] = "左の表を選択..."
        },
        ["pickRightExcel"] = new()
        {
            [AppLanguage.ZhHans] = "选择右侧表格 (Right)...",
            [AppLanguage.En] = "Select Right Spreadsheet...",
            [AppLanguage.Ja] = "右の表を選択..."
        },
        ["pickLeftDoc"] = new()
        {
            [AppLanguage.ZhHans] = "选择左侧文档 (.docx)...",
            [AppLanguage.En] = "Select Left Document...",
            [AppLanguage.Ja] = "左の文書を選択..."
        },
        ["pickRightDoc"] = new()
        {
            [AppLanguage.ZhHans] = "选择右侧文档 (.docx)...",
            [AppLanguage.En] = "Select Right Document...",
            [AppLanguage.Ja] = "右の文書を選択..."
        },
        ["inspectorTitle"] = new()
        {
            [AppLanguage.ZhHans] = "单元格明细检查器 (点击任意行查看)",
            [AppLanguage.En] = "Cell Detail Inspector (Click any row)",
            [AppLanguage.Ja] = "セル詳細インスペクター (行をクリック)"
        },
        ["inspectorNoSelection"] = new()
        {
            [AppLanguage.ZhHans] = "尚未选择行",
            [AppLanguage.En] = "No row selected",
            [AppLanguage.Ja] = "行が選択されていません"
        },
        ["notLoaded"] = new()
        {
            [AppLanguage.ZhHans] = "未加载",
            [AppLanguage.En] = "Not loaded",
            [AppLanguage.Ja] = "未読み込み"
        },
        ["notCompared"] = new()
        {
            [AppLanguage.ZhHans] = "未比对",
            [AppLanguage.En] = "Not compared",
            [AppLanguage.Ja] = "未比較"
        },
        ["metaTitle"] = new()
        {
            [AppLanguage.ZhHans] = "文档属性与元数据对比",
            [AppLanguage.En] = "Document Properties & Metadata",
            [AppLanguage.Ja] = "文書プロパティとメタデータ比較"
        },
        ["metaNotLoaded"] = new()
        {
            [AppLanguage.ZhHans] = "尚未载入文档",
            [AppLanguage.En] = "No document loaded",
            [AppLanguage.Ja] = "文書が読み込まれていません"
        },
        ["mergeOutputHeader"] = new()
        {
            [AppLanguage.ZhHans] = "最终合并预览 (Merged Output)",
            [AppLanguage.En] = "Merged Output Preview",
            [AppLanguage.Ja] = "マージ結果プレビュー"
        },
        ["saveToFile"] = new()
        {
            [AppLanguage.ZhHans] = "保存到文件",
            [AppLanguage.En] = "Save to File",
            [AppLanguage.Ja] = "ファイルに保存"
        },
        ["threeWayReady"] = new()
        {
            [AppLanguage.ZhHans] = "三向合并就绪",
            [AppLanguage.En] = "3-Way Merge Ready",
            [AppLanguage.Ja] = "3方向マージ準備完了"
        },
        ["conflictCountBadge"] = new()
        {
            [AppLanguage.ZhHans] = "{0} 个冲突待解决",
            [AppLanguage.En] = "{0} conflicts pending",
            [AppLanguage.Ja] = "{0} 件の衝突未解決"
        },
        ["followSystem"] = new()
        {
            [AppLanguage.ZhHans] = "跟随系统 (Auto)",
            [AppLanguage.En] = "Follow System (Auto)",
            [AppLanguage.Ja] = "システムに従う (Auto)"
        },
        ["aiAnalyzingIntent"] = new()
        {
            [AppLanguage.ZhHans] = "正在基于端侧/云端大模型深度提炼改动意图...",
            [AppLanguage.En] = "Analyzing change intent with AI model...",
            [AppLanguage.Ja] = "AIモデルで変更意図を解析中..."
        },
        ["textDiffStats"] = new()
        {
            [AppLanguage.ZhHans] = "{0} 处新增 · {1} 处删除 · {2} 处修改 · 共 {3} 行",
            [AppLanguage.En] = "{0} additions · {1} deletions · {2} modifications · {3} lines",
            [AppLanguage.Ja] = "追加 {0} · 削除 {1} · 変更 {2} · 合計 {3} 行"
        },
        ["engineStatusRust"] = new()
        {
            [AppLanguage.ZhHans] = "MacCompare Core v0.2.0 [Auto / Rust 极速引擎]",
            [AppLanguage.En] = "MacCompare Core v0.2.0 [Auto / Rust Engine]",
            [AppLanguage.Ja] = "MacCompare Core v0.2.0 [Auto / Rust 高速エンジン]"
        },
        ["engineStatusFallback"] = new()
        {
            [AppLanguage.ZhHans] = "WinCompare C# Managed [托管引擎]",
            [AppLanguage.En] = "WinCompare C# Managed [Fallback Engine]",
            [AppLanguage.Ja] = "WinCompare C# Managed [フォールバックエンジン]"
        },
        ["inspectorRowDetailTitle"] = new()
        {
            [AppLanguage.ZhHans] = "第 {0} 行差异明细 ({1})",
            [AppLanguage.En] = "Row {0} Diff Details ({1})",
            [AppLanguage.Ja] = "第 {0} 行 差分詳細 ({1})"
        },
        ["inspectorNone"] = new()
        {
            [AppLanguage.ZhHans] = "(无)",
            [AppLanguage.En] = "(None)",
            [AppLanguage.Ja] = "(なし)"
        },
        ["inspectorColModified"] = new()
        {
            [AppLanguage.ZhHans] = "[列 {0}] 左侧: \"{1}\"  -->  右侧: \"{2}\"  [变动]",
            [AppLanguage.En] = "[Col {0}] Left: \"{1}\"  -->  Right: \"{2}\"  [Modified]",
            [AppLanguage.Ja] = "[列 {0}] 左: \"{1}\"  -->  右: \"{2}\"  [変更]"
        },
        ["inspectorColIdentical"] = new()
        {
            [AppLanguage.ZhHans] = "[列 {0}] \"{1}\" (一致)",
            [AppLanguage.En] = "[Col {0}] \"{1}\" (Identical)",
            [AppLanguage.Ja] = "[列 {0}] \"{1}\" (一致)"
        },
        ["aiSummaryMainTitle"] = new()
        {
            [AppLanguage.ZhHans] = "主要改动集中在：{0}",
            [AppLanguage.En] = "Primary changes in: {0}",
            [AppLanguage.Ja] = "主な変更箇所：{0}"
        },
        ["aiSummaryCommitFeature"] = new()
        {
            [AppLanguage.ZhHans] = "支持一键复制标准化 Conventional Commit 提交信息",
            [AppLanguage.En] = "Supports 1-click Conventional Commit copy",
            [AppLanguage.Ja] = "1クリックで Conventional Commit メッセージをコピー可能"
        },
        ["aiSummaryConfigCloud"] = new()
        {
            [AppLanguage.ZhHans] = "可在「设置」中配置云端 API Key 开启大模型深度语义总结",
            [AppLanguage.En] = "Configure Cloud API Key in Settings for deep semantic summary",
            [AppLanguage.Ja] = "設定でクラウド API キーを設定すると、より高度な意味解析が利用できます"
        },
        ["aiSummaryCompleted"] = new()
        {
            [AppLanguage.ZhHans] = "比对完成：共检测到 {0} 处新增，{1} 处删除。",
            [AppLanguage.En] = "Diff completed: detected {0} additions, {1} deletions.",
            [AppLanguage.Ja] = "比較完了：追加 {0} 件、削除 {1} 件を検出しました。"
        },
        ["tableIndexHeader"] = new()
        {
            [AppLanguage.ZhHans] = "表格 #{0}",
            [AppLanguage.En] = "Table #{0}",
            [AppLanguage.Ja] = "表 #{0}"
        },
        ["tableInfoFormat"] = new()
        {
            [AppLanguage.ZhHans] = "{0}: {1} 行 x {2} 列",
            [AppLanguage.En] = "{0}: {1} rows x {2} cols",
            [AppLanguage.Ja] = "{0}: {1} 行 x {2} 列"
        },
        ["tableInfoNone"] = new()
        {
            [AppLanguage.ZhHans] = "{0}: (无此表)",
            [AppLanguage.En] = "{0}: (No table)",
            [AppLanguage.Ja] = "{0}: (表なし)"
        },
        ["wordMetaLeftDoc"] = new()
        {
            [AppLanguage.ZhHans] = "[左侧文档]",
            [AppLanguage.En] = "[Left Document]",
            [AppLanguage.Ja] = "[左文書]"
        },
        ["wordMetaRightDoc"] = new()
        {
            [AppLanguage.ZhHans] = "[右侧文档]",
            [AppLanguage.En] = "[Right Document]",
            [AppLanguage.Ja] = "[右文書]"
        },
        ["wordMetaFilePath"] = new()
        {
            [AppLanguage.ZhHans] = "文件路径: {0}",
            [AppLanguage.En] = "File Path: {0}",
            [AppLanguage.Ja] = "ファイルパス: {0}"
        },
        ["wordMetaFileSize"] = new()
        {
            [AppLanguage.ZhHans] = "文件大小: {0}",
            [AppLanguage.En] = "File Size: {0}",
            [AppLanguage.Ja] = "ファイルサイズ: {0}"
        },
        ["wordMetaLastMod"] = new()
        {
            [AppLanguage.ZhHans] = "最后修改: {0}",
            [AppLanguage.En] = "Last Modified: {0}",
            [AppLanguage.Ja] = "最終更新: {0}"
        },
        ["wordMetaParagraphs"] = new()
        {
            [AppLanguage.ZhHans] = "提取段落: {0} 个",
            [AppLanguage.En] = "Extracted Paragraphs: {0}",
            [AppLanguage.Ja] = "段落数: {0}"
        },
        ["wordMetaTables"] = new()
        {
            [AppLanguage.ZhHans] = "提取表格: {0} 个",
            [AppLanguage.En] = "Extracted Tables: {0}",
            [AppLanguage.Ja] = "表数: {0}"
        },
        ["wordNoTables"] = new()
        {
            [AppLanguage.ZhHans] = "双方文档均未包含嵌入表格 (No Tables Found)",
            [AppLanguage.En] = "Both documents contain no embedded tables",
            [AppLanguage.Ja] = "両方の文書に埋め込み表はありません"
        },
        ["wordNoOutline"] = new()
        {
            [AppLanguage.ZhHans] = "文档无显式标题大纲",
            [AppLanguage.En] = "No explicit headings found in document",
            [AppLanguage.Ja] = "文書に見出しアウトラインはありません"
        },
        ["wordSideLeft"] = new()
        {
            [AppLanguage.ZhHans] = "左侧",
            [AppLanguage.En] = "Left",
            [AppLanguage.Ja] = "左側"
        },
        ["wordSideRight"] = new()
        {
            [AppLanguage.ZhHans] = "右侧",
            [AppLanguage.En] = "Right",
            [AppLanguage.Ja] = "右側"
        }
    };
}
