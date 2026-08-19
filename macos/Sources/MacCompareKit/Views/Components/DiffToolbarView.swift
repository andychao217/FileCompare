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

                Button {
                    viewModel.nextDiff()
                } label: {
                    Label("Next Diff", systemImage: "arrow.down")
                        .font(.system(size: 11))
                }
                .buttonStyle(.bordered)
                .controlSize(.small)
            }

            Divider().frame(height: 16)

            // Ignore options
            Toggle(isOn: $viewModel.ignoreWhitespace) {
                Text("Ignore Whitespace")
                    .font(.system(size: 12))
            }
            .toggleStyle(.switch)
            .controlSize(.mini)

            Spacer()

            // Merge Actions
            HStack(spacing: 6) {
                Button("Take Left") {
                    viewModel.takeLeft()
                }
                .buttonStyle(.bordered)
                .controlSize(.small)

                Button("Take Right") {
                    viewModel.takeRight()
                }
                .buttonStyle(.bordered)
                .controlSize(.small)
            }

            Divider().frame(height: 16)

            // Encoding
            Picker("", selection: $viewModel.selectedEncoding) {
                Text("UTF-8").tag("UTF-8")
                Text("UTF-16").tag("UTF-16")
                Text("GBK").tag("GBK")
                Text("ASCII").tag("ASCII")
            }
            .frame(width: 90)
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

    public init(
        cursorInfo: String = "Ln 42, Col 15",
        diffStats: String = "3 changes, 1 addition, 2 deletions",
        encoding: String = "UTF-8"
    ) {
        self.cursorInfo = cursorInfo
        self.diffStats = diffStats
        self.encoding = encoding
    }

    public var body: some View {
        HStack(spacing: 16) {
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
