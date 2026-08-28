import SwiftUI
import AppKit

public struct WordDiffToolbarView: View {
    @Bindable public var viewModel: WordDiffViewModel
    @State private var isShowingExportAlert: Bool = false
    @State private var exportMessage: String = ""

    public init(viewModel: WordDiffViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        HStack(spacing: 10) {
            // View Mode Picker (Adaptive & Compact)
            Picker("", selection: $viewModel.viewMode) {
                ForEach(WordViewMode.allCases) { mode in
                    Label(modeLocalizedTitle(mode), systemImage: mode.iconName)
                        .tag(mode)
                }
            }
            .pickerStyle(.segmented)
            .fixedSize(horizontal: true, vertical: false)

            Divider().frame(height: 16)

            // Difference Navigation (Only active when both sides are loaded)
            if viewModel.leftDocument != nil && viewModel.rightDocument != nil {
                HStack(spacing: 4) {
                    Button {
                        viewModel.prevDiff()
                    } label: {
                        Image(systemName: "chevron.up")
                            .font(.system(size: 11, weight: .semibold))
                    }
                    .buttonStyle(.bordered)
                    .controlSize(.small)
                    .disabled(viewModel.activeDifferencesBlocks.isEmpty)
                    .help(LanguageManager.shared.text(.prevDiff))

                    Button {
                        viewModel.nextDiff()
                    } label: {
                        Image(systemName: "chevron.down")
                            .font(.system(size: 11, weight: .semibold))
                    }
                    .buttonStyle(.bordered)
                    .controlSize(.small)
                    .disabled(viewModel.activeDifferencesBlocks.isEmpty)
                    .help(LanguageManager.shared.text(.nextDiff))

                    Text(diffCountText)
                        .font(.system(size: 11, weight: .medium))
                        .foregroundColor(.secondary)
                        .padding(.horizontal, 4)
                        .fixedSize()
                }

                Divider().frame(height: 16)

                // Filter & Ignore Options
                Menu {
                    Toggle(LanguageManager.shared.text(.ignoreWhitespace), isOn: $viewModel.ignoreWhitespace)
                    Toggle(LanguageManager.shared.text(.ignoreFormatting), isOn: $viewModel.ignoreFormatting)
                } label: {
                    Image(systemName: "slider.horizontal.3")
                        .font(.system(size: 11))
                    Text(LanguageManager.shared.text(.diffOptions))
                        .font(.system(size: 11))
                }
                .menuStyle(.borderlessButton)
                .fixedSize()

                Divider().frame(height: 16)
            }

            // Search / Filter Text Box
            HStack(spacing: 4) {
                Image(systemName: "magnifyingglass")
                    .foregroundColor(.secondary)
                    .font(.system(size: 10))
                TextField(LanguageManager.shared.text(.searchOrFilter), text: $viewModel.filterText)
                    .textFieldStyle(.plain)
                    .font(.system(size: 11))
                if !viewModel.filterText.isEmpty {
                    Button {
                        viewModel.filterText = ""
                    } label: {
                        Image(systemName: "xmark.circle.fill")
                            .foregroundColor(.secondary)
                            .font(.system(size: 10))
                    }
                    .buttonStyle(.plain)
                }
            }
            .padding(.horizontal, 8)
            .padding(.vertical, 4)
            .background(
                RoundedRectangle(cornerRadius: 6)
                    .fill(Color(nsColor: .textBackgroundColor).opacity(0.6))
                    .overlay(RoundedRectangle(cornerRadius: 6).stroke(Color.secondary.opacity(0.2), lineWidth: 0.5))
            )
            .frame(minWidth: 100, maxWidth: 160)

            Spacer()

            // Export Diff Report
            Button {
                exportReport()
            } label: {
                Label(LanguageManager.shared.text(.exportReport), systemImage: "arrow.down.doc")
                    .font(.system(size: 11, weight: .medium))
            }
            .buttonStyle(.bordered)
            .controlSize(.small)
            .disabled(!viewModel.hasDocumentsLoaded)
            .fixedSize()

            // Clear Button
            Button {
                viewModel.clearAll()
            } label: {
                Image(systemName: "trash")
                    .font(.system(size: 11))
                    .foregroundColor(.secondary)
            }
            .buttonStyle(.plain)
            .help(LanguageManager.shared.text(.clearAll))
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 6)
        .background(Color(nsColor: .windowBackgroundColor))
        .alert(LanguageManager.shared.text(.exportReport), isPresented: $isShowingExportAlert) {
            Button(LanguageManager.shared.text(.done), role: .cancel) {}
        } message: {
            Text(exportMessage)
        }
    }

    private var diffCountText: String {
        let count = viewModel.activeDifferencesBlocks.count
        if count == 0 {
            return "0 diffs"
        }
        return "\(viewModel.currentDiffIndex + 1)/\(count) diffs"
    }

    private func modeLocalizedTitle(_ mode: WordViewMode) -> String {
        switch mode {
        case .structuredContent: return LanguageManager.shared.text(.structuredContent)
        case .formattingDiff: return LanguageManager.shared.text(.formattingDiff)
        case .tableDiff: return LanguageManager.shared.text(.tableDiff)
        case .metadataDiff: return LanguageManager.shared.text(.metadataDiff)
        }
    }

    private func exportReport() {
        let panel = NSSavePanel()
        panel.allowedContentTypes = [.html]
        panel.nameFieldStringValue = "Word_Diff_Report_\(Date().timeIntervalSince1970).html"

        if panel.runModal() == .OK, let url = panel.url {
            let htmlContent = viewModel.exportHTMLReport()
            do {
                try htmlContent.write(to: url, atomically: true, encoding: .utf8)
                exportMessage = "Report exported successfully to: \(url.lastPathComponent)"
                isShowingExportAlert = true
            } catch {
                exportMessage = "Failed to export report: \(error.localizedDescription)"
                isShowingExportAlert = true
            }
        }
    }
}
