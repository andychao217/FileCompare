import SwiftUI

public struct TwoWayDiffView: View {
    @Bindable public var viewModel: TextDiffViewModel

    public init(viewModel: TextDiffViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        VStack(spacing: 0) {
            // Toolbar
            DiffToolbarView(viewModel: viewModel)

            Divider()

            // Main Diff Split View
            HStack(spacing: 0) {
                // Left Buffer (Original)
                VStack(spacing: 0) {
                    fileHeader(title: viewModel.leftTitle, icon: "doc.text")
                    Divider()
                    ScrollView {
                        VStack(spacing: 0) {
                            ForEach(viewModel.diffResult.lines) { line in
                                leftLineRow(line: line)
                            }
                        }
                    }
                }
                .frame(maxWidth: .infinity)

                Divider()

                // Right Buffer (Modified)
                VStack(spacing: 0) {
                    fileHeader(title: viewModel.rightTitle, icon: "doc.text.fill")
                    Divider()
                    ScrollView {
                        VStack(spacing: 0) {
                            ForEach(viewModel.diffResult.lines) { line in
                                rightLineRow(line: line)
                            }
                        }
                    }
                }
                .frame(maxWidth: .infinity)

                Divider()

                // Minimap
                MinimapView(lines: viewModel.diffResult.lines)
            }

            Divider()

            // Status Bar
            StatusBarView(
                cursorInfo: viewModel.cursorPosition,
                diffStats: "\(viewModel.diffResult.totalModifications + viewModel.diffResult.totalAdditions + viewModel.diffResult.totalDeletions) changes, \(viewModel.diffResult.totalAdditions) addition, \(viewModel.diffResult.totalDeletions) deletions",
                encoding: viewModel.selectedEncoding
            )
        }
        .background(Color(nsColor: .textBackgroundColor))
    }

    private func fileHeader(title: String, icon: String) -> some View {
        HStack(spacing: 6) {
            Image(systemName: icon)
                .font(.system(size: 11))
                .foregroundColor(.secondary)
            Text(title)
                .font(.system(size: 11, weight: .medium))
                .foregroundColor(.secondary)
            Spacer()
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 6)
        .background(Color(nsColor: .controlBackgroundColor).opacity(0.5))
    }

    private func leftLineRow(line: DiffLine) -> some View {
        HStack(spacing: 0) {
            if line.isPhantomLeft {
                PhantomLineView()
            } else {
                // Line Number Gutter
                HStack {
                    if line.changeType == .deleted || line.changeType == .modified {
                        Text("-")
                            .font(.system(size: 11, weight: .bold, design: .monospaced))
                            .foregroundColor(.red.opacity(0.8))
                    } else {
                        Spacer().frame(width: 8)
                    }

                    Spacer()

                    Text(line.leftLineNumber.map { "\($0)" } ?? "")
                        .font(.system(size: 11, design: .monospaced))
                        .foregroundColor(.secondary.opacity(0.8))
                }
                .frame(width: 48)
                .padding(.trailing, 8)
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

    private func rightLineRow(line: DiffLine) -> some View {
        HStack(spacing: 0) {
            if line.isPhantomRight {
                PhantomLineView()
            } else {
                // Line Number Gutter
                HStack {
                    if line.changeType == .added || line.changeType == .modified {
                        Text("+")
                            .font(.system(size: 11, weight: .bold, design: .monospaced))
                            .foregroundColor(.green.opacity(0.8))
                    } else {
                        Spacer().frame(width: 8)
                    }

                    Spacer()

                    Text(line.rightLineNumber.map { "\($0)" } ?? "")
                        .font(.system(size: 11, design: .monospaced))
                        .foregroundColor(.secondary.opacity(0.8))
                }
                .frame(width: 48)
                .padding(.trailing, 8)
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
            // Highlight specific tokens
            HStack(spacing: 0) {
                Text(line.contentRight)
                    .font(.system(size: 12, design: .monospaced))
                    .overlay(
                        RoundedRectangle(cornerRadius: 3)
                            .fill(Color.yellow.opacity(0.25))
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
