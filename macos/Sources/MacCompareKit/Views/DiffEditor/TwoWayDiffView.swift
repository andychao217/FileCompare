import SwiftUI
import UniformTypeIdentifiers

public struct TwoWayDiffView: View {
    @Bindable public var viewModel: TextDiffViewModel
    @State private var isLeftDropTargeted: Bool = false
    @State private var isRightDropTargeted: Bool = false
    @State private var languageManager = LanguageManager.shared

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
                    // Left Buffer (Original / Source)
                    VStack(spacing: 0) {
                        fileHeader(
                            title: viewModel.leftFileURL != nil ? viewModel.leftTitle : languageManager.text(.sourceFile),
                            icon: "doc.text",
                            isDirty: viewModel.isLeftDirty,
                            onOpen: { viewModel.openLeftFile() },
                            onSave: { viewModel.saveLeftFile() }
                        )
                        Divider()

                        if viewModel.leftContent.isEmpty && viewModel.leftFileURL == nil {
                            emptyDropZone(isLeft: true)
                        } else {
                            ScrollView {
                                VStack(spacing: 0) {
                                    ForEach(Array(viewModel.diffResult.lines.enumerated()), id: \.offset) { idx, line in
                                        leftLineRow(index: idx, line: line)
                                            .id(idx)
                                    }
                                }
                            }
                        }
                    }
                    .frame(maxWidth: .infinity)
                    .background(isLeftDropTargeted ? Color.accentColor.opacity(0.1) : Color.clear)
                    .onDrop(of: [.fileURL], isTargeted: $isLeftDropTargeted) { providers in
                        handleDrop(providers: providers, isLeft: true)
                    }

                    Divider()

                    // Right Buffer (Modified / Target)
                    VStack(spacing: 0) {
                        fileHeader(
                            title: viewModel.rightFileURL != nil ? viewModel.rightTitle : languageManager.text(.targetFile),
                            icon: "doc.text.fill",
                            isDirty: viewModel.isRightDirty,
                            onOpen: { viewModel.openRightFile() },
                            onSave: { viewModel.saveRightFile() }
                        )
                        Divider()

                        if viewModel.rightContent.isEmpty && viewModel.rightFileURL == nil {
                            emptyDropZone(isLeft: false)
                        } else {
                            ScrollView {
                                VStack(spacing: 0) {
                                    ForEach(Array(viewModel.diffResult.lines.enumerated()), id: \.offset) { idx, line in
                                        rightLineRow(index: idx, line: line)
                                            .id(idx)
                                    }
                                }
                            }
                        }
                    }
                    .frame(maxWidth: .infinity)
                    .background(isRightDropTargeted ? Color.accentColor.opacity(0.1) : Color.clear)
                    .onDrop(of: [.fileURL], isTargeted: $isRightDropTargeted) { providers in
                        handleDrop(providers: providers, isLeft: false)
                    }

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
                diffStats: "\(viewModel.diffResult.totalModifications + viewModel.diffResult.totalAdditions + viewModel.diffResult.totalDeletions) \(languageManager.text(.totalChanges)), \(viewModel.diffResult.totalAdditions) \(languageManager.text(.additions)), \(viewModel.diffResult.totalDeletions) \(languageManager.text(.deletions))",
                encoding: viewModel.selectedEncoding.rawValue,
                statusMessage: viewModel.statusMessage
            )
        }
        .background(Color(nsColor: .textBackgroundColor))
    }

    private func emptyDropZone(isLeft: Bool) -> some View {
        VStack(spacing: 12) {
            Image(systemName: isLeft ? "doc.badge.plus" : "arrow.triangle.swap")
                .font(.system(size: 38))
                .foregroundColor(.secondary.opacity(0.7))

            Text(languageManager.text(.noFileSelected))
                .font(.system(size: 14, weight: .medium))
                .foregroundColor(.primary)

            Text(languageManager.text(.dropFilePrompt))
                .font(.system(size: 12))
                .foregroundColor(.secondary)

            Button(isLeft ? languageManager.text(.chooseSourceFile) : languageManager.text(.chooseTargetFile)) {
                if isLeft {
                    viewModel.openLeftFile()
                } else {
                    viewModel.openRightFile()
                }
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.small)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(24)
    }

    private func handleDrop(providers: [NSItemProvider], isLeft: Bool) -> Bool {
        guard let provider = providers.first else { return false }
        _ = provider.loadObject(ofClass: URL.self) { url, _ in
            guard let fileURL = url else { return }
            DispatchQueue.main.async {
                self.viewModel.loadSingleFile(from: fileURL, isLeft: isLeft)
            }
        }
        return true
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
                    .help(languageManager.text(.unsavedChanges))
            }

            Spacer()

            Button(languageManager.text(.chooseButton)) {
                onOpen()
            }
            .buttonStyle(.bordered)
            .controlSize(.mini)

            Button(languageManager.text(.saveButton)) {
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
