import SwiftUI
import AppKit

public struct WordParagraphDiffPane: View {
    @Bindable public var viewModel: WordDiffViewModel
    @State private var activePopoverBlockId: UUID?

    public init(viewModel: WordDiffViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        VStack(spacing: 0) {
            // Header Bar
            HStack(spacing: 0) {
                fileHeaderView(
                    title: viewModel.leftDocument?.fileName ?? LanguageManager.shared.text(.chooseSourceFile),
                    subtitle: viewModel.leftDocument != nil ? "\(viewModel.leftDocument!.metadata.fileSizeFormatted) • \(viewModel.leftDocument!.metadata.wordCount) words" : LanguageManager.shared.text(.noFileSelected),
                    isLeft: true
                )
                Divider()
                fileHeaderView(
                    title: viewModel.rightDocument?.fileName ?? LanguageManager.shared.text(.chooseTargetFile),
                    subtitle: viewModel.rightDocument != nil ? "\(viewModel.rightDocument!.metadata.fileSizeFormatted) • \(viewModel.rightDocument!.metadata.wordCount) words" : LanguageManager.shared.text(.noFileSelected),
                    isLeft: false
                )
            }
            .frame(height: 42)
            .background(Color(nsColor: .controlBackgroundColor))

            Divider()

            // Main Diff Canvas with Synchronized Scroll
            if viewModel.diffResult.blocks.isEmpty && !viewModel.isLoading {
                emptyCanvasPlaceholder
            } else {
                ScrollViewReader { proxy in
                    ScrollView([.vertical]) {
                        LazyVStack(spacing: 0) {
                            ForEach(viewModel.filteredBlocks) { block in
                                let isSelected = viewModel.selectedBlockIndex == block.blockIndex

                                HStack(spacing: 0) {
                                    // Left Pane
                                    paragraphCell(
                                        paragraph: block.leftParagraph,
                                        changeType: block.changeType,
                                        tokens: block.tokensLeft,
                                        isLeft: true,
                                        isSelected: isSelected
                                    )

                                    // Center Gutter / Marker
                                    centerGutter(for: block)

                                    // Right Pane
                                    paragraphCell(
                                        paragraph: block.rightParagraph,
                                        changeType: block.changeType,
                                        tokens: block.tokensRight,
                                        isLeft: false,
                                        isSelected: isSelected,
                                        formatDifferences: block.formatDifferences
                                    )
                                }
                                .id(block.blockIndex)
                                .background(rowBackground(for: block.changeType, isSelected: isSelected))
                                Divider()
                            }
                        }
                    }
                    .onChange(of: viewModel.selectedBlockIndex) { _, newIndex in
                        if let idx = newIndex {
                            withAnimation(.easeInOut(duration: 0.2)) {
                                proxy.scrollTo(idx, anchor: .center)
                            }
                        }
                    }
                }
            }
        }
    }

    // MARK: - File Header

    private func fileHeaderView(title: String, subtitle: String, isLeft: Bool) -> some View {
        HStack {
            Image(systemName: isLeft ? "doc.badge.arrow.up" : "doc.badge.plus")
                .foregroundColor(.accentColor)
                .font(.system(size: 14))

            VStack(alignment: .leading, spacing: 2) {
                Text(title)
                    .font(.system(size: 12, weight: .semibold))
                    .lineLimit(1)
                Text(subtitle)
                    .font(.system(size: 10))
                    .foregroundColor(.secondary)
            }

            Spacer()

            Button(LanguageManager.shared.text(.chooseButton)) {
                chooseFile(isLeft: isLeft)
            }
            .buttonStyle(.bordered)
            .controlSize(.small)
        }
        .padding(.horizontal, 12)
        .frame(maxWidth: .infinity)
    }

    // MARK: - Paragraph Cell

