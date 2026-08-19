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
                            isLeft: true
                        )

                        Divider()

                        // 2. Base Common Ancestor
                        branchColumn(
                            title: "Base (Common Ancestor)",
                            branch: viewModel.baseBranchName,
                            content: viewModel.baseContent,
                            accentColor: .gray,
                            isBase: true
                        )

                        Divider()

                        // 3. Remote Branch
                        branchColumn(
                            title: "Remote (Incoming Branch)",
                            branch: viewModel.remoteBranchName,
                            content: viewModel.remoteContent,
                            accentColor: .green,
                            isRight: true
                        )
                    }
                    .frame(height: geometry.size.height * 0.6)

                    Divider()

                    // Bottom Merged Result Output
                    mergedOutputPane()
                        .frame(height: geometry.size.height * 0.4)
                }
            }
        }
        .background(Color(nsColor: .textBackgroundColor))
    }

    private func mergeHeaderBar() -> some View {
        HStack(spacing: 12) {
            Image(systemName: "doc.text")
                .foregroundColor(.secondary)

            Text(viewModel.filePath)
                .font(.system(size: 12, weight: .medium))
                .foregroundColor(.primary)

            Spacer()

            // Conflict Navigation
            HStack(spacing: 6) {
                Button {} label: {
                    Image(systemName: "chevron.left")
                        .font(.system(size: 10, weight: .bold))
                }
                .buttonStyle(.bordered)
                .controlSize(.small)

                Button {} label: {
                    Image(systemName: "chevron.right")
                        .font(.system(size: 10, weight: .bold))
                }
                .buttonStyle(.bordered)
                .controlSize(.small)

                Text("Next Conflict (\(viewModel.currentConflictIndex) of \(viewModel.totalConflicts))")
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
        isRight: Bool = false
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
                VStack(alignment: .leading, spacing: 2) {
                    ForEach(Array(content.components(separatedBy: .newlines).enumerated()), id: \.offset) { idx, line in
                        HStack(spacing: 8) {
                            Text("\(idx + 43)")
                                .font(.system(size: 10, design: .monospaced))
                                .foregroundColor(.secondary)
                                .frame(width: 24, alignment: .trailing)

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

                Text("src/MainView.swift")
                    .font(.system(size: 11))
                    .foregroundColor(.secondary)

                Spacer()

                Button {
                    // Mark resolved
                } label: {
                    Label("Mark Resolved", systemImage: "checkmark.circle.fill")
                        .font(.system(size: 11))
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.small)

                HStack(spacing: 4) {
                    Image(systemName: "exclamationmark.triangle.fill")
                        .foregroundColor(.yellow)
                    Text("Conflicts Detected (\(viewModel.totalConflicts) total)")
                        .font(.system(size: 11, weight: .medium))
                        .foregroundColor(.secondary)
                }
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 6)
            .background(Color(nsColor: .controlBackgroundColor).opacity(0.8))

            Divider()

            // Merged Content
            ScrollView {
                VStack(alignment: .leading, spacing: 2) {
                    ForEach(Array(viewModel.mergeResult.mergedText.components(separatedBy: .newlines).enumerated()), id: \.offset) { idx, line in
                        HStack(spacing: 8) {
                            Text("\(idx + 48)")
                                .font(.system(size: 10, design: .monospaced))
                                .foregroundColor(.secondary)
                                .frame(width: 24, alignment: .trailing)

                            Text(line)
                                .font(.system(size: 11, design: .monospaced))
                                .foregroundColor(line.contains("<<<<<<<") || line.contains(">>>>>>>") ? .red : .primary)

                            Spacer()
                        }
                        .frame(height: 18)
                        .background(
                            line.contains("<<<<<<<") || line.contains(">>>>>>>") || line.contains("=======")
                                ? Color.red.opacity(0.12)
                                : Color.clear
                        )
                    }
                }
                .padding(.vertical, 4)
            }
        }
        .background(Color(nsColor: .windowBackgroundColor).opacity(0.3))
    }
}
