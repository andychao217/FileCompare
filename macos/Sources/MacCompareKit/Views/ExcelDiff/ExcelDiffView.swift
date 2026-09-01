import SwiftUI
import AppKit
import UniformTypeIdentifiers

public struct ExcelDiffView: View {
    @Bindable var viewModel: ExcelDiffViewModel
    @State private var languageManager = LanguageManager.shared
    @State private var isLeftDropTargeted: Bool = false
    @State private var isRightDropTargeted: Bool = false

    public init(viewModel: ExcelDiffViewModel) {
        self.viewModel = viewModel
    }

    private var isBothEmpty: Bool {
        !viewModel.hasLeftFile && !viewModel.hasRightFile
    }

    public var body: some View {
        VStack(spacing: 0) {
            // Top Toolbar & File Headers
            ExcelDiffToolbarView(viewModel: viewModel)

            // Content Area
            if viewModel.isLoading {
                VStack(spacing: 12) {
                    ProgressView()
                        .scaleEffect(1.2)
                    Text(languageManager.text(.excelLoadingPrompt))
                        .font(.headline)
                        .foregroundColor(.secondary)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(Color(nsColor: .windowBackgroundColor))
            } else if let error = viewModel.errorMessage {
                VStack(spacing: 12) {
                    Image(systemName: "exclamationmark.triangle.fill")
                        .font(.system(size: 36))
                        .foregroundColor(.red)
                    Text(error)
                        .font(.headline)
                        .foregroundColor(.secondary)
                        .multilineTextAlignment(.center)
                        .padding(.horizontal, 24)
                    Button(languageManager.text(.retry)) {
                        Task { await viewModel.loadAndCompare() }
                    }
                    .buttonStyle(.borderedProminent)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(Color(nsColor: .windowBackgroundColor))
            } else if isBothEmpty {
                // Dual Empty Drop Zones (Side-by-side)
                HStack(spacing: 0) {
                    emptyDropZone(isLeft: true)
                        .background(isLeftDropTargeted ? Color.accentColor.opacity(0.1) : Color.clear)
                        .onDrop(of: [.fileURL], isTargeted: $isLeftDropTargeted) { providers in
                            handleDrop(providers: providers, isLeft: true)
                        }

                    Divider()

                    emptyDropZone(isLeft: false)
                        .background(isRightDropTargeted ? Color.accentColor.opacity(0.1) : Color.clear)
                        .onDrop(of: [.fileURL], isTargeted: $isRightDropTargeted) { providers in
                            handleDrop(providers: providers, isLeft: false)
                        }
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(Color(nsColor: .textBackgroundColor))
            } else if !viewModel.hasBothFiles {
                // Single File Mode: One side shows data grid, other side shows drop zone
                HStack(spacing: 0) {
                    if viewModel.hasLeftFile, let leftWb = viewModel.leftWorkbook {
                        SingleExcelTableView(workbook: leftWb)
                            .frame(maxWidth: .infinity, maxHeight: .infinity)
                    } else {
                        emptyDropZone(isLeft: true)
                            .background(isLeftDropTargeted ? Color.accentColor.opacity(0.1) : Color.clear)
                            .onDrop(of: [.fileURL], isTargeted: $isLeftDropTargeted) { providers in
                                handleDrop(providers: providers, isLeft: true)
                            }
                    }

                    Divider()

                    if viewModel.hasRightFile, let rightWb = viewModel.rightWorkbook {
                        SingleExcelTableView(workbook: rightWb)
                            .frame(maxWidth: .infinity, maxHeight: .infinity)
                    } else {
                        emptyDropZone(isLeft: false)
                            .background(isRightDropTargeted ? Color.accentColor.opacity(0.1) : Color.clear)
                            .onDrop(of: [.fileURL], isTargeted: $isRightDropTargeted) { providers in
                                handleDrop(providers: providers, isLeft: false)
                            }
                    }
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(Color(nsColor: .textBackgroundColor))
            } else {
                // Full Dual Diff Mode
                VStack(spacing: 0) {
                    // Side-by-side Table Grid
                    ExcelTableGridView(viewModel: viewModel)

                    // Bottom Row Detail Inspector
                    ExcelRowDetailInspectorView(viewModel: viewModel)

                    // Bottom Sheet Tabs & Status Bar
                    ExcelSheetTabBarView(viewModel: viewModel)
                }
            }
        }
        .sheet(isPresented: $viewModel.isRulesSheetPresented) {
            ExcelRulesSheetView(viewModel: viewModel)
        }
    }

    @ViewBuilder
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
}

// MARK: - Single Excel Table Preview

public struct SingleExcelTableView: View {
    public let workbook: ExcelWorkbookModel
    @State private var selectedSheetIndex: Int = 0

    public init(workbook: ExcelWorkbookModel) {
        self.workbook = workbook
    }

    private var currentSheet: ExcelSheetModel? {
        guard !workbook.sheets.isEmpty, selectedSheetIndex < workbook.sheets.count else { return nil }
        return workbook.sheets[selectedSheetIndex]
    }

    public var body: some View {
        VStack(spacing: 0) {
            if let sheet = currentSheet {
                // Table Headers
                HStack(spacing: 0) {
                    Text("#")
                        .font(.system(size: 11, weight: .bold, design: .monospaced))
                        .foregroundColor(.secondary)
                        .frame(width: 44, alignment: .center)
                        .padding(.vertical, 6)
                        .background(Color(nsColor: .controlBackgroundColor))

                    Divider()

                    ScrollView(.horizontal, showsIndicators: false) {
                        HStack(spacing: 0) {
                            ForEach(0..<sheet.maxColumns, id: \.self) { colIdx in
                                Text(ExcelModelsHelper.columnLetter(for: colIdx))
                                    .font(.system(size: 11, weight: .bold, design: .monospaced))
                                    .foregroundColor(.secondary)
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

                // Rows
                ScrollView([.vertical, .horizontal]) {
                    LazyVStack(spacing: 0) {
                        ForEach(sheet.rows) { row in
                            HStack(spacing: 0) {
                                Text("\(row.rowIndex)")
                                    .font(.system(size: 11, weight: .medium, design: .monospaced))
                                    .foregroundColor(.secondary)
                                    .frame(width: 44, alignment: .center)
                                    .padding(.vertical, 5)
                                    .background(Color(nsColor: .controlBackgroundColor).opacity(0.4))

                                Divider()

                                HStack(spacing: 0) {
                                    ForEach(0..<sheet.maxColumns, id: \.self) { colIdx in
                                        let cell = row.cell(at: colIdx)
                                        Text(cell?.rawValue ?? "")
                                            .font(.system(size: 11, design: .monospaced))
                                            .foregroundColor(.primary)
                                            .lineLimit(1)
                                            .frame(width: 130, alignment: .leading)
                                            .padding(.vertical, 5)
                                            .padding(.horizontal, 8)
                                        Divider()
                                    }
                                }
                            }
                            .frame(minHeight: 24)
                            .overlay(
                                Rectangle()
                                    .frame(height: 1)
                                    .foregroundColor(Color(nsColor: .separatorColor).opacity(0.4)),
                                alignment: .bottom
                            )
                        }
                    }
                }

                // Bottom Single Sheet Bar
                if workbook.sheets.count > 1 {
                    Divider()
                    ScrollView(.horizontal, showsIndicators: false) {
                        HStack(spacing: 4) {
                            ForEach(Array(workbook.sheets.enumerated()), id: \.offset) { idx, s in
                                Button {
                                    selectedSheetIndex = idx
                                } label: {
                                    Text(s.name)
                                        .font(.system(size: 11, weight: selectedSheetIndex == idx ? .bold : .regular))
                                        .foregroundColor(selectedSheetIndex == idx ? .primary : .secondary)
                                        .padding(.horizontal, 10)
                                        .padding(.vertical, 4)
                                        .background(
                                            RoundedRectangle(cornerRadius: 5)
                                                .fill(selectedSheetIndex == idx ? Color(nsColor: .controlBackgroundColor) : Color.clear)
                                        )
                                }
                                .buttonStyle(.plain)
                            }
                        }
                        .padding(.horizontal, 8)
                        .padding(.vertical, 4)
                    }
                    .frame(height: 28)
                    .background(Color(nsColor: .windowBackgroundColor))
                }
            } else {
                VStack(spacing: 8) {
                    Image(systemName: "tablecells")
                        .font(.system(size: 28))
                        .foregroundColor(.secondary)
                    Text("No Sheets")
                        .font(.subheadline)
                        .foregroundColor(.secondary)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            }
        }
        .background(Color(nsColor: .textBackgroundColor))
    }
}
