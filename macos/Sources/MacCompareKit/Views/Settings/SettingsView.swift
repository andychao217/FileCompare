import SwiftUI

public struct SettingsView: View {
    @State private var languageManager = LanguageManager.shared
    @State private var themeManager = ThemeManager.shared
    @State private var updateChecker = UpdateCheckerService.shared
    @State private var aiModelManager = AIModelManager.shared
    @State private var enginePreference: DiffEnginePreference = DiffEngineService.shared.enginePreference
    @AppStorage("default_file_encoding") private var defaultEncoding: String = "UTF-8"
    @AppStorage("default_folder_mode") private var defaultFolderMode: String = "Quick"
    @AppStorage("default_ignore_whitespace") private var defaultIgnoreWhitespace: Bool = false
    @AppStorage("default_ignore_case") private var defaultIgnoreCase: Bool = false
    @AppStorage("create_bak_backup_on_save") private var createBakBackupOnSave: Bool = false
    @State private var isTestingCloudAPI: Bool = false
    @State private var cloudTestMessage: String? = nil
    @State private var cloudTestIsSuccess: Bool = false
    @State private var showAPIKeyPlaintext: Bool = false
    public var onDismiss: (() -> Void)?

    public init(onDismiss: (() -> Void)? = nil) {
        self.onDismiss = onDismiss
    }

    public var body: some View {
        VStack(spacing: 0) {
            // Header with title and circular close button
            HStack(alignment: .center) {
                Text(languageManager.text(.settings))
                    .font(.system(size: 14, weight: .bold))
                    .foregroundColor(.primary)

                Spacer()

                if let onDismiss {
                    Button(action: onDismiss) {
                        Image(systemName: "xmark.circle.fill")
                            .font(.system(size: 18))
                            .foregroundColor(.secondary.opacity(0.7))
                    }
                    .buttonStyle(.plain)
                    .focusEffectDisabled()
                    .help(languageManager.text(.close))
                }
            }
            .padding(.horizontal, 20)
            .padding(.top, 16)
            .padding(.bottom, 8)

            TabView {
                generalTab
                    .tabItem {
                        Label(languageManager.text(.general), systemImage: "gearshape")
                    }

                folderDiffTab
                    .tabItem {
                        Label(languageManager.text(.folderDiff), systemImage: "folder")
                    }

                aiEngineTab
                    .tabItem {
                        Label(languageManager.text(.aiEngine), systemImage: "sparkles")
                    }

                aboutTab
                    .tabItem {
                        Label(languageManager.text(.about), systemImage: "info.circle")
                    }
            }
            .focusEffectDisabled()
            .padding(.horizontal, 16)
            .padding(.bottom, 16)
        }
        .focusEffectDisabled()
        .frame(width: 540, height: 530)
        .id("settings-\(themeManager.themeRevision)-\(languageManager.effectiveLanguage.rawValue)")
        .preferredColorScheme(themeManager.effectiveColorScheme)
        .sheet(isPresented: $updateChecker.showUpdateSheet) {
            UpdateAvailableSheetView {
                updateChecker.showUpdateSheet = false
            }
        }
        .alert(languageManager.text(.upToDateTitle), isPresented: $updateChecker.showUpToDateAlert) {
            Button(languageManager.text(.done), role: .cancel) {}
        } message: {
            Text(languageManager.text(.upToDateMessage))
        }
    }

