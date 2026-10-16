import SwiftUI
import UniformTypeIdentifiers

public struct ThreeWayMergeView: View {
    @Bindable public var viewModel: ThreeWayMergeViewModel
    public var tabManager: TabManager?
    @State private var isLocalTargeted = false
    @State private var isBaseTargeted = false
    @State private var isRemoteTargeted = false
    @State private var showClearConfirmation = false
    @State private var languageManager = LanguageManager.shared
    @State private var validationResult: ValidationResult?

    public init(viewModel: ThreeWayMergeViewModel, tabManager: TabManager? = nil) {
        self.viewModel = viewModel
        self.tabManager = tabManager
    }

    public var body: some View {
        VStack(spacing: 0) {
            // Top Merge Navigation & Action Bar
            mergeHeaderBar()

            Divider()

            // 3-Way Top Pane Split (Local | Base | Remote)
            GeometryReader { geometry in
                VStack(spacing: 0) {
                    if !viewModel.hasFilesLoaded {
                        // Empty state: 3 drop zones side by side
                        HStack(spacing: 0) {
                            emptyBranchZone(
                                title: languageManager.text(.localBranch),
                                branch: viewModel.localBranchName,
                                accentColor: .orange,
                                isTargeted: isLocalTargeted,
                                type: .local
                            )

                            Divider()

                            emptyBranchZone(
                                title: languageManager.text(.baseBranch),
                                branch: viewModel.baseBranchName,
                                accentColor: .gray,
                                isTargeted: isBaseTargeted,
                                type: .base
                            )

                            Divider()

                            emptyBranchZone(
                                title: languageManager.text(.remoteBranch),
                                branch: viewModel.remoteBranchName,
                                accentColor: .green,
                                isTargeted: isRemoteTargeted,
                                type: .remote
                            )
                        }
                        .frame(height: geometry.size.height * 0.58)
                    } else {
                        // Synchronized 3-Way Merge Diff View
                        let availableWidth = max(0, geometry.size.width)
                        let colWidth = max(0, (availableWidth - 2) / 3.0)

                        VStack(spacing: 0) {
                            // Column Headers
                            HStack(spacing: 0) {
                                branchHeader(
                                    title: languageManager.text(.localBranch),
                                    branch: viewModel.localBranchName,
                                    hasFile: viewModel.localFileURL != nil || !viewModel.localContent.isEmpty,
                                    accentColor: .orange,
                                    showActions: true,
                                    onOpen: { chooseFile(for: .local) }
                                )
                                .frame(width: colWidth)
                                .background(isLocalTargeted ? Color.orange.opacity(0.1) : Color.clear)
                                .onDrop(of: [.fileURL], isTargeted: $isLocalTargeted) { providers in
                                    handleDrop(providers: providers, type: .local)
                                }

                                Divider()

                                branchHeader(
                                    title: languageManager.text(.baseBranch),
                                    branch: viewModel.baseBranchName,
                                    hasFile: viewModel.baseFileURL != nil || !viewModel.baseContent.isEmpty,
                                    accentColor: .gray,
                                    showActions: false,
                                    onOpen: { chooseFile(for: .base) }
                                )
                                .frame(width: colWidth)
                                .background(isBaseTargeted ? Color.gray.opacity(0.1) : Color.clear)
                                .onDrop(of: [.fileURL], isTargeted: $isBaseTargeted) { providers in
                                    handleDrop(providers: providers, type: .base)
                                }

                                Divider()

                                branchHeader(
                                    title: languageManager.text(.remoteBranch),
                                    branch: viewModel.remoteBranchName,
                                    hasFile: viewModel.remoteFileURL != nil || !viewModel.remoteContent.isEmpty,
                                    accentColor: .green,
                                    showActions: true,
                                    onOpen: { chooseFile(for: .remote) }
                                )
                                .frame(width: colWidth)
                                .background(isRemoteTargeted ? Color.green.opacity(0.1) : Color.clear)
                                .onDrop(of: [.fileURL], isTargeted: $isRemoteTargeted) { providers in
                                    handleDrop(providers: providers, type: .remote)
                                }
                            }
                            .frame(height: 56)

                            Divider()

                            // Unified Synchronized Scroll View
                            ScrollViewReader { proxy in
                                ScrollView(.vertical) {
                                    LazyVStack(spacing: 0) {
                                        ForEach(Array(viewModel.mergeResult.lines.enumerated()), id: \.offset) { idx, line in
                                            HStack(spacing: 0) {
                                                localLineRow(index: idx, line: line)
                                                    .frame(width: colWidth, height: 20, alignment: .leading)

                                                Divider().frame(height: 20)

                                                baseLineRow(index: idx, line: line)
                                                    .frame(width: colWidth, height: 20, alignment: .leading)

                                                Divider().frame(height: 20)

                                                remoteLineRow(index: idx, line: line)
                                                    .frame(width: colWidth, height: 20, alignment: .leading)
                                            }
                                            .frame(width: availableWidth, height: 20, alignment: .leading)
                                            .id(idx)
                                        }
                                    }
                                }
                            }
                        }
                        .frame(height: geometry.size.height * 0.58)
                    }

                    Divider()

                    // Bottom Merged Result Output
                    mergedOutputPane()
                        .frame(height: geometry.size.height * 0.42)
                }
            }
        }
        .background(Color(nsColor: .textBackgroundColor))
        .overlay {
            if let result = validationResult {
                FileValidationDialogView(
                    result: result,
                    onDismiss: {
                        self.validationResult = nil
                    },
                    onSwitchMode: { mode, url in
                        self.validationResult = nil
                        tabManager?.openSingleTarget(url: url, mode: mode)
                    }
                )
            }
        }
    }

