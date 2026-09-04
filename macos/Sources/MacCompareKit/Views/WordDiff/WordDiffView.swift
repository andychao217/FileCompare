@preconcurrency import Foundation
import SwiftUI
import UniformTypeIdentifiers

public struct WordDiffView: View {
    @Bindable public var viewModel: WordDiffViewModel
    @State private var isLeftDropTargeted: Bool = false
    @State private var isRightDropTargeted: Bool = false
    @State private var showOutlineSidebar: Bool = true

    public init(viewModel: WordDiffViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        GeometryReader { geometry in
            VStack(spacing: 0) {
                // Top Toolbar
                WordDiffToolbarView(viewModel: viewModel)
                Divider()

                // Main Content Area (Sidebar + Canvas)
                HStack(spacing: 0) {
                    if showOutlineSidebar && viewModel.viewMode == .structuredContent {
                        WordOutlineSidebarView(viewModel: viewModel) {
                            withAnimation(.easeInOut(duration: 0.15)) {
                                showOutlineSidebar = false
                            }
                        }
                        Divider()
                    }

                    // View Switcher based on viewMode
                    Group {
                        switch viewModel.viewMode {
                        case .structuredContent, .formattingDiff:
                            WordParagraphDiffPane(viewModel: viewModel)
                        case .tableDiff:
                            WordTableDiffView(viewModel: viewModel)
                        case .metadataDiff:
                            WordMetadataDiffView(viewModel: viewModel)
                        }
                    }
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                }
                .background(
                    HStack(spacing: 0) {
                        (isLeftDropTargeted ? Color.accentColor.opacity(0.08) : Color.clear)
                            .frame(maxWidth: .infinity)
                        Divider().opacity(0)
                        (isRightDropTargeted ? Color.accentColor.opacity(0.08) : Color.clear)
                            .frame(maxWidth: .infinity)
                    }
                )
                .onDrop(of: [.fileURL], delegate: DualWordDiffDropDelegate(
                    availableWidth: geometry.size.width,
                    onDrop: { providers, isLeft in
                        handleDrop(providers: providers, isLeft: isLeft)
                    },
                    isLeftTargeted: $isLeftDropTargeted,
                    isRightTargeted: $isRightDropTargeted
                ))

                Divider()

                // Bottom Status Bar
                statusBar
            }
        }
        .overlay {
            if viewModel.isLoading {
                ZStack {
                    Color.black.opacity(0.2)
                    VStack(spacing: 12) {
                        ProgressView()
                            .controlSize(.regular)
                        Text(LanguageManager.shared.text(.parsingWordDocument))
                            .font(.system(size: 13, weight: .medium))
                            .foregroundColor(.primary)
                    }
                    .padding(24)
                    .background(RoundedRectangle(cornerRadius: 10).fill(Color(nsColor: .windowBackgroundColor)))
                    .shadow(radius: 10)
                }
            }
        }
    }

    // MARK: - Status Bar

