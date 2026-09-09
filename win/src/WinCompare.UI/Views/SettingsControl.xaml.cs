using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinCompare.Core.Models;
using WinCompare.Core.Services;

namespace WinCompare.UI.Views;

public sealed partial class SettingsControl : UserControl
{
    private bool _isInitializing = true;
    private bool _isKeyVisible = false;

    public event Action<ElementTheme>? ThemeChanged;
    public event Action? CloseRequested;

    public SettingsControl()
    {
        try
        {
            this.InitializeComponent();

            if (DragHeader != null)
            {
                DragHeader.PointerPressed += OnHeaderPointerPressed;
                DragHeader.PointerMoved += OnHeaderPointerMoved;
                DragHeader.PointerReleased += OnHeaderPointerReleased;
                DragHeader.PointerCaptureLost += OnHeaderPointerCaptureLost;
            }

            // 1. Initialize Appearance Theme
            if (ThemeCombo != null)
            {
                ThemeCombo.SelectedIndex = SettingsManager.Instance.Theme switch
                {
                    "Dark" => 1,
                    "Light" => 2,
                    _ => 0
                };
            }

            // 2. Initialize Language Combo
            if (LanguageCombo != null)
            {
                LanguageCombo.SelectedIndex = SettingsManager.Instance.Language switch
                {
                    AppLanguage.Auto => 0,
                    AppLanguage.ZhHans => 1,
                    AppLanguage.En => 2,
                    AppLanguage.Ja => 3,
                    _ => 0
                };
            }

            // 3. Initialize Encoding Combo
            if (EncodingCombo != null)
            {
                EncodingCombo.SelectedIndex = SettingsManager.Instance.DefaultEncoding switch
                {
                    "UTF-8" => 0,
                    "GBK" => 1,
                    "UTF-16" => 2,
                    "Shift-JIS" => 3,
                    "ISO-8859-1" => 4,
                    _ => 0
                };
            }

            // 4. Initialize Diff Engine Selection
            if (EngineCombo != null)
            {
                EngineCombo.SelectedIndex = SettingsManager.Instance.EngineMode switch
                {
                    DiffEngineMode.Auto => 0,
                    DiffEngineMode.RustOnly => 1,
                    DiffEngineMode.CSharpOnly => 2,
                    _ => 0
                };
            }

            // 5. Initialize Diff Switches
            if (IgnoreWsSwitch != null) IgnoreWsSwitch.IsOn = SettingsManager.Instance.DefaultIgnoreWhitespace;
            if (IgnoreCaseSwitch != null) IgnoreCaseSwitch.IsOn = SettingsManager.Instance.DefaultIgnoreCase;
            if (BackupSwitch != null) BackupSwitch.IsOn = SettingsManager.Instance.CreateBakBackup;

            // 6. Initialize Folder Diff Settings
            if (SettingsManager.Instance.DefaultFolderDiffMode == FolderDiffMode.Quick)
            {
                if (QuickCompareRadio != null) QuickCompareRadio.IsChecked = true;
            }
            else
            {
                if (DeepHashCompareRadio != null) DeepHashCompareRadio.IsChecked = true;
            }
            if (ExcludedPatternsBox != null) ExcludedPatternsBox.Text = SettingsManager.Instance.ExcludedPatterns;

            // 7. Initialize AI Settings
            if (AiExecutionModeRadio != null) AiExecutionModeRadio.SelectedIndex = (int)SettingsManager.Instance.AiExecutionMode;
            if (LocalSlmPanel != null) LocalSlmPanel.Visibility = SettingsManager.Instance.AiExecutionMode == AIExecutionMode.LocalOffline ? Visibility.Visible : Visibility.Collapsed;
            if (CloudLlmPanel != null) CloudLlmPanel.Visibility = SettingsManager.Instance.AiExecutionMode == AIExecutionMode.CloudAPI ? Visibility.Visible : Visibility.Collapsed;

            if (ProviderPresetCombo != null) ProviderPresetCombo.SelectedIndex = (int)SettingsManager.Instance.AiCloudProvider;
            if (ApiBaseUrlBox != null) ApiBaseUrlBox.Text = SettingsManager.Instance.AiCloudEndpoint;
            if (ApiKeyPasswordBox != null) ApiKeyPasswordBox.Password = SettingsManager.Instance.AiCloudApiKey;
            if (ApiKeyTextBox != null) ApiKeyTextBox.Text = SettingsManager.Instance.AiCloudApiKey;
            if (ApiModelNameBox != null) ApiModelNameBox.Text = SettingsManager.Instance.AiCloudModel;
            if (AutoReleaseMemorySwitch != null) AutoReleaseMemorySwitch.IsOn = SettingsManager.Instance.AutoReleaseMemory;

            // 8. Model Library Initial Status
            UpdateAllModelCards();
            AIModelManager.Instance.ModelStateChanged += OnModelStateChanged;

            // 9. Core Engine Status & Localization
            UpdateEngineStatus();
            UpdateLocalization();
            LanguageManager.Instance.LanguageChanged += UpdateLocalization;

            _isInitializing = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsControl constructor error: {ex}");
        }
    }

