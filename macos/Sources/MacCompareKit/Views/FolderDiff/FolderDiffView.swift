import SwiftUI

public struct FolderDiffView: View {
    @Bindable public var viewModel: FolderDiffViewModel

    public init(viewModel: FolderDiffViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        NavigationSplitView {
            // Sidebar
            List(selection: $viewModel.selectedSidebarSection) {
                Section("Quick Places") {
                    Button {
                        viewModel.openDocumentsFolder()
                    } label: {
                        Label("Documents", systemImage: "doc.on.doc")
                    }
                    .buttonStyle(.plain)

                    Button {
                        viewModel.openDownloadsFolder()
                    } label: {
                        Label("Downloads", systemImage: "arrow.down.circle")
                    }
                    .buttonStyle(.plain)

                    Button {
                        viewModel.openDesktopFolder()
                    } label: {
                        Label("Desktop", systemImage: "desktopcomputer")
                    }
                    .buttonStyle(.plain)

                    Button {
                        viewModel.chooseLeftFolder()
                    } label: {
                        Label("Browse Folder...", systemImage: "folder.badge.plus")
                    }
                    .buttonStyle(.plain)
                }

                Section("Tools") {
                    Button {
                        viewModel.swapFolders()
                    } label: {
                        Label("Swap Left & Right", systemImage: "arrow.left.arrow.right")
                    }
                    .buttonStyle(.plain)

                    Button {
                        Task { await viewModel.scanDirectories() }
                    } label: {
                        Label("Rescan Folders", systemImage: "arrow.clockwise")
                    }
                    .buttonStyle(.plain)
                }

                if !viewModel.recentSessions.isEmpty {
                    Section("Recent Compares") {
                        ForEach(viewModel.recentSessions) { session in
                            Button {
                                viewModel.loadRecentSession(session)
                            } label: {
                                VStack(alignment: .leading, spacing: 2) {
                                    Text(session.title)
                                        .font(.system(size: 11, weight: .medium))
                                        .lineLimit(1)
                                    Text(session.leftPath)
                                        .font(.system(size: 9))
                                        .foregroundColor(.secondary)
                                        .lineLimit(1)
                                }
                            }
                            .buttonStyle(.plain)
                        }
                    }
                }
            }
            .listStyle(.sidebar)
            .frame(minWidth: 175, idealWidth: 190)
        } detail: {
            VStack(spacing: 0) {
                // Top Toolbar
                folderToolbar()

                Divider()

                // Dual Pane Folder Comparison Tree
                HStack(spacing: 0) {
                    // Left Folder Pane
                    folderColumn(
                        title: viewModel.leftFolderName,
                        isLeft: true,
                        onChoose: { viewModel.chooseLeftFolder() }
                    )

                    Divider()

                    // Right Folder Pane
                    folderColumn(
                        title: viewModel.rightFolderName,
                        isLeft: false,
                        onChoose: { viewModel.chooseRightFolder() }
                    )
                }

                Divider()

                // Bottom Status Bar
                HStack(spacing: 8) {
                    if viewModel.isScanning {
                        ProgressView()
                            .controlSize(.small)
                        Text("Scanning directory tree...")
                            .foregroundColor(.secondary)
                    } else {
                        Text("\(viewModel.totalScanned) items,")
                            .foregroundColor(.secondary)
                        Text("\(viewModel.modifiedCount) modified,")
                            .foregroundColor(.orange)
                        Text("\(viewModel.addedCount) added,")
                            .foregroundColor(.green)
                        Text("\(viewModel.deletedCount) deleted")
                            .foregroundColor(.red)
                    }

                    if let syncMsg = viewModel.syncExecutionResult {
                        Spacer()
                        Text(syncMsg)
                            .foregroundColor(.accentColor)
                    }

                    Spacer()
                }
                .font(.system(size: 11))
                .padding(.horizontal, 14)
                .padding(.vertical, 5)
                .background(Color(nsColor: .windowBackgroundColor).opacity(0.8))
            }
            .sheet(isPresented: $viewModel.isDryRunPresented) {
                SyncActionSheet(viewModel: viewModel)
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
            .help("Preview and sync all source files to target")

            Button {
                viewModel.syncRightToLeft()
            } label: {
                Label("Sync Right to Left", systemImage: "arrow.left")
                    .font(.system(size: 11))
            }
            .buttonStyle(.bordered)
            .controlSize(.small)
            .help("Preview and sync all target files to source")

            Spacer()

            Button {
                Task { await viewModel.scanDirectories() }
            } label: {
                Label("Refresh", systemImage: "arrow.clockwise")
                    .font(.system(size: 11))
            }
            .buttonStyle(.bordered)
            .controlSize(.small)

            Button {
                viewModel.syncLeftToRight()
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

    private func folderColumn(
        title: String,
        isLeft: Bool,
        onChoose: @escaping () -> Void
    ) -> some View {
        VStack(spacing: 0) {
            // Pane Header
            HStack(spacing: 6) {
                Image(systemName: "folder.fill")
                    .foregroundColor(isLeft ? .blue : .green)
                Text(title)
                    .font(.system(size: 12, weight: .semibold))
                    .lineLimit(1)

                Spacer()

                Button("Choose...") {
                    onChoose()
                }
                .buttonStyle(.bordered)
                .controlSize(.mini)
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 6)
            .background(Color(nsColor: .controlBackgroundColor).opacity(0.5))

            Divider()

            // Column Titles
            HStack {
                Text("Name").frame(maxWidth: .infinity, alignment: .leading)
                if viewModel.selectedMode == .deepHash {
                    Text("CRC32").frame(width: 80, alignment: .trailing)
                }
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
                            .onTapGesture(count: 2) {
                                viewModel.handleRowDoubleClick(entry: entry)
                            }
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
                if viewModel.selectedMode == .deepHash {
                    Text("---").frame(width: 80, alignment: .trailing).foregroundColor(.secondary.opacity(0.4))
                }
                Text("---").frame(width: 70, alignment: .trailing).foregroundColor(.secondary.opacity(0.4))
                Text("---").frame(width: 140, alignment: .trailing).foregroundColor(.secondary.opacity(0.4))
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

                if viewModel.selectedMode == .deepHash {
                    Text(isLeft ? (entry.leftHash ?? "---") : (entry.rightHash ?? "---"))
                        .font(.system(size: 10, design: .monospaced))
                        .foregroundColor(.secondary)
                        .frame(width: 80, alignment: .trailing)
                }

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
        .contentShape(Rectangle())
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