    private var statusBar: some View {
        HStack(spacing: 16) {
            // Sidebar toggle button
            Button {
                withAnimation(.easeInOut(duration: 0.15)) {
                    showOutlineSidebar.toggle()
                }
            } label: {
                Image(systemName: showOutlineSidebar ? "sidebar.left" : "sidebar.squares.left")
                    .font(.system(size: 11))
                    .foregroundColor(.secondary)
            }
            .buttonStyle(.plain)
            .help(LanguageManager.shared.text(.toggleOutline))

            Divider().frame(height: 12)

            // Diff Statistics Badges (Only shown when comparing two documents)
            if viewModel.leftDocument != nil && viewModel.rightDocument != nil {
                HStack(spacing: 12) {
                    HStack(spacing: 4) {
                        Circle().fill(Color.green).frame(width: 6, height: 6)
                        Text("+\(viewModel.diffResult.totalAdditions) \(LanguageManager.shared.text(.added))")
                            .font(.system(size: 11, weight: .medium))
                            .foregroundColor(.secondary)
                    }

                    HStack(spacing: 4) {
                        Circle().fill(Color.red).frame(width: 6, height: 6)
                        Text("-\(viewModel.diffResult.totalDeletions) \(LanguageManager.shared.text(.deleted))")
                            .font(.system(size: 11, weight: .medium))
                            .foregroundColor(.secondary)
                    }

                    HStack(spacing: 4) {
                        Circle().fill(Color.orange).frame(width: 6, height: 6)
                        Text("~\(viewModel.diffResult.totalModifications) \(LanguageManager.shared.text(.modified))")
                            .font(.system(size: 11, weight: .medium))
                            .foregroundColor(.secondary)
                    }

                    if viewModel.diffResult.totalFormatChanges > 0 {
                        HStack(spacing: 4) {
                            Circle().fill(Color.purple).frame(width: 6, height: 6)
                            Text("*\(viewModel.diffResult.totalFormatChanges) \(LanguageManager.shared.text(.formatChanged))")
                                .font(.system(size: 11, weight: .medium))
                                .foregroundColor(.purple)
                        }
                    }

                    if viewModel.diffResult.totalMediaChanges > 0 {
                        HStack(spacing: 4) {
                            Circle().fill(Color.blue).frame(width: 6, height: 6)
                            Text("🖼️ \(viewModel.diffResult.totalMediaChanges) 媒体变动")
                                .font(.system(size: 11, weight: .medium))
                                .foregroundColor(.blue)
                        }
                    }
                }
            } else if let doc = viewModel.leftDocument ?? viewModel.rightDocument {
                Text("\(doc.fileName) (\(doc.metadata.fileSizeFormatted) • \(doc.metadata.wordCount) words)")
                    .font(.system(size: 11))
                    .foregroundColor(.secondary)
            }

            Spacer()

            if let error = viewModel.errorMessage {
                Text(error)
                    .font(.system(size: 11))
                    .foregroundColor(.red)
                    .lineLimit(1)
            }

            // Word counts summary
            if let leftDoc = viewModel.leftDocument, let rightDoc = viewModel.rightDocument {
                Text("Words: \(leftDoc.metadata.wordCount) vs \(rightDoc.metadata.wordCount)")
                    .font(.system(size: 11, weight: .regular, design: .monospaced))
                    .foregroundColor(.secondary)
            }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 4)
        .background(Color(nsColor: .windowBackgroundColor).opacity(0.8))
    }

    private func handleDrop(providers: [NSItemProvider], isLeft: Bool) -> Bool {
        for provider in providers {
            _ = provider.loadObject(ofClass: URL.self) { url, _ in
                guard let url = url else { return }
                let ext = url.pathExtension.lowercased()
                guard ["docx", "doc", "rtf"].contains(ext) else { return }

                DispatchQueue.main.async {
                    self.viewModel.loadSingleFile(from: url, isLeft: isLeft)
                }
            }
        }
        return true
    }
}

private struct DualWordDiffDropDelegate: DropDelegate {
    let availableWidth: CGFloat
    let onDrop: ([NSItemProvider], Bool) -> Bool
    @Binding var isLeftTargeted: Bool
    @Binding var isRightTargeted: Bool

    func dropUpdated(info: DropInfo) -> DropProposal? {
        let isLeft = info.location.x < (availableWidth / 2.0)
        isLeftTargeted = isLeft
        isRightTargeted = !isLeft
        return DropProposal(operation: .copy)
    }

    func dropExited(info: DropInfo) {
        isLeftTargeted = false
        isRightTargeted = false
    }

    func performDrop(info: DropInfo) -> Bool {
        let isLeft = info.location.x < (availableWidth / 2.0)
        isLeftTargeted = false
        isRightTargeted = false
        let providers = info.itemProviders(for: [.fileURL])
        return onDrop(providers, isLeft)
    }
}
