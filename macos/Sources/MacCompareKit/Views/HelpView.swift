import SwiftUI

public struct HelpView: View {
    @State private var languageManager = LanguageManager.shared
    @State private var selectedTab: Int = 0
    public var onDismiss: (() -> Void)?

    public init(onDismiss: (() -> Void)? = nil) {
        self.onDismiss = onDismiss
    }

    public var body: some View {
        VStack(spacing: 0) {
            // Header
            HStack(spacing: 12) {
                Image(systemName: "questionmark.circle.fill")
                    .font(.system(size: 22))
                    .foregroundColor(.accentColor)

                VStack(alignment: .leading, spacing: 2) {
                    Text(languageManager.text(.help))
                        .font(.headline)
                        .foregroundColor(.primary)
                    Text("MacCompare v0.1.0 • \(languageManager.text(.helpSubtitle))")
                        .font(.caption)
                        .foregroundColor(.secondary)
                }

                Spacer()

                Picker("", selection: $selectedTab) {
                    Text(languageManager.text(.coreFeatures)).tag(0)
                    Text(languageManager.text(.shortcuts)).tag(1)
                    Text(languageManager.text(.about)).tag(2)
                }
                .pickerStyle(.segmented)
                .frame(width: 270)

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
            .padding(.vertical, 14)
            .background(Color(nsColor: .windowBackgroundColor))

            Divider()

            // Content Body
            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    if selectedTab == 0 {
                        featuresSection
                    } else if selectedTab == 1 {
                        shortcutsSection
                    } else {
                        aboutSection
                    }
                }
                .padding(24)
            }
        }
        .frame(width: 680, height: 490)
        .background(Color(nsColor: .controlBackgroundColor))
    }

    // MARK: - Features Section

    private var featuresSection: some View {
        VStack(alignment: .leading, spacing: 16) {
            featureRow(
                icon: "doc.text.magnifyingglass",
                title: languageManager.text(.textDiff),
                description: languageManager.text(.textDiffDesc)
            )

            featureRow(
                icon: "folder.badge.gearshape",
                title: languageManager.text(.folderDiff),
                description: languageManager.text(.folderDiffDesc)
            )

            featureRow(
                icon: "arrow.triangle.merge",
                title: languageManager.text(.threeWayMerge),
                description: languageManager.text(.threeWayMergeDesc)
            )

            featureRow(
                icon: "uiwindow.split.2x1",
                title: languageManager.text(.tabDragMergeTitle),
                description: languageManager.text(.tabDragMergeDesc)
            )
        }
    }

    private func featureRow(icon: String, title: String, description: String) -> some View {
        HStack(alignment: .top, spacing: 14) {
            Image(systemName: icon)
                .font(.system(size: 20))
                .foregroundColor(.accentColor)
                .frame(width: 28, height: 28)
                .background(Color.accentColor.opacity(0.12))
                .clipShape(RoundedRectangle(cornerRadius: 6))

            VStack(alignment: .leading, spacing: 4) {
                Text(title)
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundColor(.primary)
                Text(description)
                    .font(.system(size: 12))
                    .foregroundColor(.secondary)
                    .lineSpacing(3)
            }
        }
        .padding(12)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(
            RoundedRectangle(cornerRadius: 8)
                .fill(Color(nsColor: .windowBackgroundColor).opacity(0.6))
        )
    }

    // MARK: - Shortcuts Section

    private var shortcutsSection: some View {
        VStack(spacing: 10) {
            shortcutRow(key: "⌘ N", desc: languageManager.text(.newTextCompare))
            shortcutRow(key: "⇧ ⌘ N", desc: languageManager.text(.newFolderCompare))
            shortcutRow(key: "⌥ ⌘ M", desc: languageManager.text(.newThreeWayMerge))
            shortcutRow(key: "⌘ O", desc: languageManager.text(.openFile))
            shortcutRow(key: "⌘ S", desc: languageManager.text(.save))
            shortcutRow(key: "⌘ W", desc: languageManager.text(.closeTab))
            shortcutRow(key: "⌘ ]", desc: languageManager.text(.nextDiff))
            shortcutRow(key: "⌘ [", desc: languageManager.text(.prevDiff))
            shortcutRow(key: "⌥ ⌘ ←", desc: languageManager.text(.takeLeft))
            shortcutRow(key: "⌥ ⌘ →", desc: languageManager.text(.takeRight))
            shortcutRow(key: "⌥ ⌘ W", desc: languageManager.text(.ignoreWhitespace))
            shortcutRow(key: "⌥ ⌘ C", desc: languageManager.text(.ignoreCase))
            shortcutRow(key: "ESC", desc: languageManager.text(.cancelTabDragDesc))
        }
    }

    private func shortcutRow(key: String, desc: String) -> some View {
        HStack {
            Text(desc)
                .font(.system(size: 12))
                .foregroundColor(.primary)

            Spacer()

            Text(key)
                .font(.system(size: 11, weight: .bold, design: .monospaced))
                .padding(.horizontal, 8)
                .padding(.vertical, 4)
                .background(Color(nsColor: .windowBackgroundColor))
                .clipShape(RoundedRectangle(cornerRadius: 4))
                .overlay(
                    RoundedRectangle(cornerRadius: 4)
                        .stroke(Color.secondary.opacity(0.2), lineWidth: 1)
                )
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 6)
        .background(
            RoundedRectangle(cornerRadius: 6)
                .fill(Color(nsColor: .windowBackgroundColor).opacity(0.4))
        )
    }

    // MARK: - About Section

    private var aboutSection: some View {
        VStack(spacing: 16) {
            Image(systemName: "doc.on.doc.fill")
                .font(.system(size: 48))
                .foregroundColor(.accentColor)
                .padding(.top, 8)

            Text("MacCompare")
                .font(.system(size: 18, weight: .bold))

            Text("Version 0.1.0 • \(languageManager.text(.universalBinary))")
                .font(.caption)
                .foregroundColor(.secondary)

            Divider().padding(.vertical, 4)

            HStack(spacing: 8) {
                Text("\(languageManager.text(.gitHubRepo)):")
                    .font(.system(size: 12))
                    .foregroundColor(.secondary)

                Link("andychao217/FileCompare", destination: URL(string: "https://github.com/andychao217/FileCompare")!)
                    .font(.system(size: 12, weight: .medium))
            }

            Text(languageManager.text(.aboutFooter))
                .font(.caption2)
                .foregroundColor(.secondary.opacity(0.8))
        }
        .frame(maxWidth: .infinity)
        .padding(20)
    }
}
