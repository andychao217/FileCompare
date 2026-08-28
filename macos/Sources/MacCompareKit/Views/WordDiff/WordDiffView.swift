import SwiftUI
import UniformTypeIdentifiers

public struct WordDiffView: View {
    @Bindable public var viewModel: WordDiffViewModel
    @State private var isDropTargeted: Bool = false
    @State private var showOutlineSidebar: Bool = true

    public init(viewModel: WordDiffViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        VStack(spacing: 0) {
            // Top Toolbar
            WordDiffToolbarView(viewModel: viewModel)
            Divider()

            // Main Content Area (Sidebar + Canvas)
            HStack(spacing: 0) {
                if showOutlineSidebar && viewModel.viewMode == .structuredContent {
                    WordOutlineSidebarView(viewModel: viewModel)
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

            Divider()

            // Bottom Status Bar
            statusBar
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
        .onDrop(of: [.fileURL], isTargeted: $isDropTargeted) { providers in
            handleDrop(providers: providers)
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

            // Diff Statistics Badges
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

    private func handleDrop(providers: [NSItemProvider]) -> Bool {
        Task { @MainActor in
            var loadedURLs: [URL] = []
            for provider in providers {
                if let item = try? await provider.loadItem(forTypeIdentifier: UTType.fileURL.identifier, options: nil) {
                    if let data = item as? Data, let url = URL(dataRepresentation: data, relativeTo: nil) {
                        let ext = url.pathExtension.lowercased()
                        if ["docx", "doc", "rtf"].contains(ext) {
                            loadedURLs.append(url)
                        }
                    } else if let url = item as? URL {
                        let ext = url.pathExtension.lowercased()
                        if ["docx", "doc", "rtf"].contains(ext) {
                            loadedURLs.append(url)
                        }
                    }
                }
            }

            if loadedURLs.count >= 2 {
                viewModel.loadFiles(left: loadedURLs[0], right: loadedURLs[1])
            } else if let first = loadedURLs.first {
                if viewModel.leftDocument == nil {
                    viewModel.loadSingleFile(from: first, isLeft: true)
                } else {
                    viewModel.loadSingleFile(from: first, isLeft: false)
                }
            }
        }
        return true
    }
}
