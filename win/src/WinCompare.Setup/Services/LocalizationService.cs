using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using WinCompare.Setup.Models;

namespace WinCompare.Setup.Services;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
    public static LocalizationService Instance => _instance.Value;

    private AppLanguage _currentLanguage = AppLanguage.Auto;

    public AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                OnPropertyChanged(string.Empty); // Notify all properties changed
            }
        }
    }

    public AppLanguage EffectiveLanguage
    {
        get
        {
            if (_currentLanguage == AppLanguage.Auto)
            {
                var culture = CultureInfo.CurrentUICulture.Name.ToLowerInvariant();
                if (culture.StartsWith("zh")) return AppLanguage.ZhHans;
                if (culture.StartsWith("ja")) return AppLanguage.Ja;
                return AppLanguage.En;
            }
            return _currentLanguage;
        }
    }

    public string WindowTitle => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "WinCompare 安装程序",
        AppLanguage.Ja => "WinCompare セットアップ",
        _ => "WinCompare Setup Wizard"
    };

    public string Tagline => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "现代化轻量级文件与目录差异比对软件",
        AppLanguage.Ja => "モダンで軽量なファイル・ディレクトリ差分比較ツール",
        _ => "Modern Lightweight File & Directory Diff Tool"
    };

    public string InstallNow => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "立即安装",
        AppLanguage.Ja => "今すぐインストール",
        _ => "Install Now"
    };

    public string UpdateNow => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "覆盖更新",
        AppLanguage.Ja => "アップデート",
        _ => "Update Now"
    };

    public string UpgradeTagline => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "检测到已安装的旧版本，将自动进行无缝覆盖升级",
        AppLanguage.Ja => "既存のバージョンが検出されました。上書き更新を実行します",
        _ => "Existing installation detected. An in-place update will be performed."
    };

    public string AdvancedOptions => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "自定义安装选项",
        AppLanguage.Ja => "カスタム設定",
        _ => "Custom Options"
    };

    public string InstallDirectoryLabel => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "安装目标目录：",
        AppLanguage.Ja => "インストール先フォルダ：",
        _ => "Installation Directory:"
    };

    public string Browse => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "浏览...",
        AppLanguage.Ja => "参照...",
        _ => "Browse..."
    };

    public string DesktopShortcut => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "创建桌面快捷方式",
        AppLanguage.Ja => "デスクトップショートカットを作成",
        _ => "Create desktop shortcut"
    };

    public string StartMenuShortcut => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "创建开始菜单项",
        AppLanguage.Ja => "スタートメニューに追加",
        _ => "Create Start Menu item"
    };

    public string LicenseNotice => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "点击“立即安装”即代表您同意软件使用许可协议",
        AppLanguage.Ja => "「今すぐインストール」をクリックすると、ソフトウェア利用規約に同意したことになります",
        _ => "By clicking 'Install Now', you agree to the Software License Agreement."
    };

    public string InstallSuccess => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "WinCompare 已成功安装！",
        AppLanguage.Ja => "WinCompare が正常にインストールされました！",
        _ => "WinCompare installed successfully!"
    };

    public string LaunchNow => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "立即启动",
        AppLanguage.Ja => "今すぐ起動",
        _ => "Launch Now"
    };

    public string UninstallSuccess => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "WinCompare 已成功卸载。",
        AppLanguage.Ja => "WinCompare が正常にアンインストールされました。",
        _ => "WinCompare has been uninstalled."
    };

    public string Finish => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "完成",
        AppLanguage.Ja => "完了",
        _ => "Finish"
    };

    public string Close => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "关闭",
        AppLanguage.Ja => "閉じる",
        _ => "Close"
    };

    public string OperationFailed => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "操作未能完成",
        AppLanguage.Ja => "操作を完了できませんでした",
        _ => "Operation Failed"
    };

    public string Exit => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "退出",
        AppLanguage.Ja => "終了",
        _ => "Exit"
    };

    public string ThemeAppearance => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "主题外观：",
        AppLanguage.Ja => "外観テーマ：",
        _ => "Appearance:"
    };

    public string SystemTheme => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "跟随系统",
        AppLanguage.Ja => "システム連動",
        _ => "System"
    };

    public string LightTheme => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "浅色",
        AppLanguage.Ja => "ライト",
        _ => "Light"
    };

    public string DarkTheme => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "深色",
        AppLanguage.Ja => "ダーク",
        _ => "Dark"
    };

    public string SystemThemeOption => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "💻 跟随系统",
        AppLanguage.Ja => "💻 システム連動",
        _ => "💻 System"
    };

    public string LightThemeOption => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "☀️ 浅色模式",
        AppLanguage.Ja => "☀️ ライト",
        _ => "☀️ Light"
    };

    public string DarkThemeOption => EffectiveLanguage switch
    {
        AppLanguage.ZhHans => "🌙 深色模式",
        AppLanguage.Ja => "🌙 ダーク",
        _ => "🌙 Dark"
    };

    public string GetStageMessage(InstallStage stage, string detail = "")
    {
        return EffectiveLanguage switch
        {
            AppLanguage.ZhHans => stage switch
            {
                InstallStage.Ready => "准备就绪",
                InstallStage.CheckingRunning => "正在检查运行状态...",
                InstallStage.PreparingDirectory => "正在准备安装目录...",
                InstallStage.ReadingPayload => "正在读取核心资源...",
                InstallStage.Extracting => string.IsNullOrEmpty(detail) ? "正在解压核心组件..." : $"正在解压: {detail}",
                InstallStage.DeployingMaintenance => "正在部署维护与卸载程序...",
                InstallStage.CreatingShortcuts => "正在创建系统快捷方式...",
                InstallStage.RegisteringSystem => "正在写入系统配置...",
                InstallStage.InstallCompleted => "安装完成！",
                InstallStage.UninstallChecking => "正在检查程序占用...",
                InstallStage.UninstallShortcuts => "正在清理系统快捷方式...",
                InstallStage.UninstallRegistry => "正在清理注册表配置...",
                InstallStage.UninstallFiles => "正在清除应用程序文件...",
                InstallStage.UninstallCleanup => "正在调度最终自清理...",
                InstallStage.UninstallCompleted => "卸载已完成。感谢使用！",
                InstallStage.Error => "操作未能完成",
                _ => string.Empty
            },
            AppLanguage.Ja => stage switch
            {
                InstallStage.Ready => "準備完了",
                InstallStage.CheckingRunning => "実行状態を確認中...",
                InstallStage.PreparingDirectory => "インストール先を準備中...",
                InstallStage.ReadingPayload => "コアリソースを読み込み中...",
                InstallStage.Extracting => string.IsNullOrEmpty(detail) ? "コンポーネントを展開中..." : $"展開中: {detail}",
                InstallStage.DeployingMaintenance => "保守・アンインストールプログラムを配置中...",
                InstallStage.CreatingShortcuts => "ショートカットを作成中...",
                InstallStage.RegisteringSystem => "システム設定を登録中...",
                InstallStage.InstallCompleted => "インストールが完了しました！",
                InstallStage.UninstallChecking => "実行中のプロセスを確認中...",
                InstallStage.UninstallShortcuts => "ショートカットを削除中...",
                InstallStage.UninstallRegistry => "レジストリ情報をクリア中...",
                InstallStage.UninstallFiles => "アプリケーションファイルを削除中...",
                InstallStage.UninstallCleanup => "最終クリーンアップをスケジュール中...",
                InstallStage.UninstallCompleted => "アンインストールが完了しました。",
                InstallStage.Error => "操作を完了できませんでした",
                _ => string.Empty
            },
            _ => stage switch
            {
                InstallStage.Ready => "Ready",
                InstallStage.CheckingRunning => "Checking running instances...",
                InstallStage.PreparingDirectory => "Preparing installation directory...",
                InstallStage.ReadingPayload => "Reading core payload...",
                InstallStage.Extracting => string.IsNullOrEmpty(detail) ? "Extracting components..." : $"Extracting: {detail}",
                InstallStage.DeployingMaintenance => "Deploying maintenance components...",
                InstallStage.CreatingShortcuts => "Creating system shortcuts...",
                InstallStage.RegisteringSystem => "Registering system configuration...",
                InstallStage.InstallCompleted => "Installation complete!",
                InstallStage.UninstallChecking => "Checking running instances...",
                InstallStage.UninstallShortcuts => "Cleaning up shortcuts...",
                InstallStage.UninstallRegistry => "Cleaning up registry...",
                InstallStage.UninstallFiles => "Removing application files...",
                InstallStage.UninstallCleanup => "Scheduling self-cleanup...",
                InstallStage.UninstallCompleted => "Uninstallation complete.",
                InstallStage.Error => "Operation failed",
                _ => string.Empty
            }
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
