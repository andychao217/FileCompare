using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WinCompare.Setup.Models;
using WinCompare.Setup.Services;

namespace WinCompare.Setup.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IInstallService _installService;
    private readonly IProcessHelper _processHelper;

    public LocalizationService Loc => LocalizationService.Instance;
    public IReadOnlyList<ThemeColorItem> AccentColors => ThemeService.Instance.AccentColors;

    [ObservableProperty]
    private InstallState _state = InstallState.Ready;

    [ObservableProperty]
    private InstallOptions _options = new();

    [ObservableProperty]
    private double _progressPercentage;

    private InstallStage _currentStage = InstallStage.Ready;
    private string _currentDetail = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _detailedProgressText = string.Empty;

    [ObservableProperty]
    private bool _isAdvancedExpanded;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isUninstallMode;

    [ObservableProperty]
    private AppLanguage _selectedLanguage = AppLanguage.Auto;

    [ObservableProperty]
    private string _selectedThemeMode = "System";

    [ObservableProperty]
    private bool _isUpgradeMode;

    public string ActionButtonText => IsUpgradeMode ? Loc.UpdateNow : Loc.InstallNow;
    public string HeaderTaglineText => IsUpgradeMode ? Loc.UpgradeTagline : Loc.Tagline;

    public MainViewModel(
        IInstallService installService, 
        IRegistryService registryService, 
        IProcessHelper processHelper, 
        bool isUninstallMode = false,
        string? customInstallPath = null)
    {
        _installService = installService;
        _processHelper = processHelper;
        _isUninstallMode = isUninstallMode;

        StatusMessage = Loc.GetStageMessage(InstallStage.Ready);

        if (_isUninstallMode)
        {
            if (!string.IsNullOrEmpty(customInstallPath))
            {
                Options.InstallPath = customInstallPath;
            }
            State = InstallState.Uninstalling;
            _currentStage = InstallStage.UninstallChecking;
            StatusMessage = Loc.GetStageMessage(_currentStage);
        }
        else
        {
            if (!string.IsNullOrEmpty(customInstallPath))
            {
                Options.InstallPath = customInstallPath;
                if (Directory.Exists(customInstallPath) && File.Exists(Path.Combine(customInstallPath, "WinCompare.UI.exe")))
                {
                    IsUpgradeMode = true;
                }
            }
            else
            {
                // 自动探查旧版本安装路径
                var existingPath = registryService.DetectExistingInstallPath();
                if (!string.IsNullOrEmpty(existingPath))
                {
                    Options.InstallPath = existingPath;
                    IsUpgradeMode = true;
                }
            }
        }
    }

    partial void OnSelectedLanguageChanged(AppLanguage value)
    {
        Loc.CurrentLanguage = value;
        StatusMessage = Loc.GetStageMessage(_currentStage, _currentDetail);
        OnPropertyChanged(nameof(ActionButtonText));
        OnPropertyChanged(nameof(HeaderTaglineText));
    }

    partial void OnSelectedThemeModeChanged(string value)
    {
        ThemeService.Instance.SetTheme(value);
    }

    [RelayCommand]
    private void SelectAccentColor(Color color)
    {
        ThemeService.Instance.SetAccentColor(color);
    }

    [RelayCommand]
    private void ToggleAdvanced()
    {
        IsAdvancedExpanded = !IsAdvancedExpanded;
    }

    [RelayCommand]
    private void BrowsePath()
    {
        var dialog = new OpenFolderDialog
        {
            Title = Loc.InstallDirectoryLabel,
            InitialDirectory = Options.InstallPath
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            Options.InstallPath = Path.Combine(dialog.FolderName, "WinCompare");
            OnPropertyChanged(nameof(Options));
        }
    }

    [RelayCommand]
    private async Task StartInstallAsync()
    {
        if (State != InstallState.Ready) return;

        State = InstallState.Installing;
        ErrorMessage = string.Empty;

        var progress = new Progress<InstallProgressReport>(report =>
        {
            ProgressPercentage = report.Percentage;
            _currentStage = report.Stage;
            _currentDetail = report.Detail;
            StatusMessage = Loc.GetStageMessage(_currentStage, _currentDetail);

            if (report.TotalBytes > 0)
            {
                var processedMb = report.ProcessedBytes / 1048576.0;
                var totalMb = report.TotalBytes / 1048576.0;
                DetailedProgressText = $"{processedMb:F1} MB / {totalMb:F1} MB";
            }
            else
            {
                DetailedProgressText = string.Empty;
            }
        });

        try
        {
            await _installService.InstallAsync(Options, progress);
            State = InstallState.Finished;
            StatusMessage = Loc.InstallSuccess;
        }
        catch (Exception ex)
        {
            State = InstallState.Error;
            ErrorMessage = ex.Message;
            StatusMessage = Loc.OperationFailed;
        }
    }

    [RelayCommand]
    private async Task StartUninstallAsync()
    {
        var progress = new Progress<InstallProgressReport>(report =>
        {
            ProgressPercentage = report.Percentage;
            _currentStage = report.Stage;
            _currentDetail = report.Detail;
            StatusMessage = Loc.GetStageMessage(_currentStage, _currentDetail);
        });

        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            await _installService.UninstallAsync(baseDir, Options.IsPerUser, progress);
            State = InstallState.UninstallFinished;
            StatusMessage = Loc.UninstallSuccess;
        }
        catch (Exception ex)
        {
            State = InstallState.Error;
            ErrorMessage = ex.Message;
            StatusMessage = Loc.OperationFailed;
        }
    }

    [RelayCommand]
    private void LaunchApp()
    {
        var mainExe = Path.Combine(Options.InstallPath, "WinCompare.UI.exe");
        _processHelper.StartInstalledApp(mainExe);
        Application.Current.Shutdown();
    }

    [RelayCommand]
    private void CloseWindow()
    {
        Application.Current.Shutdown();
    }
}
