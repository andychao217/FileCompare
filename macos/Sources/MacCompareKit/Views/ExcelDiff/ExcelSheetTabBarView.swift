import SwiftUI
import AppKit

public struct ExcelSheetTabBarView: View {
    @Bindable var viewModel: ExcelDiffViewModel
    @State private var languageManager = LanguageManager.shared

    public init(viewModel: ExcelDiffViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        guard let diffResult = viewModel.diffResult else {
            return AnyView(EmptyView())
        }

        let currentSheet = viewModel.currentSheetDiff

        return AnyView(
            VStack(spacing: 0) {
                Divider()

                HStack(spacing: 0) {
                    // Left Sheet Navigation Tabs
                    ScrollView(.horizontal, showsIndicators: false) {
                        HStack(spacing: 4) {
                            ForEach(diffResult.sheetDiffs) { sheetDiff in
                                sheetTabButton(sheetDiff: sheetDiff)
                            }
                        }
                        .padding(.horizontal, 8)
                        .padding(.vertical, 4)
                    }

                    Spacer()

                    Divider().frame(height: 20)

                    // Status Statistics Summary
                    HStack(spacing: 12) {
                        if let sheet = currentSheet {
                            HStack(spacing: 4) {
                                Image(systemName: "exclamationmark.triangle.fill")
                                    .foregroundColor(.orange)
                                    .font(.system(size: 10))
                                Text("\(sheet.differenceRowCount) \(languageManager.text(.excelDifferenceRows))")
                                    .font(.system(size: 11, weight: .medium))
                            }

                            Text("•")
                                .foregroundColor(.secondary)

                            Text("\(sheet.sameRowCount) \(languageManager.text(.excelSameRows))")
                                .font(.system(size: 11))
                                .foregroundColor(.secondary)

                            Text("•")
                                .foregroundColor(.secondary)
                        }

                        Text("\(languageManager.text(.excelLoadTime)): \(String(format: "%.2f", diffResult.loadTimeSeconds))s")
                            .font(.system(size: 10, design: .monospaced))
                            .foregroundColor(.secondary)
                    }
                    .padding(.horizontal, 14)
                }
                .frame(height: 32)
                .background(Color(nsColor: .windowBackgroundColor))
            }
        )
    }

    @ViewBuilder
    private func sheetTabButton(sheetDiff: ExcelSheetDiffResult) -> some View {
        let isSelected = (viewModel.selectedSheetId == sheetDiff.id)

        Button {
            viewModel.selectSheet(id: sheetDiff.id)
        } label: {
            HStack(spacing: 6) {
                // Status Indicator Dot
                Circle()
                    .fill(sheetStatusColor(sheetDiff.status, diffCount: sheetDiff.differenceRowCount))
                    .frame(width: 6, height: 6)

                // Sheet Name
                Text(sheetDisplayName(sheetDiff))
                    .font(.system(size: 11, weight: isSelected ? .bold : .medium))
                    .foregroundColor(isSelected ? .primary : .secondary)

                // Diffs count badge if any
                if sheetDiff.differenceRowCount > 0 {
                    Text("(\(sheetDiff.differenceRowCount))")
                        .font(.system(size: 10, weight: .semibold))
                        .foregroundColor(isSelected ? .orange : .secondary)
                }
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 4)
            .background(
                RoundedRectangle(cornerRadius: 5)
                    .fill(isSelected ? Color(nsColor: .controlBackgroundColor) : Color.clear)
            )
        }
        .buttonStyle(.plain)
    }

    private func sheetDisplayName(_ sheetDiff: ExcelSheetDiffResult) -> String {
        switch sheetDiff.status {
        case .same, .modified:
            return sheetDiff.sheetName
        case .leftOnly:
            return "\(sheetDiff.sheetName) (Left)"
        case .rightOnly:
            return "- / \(sheetDiff.sheetName)"
        }
    }

    private func sheetStatusColor(_ status: SheetDiffStatus, diffCount: Int) -> Color {
        switch status {
        case .same:
            return .gray.opacity(0.6)
        case .modified:
            return diffCount > 0 ? .orange : .gray.opacity(0.6)
        case .leftOnly:
            return .red
        case .rightOnly:
            return .green
        }
    }
}
