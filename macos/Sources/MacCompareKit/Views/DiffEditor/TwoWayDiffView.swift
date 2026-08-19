import SwiftUI

public struct TwoWayDiffView: View {
    @Bindable public var viewModel: TextDiffViewModel

    public init(viewModel: TextDiffViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        VStack(spacing: 0) {
            // Top Toolbar
            DiffToolbarView(viewModel: viewModel)

            Divider()

            // Main Diff Split View
            ScrollViewReader { proxy in
                HStack(spacing: 0) {
                    // Left Buffer (Original)
                    VStack(spacing: 0) {
                        fileHeader(
                            title: viewModel.leftTitle,
                            icon: "doc.text",
                            isDirty: viewModel.isLeftDirty,
                            onOpen: { viewModel.openLeftFile() },
                            onSave: { viewModel.saveLeftFile() }
                        )
                        Divider()
                        ScrollView {
                            VStack(spacing: 0) {
                                ForEach(Array(viewModel.diffResult.lines.enumerated()), id: \.offset) { idx, line in
                                    leftLineRow(index: idx, line: line)
                                        .id(idx)
                                }
                            }
                        }
                    }
                    .frame(maxWidth: .infinity)

                    Divider()

                    // Right Buffer (Modified)
                    VStack(spacing: 0) {
                        fileHeader(
                            title: viewModel.rightTitle,
                            icon: "doc.text.fill",
                            isDirty: viewModel.isRightDirty,
                            onOpen: { viewModel.openRightFile() },
                            onSave: { viewModel.saveRightFile() }
                        )
                        Divider()
                        ScrollView {
                            VStack(spacing: 0) {
                                ForEach(Array(viewModel.diffResult.lines.enumerated()), id: \.offset) { idx, line in
                                    rightLineRow(index: idx, line: line)
                                        .id(idx)
                                }
                            }
                        }
                    }
                    .frame(maxWidth: .infinity)

                    Divider()

                    // Minimap with Interactive Scroll
                    MinimapView(lines: viewModel.diffResult.lines) { targetLine in
                        viewModel.scrollToLineIndex = targetLine
                    }
                }
                .onChange(of: viewModel.scrollToLineIndex) { _, targetLine in
                    if let line = targetLine {
                        withAnimation(.easeInOut(duration: 0.2)) {
                            proxy.scrollTo(line, anchor: .center)
                        }
                    }
                }
            }

            Divider()

            // Status Bar
            StatusBarView(
                cursorInfo: viewModel.cursorPosition,
                diffStats: "\(viewModel.diffResult.totalModifications + viewModel.diffResult.totalAdditions + viewModel.diffResult.totalDeletions) changes, \(viewModel.diffResult.totalAdditions) addition, \(viewModel.diffResult.totalDeletions) deletions",
                encoding: viewModel.selectedEncoding.rawValue,
                statusMessage: viewModel.statusMessage
            )
        }
        .background(Color(nsColor: .textBackgroundColor))
    }

    private func fileHeader(
        title: String,
        icon: String,
        isDirty: Bool,
        onOpen: @escaping () -> Void,
        onSave: @escaping () -> Void
    ) -> some View {
        HStack(spacing: 6) {
            Image(systemName: icon)
                .font(.system(size: 11))
                .foregroundColor(.secondary)

            Text(title)
                .font(.system(size: 11, weight: .semibold))
                .foregroundColor(.primary)
                .lineLimit(1)

            if isDirty {
                Circle()
                    .fill(Color.orange)
                    .frame(width: 6, height: 6)
                    .help("Unsaved changes")
            }

            Spacer()

            Button("Open...") {
                onOpen()
            }
            .buttonStyle(.bordered)
            .controlSize(.mini)

            Button("Save") {
                onSave()
            }
            .buttonStyle(.bordered)
            .controlSize(.mini)
            .disabled(!isDirty)
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 5)
        .background(Color(nsColor: .controlBackgroundColor).opacity(0.6))
    }

    private func leftLineRow(index: Int, line: DiffLine) -> some View {
        HStack(spacing: 0) {
            if line.isPhantomLeft {
                PhantomLineView()
            } else {
                // Line Number Gutter
                HStack(spacing: 2) {
                    if line.changeType == .deleted || line.changeType == .modified {
                        Text("-")
                            .font(.system(size: 11, weight: .bold, design: .monospaced))
                            .foregroundColor(.red.opacity(0.9))
                    } else {
                        Spacer().frame(width: 6)
                    }

                    Spacer()

                    Text(line.leftLineNumber.map { "\($0)" } ?? "")
                        .font(.system(size: 11, design: .monospaced))
                        .foregroundColor(.secondary.opacity(0.8))
                }
                .frame(width: 48)
                .padding(.trailing, 6)
                .background(Color(nsColor: .windowBackgroundColor).opacity(0.3))

                // Line Content
                HStack {
                    Text(line.contentLeft)
                        .font(.system(size: 12, design: .monospaced))
                        .foregroundColor(.primary)
                    Spacer()
                }
                .padding(.horizontal, 6)
                .frame(height: 20)
                .background(lineBackground(for: line.changeType, isLeft: true))
            }
        }
    }

    private func rightLineRow(index: Int, line: DiffLine) -> some View {
        HStack(spacing: 0) {
            if line.isPhantomRight {
                PhantomLineView()
            } else {
                // Line Number Gutter
                HStack(spacing: 2) {
                    if line.changeType == .added || line.changeType == .modified {
                        Text("+")
                            .font(.system(size: 11, weight: .bold, design: .monospaced))
                            .foregroundColor(.green.opacity(0.9))
                    } else {
                        Spacer().frame(width: 6)
                    }

                    Spacer()

                    Text(line.rightLineNumber.map { "\($0)" } ?? "")
                        .font(.system(size: 11, design: .monospaced))
                        .foregroundColor(.secondary.opacity(0.8))
                }
                .frame(width: 48)
                .padding(.trailing, 6)
                .background(Color(nsColor: .windowBackgroundColor).opacity(0.3))

                // Line Content with Token Highlight
                HStack {
                    renderRightContentWithTokens(line: line)
                    Spacer()
                }
                .padding(.horizontal, 6)
                .frame(height: 20)
                .background(lineBackground(for: line.changeType, isLeft: false))
            }
        }
    }

    @ViewBuilder
    private func renderRightContentWithTokens(line: DiffLine) -> some View {
        if line.tokensRight.isEmpty {
            Text(line.contentRight)
                .font(.system(size: 12, design: .monospaced))
        } else {
            HStack(spacing: 0) {
                Text(line.contentRight)
                    .font(.system(size: 12, design: .monospaced))
                    .overlay(
                        RoundedRectangle(cornerRadius: 3)
                            .fill(Color.yellow.opacity(0.3))
                            .padding(.vertical, 1)
                    )
            }
        }
    }

    private func lineBackground(for change: ChangeType, isLeft: Bool) -> Color {
        switch change {
        case .unchanged:
            return Color.clear
        case .deleted:
            return Color.red.opacity(0.18)
        case .added:
            return Color.green.opacity(0.18)
        case .modified:
            return isLeft ? Color.red.opacity(0.15) : Color.green.opacity(0.15)
        }
    }
}
