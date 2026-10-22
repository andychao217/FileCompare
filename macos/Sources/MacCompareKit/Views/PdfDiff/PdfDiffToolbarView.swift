import SwiftUI
import AppKit

public struct PdfDiffToolbarView: View {
    @Bindable public var viewModel: PdfDiffViewModel

    public init(viewModel: PdfDiffViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        HStack(spacing: 10) {
            // View Mode Picker
            Picker("", selection: $viewModel.viewMode) {
                ForEach(PdfViewMode.allCases, id: \.self) { mode in
                    Text(modeTitle(mode)).tag(mode)
                }
            }
            .pickerStyle(.segmented)
            .fixedSize()

            Divider().frame(height: 16)

            // Page Navigation
            HStack(spacing: 4) {
                Button {
                    viewModel.prevPage()
                } label: {
                    Image(systemName: "chevron.left")
                        .font(.system(size: 11, weight: .semibold))
                }
                .buttonStyle(.bordered)
                .controlSize(.small)
                .disabled(viewModel.currentPageIndex <= 0)

                Text(pageIndicatorText)
                    .font(.system(size: 11, weight: .medium, design: .monospaced))
                    .foregroundColor(.secondary)
                    .frame(minWidth: 70, alignment: .center)

                Button {
                    viewModel.nextPage()
                } label: {
                    Image(systemName: "chevron.right")
                        .font(.system(size: 11, weight: .semibold))
                }
                .buttonStyle(.bordered)
                .controlSize(.small)
                .disabled(viewModel.currentPageIndex + 1 >= viewModel.maxPageCount)
            }

            Divider().frame(height: 16)

            // Diff Navigation
            if viewModel.hasDocumentsLoaded {
                HStack(spacing: 4) {
                    Button {
                        viewModel.prevDiff()
                    } label: {
                        Image(systemName: "chevron.up")
                            .font(.system(size: 11, weight: .semibold))
                    }
                    .buttonStyle(.bordered)
                    .controlSize(.small)
                    .disabled(viewModel.modifiedBlocks.isEmpty && viewModel.currentSortedRegions.isEmpty)
                    .help(LanguageManager.shared.text(.prevDiff))

                    Button {
                        viewModel.nextDiff()
                    } label: {
                        Image(systemName: "chevron.down")
                            .font(.system(size: 11, weight: .semibold))
                    }
                    .buttonStyle(.bordered)
                    .controlSize(.small)
                    .disabled(viewModel.modifiedBlocks.isEmpty && viewModel.currentSortedRegions.isEmpty)
                    .help(LanguageManager.shared.text(.nextDiff))

                    Text(diffStatusText)
                        .font(.system(size: 11, weight: .medium))
                        .foregroundColor((viewModel.modifiedBlocks.isEmpty && viewModel.currentSortedRegions.isEmpty) ? .secondary : .orange)
                        .padding(.horizontal, 4)
                        .fixedSize()
                }

                Divider().frame(height: 16)
            }

            // Zoom Controls
            HStack(spacing: 2) {
                Button {
                    viewModel.zoomOut()
                } label: {
                    Image(systemName: "minus.magnifyingglass")
                        .font(.system(size: 11))
                }
                .buttonStyle(.bordered)
                .controlSize(.small)

                Button {
                    viewModel.resetZoom()
                } label: {
                    Text("\(Int(viewModel.zoomScale * 100))%")
                        .font(.system(size: 11, design: .monospaced))
                }
                .buttonStyle(.bordered)
                .controlSize(.small)

                Button {
                    viewModel.zoomIn()
                } label: {
                    Image(systemName: "plus.magnifyingglass")
                        .font(.system(size: 11))
                }
                .buttonStyle(.bordered)
                .controlSize(.small)
            }

            if viewModel.viewMode == .overlay {
                Divider().frame(height: 16)

                HStack(spacing: 4) {
                    Text(LanguageManager.shared.text(.pdfDiffOpacity))
                        .font(.system(size: 11))
                        .foregroundColor(.secondary)
                    Slider(value: $viewModel.overlayBlendAlpha, in: 0...1)
                        .frame(width: 70)
                }
            }

            Spacer()

            // Options: Diff Mask, Sync Scroll & Ignore Whitespace
            Toggle(isOn: $viewModel.showDiffMaskOverlay) {
                Label(LanguageManager.shared.text(.pdfDiffShowMask), systemImage: "square.fill.on.square.fill")
                    .font(.system(size: 11))
            }
            .toggleStyle(.checkbox)

            Toggle(isOn: $viewModel.syncScroll) {
                Label(LanguageManager.shared.text(.pdfDiffSyncScroll), systemImage: "link")
                    .font(.system(size: 11))
            }
            .toggleStyle(.checkbox)

            Toggle(isOn: $viewModel.ignoreWhitespace) {
                Label(LanguageManager.shared.text(.ignoreWhitespace), systemImage: "space")
                    .font(.system(size: 11))
            }
            .toggleStyle(.checkbox)

            Divider().frame(height: 16)

            // AI Summary & Export Report
            Button {
                viewModel.toggleAISummary()
            } label: {
                HStack(spacing: 4) {
                    Image(systemName: "sparkles")
                        .foregroundColor(viewModel.isAISummaryVisible ? .purple : .secondary)
                    Text(LanguageManager.shared.text(.aiSummary))
                        .font(.system(size: 11))
                }
            }
            .buttonStyle(.bordered)
            .controlSize(.small)
            .disabled(!viewModel.hasDocumentsLoaded)
            .help(LanguageManager.shared.text(.aiSummaryTitle))

            Button {
                viewModel.exportHtmlReport()
            } label: {
                Image(systemName: "square.and.arrow.up")
                    .font(.system(size: 11))
            }
            .buttonStyle(.bordered)
            .controlSize(.small)
            .disabled(!viewModel.hasDocumentsLoaded)
            .help(LanguageManager.shared.text(.exportReport))
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 8)
        .background(Color(nsColor: .controlBackgroundColor).opacity(0.6))
    }

    private var pageIndicatorText: String {
        let maxPages = max(1, viewModel.maxPageCount)
        return "\(viewModel.currentPageIndex + 1) / \(maxPages)"
    }

    private var diffStatusText: String {
        viewModel.currentDiffStatusText
    }

    private func modeTitle(_ mode: PdfViewMode) -> String {
        switch mode {
        case .sideBySide: return LanguageManager.shared.text(.pdfDiffSideBySide)
        case .overlay: return LanguageManager.shared.text(.pdfDiffOverlay)
        case .curtain: return LanguageManager.shared.text(.pdfDiffCurtain)
        }
    }
}
