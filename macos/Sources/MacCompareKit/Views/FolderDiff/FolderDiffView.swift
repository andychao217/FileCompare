import SwiftUI

public struct FolderDiffView: View {
    @Bindable public var viewModel: FolderDiffViewModel

    public init(viewModel: FolderDiffViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        NavigationSplitView {
            // Sidebar
            List {
                Section("Local") {
                    Label("MacCompare", systemImage: "folder")
                    Label("Directory", systemImage: "folder")
                    Label("Documents", systemImage: "doc.on.doc")
                    Label("Compare", systemImage: "arrow.triangle.2.circlepath")
                }
            }
            .listStyle(.sidebar)
            .frame(minWidth: 160)
        } detail: {
            VStack(spacing: 0) {
                // Top Toolbar
                folderToolbar()

                Divider()

                // Dual Pane Folder Comparison Tree
                HStack(spacing: 0) {
                    // Left Folder Pane
                    folderColumn(
                        title: viewModel.leftFolderPath,
                        isLeft: true
                    )

                    Divider()

                    // Right Folder Pane
                    folderColumn(
                        title: viewModel.rightFolderPath,
                        isLeft: false
                    )
                }

                Divider()

                // Bottom Status Bar
                HStack(spacing: 8) {
                    Text("\(viewModel.totalScanned) files scanned,")
                        .foregroundColor(.secondary)
                    Text("\(viewModel.modifiedCount) modified,")
                        .foregroundColor(.orange)
                    Text("\(viewModel.addedCount) added,")
                        .foregroundColor(.green)
                    Text("\(viewModel.deletedCount) deleted")
                        .foregroundColor(.red)

                    Spacer()
                }
                .font(.system(size: 11))
                .padding(.horizontal, 14)
                .padding(.vertical, 5)
                .background(Color(nsColor: .windowBackgroundColor).opacity(0.8))
            }
            .sheet(isPresented: $viewModel.isDryRunPresented) {
                SyncActionSheet()
            }
        }
    }

    private func folderToolbar() -> some View {
        HStack(spacing: 12) {
            Picker("", selection: $viewModel.selectedMode) {
                ForEach(FolderViewMode.allCases) { mode in
                    Text(mode.rawValue).tag(mode)
                }
            }
            .pickerStyle(.segmented)
            .frame(maxWidth: 420)

            Divider().frame(height: 16)

            Button {
                viewModel.syncLeftToRight()
            } label: {
                Label("Sync Left to Right", systemImage: "arrow.right")
                    .font(.system(size: 11))
            }
            .buttonStyle(.bordered)
            .controlSize(.small)

            Button {
                viewModel.syncRightToLeft()
            } label: {
                Label("Sync Right to Left", systemImage: "arrow.left")
                    .font(.system(size: 11))
            }
            .buttonStyle(.bordered)
            .controlSize(.small)

            Spacer()

            Button {
                // Filter rules
            } label: {
                Label("Filter Rules (\(viewModel.filterRulesSummary))", systemImage: "line.3.horizontal.decrease.circle")
                    .font(.system(size: 11))
            }
            .buttonStyle(.bordered)
            .controlSize(.small)

            Button {
                viewModel.isDryRunPresented = true
            } label: {
                Label("Dry Run Preview", systemImage: "play.circle")
                    .font(.system(size: 11))
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.small)
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 6)
        .background(Color(nsColor: .controlBackgroundColor))
    }

    private func folderColumn(title: String, isLeft: Bool) -> some View {
        VStack(spacing: 0) {
            // Pane Header
            HStack(spacing: 6) {
                Image(systemName: "folder.fill")
                    .foregroundColor(isLeft ? .blue : .green)
                Text(title)
                    .font(.system(size: 12, weight: .semibold))

                Spacer()

                Button {} label: {
                    Image(systemName: "info.circle")
                        .font(.system(size: 11))
                }
                .buttonStyle(.plain)

                Button {} label: {
                    Image(systemName: "square.split.2x1")
                        .font(.system(size: 11))
                }
                .buttonStyle(.plain)
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 6)
            .background(Color(nsColor: .controlBackgroundColor).opacity(0.5))

            Divider()

            // Column Titles
            HStack {
                Text("Name").frame(maxWidth: .infinity, alignment: .leading)
                Text("Size").frame(width: 70, alignment: .trailing)
                Text("Modified").frame(width: 140, alignment: .trailing)
            }
            .font(.system(size: 11, weight: .medium))
            .foregroundColor(.secondary)
            .padding(.horizontal, 12)
            .padding(.vertical, 4)
            .background(Color(nsColor: .windowBackgroundColor).opacity(0.3))

            Divider()

            // Tree Rows
            ScrollView {
                VStack(spacing: 0) {
                    ForEach(viewModel.entries) { entry in
                        rowView(entry: entry, isLeft: isLeft)
                    }
                }
            }
        }
        .frame(maxWidth: .infinity)
    }

    private func rowView(entry: AlignedFolderRow, isLeft: Bool) -> some View {
        HStack {
            if isLeft && entry.isLeftMissing || !isLeft && entry.isRightMissing {
                Text("-----------------")
                    .font(.system(size: 11, design: .monospaced))
                    .foregroundColor(.secondary.opacity(0.4))
                Spacer()
                Text("---").frame(width: 70, alignment: .trailing)
                    .foregroundColor(.secondary.opacity(0.4))
                Text("---").frame(width: 140, alignment: .trailing)
                    .foregroundColor(.secondary.opacity(0.4))
            } else {
                HStack(spacing: 6) {
                    Image(systemName: entry.isDirectory ? "folder.fill" : "doc.text")
                        .foregroundColor(entry.isDirectory ? .blue : .secondary)
                        .font(.system(size: 11))

                    Text(entry.name)
                        .font(.system(size: 11))
                        .lineLimit(1)
                }
                .frame(maxWidth: .infinity, alignment: .leading)

                Text(isLeft ? entry.leftSizeFormatted : entry.rightSizeFormatted)
                    .font(.system(size: 10, design: .monospaced))
                    .foregroundColor(.secondary)
                    .frame(width: 70, alignment: .trailing)

                Text(isLeft ? entry.leftModifiedFormatted : entry.rightModifiedFormatted)
                    .font(.system(size: 10))
                    .foregroundColor(.secondary)
                    .frame(width: 140, alignment: .trailing)
            }
        }
        .font(.system(size: 11))
        .padding(.horizontal, 12)
        .padding(.vertical, 3)
        .background(rowBackground(for: entry.status, isLeft: isLeft))
    }

    private func rowBackground(for status: FolderItemStatus, isLeft: Bool) -> Color {
        switch status {
        case .equal:
            return Color.clear
        case .contentDifferent:
            return Color.cyan.opacity(0.15)
        case .metadataDifferent:
            return Color.yellow.opacity(0.12)
        case .leftOnly:
            return isLeft ? Color.green.opacity(0.15) : Color.gray.opacity(0.1)
        case .rightOnly:
            return !isLeft ? Color.green.opacity(0.15) : Color.gray.opacity(0.1)
        }
    }
}
