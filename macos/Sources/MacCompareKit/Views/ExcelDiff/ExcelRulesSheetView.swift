import SwiftUI

public struct ExcelRulesSheetView: View {
    @Bindable var viewModel: ExcelDiffViewModel
    @Environment(\.dismiss) private var dismiss
    @State private var tempRules: ExcelCompareRules
    @State private var languageManager = LanguageManager.shared

    public init(viewModel: ExcelDiffViewModel) {
        self.viewModel = viewModel
        self._tempRules = State(initialValue: viewModel.rules)
    }

    public var body: some View {
        VStack(spacing: 16) {
            // Header
            HStack {
                Text(languageManager.text(.excelRulesTitle))
                    .font(.headline)
                Spacer()
                Button(languageManager.text(.done)) {
                    viewModel.rules = tempRules
                    dismiss()
                    Task { await viewModel.recompare() }
                }
                .buttonStyle(.borderedProminent)
            }

            Divider()

            // Settings Form
            Form {
                Section(header: Text(languageManager.text(.general))) {
                    Toggle(languageManager.text(.excelFirstRowHeader), isOn: $tempRules.firstRowAsHeader)
                    Toggle(languageManager.text(.ignoreCase), isOn: $tempRules.ignoreCase)
                    Toggle(languageManager.text(.ignoreWhitespace), isOn: $tempRules.ignoreWhitespace)
                }

                Section(header: Text(languageManager.text(.excelNumericTolerance))) {
                    HStack {
                        Text(languageManager.text(.excelToleranceValue))
                        Spacer()
                        TextField("0.0", value: $tempRules.numericTolerance, format: .number)
                            .frame(width: 80)
                            .textFieldStyle(.roundedBorder)
                    }
                }

                if let currentSheet = viewModel.currentSheetDiff {
                    Section(header: Text(languageManager.text(.excelKeyColumnsHeader))) {
                        Text(languageManager.text(.excelKeyColumnsDesc))
                            .font(.caption)
                            .foregroundColor(.secondary)

                        ForEach(Array(currentSheet.columnHeaders.enumerated()), id: \.offset) { colIdx, headerName in
                            Toggle(isOn: Binding(
                                get: { tempRules.keyColumnIndices.contains(colIdx) },
                                set: { selected in
                                    if selected {
                                        if !tempRules.keyColumnIndices.contains(colIdx) {
                                            tempRules.keyColumnIndices.append(colIdx)
                                        }
                                    } else {
                                        tempRules.keyColumnIndices.removeAll(where: { $0 == colIdx })
                                    }
                                }
                            )) {
                                Text("\(ExcelModelsHelper.columnLetter(for: colIdx)): \(headerName)")
                            }
                        }
                    }
                }
            }
            .formStyle(.grouped)
        }
        .padding(20)
        .frame(minWidth: 420, minHeight: 400)
    }
}
