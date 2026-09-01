import SwiftUI
import AppKit

public struct ExcelTableGridView: View {
    @Bindable var viewModel: ExcelDiffViewModel
    @State private var hoveredRowId: UUID?
    @State private var themeManager = ThemeManager.shared

    public init(viewModel: ExcelDiffViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        guard let currentSheet = viewModel.currentSheetDiff else {
            return AnyView(
                VStack(spacing: 12) {
                    Image(systemName: "tablecells")
                        .font(.system(size: 32))
                        .foregroundColor(.secondary)
                    Text("No Sheet Loaded")
                        .font(.headline)
                        .foregroundColor(.secondary)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(Color(nsColor: .textBackgroundColor))
            )
        }

        let columnHeaders = currentSheet.columnHeaders
        let rows = viewModel.filteredRows

        return AnyView(
            HStack(spacing: 0) {
                // Left Table Pane
                tablePane(
                    isLeft: true,
                    columnHeaders: columnHeaders,
                    rows: rows
                )

                Divider()

                // Right Table Pane
                tablePane(
                    isLeft: false,
                    columnHeaders: columnHeaders,
                    rows: rows
                )
            }
            .background(Color(nsColor: .textBackgroundColor))
        )
    }

    @ViewBuilder
    private func tablePane(
        isLeft: Bool,
        columnHeaders: [String],
        rows: [AlignedExcelRow]
    ) -> some View {
        VStack(spacing: 0) {
            // Column Headers Row (Sticky Header)
            HStack(spacing: 0) {
                // Row Number Header Column
                Text("#")
                    .font(.system(size: 11, weight: .bold, design: .monospaced))
                    .foregroundColor(.secondary)
                    .frame(width: 44, alignment: .center)
                    .padding(.vertical, 6)
                    .background(Color(nsColor: .controlBackgroundColor))

                Divider()

                // Data Column Headers
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: 0) {
                        ForEach(Array(columnHeaders.enumerated()), id: \.offset) { colIdx, headerName in
                            HStack(spacing: 4) {
                                Text(ExcelModelsHelper.columnLetter(for: colIdx))
                                    .font(.system(size: 10, weight: .bold, design: .monospaced))
                                    .foregroundColor(.secondary)
                                Text(headerName)
                                    .font(.system(size: 11, weight: .semibold))
                                    .foregroundColor(.primary)
                                    .lineLimit(1)
                            }
                            .padding(.horizontal, 8)
                            .frame(width: 130, alignment: .leading)
                            .padding(.vertical, 6)
                            .background(Color(nsColor: .controlBackgroundColor))

                            Divider()
                        }
                    }
                }
            }
            .frame(height: 28)
            .background(Color(nsColor: .controlBackgroundColor))

            Divider()

            // Table Rows
            ScrollView([.vertical, .horizontal]) {
                LazyVStack(spacing: 0) {
                    ForEach(rows) { row in
                        tableRowView(isLeft: isLeft, row: row, columnHeaders: columnHeaders)
                            .id(row.id)
                            .onTapGesture {
                                viewModel.selectRow(id: row.id)
                            }
                            .onHover { isHovered in
                                if isHovered { hoveredRowId = row.id }
                                else if hoveredRowId == row.id { hoveredRowId = nil }
                            }
                    }
                }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    @ViewBuilder
    private func tableRowView(
        isLeft: Bool,
        row: AlignedExcelRow,
        columnHeaders: [String]
    ) -> some View {
        let isSelected = (viewModel.selectedRowId == row.id)
        let isHovered = (hoveredRowId == row.id)
        let isPhantom = isLeft ? (row.leftRowIndex == nil) : (row.rightRowIndex == nil)

        HStack(spacing: 0) {
            // Row Number Cell
            HStack(spacing: 2) {
                // Diff Status Dot indicator
                Circle()
                    .fill(statusColor(for: row.rowDiffType, isPhantom: isPhantom))
                    .frame(width: 5, height: 5)
                    .opacity(row.rowDiffType == .unchanged ? 0 : 1)

                Text(isLeft ? row.displayRowNumberLeft : row.displayRowNumberRight)
                    .font(.system(size: 11, weight: .medium, design: .monospaced))
                    .foregroundColor(isPhantom ? .secondary.opacity(0.3) : .secondary)
            }
            .frame(width: 44, alignment: .center)
            .frame(maxHeight: .infinity)
            .background(rowNumberBackground(isSelected: isSelected, isHovered: isHovered, diffType: row.rowDiffType))

            Divider()

            // Data Cells
            if isPhantom {
                // Phantom / Ghost row placeholder
                phantomRowCells(columnCount: columnHeaders.count)
            } else {
                HStack(spacing: 0) {
                    ForEach(row.cellDiffs) { cellDiff in
                        cellView(
                            isLeft: isLeft,
                            cellDiff: cellDiff,
                            rowDiffType: row.rowDiffType,
                            isSelected: isSelected
                        )
                        .frame(width: 130, alignment: .leading)
                        .padding(.vertical, 5)
                        .padding(.horizontal, 8)

                        Divider()
                    }
                }
            }
        }
        .frame(minHeight: 24)
        .background(rowBackground(isSelected: isSelected, isHovered: isHovered, diffType: row.rowDiffType, isPhantom: isPhantom))
        .overlay(
            Rectangle()
                .frame(height: 1)
                .foregroundColor(Color(nsColor: .separatorColor).opacity(0.4)),
            alignment: .bottom
        )
    }

    @ViewBuilder
    private func cellView(
        isLeft: Bool,
        cellDiff: CellDiffItem,
        rowDiffType: RowDiffType,
        isSelected: Bool
    ) -> some View {
        let cell = isLeft ? cellDiff.leftCell : cellDiff.rightCell
        let chunks = isLeft ? cellDiff.leftInlineChunks : cellDiff.rightInlineChunks

        if chunks.isEmpty {
            Text(cell?.rawValue ?? "")
                .font(.system(size: 11, design: .monospaced))
                .foregroundColor(cellTextColor(diffType: cellDiff.diffType))
                .lineLimit(1)
        } else {
            // Inline Chunk rendering
            HStack(spacing: 0) {
                ForEach(chunks) { chunk in
                    if chunk.isDiff {
                        Text(chunk.text)
                            .font(.system(size: 11, weight: .bold, design: .monospaced))
                            .foregroundColor(inlineDiffTextColor(isLeft: isLeft))
                            .background(inlineDiffBackground(isLeft: isLeft))
                    } else {
                        Text(chunk.text)
                            .font(.system(size: 11, design: .monospaced))
                            .foregroundColor(.primary)
                    }
                }
            }
            .lineLimit(1)
        }
    }

    @ViewBuilder
    private func phantomRowCells(columnCount: Int) -> some View {
        HStack(spacing: 0) {
            ForEach(0..<columnCount, id: \.self) { _ in
                Text("")
                    .frame(width: 130)
                    .padding(.vertical, 5)
                    .padding(.horizontal, 8)
                Divider()
            }
        }
    }

    // MARK: - Styling & Color Helpers

    private func statusColor(for diffType: RowDiffType, isPhantom: Bool) -> Color {
        if isPhantom { return .secondary.opacity(0.4) }
        switch diffType {
        case .unchanged: return .clear
        case .modified: return .orange
        case .added: return .green
        case .deleted: return .red
        }
    }

    private func rowNumberBackground(isSelected: Bool, isHovered: Bool, diffType: RowDiffType) -> Color {
        if isSelected { return Color.accentColor.opacity(0.2) }
        if isHovered { return Color(nsColor: .controlBackgroundColor).opacity(0.8) }
        switch diffType {
        case .unchanged: return Color(nsColor: .controlBackgroundColor).opacity(0.4)
        case .modified: return Color.orange.opacity(0.12)
        case .added: return Color.green.opacity(0.12)
        case .deleted: return Color.red.opacity(0.12)
        }
    }

    private func rowBackground(isSelected: Bool, isHovered: Bool, diffType: RowDiffType, isPhantom: Bool) -> Color {
        if isPhantom {
            return Color(nsColor: .windowBackgroundColor).opacity(0.3)
        }
        if isSelected {
            return Color.accentColor.opacity(0.15)
        }
        if isHovered {
            return Color(nsColor: .controlBackgroundColor).opacity(0.5)
        }
        switch diffType {
        case .unchanged:
            return Color.clear
        case .modified:
            return Color.orange.opacity(0.08)
        case .added:
            return Color.green.opacity(0.08)
        case .deleted:
            return Color.red.opacity(0.08)
        }
    }

    private func cellTextColor(diffType: CellDiffType) -> Color {
        switch diffType {
        case .unchanged: return .primary
        case .modified: return .orange
        case .added: return .green
        case .deleted: return .red
        }
    }

    private func inlineDiffTextColor(isLeft: Bool) -> Color {
        isLeft ? .red : .green
    }

    private func inlineDiffBackground(isLeft: Bool) -> Color {
        isLeft ? Color.red.opacity(0.2) : Color.green.opacity(0.2)
    }
}
