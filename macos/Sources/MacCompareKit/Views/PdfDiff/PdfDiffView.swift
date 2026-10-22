import SwiftUI
import AppKit
import UniformTypeIdentifiers

public struct PdfDiffView: View {
    @Bindable public var viewModel: PdfDiffViewModel
    public var tabManager: TabManager?

    @State private var isLeftDropTargeted: Bool = false
    @State private var isRightDropTargeted: Bool = false
    @State private var curtainSplitFraction: CGFloat = 0.5
    @State private var scrollFraction: CGFloat = 0.0

    public init(viewModel: PdfDiffViewModel, tabManager: TabManager? = nil) {
        self.viewModel = viewModel
        self.tabManager = tabManager
    }

    public var body: some View {
        VStack(spacing: 0) {
            // Top Toolbar
            PdfDiffToolbarView(viewModel: viewModel)
            Divider()

            // AI Summary Banner
            if viewModel.isAISummaryVisible {
                AISummaryCardView(
                    result: viewModel.aiSummaryResult,
                    isGenerating: viewModel.isAIGenerating,
                    onReanalyze: { viewModel.generateAISummary() },
                    onClose: {
                        withAnimation(.easeInOut(duration: 0.2)) {
                            viewModel.isAISummaryVisible = false
                        }
                    }
                )
                .transition(.move(edge: .top).combined(with: .opacity))
                Divider()
            }

            // Main Viewport
            GeometryReader { geometry in
                if viewModel.isLoading {
                    VStack(spacing: 12) {
                        ProgressView()
                            .scaleEffect(1.2)
                        Text(LanguageManager.shared.text(.compare))
                            .font(.system(size: 13))
                            .foregroundColor(.secondary)
                    }
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                } else {
                    mainContent(in: geometry)
                }
            }

            Divider()

            // Bottom Status Bar
            statusBar
        }
        .background(Color(nsColor: .windowBackgroundColor))
    }

    // MARK: - Main Content Layout

