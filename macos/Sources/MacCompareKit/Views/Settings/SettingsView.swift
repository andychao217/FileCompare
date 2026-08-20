import SwiftUI

public struct SettingsView: View {
    @State private var languageManager = LanguageManager.shared
    @State private var themeManager = ThemeManager.shared
    @State private var updateChecker = UpdateCheckerService.shared
    @AppStorage("default_file_encoding") private var defaultEncoding: String = "UTF-8"
    @AppStorage("default_folder_mode") private var defaultFolderMode: String = "Quick"
    @AppStorage("default_ignore_whitespace") private var defaultIgnoreWhitespace: Bool = false
    @AppStorage("default_ignore_case") private var defaultIgnoreCase: Bool = false
    @AppStorage("create_bak_backup_on_save") private var createBakBackupOnSave: Bool = false
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

                aboutTab
                    .tabItem {
                        Label(languageManager.text(.about), systemImage: "info.circle")
                    }
            }
            .padding(.horizontal, 16)
            .padding(.bottom, 16)
        }
        .frame(width: 500, height: 430)
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
}
