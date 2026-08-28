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

            // Main Diff Canvas Area
            mainContentCanvas
        }
    }

    // MARK: - Main Canvas Switching

    @ViewBuilder
    private var mainContentCanvas: some View {
        if viewModel.leftDocument == nil && viewModel.rightDocument == nil {
            // Both empty: Dual-pane empty drop zones with a distinct vertical divider
            HStack(spacing: 0) {
                emptyDropZone(isLeft: true)
                Divider()
                emptyDropZone(isLeft: false)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        } else if viewModel.leftDocument != nil && viewModel.rightDocument == nil {
            // Left loaded, Right empty
            HStack(spacing: 0) {
                singleSideDocumentList(isLeft: true)
                Divider()
                emptyDropZone(isLeft: false)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        } else if viewModel.leftDocument == nil && viewModel.rightDocument != nil {
            // Left empty, Right loaded
            HStack(spacing: 0) {
                emptyDropZone(isLeft: true)
                Divider()
                singleSideDocumentList(isLeft: false)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        } else {
            // Both loaded: Synchronized Diff Canvas
            diffSynchronizedCanvas
        }
    }

    // MARK: - Synchronized Diff Canvas

    private var diffSynchronizedCanvas: some View {
        ScrollViewReader { proxy in
            ScrollView([.vertical]) {
                LazyVStack(spacing: 0) {
                    ForEach(viewModel.filteredBlocks, id: \.id) { block in
                        let isSelected = viewModel.selectedBlockIndex == block.blockIndex

                        HStack(spacing: 0) {
                            // Left Pane
                            paragraphCell(
                                paragraph: block.leftParagraph,
                                changeType: block.changeType,
                                tokens: block.tokensLeft,
                                isLeft: true,
                                isSelected: isSelected,
                                mediaDifferences: block.mediaDifferences,
                                tableDiff: block.tableDiff
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
                                formatDifferences: block.formatDifferences,
                                mediaDifferences: block.mediaDifferences,
                                tableDiff: block.tableDiff
                            )
                        }
                        .id(block.blockIndex)
                        .background(rowBackground(for: block.changeType, isSelected: isSelected))
                        Divider()
                    }
                }
            }
            .id(viewModel.diffResult.id)
            .onChange(of: viewModel.selectedBlockIndex) { _, newIndex in
                if let idx = newIndex {
                    withAnimation(.easeInOut(duration: 0.2)) {
                        proxy.scrollTo(idx, anchor: .center)
                    }
                }
            }
        }
    }

    // MARK: - Single-Side Document List Preview

    private func singleSideDocumentList(isLeft: Bool) -> some View {
        let paragraphs = isLeft ? (viewModel.leftDocument?.paragraphs ?? []) : (viewModel.rightDocument?.paragraphs ?? [])

        return ScrollView([.vertical]) {
            LazyVStack(spacing: 0) {
                ForEach(paragraphs) { p in
                    HStack(alignment: .top, spacing: 8) {
                        Text("\(p.index)")
                            .font(.system(size: 10, weight: .regular, design: .monospaced))
                            .foregroundColor(.secondary.opacity(0.7))
                            .frame(width: 28, alignment: .trailing)

                        VStack(alignment: .leading, spacing: 4) {
                            renderedRunsText(runs: p.runs, baseText: p.text, headingLevel: p.headingLevel)

                            if !p.mediaItems.isEmpty {
                                VStack(alignment: .leading, spacing: 6) {
                                    ForEach(p.mediaItems) { item in
                                        let dummyDiff = WordMediaDiffItem(
                                            changeType: .unchanged,
                                            mediaType: item.mediaType,
                                            leftMedia: isLeft ? item : nil,
                                            rightMedia: isLeft ? nil : item
                                        )
                                        WordMediaThumbnailView(mediaDiff: dummyDiff, isLeft: isLeft)
                                    }
                                }
                                .padding(.top, 4)
                            }
                        }
                        .frame(maxWidth: .infinity, alignment: .leading)
                    }
                    .padding(.horizontal, 10)
                    .padding(.vertical, 8)
                    Divider()
                }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    // MARK: - Empty Drop Zone

    private func emptyDropZone(isLeft: Bool) -> some View {
        VStack(spacing: 14) {
            Spacer()
            Image(systemName: isLeft ? "doc.badge.arrow.up" : "doc.badge.plus")
                .font(.system(size: 40))
                .foregroundColor(.secondary.opacity(0.5))

            Text(isLeft ? LanguageManager.shared.text(.noSourceFolder) : LanguageManager.shared.text(.noTargetFolder))
                .font(.system(size: 13, weight: .medium))
                .foregroundColor(.primary)

            Text(LanguageManager.shared.text(.dropWordPrompt))
                .font(.system(size: 11))
                .foregroundColor(.secondary)
                .multilineTextAlignment(.center)
                .padding(.horizontal, 24)

            Button(isLeft ? LanguageManager.shared.text(.chooseSourceFile) : LanguageManager.shared.text(.chooseTargetFile)) {
                chooseFile(isLeft: isLeft)
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.small)
            Spacer()
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(Color(nsColor: .windowBackgroundColor).opacity(0.3))
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
        formatDifferences: [FormatDiffItem] = [],
        mediaDifferences: [WordMediaDiffItem] = [],
        tableDiff: WordTableDiffResult? = nil
    ) -> some View {
        if let p = paragraph {
            VStack(alignment: .leading, spacing: 4) {
                if let table = p.table {
                    // Render Embedded Native Table Grid with Cell Diff Highlights
                    HStack(alignment: .top, spacing: 8) {
                        Text("\(p.index)")
                            .font(.system(size: 10, weight: .regular, design: .monospaced))
                            .foregroundColor(.secondary.opacity(0.7))
                            .frame(width: 28, alignment: .trailing)

                        VStack(alignment: .leading, spacing: 4) {
                            HStack(spacing: 6) {
                                Image(systemName: "tablecells")
                                    .font(.system(size: 10))
                                    .foregroundColor(.accentColor)
                                Text("表格 (\(table.rows.count)行 × \(table.columnCount)列)")
                                    .font(.system(size: 11, weight: .medium))
                                    .foregroundColor(.secondary)
                            }

                            embeddedTableGrid(table: table, tableDiff: tableDiff, isLeft: isLeft)
                        }
                        .frame(maxWidth: .infinity, alignment: .leading)
                    }
                } else {
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

                            // Media Differences Rendering
                            if !mediaDifferences.isEmpty {
                                VStack(alignment: .leading, spacing: 6) {
                                    ForEach(mediaDifferences) { mDiff in
                                        WordMediaThumbnailView(mediaDiff: mDiff, isLeft: isLeft)
                                    }
                                }
                                .padding(.top, 4)
                            } else if !p.mediaItems.isEmpty {
                                VStack(alignment: .leading, spacing: 6) {
                                    ForEach(p.mediaItems) { item in
                                        let dummyDiff = WordMediaDiffItem(
                                            changeType: .unchanged,
                                            mediaType: item.mediaType,
                                            leftMedia: isLeft ? item : nil,
                                            rightMedia: isLeft ? nil : item
                                        )
                                        WordMediaThumbnailView(mediaDiff: dummyDiff, isLeft: isLeft)
                                    }
                                }
                                .padding(.top, 4)
                            }
                        }
                        .frame(maxWidth: .infinity, alignment: .leading)
                    }
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

        var attrString = AttributedString()
        for seg in segments {
            var segAttr = AttributedString(seg.text)
            segAttr.font = .system(size: 13, weight: (seg.type == .added || seg.type == .modified) ? .medium : .regular)

            switch seg.type {
            case .added:
                segAttr.foregroundColor = Color.green
                segAttr.backgroundColor = Color.green.opacity(0.2)
            case .deleted:
                segAttr.foregroundColor = Color.red
                segAttr.strikethroughStyle = .single
                segAttr.backgroundColor = Color.red.opacity(0.2)
            case .modified:
                segAttr.foregroundColor = Color.orange
                segAttr.backgroundColor = Color.orange.opacity(0.2)
            case .unchanged:
                segAttr.foregroundColor = Color.primary
            }
            attrString.append(segAttr)
        }

        return Text(attrString)
            .lineSpacing(4)
            .fixedSize(horizontal: false, vertical: true)
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

        if runs.isEmpty {
            return Text(baseText)
                .font(.system(size: baseFontSize, weight: isHeading ? .bold : .regular))
                .foregroundColor(isHeading ? .primary : .primary.opacity(0.9))
                .lineSpacing(4)
                .fixedSize(horizontal: false, vertical: true)
        }

        var attrString = AttributedString()
        for run in runs {
            var runAttr = AttributedString(run.text)
            let size = run.fontSize ?? baseFontSize
            let weight: Font.Weight = (run.isBold || isHeading) ? .bold : .regular
            runAttr.font = .system(size: size, weight: weight)

            if run.isItalic {
                runAttr.font = runAttr.font?.italic()
            }
            if run.isUnderline {
                runAttr.underlineStyle = .single
            }
            if run.isStrikethrough {
                runAttr.strikethroughStyle = .single
            }
            runAttr.foregroundColor = adaptiveTextColor(from: run.fontColorHex, isHeading: isHeading)

            if let bgColor = colorFromHex(run.highlightColorHex) {
                runAttr.backgroundColor = bgColor.opacity(0.3)
            }

            attrString.append(runAttr)
        }

        return Text(attrString)
            .lineSpacing(4)
            .fixedSize(horizontal: false, vertical: true)
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

    private func adaptiveTextColor(from hex: String?, isHeading: Bool = false) -> Color {
        guard let hex = hex?.trimmingCharacters(in: CharacterSet.alphanumerics.inverted),
              hex.count == 6,
              let intVal = UInt64(hex, radix: 16) else {
            return isHeading ? Color.primary : Color.primary.opacity(0.9)
        }
        let r = Double((intVal & 0xFF0000) >> 16) / 255.0
        let g = Double((intVal & 0x00FF00) >> 8) / 255.0
        let b = Double(intVal & 0x0000FF) / 255.0

        let luminance = 0.299 * r + 0.587 * g + 0.114 * b

        // If color is very dark (like #000000, #1F2329, #333333), map to Color.primary so it adapts to dark/light themes perfectly
        if luminance < 0.3 {
            return isHeading ? Color.primary : Color.primary.opacity(0.9)
        }

        // If color is neutral gray (like #8F959E), map to Color.secondary
        if abs(r - g) < 0.05 && abs(g - b) < 0.05 && luminance < 0.65 {
            return Color.secondary
        }

        return Color(red: r, green: g, blue: b)
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

    // MARK: - Embedded Table Grid
    
    private func embeddedTableGrid(table: WordTable, tableDiff: WordTableDiffResult?, isLeft: Bool) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            ForEach(Array(table.rows.enumerated()), id: \.offset) { rowIdx, row in
                HStack(spacing: 0) {
                    ForEach(Array(row.cells.enumerated()), id: \.offset) { colIdx, cell in
                        let text = cell.text.trimmingCharacters(in: .whitespacesAndNewlines)
                        let cellDiff = tableDiff?.cellDiffs.first { $0.rowIndex == rowIdx && $0.colIndex == colIdx }
                        let cType = cellDiff?.changeType ?? .unchanged

                        let cellBg: Color = {
                            if row.isHeader { return Color.secondary.opacity(0.08) }
                            switch cType {
                            case .added: return isLeft ? Color.clear : Color.green.opacity(0.18)
                            case .deleted: return isLeft ? Color.red.opacity(0.18) : Color.clear
                            case .modified: return Color.orange.opacity(0.18)
                            case .unchanged: return Color.clear
                            }
                        }()

                        let cellFg: Color = {
                            switch cType {
                            case .added: return isLeft ? adaptiveTextColor(from: nil) : Color.green
                            case .deleted: return isLeft ? Color.red : adaptiveTextColor(from: nil)
                            case .modified: return Color.orange
                            case .unchanged: return adaptiveTextColor(from: nil)
                            }
                        }()

                        Text(text.isEmpty ? " " : text)
                            .font(.system(size: 11, weight: (row.isHeader || cType != .unchanged) ? .semibold : .regular))
                            .foregroundColor(cellFg)
                            .padding(.horizontal, 8)
                            .padding(.vertical, 6)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .background(Rectangle().fill(cellBg))
                            .overlay(
                                Rectangle()
                                    .stroke(
                                        cType != .unchanged
                                            ? (cType == .added ? Color.green.opacity(0.5) : (cType == .deleted ? Color.red.opacity(0.5) : Color.orange.opacity(0.5)))
                                            : Color.secondary.opacity(0.2),
                                        lineWidth: cType != .unchanged ? 1 : 0.5
                                    )
                            )
                    }
                }
            }
        }
        .clipShape(RoundedRectangle(cornerRadius: 4))
        .overlay(
            RoundedRectangle(cornerRadius: 4)
                .stroke(Color.secondary.opacity(0.3), lineWidth: 1)
        )
        .shadow(color: Color.black.opacity(0.04), radius: 2, x: 0, y: 1)
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