    private var generalTab: some View {
        Form {
            Section {
                Picker(languageManager.text(.appearance), selection: $themeManager.currentTheme) {
                    ForEach(AppTheme.allCases) { theme in
                        Text(theme.localizedName(for: languageManager.effectiveLanguage)).tag(theme)
                    }
                }
                .pickerStyle(.menu)

                Picker(languageManager.text(.selectLanguage), selection: $languageManager.currentLanguage) {
                    ForEach(AppLanguage.allCases) { lang in
                        Text(lang.localizedName(for: languageManager.effectiveLanguage)).tag(lang)
                    }
                }
                .pickerStyle(.menu)

                Picker(languageManager.text(.defaultEncoding), selection: $defaultEncoding) {
                    ForEach(FileEncoding.allCases) { enc in
                        Text(enc.rawValue).tag(enc.rawValue)
                    }
                }
                .pickerStyle(.menu)
            }

            Section(header: Text(languageManager.text(.coreEngineSection)).font(.caption).foregroundColor(.secondary)) {
                Picker(languageManager.text(.diffEngine), selection: Binding(
                    get: { enginePreference },
                    set: { newValue in
                        enginePreference = newValue
                        DiffEngineService.shared.enginePreference = newValue
                    }
                )) {
                    ForEach(DiffEnginePreference.allCases) { pref in
                        Text(pref.localizedName(for: languageManager.effectiveLanguage)).tag(pref)
                    }
                }
                .pickerStyle(.menu)

                HStack {
                    Text(languageManager.text(.currentEngineStatus))
                        .foregroundColor(.secondary)
                    Spacer()
                    HStack(spacing: 6) {
                        Circle()
                            .fill(currentEngineStatusColor)
                            .frame(width: 7, height: 7)
                        Text(currentEngineStatusText)
                            .font(.system(size: 11, weight: .medium, design: .monospaced))
                            .foregroundColor(.secondary)
                    }
                }
            }

            Section(header: Text(languageManager.text(.defaultDiffSettings)).font(.caption).foregroundColor(.secondary)) {
                Toggle(languageManager.text(.ignoreWhitespace), isOn: $defaultIgnoreWhitespace)
                Toggle(languageManager.text(.ignoreCase), isOn: $defaultIgnoreCase)
                Toggle(isOn: $createBakBackupOnSave) {
                    VStack(alignment: .leading, spacing: 2) {
                        Text(languageManager.text(.createBakBackupTitle))
                            .font(.system(size: 13))
                        Text(languageManager.text(.createBakBackupDesc))
                            .font(.system(size: 11))
                            .foregroundColor(.secondary)
                    }
                }
                Toggle(languageManager.text(.autoCheckUpdatesOnLaunch), isOn: $updateChecker.isAutoCheckEnabled)
            }
        }
        .formStyle(.grouped)
        .focusEffectDisabled()
    }

    private var folderDiffTab: some View {
        Form {
            Section {
                Picker(languageManager.text(.defaultCompareMode), selection: $defaultFolderMode) {
                    Text(languageManager.text(.quickCompareMode)).tag("Quick")
                    Text(languageManager.text(.deepHashCompareMode)).tag("DeepHash")
                }
                .pickerStyle(.radioGroup)
            }

            Section(header: Text(languageManager.text(.defaultExcludedPatterns)).font(.caption).foregroundColor(.secondary)) {
                Text(".git, .DS_Store, node_modules, target, build")
                    .font(.system(size: 11, design: .monospaced))
                    .foregroundColor(.secondary)
            }
        }
        .formStyle(.grouped)
        .focusEffectDisabled()
    }

    private var aiEngineTab: some View {
        VStack(spacing: 8) {
            Picker("", selection: $aiModelManager.executionMode) {
                Text(languageManager.text(.aiEngineModeLocal)).tag(AIExecutionMode.localOffline)
                Text(languageManager.text(.aiEngineModeCloud)).tag(AIExecutionMode.cloudAPI)
            }
            .pickerStyle(.segmented)
            .labelsHidden()
            .padding(.horizontal, 20)
            .padding(.top, 8)

            Form {
                if aiModelManager.executionMode == .localOffline {
                    Section(header: Text(languageManager.text(.aiModelLibrary)).font(.caption).foregroundColor(.secondary)) {
                        VStack(spacing: 10) {
                            modelCardView(for: .gemma3_1b)
                            modelCardView(for: .gemma3_4b)
                        }
                        .padding(.vertical, 2)
                    }

                    Section(header: Text(languageManager.text(.defaultDiffSettings)).font(.caption).foregroundColor(.secondary)) {
                        Toggle(languageManager.text(.aiAutoReleaseMemory), isOn: $aiModelManager.autoReleaseMemory)
                        Text(languageManager.text(.aiAutoReleaseMemoryDesc))
                            .font(.system(size: 11))
                            .foregroundColor(.secondary)
                    }
                } else {
                    cloudAPISettingsSection
                }
            }
            .formStyle(.grouped)
            .focusEffectDisabled()
            .animation(.easeInOut(duration: 0.15), value: aiModelManager.executionMode)
        }
    }