    private func chooseFile(for type: ThreeWayMergeViewModel.MergeFileType) {
        let panel = NSOpenPanel()
        panel.canChooseFiles = true
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = false
        switch type {
        case .local:
            panel.title = languageManager.text(.localBranch)
        case .base:
            panel.title = languageManager.text(.baseBranch)
        case .remote:
            panel.title = languageManager.text(.remoteBranch)
        }
        if panel.runModal() == .OK, let url = panel.url {
            let check = FileFormatValidator.validate(url: url, expectedMode: .threeWayMerge)
            if !check.isValid {
                self.validationResult = check
                return
            }
            viewModel.loadSingleFile(from: url, type: type)
        }
    }

    private func handleDrop(providers: [NSItemProvider], type: ThreeWayMergeViewModel.MergeFileType) -> Bool {
        guard let provider = providers.first else { return false }
        provider.loadFileURL { fileURL in
            guard let fileURL = fileURL else { return }
            DispatchQueue.main.async {
                let check = FileFormatValidator.validate(url: fileURL, expectedMode: .threeWayMerge)
                if !check.isValid {
                    self.validationResult = check
                    return
                }
                self.viewModel.loadSingleFile(from: fileURL, type: type)
            }
        }
        return true
    }

    private func mergeHeaderBar() -> some View {
        HStack(spacing: 12) {
            Image(systemName: "arrow.triangle.branch")
                .foregroundColor(.secondary)

            Text(viewModel.hasFilesLoaded ? viewModel.filePath : languageManager.text(.noFilesSelected))
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

                Text("\(languageManager.text(.conflictCountFormat)) (\(viewModel.totalConflicts > 0 ? viewModel.currentConflictIndex + 1 : 0) / \(viewModel.totalConflicts))")
                    .font(.system(size: 11))
                    .foregroundColor(.secondary)
            }

            Divider().frame(height: 16)

            Button {
                viewModel.autoResolveNonConflicts()
            } label: {
                Label(languageManager.text(.autoResolveNonConflicts), systemImage: "wand.and.stars")
                    .font(.system(size: 11))
            }
            .buttonStyle(.bordered)
            .controlSize(.small)
            .disabled(!viewModel.hasFilesLoaded)

            Button {
                showClearConfirmation = true
            } label: {
                Label(languageManager.text(.clearAll), systemImage: "trash")
                    .font(.system(size: 11))
            }
            .buttonStyle(.bordered)
            .controlSize(.small)
            .disabled(!viewModel.hasFilesLoaded)
            .help(languageManager.text(.clearAll))
            .alert(languageManager.text(.confirmClearTitle), isPresented: $showClearConfirmation) {
                Button(languageManager.text(.clear), role: .destructive) {
                    viewModel.clearAll()
                }
                Button(languageManager.text(.cancel), role: .cancel) {}
            } message: {
                Text(languageManager.text(.confirmClearMessage))
            }

            Button {
                viewModel.saveAndCompleteMerge()
            } label: {
                Label(languageManager.text(.saveAndCompleteMerge), systemImage: "arrow.down.doc.fill")
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

    private func branchHeader(
        title: String,
        branch: String,
        hasFile: Bool,
        accentColor: Color,
        showActions: Bool,
        onOpen: @escaping () -> Void
    ) -> some View {
        VStack(spacing: 0) {
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

                Button(languageManager.text(.chooseButton)) {
                    onOpen()
                }
                .buttonStyle(.bordered)
                .controlSize(.mini)
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 4)

            if showActions {
                HStack(spacing: 6) {
                    Button(languageManager.text(.acceptLocal)) { viewModel.acceptLocal() }
                        .buttonStyle(.bordered)
                        .tint(.orange)
                        .controlSize(.mini)

                    Button(languageManager.text(.takeBoth)) { viewModel.takeBoth() }
                        .buttonStyle(.bordered)
                        .tint(.purple)
                        .controlSize(.mini)

                    Button(languageManager.text(.acceptRemote)) { viewModel.acceptRemote() }
                        .buttonStyle(.bordered)
                        .tint(.green)
                        .controlSize(.mini)
                }
                .padding(.vertical, 2)
            } else {
                Spacer().frame(height: 22)
            }
        }
        .frame(height: 56)
        .background(Color(nsColor: .controlBackgroundColor).opacity(0.4))
    }

    private func emptyBranchZone(
        title: String,
        branch: String,
        accentColor: Color,
        isTargeted: Bool,
        type: ThreeWayMergeViewModel.MergeFileType
    ) -> some View {
        VStack(spacing: 0) {
            // Header
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

                Button(languageManager.text(.chooseButton)) {
                    chooseFile(for: type)
                }
                .buttonStyle(.bordered)
                .controlSize(.mini)
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 6)
            .background(Color(nsColor: .controlBackgroundColor).opacity(0.4))

            Divider()

            VStack(spacing: 8) {
                Image(systemName: "doc.badge.plus")
                    .font(.system(size: 28))
                    .foregroundColor(.secondary.opacity(0.7))
                Text(languageManager.text(.noFileSelected))
                    .font(.system(size: 12, weight: .medium))
                    .foregroundColor(.secondary)
                Button(languageManager.text(.chooseButton)) {
                    chooseFile(for: type)
                }
                .buttonStyle(.bordered)
                .controlSize(.mini)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .padding()
            .background(isTargeted ? accentColor.opacity(0.1) : Color.clear)
            .onDrop(of: [.fileURL], isTargeted: Binding(
                get: {
                    switch type {
                    case .local: return isLocalTargeted
                    case .base: return isBaseTargeted
                    case .remote: return isRemoteTargeted
                    }
                },
                set: { targeted in
                    switch type {
                    case .local: isLocalTargeted = targeted
                    case .base: isBaseTargeted = targeted
                    case .remote: isRemoteTargeted = targeted
                    }
                }
            )) { providers in
                handleDrop(providers: providers, type: type)
            }
        }
        .frame(maxWidth: .infinity)
    }

    private func localLineRow(index: Int, line: MergeLine) -> some View {
        HStack(spacing: 0) {
            if line.localLineNumber == nil {
                PhantomLineView()
                    .frame(maxWidth: .infinity, maxHeight: 20)
            } else {
                // Line Number Gutter
                HStack(spacing: 2) {
                    if line.status == .conflict {
                        Text("!")
                            .font(.system(size: 10, weight: .bold, design: .monospaced))
                            .foregroundColor(.red)
                    } else if line.status == .cleanLocal {
                        Text(line.baseLineNumber == nil ? "+" : "M")
                            .font(.system(size: 10, weight: .bold, design: .monospaced))
                            .foregroundColor(.orange)
                    } else {
                        Spacer().frame(width: 8)
                    }

                    Spacer()

                    Text(line.localLineNumber.map { "\($0)" } ?? "")
                        .font(.system(size: 10, design: .monospaced))
                        .foregroundColor(.secondary.opacity(0.8))
                }
                .frame(width: 44, height: 20)
                .padding(.trailing, 4)
                .background(Color(nsColor: .windowBackgroundColor).opacity(0.3))

                // Line Content
                HStack {
                    Text(line.contentLocal)
                        .font(.system(size: 11, design: .monospaced))
                        .foregroundColor(.primary)
                        .lineLimit(1)
                    Spacer()
                }
                .padding(.horizontal, 6)
                .frame(maxWidth: .infinity, maxHeight: 20, alignment: .leading)
                .background(
                    line.status == .conflict
                        ? Color.red.opacity(0.22)
                        : (line.status == .cleanLocal ? Color.orange.opacity(0.18) : Color.clear)
                )
            }
        }
        .frame(maxWidth: .infinity, maxHeight: 20, alignment: .leading)
    }

    private func baseLineRow(index: Int, line: MergeLine) -> some View {
        HStack(spacing: 0) {
            if line.baseLineNumber == nil {
                PhantomLineView()
                    .frame(maxWidth: .infinity, maxHeight: 20)
            } else {
                // Line Number Gutter
                HStack(spacing: 2) {
                    if line.status == .conflict {
                        Text("!")
                            .font(.system(size: 10, weight: .bold, design: .monospaced))
                            .foregroundColor(.yellow)
                    } else {
                        Spacer().frame(width: 8)
                    }

                    Spacer()

                    Text(line.baseLineNumber.map { "\($0)" } ?? "")
                        .font(.system(size: 10, design: .monospaced))
                        .foregroundColor(.secondary.opacity(0.8))
                }
                .frame(width: 44, height: 20)
                .padding(.trailing, 4)
                .background(Color(nsColor: .windowBackgroundColor).opacity(0.3))

                // Line Content
                HStack {
                    Text(line.contentBase)
                        .font(.system(size: 11, design: .monospaced))
                        .foregroundColor(.primary)
                        .lineLimit(1)
                    Spacer()
                }
                .padding(.horizontal, 6)
                .frame(maxWidth: .infinity, maxHeight: 20, alignment: .leading)
                .background(
                    line.status == .conflict
                        ? Color.red.opacity(0.12)
                        : (line.status == .cleanLocal ? Color.orange.opacity(0.08) :
                           (line.status == .cleanRemote ? Color.green.opacity(0.08) : Color.clear))
                )
            }
        }
        .frame(maxWidth: .infinity, maxHeight: 20, alignment: .leading)
    }

    private func remoteLineRow(index: Int, line: MergeLine) -> some View {
        HStack(spacing: 0) {
            if line.remoteLineNumber == nil {
                PhantomLineView()
                    .frame(maxWidth: .infinity, maxHeight: 20)
            } else {
                // Line Number Gutter
                HStack(spacing: 2) {
                    if line.status == .conflict {
                        Text("!")
                            .font(.system(size: 10, weight: .bold, design: .monospaced))
                            .foregroundColor(.red)
                    } else if line.status == .cleanRemote {
                        Text(line.baseLineNumber == nil ? "+" : "M")
                            .font(.system(size: 10, weight: .bold, design: .monospaced))
                            .foregroundColor(.green)
                    } else {
                        Spacer().frame(width: 8)
                    }

                    Spacer()

                    Text(line.remoteLineNumber.map { "\($0)" } ?? "")
                        .font(.system(size: 10, design: .monospaced))
                        .foregroundColor(.secondary.opacity(0.8))
                }
                .frame(width: 44, height: 20)
                .padding(.trailing, 4)
                .background(Color(nsColor: .windowBackgroundColor).opacity(0.3))

                // Line Content
                HStack {
                    Text(line.contentRemote)
                        .font(.system(size: 11, design: .monospaced))
                        .foregroundColor(.primary)
                        .lineLimit(1)
                    Spacer()
                }
                .padding(.horizontal, 6)
                .frame(maxWidth: .infinity, maxHeight: 20, alignment: .leading)
                .background(
                    line.status == .conflict
                        ? Color.red.opacity(0.22)
                        : (line.status == .cleanRemote ? Color.green.opacity(0.18) : Color.clear)
                )
            }
        }
        .frame(maxWidth: .infinity, maxHeight: 20, alignment: .leading)
    }

    private func mergedOutputPane() -> some View {
        VStack(spacing: 0) {
            // Output Header
            HStack(spacing: 8) {
                Text(languageManager.text(.mergedOutputResult))
                    .font(.system(size: 12, weight: .semibold))

                Text(viewModel.hasFilesLoaded ? viewModel.filePath : languageManager.text(.noFilesSelected))
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
                    Label(languageManager.text(.saveAndCompleteMerge), systemImage: "checkmark.circle.fill")
                        .font(.system(size: 11))
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.small)
                .disabled(!viewModel.hasFilesLoaded)

                HStack(spacing: 4) {
                    if viewModel.totalConflicts > 0 {
                        Image(systemName: "exclamationmark.triangle.fill")
                            .foregroundColor(.yellow)
                        Text("\(viewModel.totalConflicts) \(languageManager.text(.conflictsRemaining))")
                            .font(.system(size: 11, weight: .medium))
                            .foregroundColor(.orange)
                    } else if viewModel.hasFilesLoaded {
                        Image(systemName: "checkmark.circle.fill")
                            .foregroundColor(.green)
                        Text(languageManager.text(.allConflictsResolved))
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
