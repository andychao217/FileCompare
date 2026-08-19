import SwiftUI
import UniformTypeIdentifiers

public struct ThreeWayMergeView: View {
    @Bindable public var viewModel: ThreeWayMergeViewModel
    @State private var isLocalTargeted = false
    @State private var isBaseTargeted = false
    @State private var isRemoteTargeted = false

    public init(viewModel: ThreeWayMergeViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        VStack(spacing: 0) {
            // Top Merge Navigation & Action Bar
            mergeHeaderBar()

            Divider()

            // 3-Way Top Pane Split (Local | Base | Remote)
            GeometryReader { geometry in
                VStack(spacing: 0) {
                    HStack(spacing: 0) {
                        // 1. Local Branch
                        branchColumn(
                            title: "Local (Current Branch)",
                            branch: viewModel.localBranchName,
                            content: viewModel.localContent,
                            hasFile: viewModel.localFileURL != nil || !viewModel.localContent.isEmpty,
                            accentColor: .orange,
                            isLeft: true,
                            onOpen: { viewModel.openLocalFile() }
                        )
                        .background(isLocalTargeted ? Color.orange.opacity(0.1) : Color.clear)
                        .onDrop(of: [.fileURL], isTargeted: $isLocalTargeted) { providers in
                            handleDrop(providers: providers, type: .local)
                        }

                        Divider()

                        // 2. Base Common Ancestor
                        branchColumn(
                            title: "Base (Common Ancestor)",
                            branch: viewModel.baseBranchName,
                            content: viewModel.baseContent,
                            hasFile: viewModel.baseFileURL != nil || !viewModel.baseContent.isEmpty,
                            accentColor: .gray,
                            isBase: true,
                            onOpen: { viewModel.openBaseFile() }
                        )
                        .background(isBaseTargeted ? Color.gray.opacity(0.1) : Color.clear)
                        .onDrop(of: [.fileURL], isTargeted: $isBaseTargeted) { providers in
                            handleDrop(providers: providers, type: .base)
                        }

                        Divider()

                        // 3. Remote Branch
                        branchColumn(
                            title: "Remote (Incoming Branch)",
                            branch: viewModel.remoteBranchName,
                            content: viewModel.remoteContent,
                            hasFile: viewModel.remoteFileURL != nil || !viewModel.remoteContent.isEmpty,
                            accentColor: .green,
                            isRight: true,
                            onOpen: { viewModel.openRemoteFile() }
                        )
                        .background(isRemoteTargeted ? Color.green.opacity(0.1) : Color.clear)
                        .onDrop(of: [.fileURL], isTargeted: $isRemoteTargeted) { providers in
                            handleDrop(providers: providers, type: .remote)
                        }
                    }
                    .frame(height: geometry.size.height * 0.58)

                    Divider()

                    // Bottom Merged Result Output
                    mergedOutputPane()
                        .frame(height: geometry.size.height * 0.42)
                }
            }
        }
        .background(Color(nsColor: .textBackgroundColor))
    }

    private func handleDrop(providers: [NSItemProvider], type: ThreeWayMergeViewModel.MergeFileType) -> Bool {
        guard let provider = providers.first else { return false }
        _ = provider.loadObject(ofClass: URL.self) { url, _ in
            guard let fileURL = url else { return }
            DispatchQueue.main.async {
                self.viewModel.loadSingleFile(from: fileURL, type: type)
            }
        }
        return true
    }

    private func mergeHeaderBar() -> some View {
        HStack(spacing: 12) {
            Image(systemName: "arrow.triangle.branch")
                .foregroundColor(.secondary)

            Text(viewModel.filePath)
                .font(.system(size: 12, weight: .semibold))
                .foregroundColor(.primary)

            Spacer()

            // Conflict Navigation
            HStack(spacing: 6) {
                Button {
                    viewModel.previousConflict()
                } label: {
                    Image(systemName: "chevron.left")
                        .font(.system(size: 10, weight: .bold))
                }
                .buttonStyle(.bordered)
                .controlSize(.small)
                .disabled(viewModel.totalConflicts == 0)

                Button {
                    viewModel.nextConflict()
                } label: {
                    Image(systemName: "chevron.right")
                        .font(.system(size: 10, weight: .bold))
                }
                .buttonStyle(.bordered)
                .controlSize(.small)
                .disabled(viewModel.totalConflicts == 0)

                Text("Conflict (\(viewModel.totalConflicts > 0 ? viewModel.currentConflictIndex + 1 : 0) of \(viewModel.totalConflicts))")
                    .font(.system(size: 11))
                    .foregroundColor(.secondary)
            }

            Divider().frame(height: 16)

            Button {
                viewModel.autoResolveNonConflicts()
            } label: {
                Label("Auto-Resolve Non-Conflicts", systemImage: "wand.and.stars")
                    .font(.system(size: 11))
            }
            .buttonStyle(.bordered)
            .controlSize(.small)
            .disabled(!viewModel.hasFilesLoaded)

            Button {
                viewModel.saveAndCompleteMerge()
            } label: {
                Label("Save & Complete Merge", systemImage: "arrow.down.doc.fill")
                    .font(.system(size: 11, weight: .semibold))
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.small)
            .disabled(!viewModel.hasFilesLoaded)
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 6)
        .background(Color(nsColor: .controlBackgroundColor))
    }

