import Foundation
import SwiftUI
import AppKit
import UniformTypeIdentifiers

@MainActor
@Observable
public final class ExcelDiffViewModel: Identifiable {
    public let id = UUID()

    // URLs & Loaded Workbooks
    public var leftURL: URL?
    public var rightURL: URL?
    public var leftWorkbook: ExcelWorkbookModel?
    public var rightWorkbook: ExcelWorkbookModel?

    // Diff State
    public var diffResult: ExcelWorkbookDiffResult?
    public var selectedSheetId: String?
    public var selectedRowId: UUID?
    public var filterMode: ExcelDiffFilter = .all
    public var rules: ExcelCompareRules = ExcelCompareRules()

    // Loading & UI State
    public var isLoading: Bool = false
    public var errorMessage: String?
    public var isRulesSheetPresented: Bool = false
    public var searchQuery: String = ""

    public init(leftURL: URL? = nil, rightURL: URL? = nil) {
        self.leftURL = leftURL
        self.rightURL = rightURL

        if leftURL != nil || rightURL != nil {
            Task {
                await loadAndCompare()
            }
        }
    }

    // MARK: - File State

    public var hasLeftFile: Bool {
        leftURL != nil && leftWorkbook != nil
    }

    public var hasRightFile: Bool {
        rightURL != nil && rightWorkbook != nil
    }

    public var hasBothFiles: Bool {
        hasLeftFile && hasRightFile
    }

    public var leftTitle: String {
        leftURL?.lastPathComponent ?? LanguageManager.shared.text(.sourceFile)
    }

    public var rightTitle: String {
        rightURL?.lastPathComponent ?? LanguageManager.shared.text(.targetFile)
    }

    // MARK: - Computed Properties

    public var currentSheetDiff: ExcelSheetDiffResult? {
        guard let diffResult = diffResult, !diffResult.sheetDiffs.isEmpty else { return nil }
        if let selectedId = selectedSheetId {
            return diffResult.sheetDiffs.first(where: { $0.id == selectedId }) ?? diffResult.sheetDiffs.first
        }
        return diffResult.sheetDiffs.first
    }

    public var filteredRows: [AlignedExcelRow] {
        guard let current = currentSheetDiff else { return [] }
        var rows = current.alignedRows

        switch filterMode {
        case .all:
            break
        case .diffs:
            rows = rows.filter { $0.rowDiffType != .unchanged }
        case .same:
            rows = rows.filter { $0.rowDiffType == .unchanged }
        }

        if !searchQuery.trimmingCharacters(in: .whitespaces).isEmpty {
            let q = searchQuery.lowercased()
            rows = rows.filter { row in
                let leftMatch = row.leftCells?.contains(where: { $0.rawValue.lowercased().contains(q) }) ?? false
                let rightMatch = row.rightCells?.contains(where: { $0.rawValue.lowercased().contains(q) }) ?? false
                return leftMatch || rightMatch
            }
        }

        return rows
    }

    public var selectedRow: AlignedExcelRow? {
        guard let selectedId = selectedRowId else {
            return filteredRows.first(where: { $0.rowDiffType != .unchanged }) ?? filteredRows.first
        }
        return filteredRows.first(where: { $0.id == selectedId })
    }

    // MARK: - Actions

    public func loadAndCompare() async {
        if leftURL == nil && rightURL == nil {
            return
        }

        isLoading = true
        errorMessage = nil

        do {
            if let leftURL = leftURL {
                self.leftWorkbook = try await ExcelDocumentParser.shared.parseWorkbook(from: leftURL)
            } else {
                self.leftWorkbook = nil
            }

            if let rightURL = rightURL {
                self.rightWorkbook = try await ExcelDocumentParser.shared.parseWorkbook(from: rightURL)
            } else {
                self.rightWorkbook = nil
            }

            if let leftWb = leftWorkbook, let rightWb = rightWorkbook, let leftURL = leftURL, let rightURL = rightURL {
                let result = await ExcelDiffEngine.shared.compareWorkbooks(left: leftWb, right: rightWb, rules: rules)
                self.diffResult = result

                RecentHistoryManager.shared.addRecord(left: leftURL, right: rightURL, type: .excelDiff)

                if selectedSheetId == nil || !result.sheetDiffs.contains(where: { $0.id == selectedSheetId }) {
                    self.selectedSheetId = result.sheetDiffs.first?.id
                }

                if let firstDiff = filteredRows.first(where: { $0.rowDiffType != .unchanged }) {
                    self.selectedRowId = firstDiff.id
                } else {
                    self.selectedRowId = filteredRows.first?.id
                }
            } else {
                self.diffResult = nil
            }

            isLoading = false
        } catch {
            self.errorMessage = error.localizedDescription
            self.isLoading = false
        }
    }

