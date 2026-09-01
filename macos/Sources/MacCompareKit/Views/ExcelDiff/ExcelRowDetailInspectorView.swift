import SwiftUI
import AppKit

public struct ExcelRowDetailInspectorView: View {
    @Bindable var viewModel: ExcelDiffViewModel
    @State private var languageManager = LanguageManager.shared

    public init(viewModel: ExcelDiffViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        guard let row = viewModel.selectedRow else {
            return AnyView(EmptyView())
        }

        let leftIdxStr = row.leftRowIndex.map { "Row \($0)" } ?? "None"
        let rightIdxStr = row.rightRowIndex.map { "Row \($0)" } ?? "None"

        return AnyView(
            VStack(spacing: 0) {
                Divider()

                // Inspector Title Bar
                HStack(spacing: 8) {
                    Image(systemName: "rectangle.and.text.magnifyingglass")
                        .font(.system(size: 11))
                        .foregroundColor(.accentColor)
                    Text(languageManager.text(.excelRowInspectorTitle))
                        .font(.system(size: 11, weight: .bold))
                    Text("(Left: \(leftIdxStr) ↔ Right: \(rightIdxStr))")
                        .font(.system(size: 10, design: .monospaced))
                        .foregroundColor(.secondary)

                    Spacer()

                    // Diff Type Badge
                    diffBadge(for: row.rowDiffType)
                }
                .padding(.horizontal, 14)
                .padding(.vertical, 5)
                .background(Color(nsColor: .windowBackgroundColor))

                Divider()

                // Horizontal Columns Comparison Cards
                ScrollView(.horizontal, showsIndicators: true) {
                    HStack(spacing: 12) {
                        ForEach(row.cellDiffs) { cellDiff in
                            columnComparisonCard(cellDiff: cellDiff)
                        }
                    }
                    .padding(.horizontal, 14)
                    .padding(.vertical, 8)
                }
                .background(Color(nsColor: .controlBackgroundColor).opacity(0.5))
            }
            .frame(maxHeight: 130)
        )
    }

    @ViewBuilder
    private func columnComparisonCard(cellDiff: CellDiffItem) -> some View {
        let isDiff = cellDiff.diffType != .unchanged

        VStack(alignment: .leading, spacing: 4) {
            // Column Header
            HStack(spacing: 4) {
                Text(cellDiff.columnLetter)
                    .font(.system(size: 10, weight: .bold, design: .monospaced))
                    .foregroundColor(.secondary)
                Text(cellDiff.headerName ?? "")
                    .font(.system(size: 10, weight: .semibold))
                    .lineLimit(1)
                Spacer()
                if isDiff {
                    Circle()
                        .fill(Color.orange)
                        .frame(width: 5, height: 5)
                }
            }

            Divider()

            // Left Value
            HStack(alignment: .top, spacing: 4) {
                Text("L:")
                    .font(.system(size: 9, weight: .bold, design: .monospaced))
                    .foregroundColor(.secondary)
                Text(cellDiff.leftCell?.rawValue ?? "<Empty>")
                    .font(.system(size: 10, design: .monospaced))
                    .foregroundColor(cellDiff.leftCell == nil ? .secondary : (isDiff ? .red : .primary))
                    .lineLimit(2)
            }

            // Right Value
            HStack(alignment: .top, spacing: 4) {
                Text("R:")
                    .font(.system(size: 9, weight: .bold, design: .monospaced))
                    .foregroundColor(.secondary)
                Text(cellDiff.rightCell?.rawValue ?? "<Empty>")
                    .font(.system(size: 10, design: .monospaced))
                    .foregroundColor(cellDiff.rightCell == nil ? .secondary : (isDiff ? .green : .primary))
                    .lineLimit(2)
            }
        }
        .padding(8)
        .frame(width: 160, alignment: .leading)
        .background(
            RoundedRectangle(cornerRadius: 6)
                .fill(Color(nsColor: .textBackgroundColor))
                .overlay(
                    RoundedRectangle(cornerRadius: 6)
                        .stroke(isDiff ? Color.orange.opacity(0.5) : Color(nsColor: .separatorColor), lineWidth: 1)
                )
        )
        .contextMenu {
            Button(languageManager.text(.copyLeft)) {
                if let left = cellDiff.leftCell?.rawValue {
                    NSPasteboard.general.clearContents()
                    NSPasteboard.general.setString(left, forType: .string)
                }
            }
            Button(languageManager.text(.copyRight)) {
                if let right = cellDiff.rightCell?.rawValue {
                    NSPasteboard.general.clearContents()
                    NSPasteboard.general.setString(right, forType: .string)
                }
            }
        }
    }

    @ViewBuilder
    private func diffBadge(for type: RowDiffType) -> some View {
        switch type {
        case .unchanged:
            Text(languageManager.text(.filterSame))
                .font(.system(size: 9, weight: .medium))
                .padding(.horizontal, 6)
                .padding(.vertical, 2)
                .background(Color.secondary.opacity(0.15))
                .cornerRadius(4)
        case .modified:
            Text(languageManager.text(.statusModified))
                .font(.system(size: 9, weight: .bold))
                .foregroundColor(.orange)
                .padding(.horizontal, 6)
                .padding(.vertical, 2)
                .background(Color.orange.opacity(0.15))
                .cornerRadius(4)
        case .added:
            Text(languageManager.text(.statusAdded))
                .font(.system(size: 9, weight: .bold))
                .foregroundColor(.green)
                .padding(.horizontal, 6)
                .padding(.vertical, 2)
                .background(Color.green.opacity(0.15))
                .cornerRadius(4)
        case .deleted:
            Text(languageManager.text(.statusDeleted))
                .font(.system(size: 9, weight: .bold))
                .foregroundColor(.red)
                .padding(.horizontal, 6)
                .padding(.vertical, 2)
                .background(Color.red.opacity(0.15))
                .cornerRadius(4)
        }
    }
}
