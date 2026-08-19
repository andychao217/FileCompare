import SwiftUI

public struct ThreeWayMergeView: View {
    @Bindable public var viewModel: ThreeWayMergeViewModel

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
                            accentColor: .orange,
                            isLeft: true,
                            onOpen: { viewModel.openLocalFile() }
                        )

                        Divider()

                        // 2. Base Common Ancestor
                        branchColumn(
                            title: "Base (Common Ancestor)",
                            branch: viewModel.baseBranchName,
                            content: viewModel.baseContent,
                            accentColor: .gray,
                            isBase: true,
                            onOpen: { viewModel.openBaseFile() }
                        )

                        Divider()

                        // 3. Remote Branch
                        branchColumn(
                            title: "Remote (Incoming Branch)",
                            branch: viewModel.remoteBranchName,
                            content: viewModel.remoteContent,
                            accentColor: .green,
                            isRight: true,
                            onOpen: { viewModel.openRemoteFile() }
                        )
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

            Button {
                viewModel.saveAndCompleteMerge()
            } label: {
                Label("Save & Complete Merge", systemImage: "arrow.down.doc.fill")
                    .font(.system(size: 11, weight: .semibold))
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.small)
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 6)
        .background(Color(nsColor: .controlBackgroundColor))
    }

    private func branchColumn(
        title: String,
        branch: String,
        content: String,
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

                Button("Open...") {
                    onOpen()
                }
                .buttonStyle(.bordered)
                .controlSize(.mini)
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 6)
            .background(Color(nsColor: .controlBackgroundColor).opacity(0.4))

            Divider()

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
                        .background(
                            (idx >= 5 && idx <= 8 && !isBase)
                                ? (isLeft ? Color.orange.opacity(0.2) : Color.green.opacity(0.2))
                                : Color.clear
                        )
                    }
                }
                .padding(.vertical, 4)
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

                HStack(spacing: 4) {
                    if viewModel.totalConflicts > 0 {
                        Image(systemName: "exclamationmark.triangle.fill")
                            .foregroundColor(.yellow)
                        Text("\(viewModel.totalConflicts) Conflicts Remaining")
                            .font(.system(size: 11, weight: .medium))
                            .foregroundColor(.orange)
                    } else {
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
