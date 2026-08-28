import SwiftUI

public struct WordTableDiffView: View {
    @Bindable public var viewModel: WordDiffViewModel

    public init(viewModel: WordDiffViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        VStack(spacing: 0) {
            if viewModel.diffResult.tableDiffs.isEmpty {
                VStack(spacing: 12) {
                    Spacer()
                    Image(systemName: "tablecells.badge.ellipsis")
                        .font(.system(size: 36))
                        .foregroundColor(.secondary.opacity(0.5))
                    Text(LanguageManager.shared.text(.noTablesDetected))
                        .font(.headline)
                        .foregroundColor(.secondary)
                    Spacer()
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: 24) {
                        ForEach(viewModel.diffResult.tableDiffs) { tblDiff in
                            VStack(alignment: .leading, spacing: 8) {
                                // Table header
                                HStack {
                                    Image(systemName: "tablecells")
                                        .foregroundColor(.accentColor)
                                    Text("Table #\(tblDiff.tableIndex) (\(tblDiff.maxRows) rows × \(tblDiff.maxCols) cols)")
                                        .font(.system(size: 13, weight: .bold))

                                    Spacer()

                                    badgeForTableStatus(tblDiff.changeType)
                                }
                                .padding(.horizontal, 8)

                                // Table Grid
                                renderTableGrid(tblDiff: tblDiff)
                            }
                            .padding(12)
                            .background(
                                RoundedRectangle(cornerRadius: 8)
                                    .fill(Color(nsColor: .controlBackgroundColor))
                            )
                        }
                    }
                    .padding(16)
                }
            }
        }
    }

    private func renderTableGrid(tblDiff: WordTableDiffResult) -> some View {
        VStack(spacing: 1) {
            ForEach(0..<tblDiff.maxRows, id: \.self) { rowIdx in
                HStack(spacing: 1) {
                    ForEach(0..<tblDiff.maxCols, id: \.self) { colIdx in
                        let cellDiff = tblDiff.cellDiffs.first(where: { $0.rowIndex == rowIdx && $0.colIndex == colIdx })

                        VStack(alignment: .leading, spacing: 4) {
                            if let leftText = cellDiff?.leftCell?.text, !leftText.isEmpty {
                                Text("L: \(leftText)")
                                    .font(.system(size: 11))
                                    .foregroundColor(cellDiff?.changeType == .deleted ? .red : .secondary)
                                    .strikethrough(cellDiff?.changeType == .deleted)
                            }
                            if let rightText = cellDiff?.rightCell?.text, !rightText.isEmpty {
                                Text("R: \(rightText)")
                                    .font(.system(size: 11, weight: cellDiff?.changeType == .modified ? .bold : .regular))
                                    .foregroundColor(cellDiff?.changeType == .added ? .green : (cellDiff?.changeType == .modified ? .orange : .primary))
                            }
                            if (cellDiff?.leftCell == nil || cellDiff!.leftCell!.text.isEmpty) &&
                                (cellDiff?.rightCell == nil || cellDiff!.rightCell!.text.isEmpty) {
                                Text("—")
                                    .font(.system(size: 11))
                                    .foregroundColor(.secondary.opacity(0.3))
                            }
                        }
                        .padding(8)
                        .frame(maxWidth: .infinity, minHeight: 38, alignment: .topLeading)
                        .background(cellBackgroundColor(for: cellDiff?.changeType ?? .unchanged))
                    }
                }
            }
        }
        .background(Color(nsColor: .separatorColor))
        .cornerRadius(6)
        .overlay(
            RoundedRectangle(cornerRadius: 6)
                .stroke(Color(nsColor: .separatorColor), lineWidth: 1)
        )
    }

    private func cellBackgroundColor(for changeType: ChangeType) -> Color {
        switch changeType {
        case .added: return Color.green.opacity(0.12)
        case .deleted: return Color.red.opacity(0.12)
        case .modified: return Color.orange.opacity(0.12)
        case .unchanged: return Color(nsColor: .windowBackgroundColor)
        }
    }

    private func badgeForTableStatus(_ type: ChangeType) -> some View {
        HStack(spacing: 4) {
            Circle()
                .fill(type == .added ? Color.green : (type == .deleted ? Color.red : (type == .modified ? Color.orange : Color.gray)))
                .frame(width: 6, height: 6)
            Text(type.rawValue)
                .font(.caption2)
                .foregroundColor(.secondary)
        }
        .padding(.horizontal, 8)
        .padding(.vertical, 3)
        .background(Capsule().fill(Color.secondary.opacity(0.1)))
    }
}