    @ViewBuilder
    private func mainContent(in geometry: GeometryProxy) -> some View {
        switch viewModel.viewMode {
        case .sideBySide:
            HStack(spacing: 0) {
                // Left PDF Pane
                pdfPane(
                    url: viewModel.leftFileURL,
                    doc: viewModel.leftDocument,
                    isLeft: true,
                    isTargeted: isLeftDropTargeted
                )
                .frame(width: (geometry.size.width - 1) / 2)

                Divider()

                // Right PDF Pane
                pdfPane(
                    url: viewModel.rightFileURL,
                    doc: viewModel.rightDocument,
                    isLeft: false,
                    isTargeted: isRightDropTargeted
                )
                .frame(width: (geometry.size.width - 1) / 2)
            }

        case .overlay:
            ZStack {
                if let leftURL = viewModel.leftFileURL {
                    PdfKitViewRepresentable(
                        url: leftURL,
                        pageIndex: viewModel.currentPageIndex,
                        zoomScale: viewModel.zoomScale,
                        syncScroll: viewModel.syncScroll,
                        isLeft: true,
                        diffBlocks: viewModel.diffResult.blocks,
                        visualDiff: viewModel.currentVisualDiff,
                        showDiffMaskOverlay: viewModel.showDiffMaskOverlay,
                        scrollFraction: $scrollFraction
                    )
                    .opacity(1.0 - viewModel.overlayBlendAlpha)
                }
                if let rightURL = viewModel.rightFileURL {
                    PdfKitViewRepresentable(
                        url: rightURL,
                        pageIndex: viewModel.currentPageIndex,
                        zoomScale: viewModel.zoomScale,
                        syncScroll: viewModel.syncScroll,
                        isLeft: false,
                        diffBlocks: viewModel.diffResult.blocks,
                        visualDiff: viewModel.currentVisualDiff,
                        showDiffMaskOverlay: viewModel.showDiffMaskOverlay,
                        scrollFraction: $scrollFraction
                    )
                    .opacity(viewModel.overlayBlendAlpha)
                }

                if viewModel.showDiffMaskOverlay, let maskImg = viewModel.currentDiffMaskImage {
                    Image(decorative: maskImg, scale: 1.5)
                        .resizable()
                        .scaledToFit()
                        .allowsHitTesting(false)
                }
            }

        case .curtain:
            ZStack(alignment: .leading) {
                // Underneath: Right Document (Full width)
                if let rightURL = viewModel.rightFileURL {
                    PdfKitViewRepresentable(
                        url: rightURL,
                        pageIndex: viewModel.currentPageIndex,
                        zoomScale: viewModel.zoomScale,
                        syncScroll: viewModel.syncScroll,
                        isLeft: false,
                        diffBlocks: viewModel.diffResult.blocks,
                        visualDiff: viewModel.currentVisualDiff,
                        showDiffMaskOverlay: viewModel.showDiffMaskOverlay,
                        scrollFraction: $scrollFraction
                    )
                    .frame(width: geometry.size.width, height: geometry.size.height)
                }

                // Top: Left Document masked to curtainSplitFraction width
                if let leftURL = viewModel.leftFileURL {
                    PdfKitViewRepresentable(
                        url: leftURL,
                        pageIndex: viewModel.currentPageIndex,
                        zoomScale: viewModel.zoomScale,
                        syncScroll: viewModel.syncScroll,
                        isLeft: true,
                        diffBlocks: viewModel.diffResult.blocks,
                        visualDiff: viewModel.currentVisualDiff,
                        showDiffMaskOverlay: viewModel.showDiffMaskOverlay,
                        scrollFraction: $scrollFraction
                    )
                    .frame(width: geometry.size.width, height: geometry.size.height)
                    .mask(
                        HStack(spacing: 0) {
                            Rectangle()
                                .frame(width: max(0, geometry.size.width * curtainSplitFraction))
                            Spacer(minLength: 0)
                        }
                    )
                }

                // Optional Diff Mask overlay on curtain
                if viewModel.showDiffMaskOverlay, let maskImg = viewModel.currentDiffMaskImage {
                    Image(decorative: maskImg, scale: 1.5)
                        .resizable()
                        .scaledToFit()
                        .allowsHitTesting(false)
                        .frame(width: geometry.size.width, height: geometry.size.height)
                }

                // Curtain Divider & Interactive Handle
                HStack(spacing: 0) {
                    Spacer(minLength: 0)
                        .frame(width: max(0, geometry.size.width * curtainSplitFraction - 10))

                    ZStack {
                        Rectangle()
                            .fill(Color.accentColor)
                            .frame(width: 2)

                        Capsule()
                            .fill(Color(nsColor: .controlBackgroundColor))
                            .overlay(
                                Capsule()
                                    .stroke(Color.accentColor, lineWidth: 1.5)
                            )
                            .overlay(
                                Image(systemName: "line.3.horizontal")
                                    .font(.system(size: 9, weight: .bold))
                                    .foregroundColor(.secondary)
                                    .rotationEffect(.degrees(90))
                            )
                            .frame(width: 18, height: 40)
                            .shadow(color: Color.black.opacity(0.18), radius: 4, x: 0, y: 1)
                    }
                    .frame(width: 20)
                    .contentShape(Rectangle())
                    .gesture(
                        DragGesture()
                            .onChanged { value in
                                let newFraction = value.location.x / geometry.size.width
                                curtainSplitFraction = max(0.02, min(0.98, newFraction))
                            }
                    )

                    Spacer(minLength: 0)
                }
            }
        }
    }

    // MARK: - Single PDF Pane

    @ViewBuilder
    private func pdfPane(url: URL?, doc: PdfDocumentModel?, isLeft: Bool, isTargeted: Bool) -> some View {
        VStack(spacing: 0) {
            // Pane Header
            HStack {
                Image(systemName: "doc.viewfinder.fill")
                    .foregroundColor(isLeft ? .blue : .green)
                    .font(.system(size: 13))

                Text(doc?.fileName ?? (isLeft ? LanguageManager.shared.text(.pdfDiffSelectLeftFile) : LanguageManager.shared.text(.pdfDiffSelectRightFile)))
                    .font(.system(size: 12, weight: .medium))
                    .lineLimit(1)
                    .truncationMode(.middle)

                Spacer()

                Button {
                    chooseFile(isLeft: isLeft)
                } label: {
                    Image(systemName: "folder")
                        .font(.system(size: 11))
                }
                .buttonStyle(.borderless)
                .help(LanguageManager.shared.text(.openFile))
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 6)
            .background(Color(nsColor: .controlBackgroundColor))

            Divider()

            // Viewport or Empty Drop Zone
            if let docURL = url, doc != nil {
                ZStack(alignment: .topTrailing) {
                    PdfKitViewRepresentable(
                        url: docURL,
                        pageIndex: viewModel.currentPageIndex,
                        zoomScale: viewModel.zoomScale,
                        syncScroll: viewModel.syncScroll,
                        isLeft: isLeft,
                        diffBlocks: viewModel.diffResult.blocks,
                        visualDiff: viewModel.currentVisualDiff,
                        showDiffMaskOverlay: viewModel.showDiffMaskOverlay,
                        scrollFraction: $scrollFraction,
                        onScrollFractionChanged: { fraction in
                            scrollFraction = fraction
                        }
                    )

                    // Visual diff indicator badge on right pane
                    if !isLeft, viewModel.showDiffMaskOverlay, let visual = viewModel.currentVisualDiff, visual.diffPixelCount > 0 {
                        HStack(spacing: 4) {
                            Circle()
                                .fill(Color.red)
                                .frame(width: 6, height: 6)
                            Text(String(format: LanguageManager.shared.text(.pdfDiffAreaBadgeFormat), visual.diffBoundingBoxes.count, visual.diffPercentage))
                                .font(.system(size: 10, weight: .semibold, design: .monospaced))
                                .foregroundColor(.red)
                        }
                        .padding(.horizontal, 8)
                        .padding(.vertical, 4)
                        .background(
                            Capsule()
                                .fill(Color(nsColor: .controlBackgroundColor).opacity(0.92))
                                .shadow(color: Color.black.opacity(0.12), radius: 3, x: 0, y: 1)
                        )
                        .padding(8)
                    }
                }
            } else {
                emptyDropZone(isLeft: isLeft, isTargeted: isTargeted)
            }
        }
        .onDrop(of: [.fileURL], isTargeted: isLeft ? $isLeftDropTargeted : $isRightDropTargeted) { providers in
            handleDrop(providers: providers, isLeft: isLeft)
        }
    }