    private Windows.Foundation.Point _startPoint;
    private bool _isDragging = false;

    private UIElement? GetDragReferenceElement()
    {
        return this.XamlRoot?.Content ?? App.GetWindowForElement(this)?.Content;
    }

    private void OnHeaderPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var refElement = GetDragReferenceElement();
        if (refElement == null) return;

        var ptr = e.GetCurrentPoint(refElement);
        if (ptr.Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _startPoint = ptr.Position;
            DragHeader.CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }

    private void OnHeaderPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            var refElement = GetDragReferenceElement();
            if (refElement == null) return;

            var curPoint = e.GetCurrentPoint(refElement).Position;
            double dx = curPoint.X - _startPoint.X;
            double dy = curPoint.Y - _startPoint.Y;

            DragTransform.X += dx;
            DragTransform.Y += dy;

            _startPoint = curPoint;
            e.Handled = true;
        }
    }

    private void OnHeaderPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            DragHeader.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }
    }

    private void OnHeaderPointerCaptureLost(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _isDragging = false;
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke();
    }

    private void UpdateEngineStatus()
    {
        try
        {
            bool rustAvailable = DiffEngineService.Instance.IsRustAvailable();
            if (rustAvailable)
            {
                if (EngineStatusDot != null) EngineStatusDot.Fill = new SolidColorBrush(ColorHelper.FromArgb(255, 16, 185, 129));
                if (EngineStatusText != null) EngineStatusText.Text = LanguageManager.Instance.Text("engineStatusRust");
            }
            else
            {
                if (EngineStatusDot != null) EngineStatusDot.Fill = new SolidColorBrush(ColorHelper.FromArgb(255, 59, 130, 246));
                if (EngineStatusText != null) EngineStatusText.Text = LanguageManager.Instance.Text("engineStatusFallback");
            }
        }
        catch { }
    }

    private void UpdateLocalization()
    {
        try
        {
            if (DialogTitleText != null) DialogTitleText.Text = LanguageManager.Instance.Text("settingsTitle");
            if (GeneralTabItem != null) GeneralTabItem.Header = LanguageManager.Instance.Text("general");
            if (FolderDiffTabItem != null) FolderDiffTabItem.Header = LanguageManager.Instance.Text("folderDiff");
            if (AiTabItem != null) AiTabItem.Header = LanguageManager.Instance.Text("aiEngine");
            if (AboutTabItem != null) AboutTabItem.Header = LanguageManager.Instance.Text("about");

            if (ThemeLabel != null) ThemeLabel.Text = LanguageManager.Instance.Text("appearance");
            if (LangLabel != null) LangLabel.Text = LanguageManager.Instance.Text("selectLanguage");
            if (EncodingLabel != null) EncodingLabel.Text = LanguageManager.Instance.Text("defaultEncoding");

            if (CoreEngineTitle != null) CoreEngineTitle.Text = LanguageManager.Instance.Text("coreEngineSection");
            if (DiffEngineLabel != null) DiffEngineLabel.Text = LanguageManager.Instance.Text("diffEngine");
            if (EngineStatusLabel != null) EngineStatusLabel.Text = LanguageManager.Instance.Text("currentEngineStatus");

            if (DiffPrefLabel != null) DiffPrefLabel.Text = LanguageManager.Instance.Text("defaultDiffSettings");
            if (BackupTitleText != null) BackupTitleText.Text = LanguageManager.Instance.Text("createBakBackupTitle");
            if (BackupDescText != null) BackupDescText.Text = LanguageManager.Instance.Text("createBakBackupDesc");
            if (AutoUpdateSwitch != null) AutoUpdateSwitch.Header = LanguageManager.Instance.Text("autoCheckUpdatesOnLaunch");

            if (DefaultCompareModeLabel != null) DefaultCompareModeLabel.Text = LanguageManager.Instance.Text("defaultCompareMode");
            if (QuickCompareRadio != null) QuickCompareRadio.Content = LanguageManager.Instance.Text("quickCompareMode");
            if (DeepHashCompareRadio != null) DeepHashCompareRadio.Content = LanguageManager.Instance.Text("deepHashCompareMode");
            if (ExcludedPatternsLabel != null) ExcludedPatternsLabel.Text = LanguageManager.Instance.Text("defaultExcludedPatterns");
            if (ExcludedPatternsDesc != null) ExcludedPatternsDesc.Text = LanguageManager.Instance.Text("defaultExcludedPatternsDesc");

            if (ModelLibraryLabel != null) ModelLibraryLabel.Text = LanguageManager.Instance.Text("aiModelLibrary");
            if (AutoReleaseTitle != null) AutoReleaseTitle.Text = LanguageManager.Instance.Text("aiAutoReleaseMemory");
            if (AutoReleaseDesc != null) AutoReleaseDesc.Text = LanguageManager.Instance.Text("aiAutoReleaseMemoryDesc");
            if (PresetLabel != null) PresetLabel.Text = LanguageManager.Instance.Text("aiCloudPreset");
            if (TestConnBtnText != null) TestConnBtnText.Text = LanguageManager.Instance.Text("aiCloudTestConnection");
            if (CheckUpdateBtnText != null) CheckUpdateBtnText.Text = LanguageManager.Instance.Text("checkForUpdates");

            UpdateEngineStatus();
            UpdateAllModelCards();
        }
        catch { }
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        var targetTheme = ThemeCombo.SelectedIndex switch
        {
            1 => ElementTheme.Dark,
            2 => ElementTheme.Light,
            _ => ElementTheme.Default
        };

        SettingsManager.Instance.Theme = targetTheme.ToString();
        this.RequestedTheme = targetTheme;
        ThemeChanged?.Invoke(targetTheme);
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        if (LanguageCombo.SelectedItem is ComboBoxItem item)
        {
            var targetLang = item.Tag?.ToString() switch
            {
                "Auto" => AppLanguage.Auto,
                "ZhHans" => AppLanguage.ZhHans,
                "En" => AppLanguage.En,
                "Ja" => AppLanguage.Ja,
                _ => AppLanguage.Auto
            };

            LanguageManager.Instance.SetLanguage(targetLang);
        }
    }

    private void OnEncodingChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        SettingsManager.Instance.DefaultEncoding = EncodingCombo.SelectedIndex switch
        {
            0 => "UTF-8",
            1 => "GBK",
            2 => "UTF-16",
            3 => "Shift-JIS",
            4 => "ISO-8859-1",
            _ => "UTF-8"
        };
    }

    private void OnEngineSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        SettingsManager.Instance.EngineMode = EngineCombo.SelectedIndex switch
        {
            0 => DiffEngineMode.Auto,
            1 => DiffEngineMode.RustOnly,
            2 => DiffEngineMode.CSharpOnly,
            _ => DiffEngineMode.Auto
        };

        UpdateEngineStatus();
    }

    private void OnIgnoreWsToggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitializing) SettingsManager.Instance.DefaultIgnoreWhitespace = IgnoreWsSwitch.IsOn;
    }

    private void OnIgnoreCaseToggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitializing) SettingsManager.Instance.DefaultIgnoreCase = IgnoreCaseSwitch.IsOn;
    }

    private void OnBackupToggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitializing) SettingsManager.Instance.CreateBakBackup = BackupSwitch.IsOn;
    }

    private void OnFolderModeChecked(object sender, RoutedEventArgs e)
    {
        if (!_isInitializing)
        {
            SettingsManager.Instance.DefaultFolderDiffMode = QuickCompareRadio.IsChecked == true
                ? FolderDiffMode.Quick
                : FolderDiffMode.DeepHash;
        }
    }

    private void OnExcludedPatternsChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitializing)
        {
            SettingsManager.Instance.ExcludedPatterns = ExcludedPatternsBox.Text;
        }
    }

    private void OnAiModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        if (AiExecutionModeRadio != null)
        {
            SettingsManager.Instance.AiExecutionMode = (AIExecutionMode)AiExecutionModeRadio.SelectedIndex;

            if (LocalSlmPanel != null)
            {
                LocalSlmPanel.Visibility = AiExecutionModeRadio.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            if (CloudLlmPanel != null)
            {
                CloudLlmPanel.Visibility = AiExecutionModeRadio.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private void OnPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (ProviderPresetCombo == null) return;

        SettingsManager.Instance.AiCloudProvider = ProviderPresetCombo.SelectedIndex;

        switch (ProviderPresetCombo.SelectedIndex)
        {
            case 0: // DeepSeek
                if (ApiBaseUrlBox != null) ApiBaseUrlBox.Text = "https://api.deepseek.com/v1";
                if (ApiModelNameBox != null) ApiModelNameBox.Text = "deepseek-chat";
                break;
            case 1: // OpenAI
                if (ApiBaseUrlBox != null) ApiBaseUrlBox.Text = "https://api.openai.com/v1";
                if (ApiModelNameBox != null) ApiModelNameBox.Text = "gpt-4o-mini";
                break;
            case 2: // Claude
                if (ApiBaseUrlBox != null) ApiBaseUrlBox.Text = "https://api.anthropic.com/v1";
                if (ApiModelNameBox != null) ApiModelNameBox.Text = "claude-3-5-sonnet";
                break;
            case 3: // Ollama
                if (ApiBaseUrlBox != null) ApiBaseUrlBox.Text = "http://localhost:11434/v1";
                if (ApiModelNameBox != null) ApiModelNameBox.Text = "qwen2.5-coder:7b";
                break;
            case 4: // Custom
                break;
        }
    }

    private void OnApiConfigChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitializing)
        {
            SettingsManager.Instance.AiCloudEndpoint = ApiBaseUrlBox?.Text ?? "";
            SettingsManager.Instance.AiCloudModel = ApiModelNameBox?.Text ?? "";
        }
    }

    private void OnApiKeyPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_isInitializing)
        {
            SettingsManager.Instance.AiCloudApiKey = ApiKeyPasswordBox?.Password ?? "";
        }
    }

    private void OnApiKeyTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitializing)
        {
            SettingsManager.Instance.AiCloudApiKey = ApiKeyTextBox?.Text ?? "";
        }
    }

    private void OnAutoReleaseMemoryToggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitializing)
        {
            SettingsManager.Instance.AutoReleaseMemory = AutoReleaseMemorySwitch.IsOn;
        }
    }

    private void OnToggleApiKeyVisibility(object sender, RoutedEventArgs e)
    {
        _isKeyVisible = !_isKeyVisible;
        if (_isKeyVisible)
        {
            ApiKeyTextBox.Text = ApiKeyPasswordBox.Password;
            ApiKeyPasswordBox.Visibility = Visibility.Collapsed;
            ApiKeyTextBox.Visibility = Visibility.Visible;
            EyeIcon.Glyph = "\uED1A"; // Eye slash
        }
        else
        {
            ApiKeyPasswordBox.Password = ApiKeyTextBox.Text;
            ApiKeyTextBox.Visibility = Visibility.Collapsed;
            ApiKeyPasswordBox.Visibility = Visibility.Visible;
            EyeIcon.Glyph = "\uE7B3"; // Eye
        }
    }

    // MARK: - Model Cards Actions & State Rendering

    private void OnModelStateChanged(AIModelId model, AIModelDownloadState state)
    {
        this.DispatcherQueue.TryEnqueue(() =>
        {
            UpdateModelCard(model, state);
        });
    }

    private void UpdateAllModelCards()
    {
        UpdateModelCard(AIModelId.Gemma3_1B, AIModelManager.Instance.GetModelState(AIModelId.Gemma3_1B));
        UpdateModelCard(AIModelId.Gemma3_4B, AIModelManager.Instance.GetModelState(AIModelId.Gemma3_4B));
    }

    private void UpdateModelCard(AIModelId model, AIModelDownloadState state)
    {
        if (model == AIModelId.Gemma3_1B)
        {
            ApplyCardState(
                state,
                AIModelId.Gemma3_1B,
                Gemma1bReadyPanel,
                Gemma1bActiveBadge,
                Gemma1bSelectBtn,
                Gemma1bDeleteBtn,
                Gemma1bDownloadBtn,
                Gemma1bCancelBtn,
                Gemma1bDownloadProgressPanel,
                Gemma1bProgressBar,
                Gemma1bProgressPercentText,
                Gemma1bProgressStatsText,
                Gemma1bErrorPanel,
                Gemma1bErrorText);
        }
        else if (model == AIModelId.Gemma3_4B)
        {
            ApplyCardState(
                state,
                AIModelId.Gemma3_4B,
                Gemma4bReadyPanel,
                Gemma4bActiveBadge,
                Gemma4bSelectBtn,
                Gemma4bDeleteBtn,
                Gemma4bDownloadBtn,
                Gemma4bCancelBtn,
                Gemma4bDownloadProgressPanel,
                Gemma4bProgressBar,
                Gemma4bProgressPercentText,
                Gemma4bProgressStatsText,
                Gemma4bErrorPanel,
                Gemma4bErrorText);
        }
    }

    private void ApplyCardState(
        AIModelDownloadState state,
        AIModelId model,
        StackPanel? readyPanel,
        Border? activeBadge,
        Button? selectBtn,
        Button? deleteBtn,
        Button? downloadBtn,
        Button? cancelBtn,
        StackPanel? progressPanel,
        ProgressBar? progressBar,
        TextBlock? percentText,
        TextBlock? statsText,
        StackPanel? errorPanel,
        TextBlock? errorText)
    {
        switch (state.Status)
        {
            case AIModelDownloadStatus.NotDownloaded:
                if (readyPanel != null) readyPanel.Visibility = Visibility.Collapsed;
                if (downloadBtn != null) downloadBtn.Visibility = Visibility.Visible;
                if (cancelBtn != null) cancelBtn.Visibility = Visibility.Collapsed;
                if (progressPanel != null) progressPanel.Visibility = Visibility.Collapsed;
                if (errorPanel != null) errorPanel.Visibility = Visibility.Collapsed;
                break;

            case AIModelDownloadStatus.Downloading:
                if (readyPanel != null) readyPanel.Visibility = Visibility.Collapsed;
                if (downloadBtn != null) downloadBtn.Visibility = Visibility.Collapsed;
                if (cancelBtn != null) cancelBtn.Visibility = Visibility.Visible;
                if (progressPanel != null) progressPanel.Visibility = Visibility.Visible;
                if (progressBar != null) progressBar.Value = state.Progress * 100.0;
                if (percentText != null) percentText.Text = $"{(int)(state.Progress * 100)}%";
                if (statsText != null) statsText.Text = $"{state.SpeedText} · {state.RemainingText}";
                if (errorPanel != null) errorPanel.Visibility = Visibility.Collapsed;
                break;

            case AIModelDownloadStatus.Ready:
                if (readyPanel != null) readyPanel.Visibility = Visibility.Visible;
                if (downloadBtn != null) downloadBtn.Visibility = Visibility.Collapsed;
                if (cancelBtn != null) cancelBtn.Visibility = Visibility.Collapsed;
                if (progressPanel != null) progressPanel.Visibility = Visibility.Collapsed;
                if (errorPanel != null) errorPanel.Visibility = Visibility.Collapsed;

                bool isActive = AIModelManager.Instance.EffectiveModel == model;
                if (activeBadge != null) activeBadge.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
                if (selectBtn != null) selectBtn.Visibility = isActive ? Visibility.Collapsed : Visibility.Visible;
                break;

            case AIModelDownloadStatus.Error:
                if (readyPanel != null) readyPanel.Visibility = Visibility.Collapsed;
                if (downloadBtn != null) downloadBtn.Visibility = Visibility.Collapsed;
                if (cancelBtn != null) cancelBtn.Visibility = Visibility.Collapsed;
                if (progressPanel != null) progressPanel.Visibility = Visibility.Collapsed;
                if (errorPanel != null) errorPanel.Visibility = Visibility.Visible;
                if (errorText != null) errorText.Text = state.ErrorMessage ?? LanguageManager.Instance.Text("aiDownloadFailed");
                break;
        }
    }

    private void OnDownloadGemma1bClicked(object sender, RoutedEventArgs e)
    {
        AIModelManager.Instance.StartDownload(AIModelId.Gemma3_1B);
    }

    private void OnCancelGemma1bClicked(object sender, RoutedEventArgs e)
    {
        AIModelManager.Instance.CancelDownload(AIModelId.Gemma3_1B);
    }

    private void OnSelectGemma1bClicked(object sender, RoutedEventArgs e)
    {
        AIModelManager.Instance.SelectedModel = AIModelId.Gemma3_1B;
        UpdateAllModelCards();
    }

    private void OnDeleteGemma1bClicked(object sender, RoutedEventArgs e)
    {
        AIModelManager.Instance.DeleteModel(AIModelId.Gemma3_1B);
        UpdateAllModelCards();
    }

    private void OnDownloadGemma4bClicked(object sender, RoutedEventArgs e)
    {
        AIModelManager.Instance.StartDownload(AIModelId.Gemma3_4B);
    }

    private void OnCancelGemma4bClicked(object sender, RoutedEventArgs e)
    {
        AIModelManager.Instance.CancelDownload(AIModelId.Gemma3_4B);
    }

    private void OnSelectGemma4bClicked(object sender, RoutedEventArgs e)
    {
        AIModelManager.Instance.SelectedModel = AIModelId.Gemma3_4B;
        UpdateAllModelCards();
    }

    private void OnDeleteGemma4bClicked(object sender, RoutedEventArgs e)
    {
        AIModelManager.Instance.DeleteModel(AIModelId.Gemma3_4B);
        UpdateAllModelCards();
    }

    // MARK: - Cloud Connection Test & Update Check

    private async void OnTestConnectionClicked(object sender, RoutedEventArgs e)
    {
        TestConnectionBtn.IsEnabled = false;
        TestProgressRing.Visibility = Visibility.Visible;
        TestProgressRing.IsActive = true;
        TestResultText.Text = LanguageManager.Instance.Text("aiCloudTesting");
        TestResultText.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 150, 150, 150));

        string endpoint = ApiBaseUrlBox?.Text ?? "";
        string apiKey = ApiKeyPasswordBox?.Password.Length > 0 ? ApiKeyPasswordBox.Password : (ApiKeyTextBox?.Text ?? "");
        string model = ApiModelNameBox?.Text ?? "";

        var (success, latency, msg) = await AIService.Instance.TestCloudConnectionAsync(endpoint, apiKey, model);

        TestProgressRing.IsActive = false;
        TestProgressRing.Visibility = Visibility.Collapsed;
        TestConnectionBtn.IsEnabled = true;

        if (success)
        {
            string okText = LanguageManager.Instance.Text("aiCloudTestSuccess");
            TestResultText.Text = $"✔ {okText} ({latency:F2}s)";
            TestResultText.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 16, 185, 129));
        }
        else
        {
            string failText = LanguageManager.Instance.Text("aiCloudTestFailed");
            TestResultText.Text = $"❌ {failText}: {msg}";
            TestResultText.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 239, 68, 68));
        }
    }

    private async void OnCheckUpdateClicked(object sender, RoutedEventArgs e)
    {
        CheckUpdateBtn.IsEnabled = false;
        UpdateProgressRing.Visibility = Visibility.Visible;
        UpdateProgressRing.IsActive = true;
        UpdateStatusText.Text = LanguageManager.Instance.Text("checkingForUpdates");

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WinCompare-App");
            using var resp = await client.GetAsync("https://api.github.com/repos/andychao217/FileCompare/releases/latest");
            if (resp.IsSuccessStatusCode)
            {
                string json = await resp.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                string tagName = doc.RootElement.GetProperty("tag_name").GetString() ?? "";

                string currentVer = "v1.0.0";
                if (!string.IsNullOrEmpty(tagName) && !tagName.Equals(currentVer, StringComparison.OrdinalIgnoreCase))
                {
                    UpdateStatusText.Text = string.Format(LanguageManager.Instance.Text("newVersionFoundFormat"), tagName, currentVer);
                    UpdateStatusText.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 16, 185, 129));
                }
                else
                {
                    string upToDate = LanguageManager.Instance.Text("upToDateTitle");
                    string lastChecked = LanguageManager.Instance.Text("lastChecked");
                    UpdateStatusText.Text = $"{upToDate} ({currentVer}) · {lastChecked}: {DateTime.Now:HH:mm:ss}";
                }
            }
            else
            {
                string upToDate = LanguageManager.Instance.Text("upToDateTitle");
                string lastChecked = LanguageManager.Instance.Text("lastChecked");
                UpdateStatusText.Text = $"{upToDate} · {lastChecked}: {DateTime.Now:HH:mm:ss}";
            }
        }
        catch
        {
            string upToDate = LanguageManager.Instance.Text("upToDateTitle");
            string lastChecked = LanguageManager.Instance.Text("lastChecked");
            UpdateStatusText.Text = $"{upToDate} · {lastChecked}: {DateTime.Now:HH:mm:ss}";
        }
        finally
        {
            UpdateProgressRing.IsActive = false;
            UpdateProgressRing.Visibility = Visibility.Collapsed;
            CheckUpdateBtn.IsEnabled = true;
        }
    }
}