    private func branchColumn(
        title: String,
        branch: String,
        content: String,
        hasFile: Bool,
        accentColor: Color,
        isLeft: Bool = false,
        isBase: Bool = false,
        isRight: Bool = false,
        onOpen: @escaping () -> Void
    ) -> some View {
        VStack(spacing: 0) {
            // Column Header
            HStack(spacing: 6) {
                VStack(alignment: .leading, spacing: 2) {
                    Text(title)
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundColor(.primary)

                    HStack(spacing: 4) {
                        Image(systemName: "arrow.triangle.branch")
                            .font(.system(size: 10))
                        Text(branch)
                            .font(.system(size: 10))
                    }
                    .foregroundColor(.secondary)
                }
                Spacer()

                Button("Choose...") {
                    onOpen()
                }
                .buttonStyle(.bordered)
                .controlSize(.mini)
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 6)
            .background(Color(nsColor: .controlBackgroundColor).opacity(0.4))

            Divider()

            if !hasFile {
                VStack(spacing: 8) {
                    Image(systemName: "doc.badge.plus")
                        .font(.system(size: 28))
                        .foregroundColor(.secondary.opacity(0.7))
                    Text("No File Loaded")
                        .font(.system(size: 12, weight: .medium))
                        .foregroundColor(.secondary)
                    Button("Choose...") {
                        onOpen()
                    }
                    .buttonStyle(.bordered)
                    .controlSize(.mini)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .padding()
            } else {
                // Conflict Action Overlay Bar (for Local & Remote)
                if isLeft || isRight {
                    HStack(spacing: 8) {
                        Button("Accept Local") { viewModel.acceptLocal() }
                            .buttonStyle(.bordered)
                            .tint(.orange)
                            .controlSize(.mini)

                        Button("Take Both") { viewModel.takeBoth() }
                            .buttonStyle(.bordered)
                            .tint(.purple)
                            .controlSize(.mini)

                        Button("Accept Remote") { viewModel.acceptRemote() }
                            .buttonStyle(.bordered)
                            .tint(.green)
                            .controlSize(.mini)
                    }
                    .padding(.vertical, 4)
                    .background(Color(nsColor: .windowBackgroundColor).opacity(0.8))

                    Divider()
                }

                // Code Content
                ScrollView {
                    VStack(alignment: .leading, spacing: 1) {
                        ForEach(Array(content.components(separatedBy: .newlines).enumerated()), id: \.offset) { idx, line in
                            HStack(spacing: 8) {
                                Text("\(idx + 1)")
                                    .font(.system(size: 10, design: .monospaced))
                                    .foregroundColor(.secondary)
                                    .frame(width: 28, alignment: .trailing)

                                Text(line)
                                    .font(.system(size: 11, design: .monospaced))
                                    .foregroundColor(.primary)

                                Spacer()
                            }
                            .frame(height: 18)
                        }
                    }
                    .padding(.vertical, 4)
                }
            }
        }
        .frame(maxWidth: .infinity)
    }

    private func mergedOutputPane() -> some View {
        VStack(spacing: 0) {
            // Output Header
            HStack(spacing: 8) {
                Text("Merged Output Result")
                    .font(.system(size: 12, weight: .semibold))

                Text(viewModel.filePath)
                    .font(.system(size: 11))
                    .foregroundColor(.secondary)

                if let msg = viewModel.statusMessage {
                    Text("• \(msg)")
                        .font(.system(size: 11))
                        .foregroundColor(.accentColor)
                }

                Spacer()

                Button {
                    viewModel.saveAndCompleteMerge()
                } label: {
                    Label("Mark Resolved & Save", systemImage: "checkmark.circle.fill")
                        .font(.system(size: 11))
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.small)
                .disabled(!viewModel.hasFilesLoaded)

                HStack(spacing: 4) {
                    if viewModel.totalConflicts > 0 {
                        Image(systemName: "exclamationmark.triangle.fill")
                            .foregroundColor(.yellow)
                        Text("\(viewModel.totalConflicts) Conflicts Remaining")
                            .font(.system(size: 11, weight: .medium))
                            .foregroundColor(.orange)
                    } else if viewModel.hasFilesLoaded {
                        Image(systemName: "checkmark.circle.fill")
                            .foregroundColor(.green)
                        Text("All Conflicts Resolved")
                            .font(.system(size: 11, weight: .medium))
                            .foregroundColor(.green)
                    }
                }
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 6)
            .background(Color(nsColor: .controlBackgroundColor).opacity(0.8))

            Divider()

            // Merged Content Text Editor
            TextEditor(text: $viewModel.mergeResult.mergedText)
                .font(.system(size: 12, design: .monospaced))
                .scrollContentBackground(.hidden)
                .background(Color(nsColor: .textBackgroundColor))
                .padding(6)
        }
        .background(Color(nsColor: .windowBackgroundColor).opacity(0.3))
    }
}