    public func loadSingleFile(from url: URL, isLeft: Bool) {
        if isLeft {
            self.leftURL = url
        } else {
            self.rightURL = url
        }
        Task {
            await loadAndCompare()
        }
    }

    public func openLeftFile() {
        showOpenPanel { [weak self] url in
            self?.loadSingleFile(from: url, isLeft: true)
        }
    }

    public func openRightFile() {
        showOpenPanel { [weak self] url in
            self?.loadSingleFile(from: url, isLeft: false)
        }
    }

    private func showOpenPanel(completion: @escaping (URL) -> Void) {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [
            UTType(filenameExtension: "xlsx") ?? .data,
            UTType(filenameExtension: "xls") ?? .data,
            UTType.commaSeparatedText,
            UTType.tabSeparatedText
        ]
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        if panel.runModal() == .OK, let url = panel.url {
            completion(url)
        }
    }

    public func clearAll() {
        self.leftURL = nil
        self.rightURL = nil
        self.leftWorkbook = nil
        self.rightWorkbook = nil
        self.diffResult = nil
        self.selectedSheetId = nil
        self.selectedRowId = nil
        self.searchQuery = ""
    }

    public func takeLeft() {
        // Take left differences to right: swap or reload
        guard let leftURL = leftURL, let rightURL = rightURL else { return }
        try? FileManager.default.removeItem(at: rightURL)
        try? FileManager.default.copyItem(at: leftURL, to: rightURL)
        Task { await loadAndCompare() }
    }

    public func takeRight() {
        // Take right differences to left
        guard let leftURL = leftURL, let rightURL = rightURL else { return }
        try? FileManager.default.removeItem(at: leftURL)
        try? FileManager.default.copyItem(at: rightURL, to: leftURL)
        Task { await loadAndCompare() }
    }

    public func recompare() async {
        guard let leftWb = leftWorkbook, let rightWb = rightWorkbook else {
            await loadAndCompare()
            return
        }

        isLoading = true
        let result = await ExcelDiffEngine.shared.compareWorkbooks(left: leftWb, right: rightWb, rules: rules)
        self.diffResult = result
        if selectedSheetId == nil || !result.sheetDiffs.contains(where: { $0.id == selectedSheetId }) {
            self.selectedSheetId = result.sheetDiffs.first?.id
        }
        isLoading = false
    }

    public func selectSheet(id: String) {
        self.selectedSheetId = id
        if let firstDiff = filteredRows.first(where: { $0.rowDiffType != .unchanged }) {
            self.selectedRowId = firstDiff.id
        } else {
            self.selectedRowId = filteredRows.first?.id
        }
    }

    public func selectRow(id: UUID) {
        self.selectedRowId = id
    }

    public func nextDiff() {
        let diffRows = filteredRows.filter { $0.rowDiffType != .unchanged }
        guard !diffRows.isEmpty else { return }

        if let currentId = selectedRowId,
           let currentIdx = diffRows.firstIndex(where: { $0.id == currentId }),
           currentIdx + 1 < diffRows.count {
            selectedRowId = diffRows[currentIdx + 1].id
        } else {
            selectedRowId = diffRows.first?.id
        }
    }

    public func prevDiff() {
        let diffRows = filteredRows.filter { $0.rowDiffType != .unchanged }
        guard !diffRows.isEmpty else { return }

        if let currentId = selectedRowId,
           let currentIdx = diffRows.firstIndex(where: { $0.id == currentId }),
           currentIdx - 1 >= 0 {
            selectedRowId = diffRows[currentIdx - 1].id
        } else {
            selectedRowId = diffRows.last?.id
        }
    }

    public func swapFiles() {
        let tempURL = leftURL
        leftURL = rightURL
        rightURL = tempURL

        let tempWb = leftWorkbook
        leftWorkbook = rightWorkbook
        rightWorkbook = tempWb

        Task {
            await recompare()
        }
    }

    public func setLeftURL(_ url: URL) {
        self.leftURL = url
        Task { await loadAndCompare() }
    }

    public func setRightURL(_ url: URL) {
        self.rightURL = url
        Task { await loadAndCompare() }
    }
}