    @ViewBuilder
    private var cloudAPISettingsSection: some View {
        Section(header: Text(languageManager.text(.aiCloudPreset)).font(.caption).foregroundColor(.secondary)) {
            Picker(languageManager.text(.aiCloudPreset), selection: Binding(
                get: { aiModelManager.cloudConfig.providerPreset },
                set: { newPreset in
                    var cfg = aiModelManager.cloudConfig
                    cfg.providerPreset = newPreset
                    if newPreset != .custom {
                        cfg.baseURL = newPreset.defaultBaseURL
                        cfg.modelName = newPreset.defaultModel
                    }
                    aiModelManager.cloudConfig = cfg
                }
            )) {
                ForEach(CloudProviderPreset.allCases) { preset in
                    Text(preset.displayName).tag(preset)
                }
            }
        }

        Section(header: Text("API Configuration").font(.caption).foregroundColor(.secondary)) {
            // Base URL
            VStack(alignment: .leading, spacing: 4) {
                Text(languageManager.text(.aiCloudBaseURL))
                    .font(.system(size: 11, weight: .medium))
                    .foregroundColor(.secondary)
                TextField("", text: Binding(
                    get: { aiModelManager.cloudConfig.baseURL },
                    set: {
                        var cfg = aiModelManager.cloudConfig
                        cfg.baseURL = $0
                        aiModelManager.cloudConfig = cfg
                    }
                ), prompt: Text("https://api.deepseek.com/v1"))
                .textFieldStyle(.roundedBorder)
                .labelsHidden()
            }

            // API Key
            VStack(alignment: .leading, spacing: 4) {
                Text(languageManager.text(.aiCloudAPIKey))
                    .font(.system(size: 11, weight: .medium))
                    .foregroundColor(.secondary)
                HStack(spacing: 8) {
                    if showAPIKeyPlaintext {
                        TextField("", text: Binding(
                            get: { aiModelManager.cloudConfig.apiKey },
                            set: {
                                var cfg = aiModelManager.cloudConfig
                                cfg.apiKey = $0
                                aiModelManager.cloudConfig = cfg
                            }
                        ), prompt: Text("sk-..."))
                        .textFieldStyle(.roundedBorder)
                        .labelsHidden()
                    } else {
                        SecureField("", text: Binding(
                            get: { aiModelManager.cloudConfig.apiKey },
                            set: {
                                var cfg = aiModelManager.cloudConfig
                                cfg.apiKey = $0
                                aiModelManager.cloudConfig = cfg
                            }
                        ), prompt: Text("sk-..."))
                        .textFieldStyle(.roundedBorder)
                        .labelsHidden()
                    }

                    Button {
                        showAPIKeyPlaintext.toggle()
                    } label: {
                        Image(systemName: showAPIKeyPlaintext ? "eye.slash" : "eye")
                            .foregroundColor(.secondary)
                    }
                    .buttonStyle(.plain)
                    .help(showAPIKeyPlaintext ? "隐藏 API Key" : "显示 API Key")
                }
            }

            // Model Name
            VStack(alignment: .leading, spacing: 4) {
                Text(languageManager.text(.aiCloudModelName))
                    .font(.system(size: 11, weight: .medium))
                    .foregroundColor(.secondary)
                TextField("", text: Binding(
                    get: { aiModelManager.cloudConfig.modelName },
                    set: {
                        var cfg = aiModelManager.cloudConfig
                        cfg.modelName = $0
                        aiModelManager.cloudConfig = cfg
                    }
                ), prompt: Text("deepseek-chat / gpt-4o-mini"))
                .textFieldStyle(.roundedBorder)
                .labelsHidden()
            }

            // Test Connection Button & Status
            HStack(spacing: 12) {
                Button {
                    testCloudConnection()
                } label: {
                    HStack(spacing: 6) {
                        if isTestingCloudAPI {
                            ProgressView()
                                .controlSize(.mini)
                            Text(languageManager.text(.aiCloudTesting))
                        } else {
                            Image(systemName: "network")
                            Text(languageManager.text(.aiCloudTestConnection))
                        }
                    }
                }
                .disabled(isTestingCloudAPI)
                .controlSize(.small)

                if let msg = cloudTestMessage {
                    HStack(spacing: 4) {
                        Image(systemName: cloudTestIsSuccess ? "checkmark.circle.fill" : "exclamationmark.triangle.fill")
                            .foregroundColor(cloudTestIsSuccess ? .green : .red)
                            .font(.system(size: 12))
                        Text(msg)
                            .font(.system(size: 11))
                            .foregroundColor(cloudTestIsSuccess ? .green : .red)
                            .lineLimit(1)
                    }
                }
            }
            .padding(.top, 4)
        }
    }