    @ViewBuilder
    private func paragraphCell(
        paragraph: WordParagraph?,
        changeType: ChangeType,
        tokens: [DiffToken],
        isLeft: Bool,
        isSelected: Bool,
        formatDifferences: [FormatDiffItem] = []
    ) -> some View {
        if let p = paragraph {
            VStack(alignment: .leading, spacing: 4) {
                HStack(alignment: .top, spacing: 8) {
                    // Paragraph index badge
                    Text("\(p.index)")
                        .font(.system(size: 10, weight: .regular, design: .monospaced))
                        .foregroundColor(.secondary.opacity(0.7))
                        .frame(width: 28, alignment: .trailing)

                    // Text Content with runs / fine diff tokens
                    VStack(alignment: .leading, spacing: 2) {
                        if !tokens.isEmpty {
                            highlightedTokenText(fullText: p.text, tokens: tokens, isLeft: isLeft)
                        } else {
                            renderedRunsText(runs: p.runs, baseText: p.text, headingLevel: p.headingLevel)
                        }

                        // Formatting Difference Badge if present on right pane
                        if !formatDifferences.isEmpty && !isLeft {
                            HStack(spacing: 4) {
                                Image(systemName: "sparkles")
                                    .font(.system(size: 10))
                                    .foregroundColor(.purple)
                                Text("\(formatDifferences.count) format changes")
                                    .font(.system(size: 10, weight: .medium))
                                    .foregroundColor(.purple)
                            }
                            .padding(.horizontal, 6)
                            .padding(.vertical, 2)
                            .background(Capsule().fill(Color.purple.opacity(0.12)))
                            .popover(isPresented: Binding(
                                get: { activePopoverBlockId == p.id },
                                set: { if !$0 { activePopoverBlockId = nil } }
                            )) {
                                WordFormatPopoverView(formatDifferences: formatDifferences)
                            }
                            .onTapGesture {
                                activePopoverBlockId = p.id
                            }
                        }
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 8)
            .frame(maxWidth: .infinity, alignment: .leading)
        } else {
            // Phantom Alignment Placeholder
            HStack {
                Spacer()
                Text("—")
                    .font(.system(size: 12))
                    .foregroundColor(.secondary.opacity(0.3))
                Spacer()
            }
            .padding(.vertical, 16)
            .frame(maxWidth: .infinity)
            .background(
                Rectangle()
                    .fill(Color.secondary.opacity(0.04))
            )
        }
    }

    // MARK: - Center Gutter

    private func centerGutter(for block: WordDiffBlock) -> some View {
        ZStack {
            Rectangle()
                .fill(Color(nsColor: .separatorColor).opacity(0.4))
                .frame(width: 24)

            switch block.changeType {
            case .added:
                Image(systemName: "plus.circle.fill")
                    .font(.system(size: 11))
                    .foregroundColor(.green)
            case .deleted:
                Image(systemName: "minus.circle.fill")
                    .font(.system(size: 11))
                    .foregroundColor(.red)
            case .modified:
                if block.isFormatOnly {
                    Image(systemName: "paintpalette.fill")
                        .font(.system(size: 10))
                        .foregroundColor(.purple)
                } else {
                    Image(systemName: "pencil.circle.fill")
                        .font(.system(size: 11))
                        .foregroundColor(.orange)
                }
            case .unchanged:
                EmptyView()
            }
        }
        .frame(width: 24)
    }

    // MARK: - Highlighted Token Rendering

    private func highlightedTokenText(fullText: String, tokens: [DiffToken], isLeft: Bool) -> some View {
        let chars = Array(fullText)
        var segments: [(text: String, type: ChangeType)] = []
        var lastOffset = 0

        for token in tokens.sorted(by: { $0.startOffset < $1.startOffset }) {
            let start = Int(token.startOffset)
            let length = Int(token.length)

            if start > lastOffset && lastOffset < chars.count {
                let unchangedSub = String(chars[lastOffset..<min(start, chars.count)])
                segments.append((unchangedSub, .unchanged))
            }

            let end = min(start + length, chars.count)
            if start < chars.count && end <= chars.count && start < end {
                let tokenSub = String(chars[start..<end])
                segments.append((tokenSub, token.changeType))
            }
            lastOffset = end
        }

        if lastOffset < chars.count {
            let trailingSub = String(chars[lastOffset..<chars.count])
            segments.append((trailingSub, .unchanged))
        }

        return HStack(spacing: 0) {
            ForEach(Array(segments.enumerated()), id: \.offset) { _, seg in
                switch seg.type {
                case .added:
                    Text(seg.text)
                        .font(.system(size: 13, weight: .medium))
                        .foregroundColor(.green)
                        .padding(.horizontal, 2)
                        .background(RoundedRectangle(cornerRadius: 2).fill(Color.green.opacity(0.2)))
                case .deleted:
                    Text(seg.text)
                        .font(.system(size: 13))
                        .foregroundColor(.red)
                        .strikethrough()
                        .padding(.horizontal, 2)
                        .background(RoundedRectangle(cornerRadius: 2).fill(Color.red.opacity(0.2)))
                case .modified:
                    Text(seg.text)
                        .font(.system(size: 13, weight: .medium))
                        .foregroundColor(.orange)
                        .padding(.horizontal, 2)
                        .background(RoundedRectangle(cornerRadius: 2).fill(Color.orange.opacity(0.2)))
                case .unchanged:
                    Text(seg.text)
                        .font(.system(size: 13))
                        .foregroundColor(.primary)
                }
            }
        }
    }

    // MARK: - Rich Text Runs Rendering

    private func renderedRunsText(runs: [WordTextRun], baseText: String, headingLevel: Int?) -> some View {
        let isHeading = headingLevel != nil
        let baseFontSize: CGFloat = {
            switch headingLevel {
            case 1: return 18
            case 2: return 16
            case 3: return 14
            default: return 13
            }
        }()

        return HStack(alignment: .firstTextBaseline, spacing: 0) {
            ForEach(runs) { run in
                var text = Text(run.text)
                    .font(.system(
                        size: run.fontSize ?? baseFontSize,
                        weight: run.isBold || isHeading ? .bold : .regular
                    ))

                if run.isItalic {
                    text = text.italic()
                }
                if run.isUnderline {
                    text = text.underline()
                }
                if run.isStrikethrough {
                    text = text.strikethrough()
                }

                return text
                    .foregroundColor(colorFromHex(run.fontColorHex) ?? (isHeading ? .primary : .primary.opacity(0.9)))
                    .background(colorFromHex(run.highlightColorHex)?.opacity(0.3) ?? Color.clear)
            }
        }
    }

    // MARK: - Row Background

    private func rowBackground(for changeType: ChangeType, isSelected: Bool) -> Color {
        if isSelected {
            return Color.accentColor.opacity(0.15)
        }
        switch changeType {
        case .added: return Color.green.opacity(0.08)
        case .deleted: return Color.red.opacity(0.08)
        case .modified: return Color.orange.opacity(0.08)
        case .unchanged: return Color.clear
        }
    }

    private var emptyCanvasPlaceholder: some View {
        VStack(spacing: 12) {
            Image(systemName: "doc.badge.plus")
                .font(.system(size: 36))
                .foregroundColor(.secondary.opacity(0.6))
            Text(LanguageManager.shared.text(.noFileSelected))
                .font(.headline)
                .foregroundColor(.secondary)
            Text(LanguageManager.shared.text(.dropWordPrompt))
                .font(.caption)
                .foregroundColor(.secondary.opacity(0.8))
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private func colorFromHex(_ hex: String?) -> Color? {
        guard let hex = hex?.trimmingCharacters(in: CharacterSet.alphanumerics.inverted),
              hex.count == 6,
              let intVal = UInt64(hex, radix: 16) else { return nil }
        let r = Double((intVal & 0xFF0000) >> 16) / 255.0
        let g = Double((intVal & 0x00FF00) >> 8) / 255.0
        let b = Double(intVal & 0x0000FF) / 255.0
        return Color(red: r, green: g, blue: b)
    }

    private func chooseFile(isLeft: Bool) {
        let panel = NSOpenPanel()
        panel.canChooseFiles = true
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = false
        panel.allowedContentTypes = [.init(filenameExtension: "docx")!, .init(filenameExtension: "doc")!, .rtf]

        if panel.runModal() == .OK, let url = panel.url {
            viewModel.loadSingleFile(from: url, isLeft: isLeft)
        }
    }
}
