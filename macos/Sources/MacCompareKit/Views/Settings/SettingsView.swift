import SwiftUI

public struct SettingsView: View {
    @State private var languageManager = LanguageManager.shared
    @AppStorage("default_file_encoding") private var defaultEncoding: String = "UTF-8"
    @AppStorage("default_folder_mode") private var defaultFolderMode: String = "Quick"
    @AppStorage("default_ignore_whitespace") private var defaultIgnoreWhitespace: Bool = false
    @AppStorage("default_ignore_case") private var defaultIgnoreCase: Bool = false

    public init() {}

    public var body: some View {
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
        .frame(width: 480, height: 320)
        .padding(16)
    }

    private var generalTab: some View {
        Form {
            Section {
                Picker(languageManager.text(.selectLanguage), selection: $languageManager.currentLanguage) {
                    ForEach(AppLanguage.allCases) { lang in
                        Text(lang.displayName).tag(lang)
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
                .frame(width: 64, height: 64)

            VStack(spacing: 3) {
                Text("MacCompare")
                    .font(.title2.bold())
                Text("\(languageManager.text(.version)) 0.1.0")
                    .font(.subheadline)
                    .foregroundColor(.secondary)
            }

            Text(languageManager.text(.universalBinary))
                .font(.caption)
                .padding(.horizontal, 8)
                .padding(.vertical, 3)
                .background(Capsule().fill(Color.accentColor.opacity(0.15)))
                .foregroundColor(.accentColor)

            Divider().padding(.vertical, 4)

            Link(languageManager.text(.gitHubRepo), destination: URL(string: "https://github.com/andychao217/FileCompare")!)
                .font(.caption)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}