    private func testCloudConnection() {
        isTestingCloudAPI = true
        cloudTestMessage = nil
        let cfg = aiModelManager.cloudConfig

        Task {
            do {
                let latency = try await CloudLLMService.shared.testConnection(config: cfg)
                await MainActor.run {
                    self.isTestingCloudAPI = false
                    self.cloudTestIsSuccess = true
                    self.cloudTestMessage = "\(languageManager.text(.aiCloudTestSuccess)) (\(String(format: "%.2fs", latency)))"
                }
            } catch {
                await MainActor.run {
                    self.isTestingCloudAPI = false
                    self.cloudTestIsSuccess = false
                    self.cloudTestMessage = "\(languageManager.text(.aiCloudTestFailed)): \(error.localizedDescription)"
                }
            }
        }
    }

    @ViewBuilder
    private func modelCardView(for model: AIModelId) -> some View {
        let state = aiModelManager.modelStates[model] ?? .notDownloaded

        VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 3) {
                    HStack(spacing: 6) {
                        Image(systemName: "sparkles")
                            .foregroundColor(.purple)
                            .font(.system(size: 12))
                        Text(model.displayName)
                            .font(.system(size: 12, weight: .bold))
                            .foregroundColor(.primary)
                    }

                    Text(model.subtitle)
                        .font(.system(size: 11))
                        .foregroundColor(.secondary)
                }

                Spacer()