    @ViewBuilder
    private func emptyDropZone(isLeft: Bool, isTargeted: Bool) -> some View {
        VStack(spacing: 12) {
            Image(systemName: "doc.viewfinder")
                .font(.system(size: 36))
                .foregroundColor(isTargeted ? .accentColor : .secondary)

            Text(LanguageManager.shared.text(.pdfDiffDropHint))
                .font(.system(size: 13, weight: .medium))
                .foregroundColor(.secondary)

            Button(isLeft ? LanguageManager.shared.text(.pdfDiffSelectLeftFile) : LanguageManager.shared.text(.pdfDiffSelectRightFile)) {
                chooseFile(isLeft: isLeft)
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.regular)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(isTargeted ? Color.accentColor.opacity(0.08) : Color.clear)
    }

    // MARK: - Status Bar

    private var statusBar: some View {
        HStack(spacing: 16) {
            if let leftDoc = viewModel.leftDocument {
                Text("\(leftDoc.fileName): \(String(format: LanguageManager.shared.text(.pdfPagesCountFormat), leftDoc.pageCount)) (\(leftDoc.metadata.fileSizeFormatted))")
                    .font(.system(size: 11))
                    .foregroundColor(.secondary)
            }

            Spacer()

            if let visual = viewModel.currentVisualDiff, visual.diffPixelCount > 0 {
                HStack(spacing: 4) {
                    Image(systemName: "eye.trianglebadge.exclamationmark")
                        .foregroundColor(.orange)
                    Text(String(format: LanguageManager.shared.text(.pdfDiffPixelStatsFormat), visual.diffBoundingBoxes.count, visual.diffPercentage))
                        .font(.system(size: 11, weight: .medium, design: .monospaced))
                        .foregroundColor(.primary)
                }
            } else if viewModel.hasDocumentsLoaded {
                Text(LanguageManager.shared.text(.pdfDiffNoDiff))
                    .font(.system(size: 11))
                    .foregroundColor(.secondary)
            }

            Spacer()

            if let rightDoc = viewModel.rightDocument {
                Text("\(rightDoc.fileName): \(String(format: LanguageManager.shared.text(.pdfPagesCountFormat), rightDoc.pageCount)) (\(rightDoc.metadata.fileSizeFormatted))")
                    .font(.system(size: 11))
                    .foregroundColor(.secondary)
            }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 4)
        .background(Color(nsColor: .controlBackgroundColor))
    }

    // MARK: - File Selection & Drop

    private func chooseFile(isLeft: Bool) {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [UTType.pdf]
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false

        if panel.runModal() == .OK, let url = panel.url {
            viewModel.loadSingleFile(from: url, isLeft: isLeft)
        }
    }

    private func handleDrop(providers: [NSItemProvider], isLeft: Bool) -> Bool {
        guard let provider = providers.first else { return false }
        provider.loadItem(forTypeIdentifier: UTType.fileURL.identifier, options: nil) { item, _ in
            guard let data = item as? Data,
                  let url = URL(dataRepresentation: data, relativeTo: nil) else { return }

            DispatchQueue.main.async {
                viewModel.loadSingleFile(from: url, isLeft: isLeft)
            }
        }
        return true
    }
}
