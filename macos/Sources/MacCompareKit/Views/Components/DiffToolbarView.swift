import SwiftUI

public struct DiffToolbarView: View {
    @Bindable public var viewModel: TextDiffViewModel

    public init(viewModel: TextDiffViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        HStack(spacing: 12) {
            // Navigation
            HStack(spacing: 4) {
                Button {
                    viewModel.previousDiff()
                } label: {
                    Label("Previous Diff", systemImage: "arrow.up")
                        .font(.system(size: 11))
                }
                .buttonStyle(.bordered)
                .controlSize(.small)
                .help("Jump to previous difference hunk")

                Button {
                    viewModel.nextDiff()
                } label: {
                    Label("Next Diff", systemImage: "arrow.down")
                        .font(.system(size: 11))
                }
                .buttonStyle(.bordered)
                .controlSize(.small)
                .help("Jump to next difference hunk")
            }

            Divider().frame(height: 16)

            // Ignore options
            Toggle(isOn: $viewModel.ignoreWhitespace) {
                Text("Ignore Whitespace")
                    .font(.system(size: 12))
            }
            .toggleStyle(.switch)
            .controlSize(.mini)

            Toggle(isOn: $viewModel.ignoreCase) {
                Text("Ignore Case")
                    .font(.system(size: 12))
            }
            .toggleStyle(.switch)
            .controlSize(.mini)

            Spacer()

            // Merge Actions
            HStack(spacing: 6) {
                Button {
                    viewModel.takeLeft()
                } label: {
                    Label("Take Left", systemImage: "arrow.right.to.line")
                        .font(.system(size: 11))
                }
                .buttonStyle(.bordered)
                .controlSize(.small)
                .help("Copy current diff hunk from left to right")

                Button {
                    viewModel.takeRight()
                } label: {
                    Label("Take Right", systemImage: "arrow.left.to.line")
                        .font(.system(size: 11))
                }
                .buttonStyle(.bordered)
                .controlSize(.small)
                .help("Copy current diff hunk from right to left")
            }

            Divider().frame(height: 16)

            // Encoding Selector
            Picker("", selection: $viewModel.selectedEncoding) {
                ForEach(FileEncoding.allCases) { enc in
                    Text(enc.rawValue).tag(enc)
                }
            }
            .frame(width: 100)
            .controlSize(.small)
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 6)
        .background(Color(nsColor: .controlBackgroundColor))
    }
}

public struct StatusBarView: View {
    public let cursorInfo: String
    public let diffStats: String
    public let encoding: String
    public let statusMessage: String?

    public init(
        cursorInfo: String = "Ln 1, Col 1",
        diffStats: String = "0 changes",
        encoding: String = "UTF-8",
        statusMessage: String? = nil
    ) {
        self.cursorInfo = cursorInfo
        self.diffStats = diffStats
        self.encoding = encoding
        self.statusMessage = statusMessage
    }

    public var body: some View {
        HStack(spacing: 16) {
            if let msg = statusMessage {
                Text(msg)
                    .font(.system(size: 11))
                    .foregroundColor(.accentColor)
                    .lineLimit(1)
            }

            Spacer()

            Text(cursorInfo)
                .font(.system(size: 11, design: .monospaced))
                .foregroundColor(.secondary)

            Text(diffStats)
                .font(.system(size: 11))
                .foregroundColor(.secondary)

            Text(encoding)
                .font(.system(size: 11, design: .monospaced))
                .foregroundColor(.secondary)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 4)
        .background(Color(nsColor: .windowBackgroundColor).opacity(0.8))
    }
}
