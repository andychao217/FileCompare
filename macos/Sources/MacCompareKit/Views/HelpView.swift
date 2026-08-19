import SwiftUI

public struct HelpView: View {
    @State private var languageManager = LanguageManager.shared
    @State private var selectedTab: Int = 0

    public init() {}

    public var body: some View {
        VStack(spacing: 0) {
            // Header
            HStack(spacing: 12) {
                Image(systemName: "questionmark.circle.fill")
                    .font(.system(size: 24))
                    .foregroundColor(.accentColor)

                VStack(alignment: .leading, spacing: 2) {
                    Text(languageManager.text(.help))
                        .font(.headline)
                        .foregroundColor(.primary)
                    Text("MacCompare v0.1.0 • macOS Native Diff & Merge")
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
                .frame(width: 320)
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
        .frame(width: 640, height: 480)
        .background(Color(nsColor: .controlBackgroundColor))
    }

    // MARK: - Features Section

    private var featuresSection: some View {
        VStack(alignment: .leading, spacing: 16) {
            featureRow(
                icon: "doc.text.magnifyingglass",
                title: languageManager.text(.textDiff),
                description: "高性能双向文本比对，支持逐行差异高亮、一键采纳左侧/右侧差异、字符级高亮、忽略空白符与大小写。"
            )

            featureRow(
                icon: "folder.badge.gearshape",
                title: languageManager.text(.folderDiff),
                description: "深度目录比对与同步工具。支持递归树形扫描、CRC32 深度哈希比对、常用位置快捷跳转与 Dry-Run 预演同步。"
            )

            featureRow(
                icon: "arrow.triangle.merge",
                title: languageManager.text(.threeWayMerge),
                description: "支持 Git 冲突三向合并 (Local, Base, Remote)。智能自动解决非冲突块，支持一键保存合并产物。"
            )

            featureRow(
                icon: "uiwindow.split.2x1",
                title: "Chrome 风格标签页拖拽与合并",
                description: "支持在窗口内拖拽排序、按住标签拖出窗口拆分为独立新窗口、将标签拖入其他窗口顶部标签栏实现多窗口自由合并。"
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
            shortcutRow(key: "ESC", desc: "取消当前 Tab 拖拽操作")
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

            Text("Version 0.1.0 • Universal Binary 2 (x86_64 + arm64)")
                .font(.caption)
                .foregroundColor(.secondary)

            Divider().padding(.vertical, 4)

            HStack(spacing: 8) {
                Text("GitHub Repository:")
                    .font(.system(size: 12))
                    .foregroundColor(.secondary)

                Link("andychao217/FileCompare", destination: URL(string: "https://github.com/andychao217/FileCompare")!)
                    .font(.system(size: 12, weight: .medium))
            }

            Text("Created for macOS 14.0+ with Swift & SwiftUI.")
                .font(.caption2)
                .foregroundColor(.secondary.opacity(0.8))
        }
        .frame(maxWidth: .infinity)
        .padding(20)
    }
}
