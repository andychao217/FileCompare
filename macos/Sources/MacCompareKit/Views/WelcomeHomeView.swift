import SwiftUI
import UniformTypeIdentifiers

public struct WelcomeHomeView: View {
    @Bindable var tabManager: TabManager
    @State private var languageManager = LanguageManager.shared
    @State private var historyManager = RecentHistoryManager.shared
    @State private var hoveredCardType: TabContentType?
    @State private var showClearAllHistoryConfirmation: Bool = false

    public init(tabManager: TabManager) {
        self.tabManager = tabManager
    }

    public var body: some View {
        VStack(spacing: 0) {
            Spacer(minLength: 24)

            // Header Section: App Icon + Title + Subtitle
            VStack(spacing: 10) {
                appIconView
                    .shadow(color: Color.accentColor.opacity(0.25), radius: 16, x: 0, y: 8)

                Text(languageManager.text(.welcomeTitle))
                    .font(.system(size: 26, weight: .bold))
                    .foregroundColor(.primary)

                Text(languageManager.text(.welcomeSubtitle))
                    .font(.system(size: 13))
                    .foregroundColor(.secondary)
            }

            Spacer(minLength: 28)

            // Grid of Mode Launch Cards
            VStack(spacing: 14) {
                HStack(spacing: 14) {
                    modeCard(
                        type: .textDiff,
                        title: languageManager.text(.welcomeTextDiffTitle),
                        desc: languageManager.text(.welcomeTextDiffDesc),
                        icon: "doc.text.fill",
                        iconColor: .blue,
                        shortcut: "⌘N"
                    )

                    modeCard(
                        type: .excelDiff,
                        title: languageManager.text(.welcomeExcelDiffTitle),
                        desc: languageManager.text(.welcomeExcelDiffDesc),
                        icon: "tablecells.fill",
                        iconColor: .green,
                        shortcut: "⇧⌘E"
                    )
                }

                HStack(spacing: 14) {
                    modeCard(
                        type: .wordDiff,
                        title: languageManager.text(.welcomeWordDiffTitle),
                        desc: languageManager.text(.welcomeWordDiffDesc),
                        icon: "doc.richtext.fill",
                        iconColor: Color(red: 0.18, green: 0.45, blue: 0.95),
                        shortcut: "⇧⌘D"
                    )

                    modeCard(
                        type: .pdfDiff,
                        title: languageManager.text(.welcomePdfDiffTitle),
                        desc: languageManager.text(.welcomePdfDiffDesc),
                        icon: "doc.viewfinder.fill",
                        iconColor: Color(red: 0.88, green: 0.11, blue: 0.28),
                        shortcut: "⇧⌘P"
                    )
                }

                HStack(spacing: 14) {
                    modeCard(
                        type: .folderDiff,
                        title: languageManager.text(.welcomeFolderDiffTitle),
                        desc: languageManager.text(.welcomeFolderDiffDesc),
                        icon: "folder.fill",
                        iconColor: .orange,
                        shortcut: "⇧⌘N"
                    )
                }
            }
            .frame(maxWidth: 720)
            .padding(.horizontal, 24)

            Spacer(minLength: 32)

            Divider()

            // Bottom Section: Recent Comparisons & Open Existing
            bottomHistoryBar
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(Color(nsColor: .windowBackgroundColor))
    }

    // MARK: - App Icon View

    private var appIconView: some View {
        Group {
            if let icon = NSImage(named: "AppIcon") ?? NSApp.applicationIconImage {
                Image(nsImage: icon)
                    .resizable()
                    .aspectRatio(contentMode: .fit)
                    .frame(width: 76, height: 76)
                    .cornerRadius(17)
            } else {
                ZStack {
                    RoundedRectangle(cornerRadius: 17, style: .continuous)
                        .fill(
                            LinearGradient(
                                colors: [Color.blue, Color.indigo],
                                startPoint: .topLeading,
                                endPoint: .bottomTrailing
                            )
                        )
                        .frame(width: 76, height: 76)

                    Image(systemName: "arrow.left.arrow.right")
                        .font(.system(size: 34, weight: .bold))
                        .foregroundColor(.white)
                }
            }
        }
    }

    // MARK: - Mode Card Button

    @ViewBuilder
    private func modeCard(
        type: TabContentType,
        title: String,
        desc: String,
        icon: String,
        iconColor: Color,
        shortcut: String
    ) -> some View {
        let isHovered = (hoveredCardType == type)

        Button {
            tabManager.addTab(type: type)
        } label: {
            HStack(spacing: 14) {
                // Icon Badge
                ZStack {
                    RoundedRectangle(cornerRadius: 10, style: .continuous)
                        .fill(iconColor.opacity(0.18))
                        .frame(width: 44, height: 44)

                    Image(systemName: icon)
                        .font(.system(size: 22))
                        .foregroundColor(iconColor)
                }

                // Text info
                VStack(alignment: .leading, spacing: 3) {
                    HStack {
                        Text(title)
                            .font(.system(size: 14, weight: .semibold))
                            .foregroundColor(.primary)
                        Spacer()
                        Text(shortcut)
                            .font(.system(size: 11, weight: .bold, design: .rounded))
                            .foregroundColor(.primary)
                            .padding(.horizontal, 7)
                            .padding(.vertical, 3)
                            .background(
                                RoundedRectangle(cornerRadius: 6, style: .continuous)
                                    .fill(Color(nsColor: .controlColor).opacity(0.9))
                                    .overlay(
                                        RoundedRectangle(cornerRadius: 6, style: .continuous)
                                            .stroke(Color.primary.opacity(0.2), lineWidth: 1)
                                    )
                            )
                    }

                    Text(desc)
                        .font(.system(size: 11))
                        .foregroundColor(.secondary)
                        .lineLimit(2)
                        .multilineTextAlignment(.leading)
                }
            }
            .padding(14)
            .frame(maxWidth: .infinity, minHeight: 76, alignment: .leading)
            .background(
                RoundedRectangle(cornerRadius: 12, style: .continuous)
                    .fill(Color(nsColor: .controlBackgroundColor).opacity(isHovered ? 0.9 : 0.6))
                    .overlay(
                        RoundedRectangle(cornerRadius: 12, style: .continuous)
                            .stroke(isHovered ? iconColor.opacity(0.4) : Color(nsColor: .separatorColor).opacity(0.5), lineWidth: 1)
                    )
            )
            .scaleEffect(isHovered ? 1.015 : 1.0)
            .animation(.easeInOut(duration: 0.15), value: isHovered)
        }
        .buttonStyle(.plain)
        .focusEffectDisabled()
        .focusable(false)
        .onHover { h in
            hoveredCardType = h ? type : nil
        }
    }

    // MARK: - Bottom History Bar

    private var bottomHistoryBar: some View {
        HStack(spacing: 14) {
            // Recent comparisons summary
            VStack(alignment: .leading, spacing: 4) {
                HStack(spacing: 8) {
                    Text(languageManager.text(.recentComparisons))
                        .font(.system(size: 12, weight: .semibold))
                        .foregroundColor(.primary)

                    if !historyManager.records.isEmpty {
                        Button(languageManager.text(.clearAll)) {
                            showClearAllHistoryConfirmation = true
                        }
                        .font(.system(size: 10))
                        .foregroundColor(.secondary)
                        .buttonStyle(.plain)
                        .alert(languageManager.text(.confirmClearHistoryTitle), isPresented: $showClearAllHistoryConfirmation) {
                            Button(languageManager.text(.clear), role: .destructive) {
                                withAnimation {
                                    historyManager.clearAll()
                                }
                            }
                            Button(languageManager.text(.cancel), role: .cancel) {}
                        } message: {
                            Text(languageManager.text(.confirmClearHistoryMessage))
                        }
                    }
                }

                if historyManager.records.isEmpty {
                    Text(languageManager.text(.noRecentComparisons))
                        .font(.system(size: 11))
                        .foregroundColor(.secondary)
                } else {
                    ScrollView(.horizontal, showsIndicators: false) {
                        HStack(spacing: 8) {
                            ForEach(historyManager.records) { rec in
                                HStack(spacing: 6) {
                                    Button {
                                        openRecentRecord(rec)
                                    } label: {
                                        HStack(spacing: 4) {
                                            Image(systemName: rec.type.iconName)
                                                .font(.system(size: 10))
                                                .foregroundColor(.accentColor)
                                            Text(rec.title)
                                                .font(.system(size: 11))
                                                .lineLimit(1)
                                            Text("· \(rec.formattedDate)")
                                                .font(.system(size: 9))
                                                .foregroundColor(.secondary)
                                        }
                                    }
                                    .buttonStyle(.plain)
                                    .focusEffectDisabled()

                                    Button {
                                        withAnimation(.easeInOut(duration: 0.2)) {
                                            historyManager.removeRecord(id: rec.id)
                                        }
                                    } label: {
                                        Image(systemName: "xmark")
                                            .font(.system(size: 8, weight: .bold))
                                            .foregroundColor(.secondary.opacity(0.8))
                                            .padding(3)
                                            .contentShape(Rectangle())
                                    }
                                    .buttonStyle(.plain)
                                    .help(languageManager.text(.deleteRecord))
                                }
                                .padding(.leading, 8)
                                .padding(.trailing, 6)
                                .padding(.vertical, 4)
                                .background(Color(nsColor: .controlBackgroundColor))
                                .cornerRadius(6)
                                .contextMenu {
                                    Button(role: .destructive) {
                                        withAnimation(.easeInOut(duration: 0.2)) {
                                            historyManager.removeRecord(id: rec.id)
                                        }
                                    } label: {
                                        Label(languageManager.text(.delete), systemImage: "trash")
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Spacer()

            // Open Existing button
            Button {
                openExistingFiles()
            } label: {
                Label(languageManager.text(.openExisting), systemImage: "folder")
                    .font(.system(size: 12, weight: .medium))
            }
            .buttonStyle(.bordered)
            .controlSize(.regular)
            .focusEffectDisabled()
        }
        .padding(.horizontal, 24)
        .padding(.vertical, 12)
        .background(Color(nsColor: .windowBackgroundColor).opacity(0.8))
    }

    private func openRecentRecord(_ record: RecentCompareRecord) {
        let leftURL = URL(fileURLWithPath: record.leftPath)
        let rightURL = URL(fileURLWithPath: record.rightPath)
        tabManager.openAutoDiff(left: leftURL, right: rightURL)
    }

    private func openExistingFiles() {
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = true
        panel.canChooseDirectories = true
        panel.canChooseFiles = true
        panel.prompt = "Compare"
        if panel.runModal() == .OK {
            let urls = panel.urls
            if urls.count >= 2 {
                tabManager.openAutoDiff(left: urls[0], right: urls[1])
            } else if urls.count == 1 {
                tabManager.openAutoDiff(left: urls[0], right: urls[0])
            }
        }
    }
}