                // Action buttons / status badge
                switch state {
                case .ready:
                    HStack(spacing: 8) {
                        let isCurrent = aiModelManager.effectiveModel == model
                        if isCurrent {
                            HStack(spacing: 4) {
                                Circle().fill(Color.green).frame(width: 6, height: 6)
                                Text(languageManager.text(.aiModelInUse))
                                    .font(.system(size: 11, weight: .semibold))
                                    .foregroundColor(.green)
                            }
                            .padding(.horizontal, 8)
                            .padding(.vertical, 3)
                            .background(Color.green.opacity(0.12))
                            .clipShape(Capsule())
                        } else {
                            Button {
                                aiModelManager.selectedModel = model
                            } label: {
                                Text(languageManager.text(.aiModelSetDefault))
                                    .font(.system(size: 11, weight: .medium))
                            }
                            .buttonStyle(.bordered)
                            .controlSize(.small)
                        }

                        Button {
                            aiModelManager.deleteModel(model: model)
                        } label: {
                            Image(systemName: "trash")
                                .font(.system(size: 11))
                                .foregroundColor(.secondary)
                        }
                        .buttonStyle(.plain)
                        .help(languageManager.text(.aiCleanModel))
                    }

                case .notDownloaded:
                    Button {
                        aiModelManager.startDownload(model: model)
                    } label: {
                        HStack(spacing: 4) {
                            Image(systemName: "arrow.down.circle")
                            Text(languageManager.text(.aiDownloadModel))
                        }
                        .font(.system(size: 11, weight: .medium))
                    }
                    .buttonStyle(.borderedProminent)
                    .tint(.purple.opacity(0.85))
                    .controlSize(.small)

                case .downloading(let progress, _, _):
                    HStack(spacing: 8) {
                        ProgressView(value: progress)
                            .progressViewStyle(.linear)
                            .frame(width: 70)

                        Button {
                            aiModelManager.cancelDownload(for: model)
                        } label: {
                            Image(systemName: "xmark.circle.fill")
                                .font(.system(size: 13))
                                .foregroundColor(.secondary)
                        }
                        .buttonStyle(.plain)
                    }

                case .error(let msg):
                    HStack(spacing: 6) {
                        Text(msg)
                            .font(.system(size: 10))
                            .foregroundColor(.red)
                            .lineLimit(1)

                        Button {
                            aiModelManager.startDownload(model: model)
                        } label: {
                            Image(systemName: "arrow.clockwise")
                        }
                        .controlSize(.mini)
                    }
                }
            }

            if case .downloading(let progress, let speed, let rem) = state {
                HStack {
                    Text("\(Int(progress * 100))%")
                        .font(.system(size: 10, weight: .semibold, design: .monospaced))
                    Spacer()
                    Text("\(speed) · \(rem)")
                        .font(.system(size: 10, design: .monospaced))
                        .foregroundColor(.secondary)
                }
            }
        }
        .padding(10)
        .background(Color.secondary.opacity(0.08))
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 8, style: .continuous)
                .stroke(state.isReady ? Color.purple.opacity(0.3) : Color.primary.opacity(0.06), lineWidth: 1)
        )
    }

    private var aboutTab: some View {
        VStack(spacing: 12) {
            Image(nsImage: NSApp.applicationIconImage)
                .resizable()
                .frame(width: 56, height: 56)

            VStack(spacing: 2) {
                Text("MacCompare")
                    .font(.title3.bold())
                Text("\(languageManager.text(.version)) \(updateChecker.currentVersion)")
                    .font(.subheadline)
                    .foregroundColor(.secondary)
            }

            Text(languageManager.text(.universalBinary))
                .font(.caption)
                .padding(.horizontal, 8)
                .padding(.vertical, 3)
                .background(Capsule().fill(Color.accentColor.opacity(0.15)))
                .foregroundColor(.accentColor)

            Divider().padding(.vertical, 2)

            // Check for Updates button
            Button(action: {
                updateChecker.checkForUpdates(isUserInitiated: true)
            }) {
                HStack(spacing: 6) {
                    if updateChecker.status == .checking {
                        ProgressView()
                            .controlSize(.small)
                        Text(languageManager.text(.checkingForUpdates))
                    } else {
                        Image(systemName: "arrow.triangle.2.circlepath")
                        Text(languageManager.text(.checkForUpdates))
                    }
                }
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.regular)
            .disabled(updateChecker.status == .checking)

            if let date = updateChecker.lastCheckedDate {
                Text("\(languageManager.text(.lastChecked)): \(formattedDate(date))")
                    .font(.caption2)
                    .foregroundColor(.secondary)
            }

            Link(languageManager.text(.gitHubRepo), destination: URL(string: "https://github.com/andychao217/FileCompare")!)
                .font(.caption)
                .padding(.top, 2)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(.vertical, 8)
    }

    private func formattedDate(_ date: Date) -> String {
        let formatter = DateFormatter()
        formatter.dateStyle = .short
        formatter.timeStyle = .short
        return formatter.string(from: date)
    }

    private var currentEngineStatusText: String {
        switch enginePreference {
        case .swift:
            return languageManager.text(.engineStatusSwift)
        case .rust:
            return DiffEngineService.shared.isRustAvailable ? "\(RustDiffBridge.version) [Rust]" : languageManager.text(.engineStatusRustUnavailable)
        case .auto:
            return DiffEngineService.shared.isRustAvailable ? "\(RustDiffBridge.version) [Auto / Rust]" : languageManager.text(.engineStatusAutoFallback)
        }
    }

    private var currentEngineStatusColor: Color {
        switch enginePreference {
        case .swift:
            return .blue
        case .rust:
            return DiffEngineService.shared.isRustAvailable ? .green : .orange
        case .auto:
            return DiffEngineService.shared.isRustAvailable ? .green : .blue
        }
    }
}
