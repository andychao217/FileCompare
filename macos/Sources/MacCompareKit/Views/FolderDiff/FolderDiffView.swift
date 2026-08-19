import SwiftUI
import UniformTypeIdentifiers

public struct FolderDiffView: View {
    @Bindable public var viewModel: FolderDiffViewModel
    @State private var isLeftDropTargeted: Bool = false
    @State private var isRightDropTargeted: Bool = false
    @State private var languageManager = LanguageManager.shared

    public init(viewModel: FolderDiffViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        NavigationSplitView {
            // Sidebar
            List(selection: $viewModel.selectedSidebarSection) {
                Section(languageManager.text(.quickPlaces)) {
                    Button {
                        viewModel.openDocumentsFolder()
                    } label: {
                        Label(languageManager.text(.documents), systemImage: "doc.on.doc")
                    }
                    .buttonStyle(.plain)

                    Button {
                        viewModel.openDownloadsFolder()
                    } label: {
                        Label(languageManager.text(.downloads), systemImage: "arrow.down.circle")
                    }
                    .buttonStyle(.plain)

                    Button {
                        viewModel.openDesktopFolder()
                    } label: {
                        Label(languageManager.text(.desktop), systemImage: "desktopcomputer")
                    }
                    .buttonStyle(.plain)

                    Button {
                        viewModel.chooseLeftFolder()
                    } label: {
                        Label(languageManager.text(.browseFolder), systemImage: "folder.badge.plus")
                    }
                    .buttonStyle(.plain)
                }

                Section(languageManager.text(.tools)) {
                    Button {
                        viewModel.swapFolders()
                    } label: {
                        Label(languageManager.text(.swapFolders), systemImage: "arrow.left.arrow.right")
                    }
                    .buttonStyle(.plain)

                    Button {
                        Task { await viewModel.scanDirectories() }
                    } label: {
                        Label(languageManager.text(.rescanFolders), systemImage: "arrow.clockwise")
                    }
                    .buttonStyle(.plain)

                    Button {
                        viewModel.clearFolders()
                    } label: {
                        Label(languageManager.text(.clearAll), systemImage: "trash")
                    }
                    .buttonStyle(.plain)
                    .disabled(!viewModel.hasFoldersLoaded)
                }

                if !viewModel.recentSessions.isEmpty {
                    Section(languageManager.text(.recentCompares)) {
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
                        title: viewModel.leftFolderURL != nil ? viewModel.leftFolderName : languageManager.text(.noSourceFolder),
                        isLeft: true,
                        hasFolder: viewModel.leftFolderURL != nil,
                        onChoose: { viewModel.chooseLeftFolder() }
                    )
                    .background(isLeftDropTargeted ? Color.accentColor.opacity(0.1) : Color.clear)
                    .onDrop(of: [.fileURL], isTargeted: $isLeftDropTargeted) { providers in
                        handleFolderDrop(providers: providers, isLeft: true)
                    }

                    Divider()

                    // Right Folder Pane
                    folderColumn(
                        title: viewModel.rightFolderURL != nil ? viewModel.rightFolderName : languageManager.text(.noTargetFolder),
                        isLeft: false,
                        hasFolder: viewModel.rightFolderURL != nil,
                        onChoose: { viewModel.chooseRightFolder() }
                    )
                    .background(isRightDropTargeted ? Color.accentColor.opacity(0.1) : Color.clear)
                    .onDrop(of: [.fileURL], isTargeted: $isRightDropTargeted) { providers in
                        handleFolderDrop(providers: providers, isLeft: false)
                    }
                }

                Divider()

                // Bottom Status Bar
                HStack(spacing: 8) {
                    if viewModel.isScanning {
                        ProgressView()
                            .controlSize(.small)
                        Text(languageManager.text(.scanningTree))
                            .foregroundColor(.secondary)
                    } else if !viewModel.hasFoldersLoaded {
                        Text(languageManager.text(.selectTwoFoldersPrompt))
                            .foregroundColor(.secondary)
                    } else {
                        Text("\(viewModel.totalScanned) \(languageManager.text(.itemsCount)),")
                            .foregroundColor(.secondary)
                        Text("\(viewModel.modifiedCount) \(languageManager.text(.modifiedCount)),")
                            .foregroundColor(.orange)
                        Text("\(viewModel.addedCount) \(languageManager.text(.addedCount)),")
                            .foregroundColor(.green)
                        Text("\(viewModel.deletedCount) \(languageManager.text(.deletedCount))")
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

    private func handleFolderDrop(providers: [NSItemProvider], isLeft: Bool) -> Bool {
        guard let provider = providers.first else { return false }
        _ = provider.loadObject(ofClass: URL.self) { url, _ in
            guard let folderURL = url else { return }
            var isDir: ObjCBool = false
            if FileManager.default.fileExists(atPath: folderURL.path, isDirectory: &isDir), isDir.boolValue {
                DispatchQueue.main.async {
                    self.viewModel.loadSingleFolder(from: folderURL, isLeft: isLeft)
                }
            }
        }
        return true
    }

    private func folderToolbar() -> some View {
        HStack(spacing: 12) {
            Picker("", selection: $viewModel.selectedMode) {
                Text(languageManager.text(.quickCompareMode)).tag(FolderViewMode.quick)
                Text(languageManager.text(.deepHashCompareMode)).tag(FolderViewMode.deepHash)
            }
            .pickerStyle(.segmented)
            .frame(maxWidth: 420)

            Divider().frame(height: 16)

            Button {
                viewModel.syncLeftToRight()
            } label: {
                Label(languageManager.text(.syncLeftToRight), systemImage: "arrow.right")
                    .font(.system(size: 11))
            }
            .buttonStyle(.bordered)
            .controlSize(.small)
            .disabled(!viewModel.hasFoldersLoaded)
            .help("Preview and sync all source files to target")

            Button {
                viewModel.syncRightToLeft()
            } label: {
                Label(languageManager.text(.syncRightToLeft), systemImage: "arrow.left")
                    .font(.system(size: 11))
            }
            .buttonStyle(.bordered)
            .controlSize(.small)
            .disabled(!viewModel.hasFoldersLoaded)
            .help("Preview and sync all target files to source")

            Spacer()

            Button {
                Task { await viewModel.scanDirectories() }
            } label: {
                Label(languageManager.text(.refresh), systemImage: "arrow.clockwise")
                    .font(.system(size: 11))
            }
            .buttonStyle(.bordered)
            .controlSize(.small)
            .disabled(!viewModel.hasFoldersLoaded)

            Button {
                viewModel.clearFolders()
            } label: {
                Label(languageManager.text(.clearAll), systemImage: "trash")
                    .font(.system(size: 11))
            }
            .buttonStyle(.bordered)
            .controlSize(.small)
            .disabled(!viewModel.hasFoldersLoaded)
            .help(languageManager.text(.clearAll))

            Button {
                viewModel.syncLeftToRight()
            } label: {
                Label(languageManager.text(.dryRunPreview), systemImage: "play.circle")
                    .font(.system(size: 11))
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.small)
            .disabled(!viewModel.hasFoldersLoaded)
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 6)
        .background(Color(nsColor: .controlBackgroundColor))
    }

    private func folderColumn(
        title: String,
        isLeft: Bool,
        hasFolder: Bool,
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

                Button(languageManager.text(.chooseButton)) {
                    onChoose()
                }
                .buttonStyle(.bordered)
                .controlSize(.mini)
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 6)
            .background(Color(nsColor: .controlBackgroundColor).opacity(0.5))

            Divider()

            if !hasFolder {
                emptyFolderDropZone(isLeft: isLeft, onChoose: onChoose)
            } else {
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
        }
        .frame(maxWidth: .infinity)
    }

    private func emptyFolderDropZone(isLeft: Bool, onChoose: @escaping () -> Void) -> some View {
        VStack(spacing: 12) {
            Image(systemName: isLeft ? "folder.badge.plus" : "folder.badge.gearshape")
                .font(.system(size: 38))
                .foregroundColor(.secondary.opacity(0.7))

            Text(isLeft ? languageManager.text(.noSourceFolder) : languageManager.text(.noTargetFolder))
                .font(.system(size: 14, weight: .medium))
                .foregroundColor(.primary)

            Text(languageManager.text(.dropFolderPrompt))
                .font(.system(size: 12))
                .foregroundColor(.secondary)

            Button(isLeft ? languageManager.text(.chooseSourceFolder) : languageManager.text(.chooseTargetFolder)) {
                onChoose()
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.small)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(24)
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
