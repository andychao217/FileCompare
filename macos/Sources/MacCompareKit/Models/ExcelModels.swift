import Foundation
import SwiftUI

// MARK: - Core Excel Data Models

public enum ExcelCellType: String, Sendable, Codable {
    case string
    case number
    case boolean
    case date
    case formula
    case blank
    case error
}

public struct ExcelCellData: Sendable, Identifiable, Equatable {
    public var id: String { "\(columnIndex)_\(rawValue)" }
    public let columnIndex: Int // 0-based column index (0 = A, 1 = B...)
    public let columnLetter: String // "A", "B", "AA"
    public let rawValue: String
    public let formattedValue: String
    public let formula: String?
    public let cellType: ExcelCellType

    public init(
        columnIndex: Int,
        columnLetter: String? = nil,
        rawValue: String,
        formattedValue: String? = nil,
        formula: String? = nil,
        cellType: ExcelCellType = .string
    ) {
        self.columnIndex = columnIndex
        self.columnLetter = columnLetter ?? ExcelModelsHelper.columnLetter(for: columnIndex)
        self.rawValue = rawValue
        self.formattedValue = formattedValue ?? rawValue
        self.formula = formula
        self.cellType = cellType
    }
}

public struct ExcelRowData: Sendable, Identifiable, Equatable {
    public let id: Int // 1-based Row Index
    public let rowIndex: Int
    public let cells: [ExcelCellData]

    public init(rowIndex: Int, cells: [ExcelCellData]) {
        self.id = rowIndex
        self.rowIndex = rowIndex
        self.cells = cells
    }

    public func cell(at columnIndex: Int) -> ExcelCellData? {
        cells.first(where: { $0.columnIndex == columnIndex })
    }
}

public struct ExcelSheetModel: Sendable, Identifiable, Equatable {
    public let id: String
    public let name: String
    public let maxColumns: Int
    public let maxRows: Int
    public let rows: [ExcelRowData]

    public init(id: String, name: String, maxColumns: Int, maxRows: Int, rows: [ExcelRowData]) {
        self.id = id
        self.name = name
        self.maxColumns = maxColumns
        self.maxRows = maxRows
        self.rows = rows
    }
}

public struct ExcelMetadata: Sendable, Equatable {
    public var title: String?
    public var author: String?
    public var lastModifiedBy: String?
    public var createdAt: Date?
    public var modifiedAt: Date?
    public var sheetCount: Int
    public var fileSizeFormatted: String
    public var fileFormat: String

    public init(
        title: String? = nil,
        author: String? = nil,
        lastModifiedBy: String? = nil,
        createdAt: Date? = nil,
        modifiedAt: Date? = nil,
        sheetCount: Int = 0,
        fileSizeFormatted: String = "",
        fileFormat: String = "Excel Workbook (.xlsx)"
    ) {
        self.title = title
        self.author = author
        self.lastModifiedBy = lastModifiedBy
        self.createdAt = createdAt
        self.modifiedAt = modifiedAt
        self.sheetCount = sheetCount
        self.fileSizeFormatted = fileSizeFormatted
        self.fileFormat = fileFormat
    }
}

public struct ExcelWorkbookModel: Sendable, Equatable {
    public let fileURL: URL
    public let fileName: String
    public let metadata: ExcelMetadata
    public let sheets: [ExcelSheetModel]

    public init(fileURL: URL, fileName: String, metadata: ExcelMetadata, sheets: [ExcelSheetModel]) {
        self.fileURL = fileURL
        self.fileName = fileName
        self.metadata = metadata
        self.sheets = sheets
    }
}

// MARK: - Diff & Comparison Models

public enum RowDiffType: String, Sendable, Codable {
    case unchanged
    case modified
    case added      // Only on right (left has phantom row)
    case deleted    // Only on left (right has phantom row)
}

public enum CellDiffType: String, Sendable, Codable {
    case unchanged
    case modified
    case added
    case deleted
}

public struct CellDiffInlineChunk: Sendable, Identifiable, Equatable {
    public let id = UUID()
    public let text: String
    public let isDiff: Bool

    public init(text: String, isDiff: Bool) {
        self.text = text
        self.isDiff = isDiff
    }
}

public struct CellDiffItem: Sendable, Identifiable, Equatable {
    public var id: String { "\(columnIndex)" }
    public let columnIndex: Int
    public let columnLetter: String
    public let headerName: String?
    public let leftCell: ExcelCellData?
    public let rightCell: ExcelCellData?
    public let diffType: CellDiffType
    public let leftInlineChunks: [CellDiffInlineChunk]
    public let rightInlineChunks: [CellDiffInlineChunk]

    public init(
        columnIndex: Int,
        columnLetter: String? = nil,
        headerName: String? = nil,
        leftCell: ExcelCellData?,
        rightCell: ExcelCellData?,
        diffType: CellDiffType,
        leftInlineChunks: [CellDiffInlineChunk] = [],
        rightInlineChunks: [CellDiffInlineChunk] = []
    ) {
        self.columnIndex = columnIndex
        self.columnLetter = columnLetter ?? ExcelModelsHelper.columnLetter(for: columnIndex)
        self.headerName = headerName
        self.leftCell = leftCell
        self.rightCell = rightCell
        self.diffType = diffType
        self.leftInlineChunks = leftInlineChunks
        self.rightInlineChunks = rightInlineChunks
    }
}

public struct AlignedExcelRow: Sendable, Identifiable, Equatable {
    public let id: UUID
    public let leftRowIndex: Int?   // 1-based, nil if phantom row on left
    public let rightRowIndex: Int?  // 1-based, nil if phantom row on right
    public let leftCells: [ExcelCellData]?
    public let rightCells: [ExcelCellData]?
    public let rowDiffType: RowDiffType
    public let cellDiffs: [CellDiffItem]

    public init(
        id: UUID = UUID(),
        leftRowIndex: Int?,
        rightRowIndex: Int?,
        leftCells: [ExcelCellData]?,
        rightCells: [ExcelCellData]?,
        rowDiffType: RowDiffType,
        cellDiffs: [CellDiffItem]
    ) {
        self.id = id
        self.leftRowIndex = leftRowIndex
        self.rightRowIndex = rightRowIndex
        self.leftCells = leftCells
        self.rightCells = rightCells
        self.rowDiffType = rowDiffType
        self.cellDiffs = cellDiffs
    }

    public var displayRowNumberLeft: String {
        if let idx = leftRowIndex { return "\(idx)" }
        return " "
    }

    public var displayRowNumberRight: String {
        if let idx = rightRowIndex { return "\(idx)" }
        return " "
    }
}

public enum SheetDiffStatus: String, Sendable, Codable {
    case same
    case modified
    case leftOnly
    case rightOnly
}

public struct ExcelSheetDiffResult: Sendable, Identifiable, Equatable {
    public let id: String
    public let sheetName: String
    public let leftSheetName: String?
    public let rightSheetName: String?
    public let status: SheetDiffStatus
    public let columnHeaders: [String]
    public let alignedRows: [AlignedExcelRow]
    public let totalRows: Int
    public let differenceRowCount: Int
    public let sameRowCount: Int
    public let addedRowCount: Int
    public let deletedRowCount: Int

    public init(
        id: String,
        sheetName: String,
        leftSheetName: String?,
        rightSheetName: String?,
        status: SheetDiffStatus,
        columnHeaders: [String],
        alignedRows: [AlignedExcelRow],
        totalRows: Int,
        differenceRowCount: Int,
        sameRowCount: Int,
        addedRowCount: Int,
        deletedRowCount: Int
    ) {
        self.id = id
        self.sheetName = sheetName
        self.leftSheetName = leftSheetName
        self.rightSheetName = rightSheetName
        self.status = status
        self.columnHeaders = columnHeaders
        self.alignedRows = alignedRows
        self.totalRows = totalRows
        self.differenceRowCount = differenceRowCount
        self.sameRowCount = sameRowCount
        self.addedRowCount = addedRowCount
        self.deletedRowCount = deletedRowCount
    }
}

public struct ExcelWorkbookDiffResult: Sendable, Equatable {
    public let sheetDiffs: [ExcelSheetDiffResult]
    public let totalDifferences: Int
    public let loadTimeSeconds: Double

    public init(sheetDiffs: [ExcelSheetDiffResult], totalDifferences: Int, loadTimeSeconds: Double) {
        self.sheetDiffs = sheetDiffs
        self.totalDifferences = totalDifferences
        self.loadTimeSeconds = loadTimeSeconds
    }
}

// MARK: - Filter & Rules Models

public enum ExcelDiffFilter: String, CaseIterable, Identifiable, Sendable {
    case all = "All"
    case diffs = "Diffs"
    case same = "Same"

    public var id: String { rawValue }
}

public struct ExcelCompareRules: Sendable, Equatable {
    public var firstRowAsHeader: Bool
    public var ignoreCase: Bool
    public var ignoreWhitespace: Bool
    public var numericTolerance: Double // e.g. 0.0 for exact, 0.0001 for float
    public var keyColumnIndices: [Int] // User selected primary key columns (0-based)

    public init(
        firstRowAsHeader: Bool = true,
        ignoreCase: Bool = false,
        ignoreWhitespace: Bool = false,
        numericTolerance: Double = 0.0,
        keyColumnIndices: [Int] = []
    ) {
        self.firstRowAsHeader = firstRowAsHeader
        self.ignoreCase = ignoreCase
        self.ignoreWhitespace = ignoreWhitespace
        self.numericTolerance = numericTolerance
        self.keyColumnIndices = keyColumnIndices
    }
}

// MARK: - Helpers

public enum ExcelModelsHelper {
    public static func columnLetter(for columnIndex: Int) -> String {
        var index = columnIndex + 1
        var result = ""
        while index > 0 {
            let remainder = (index - 1) % 26
            if let scalar = UnicodeScalar(65 + remainder) {
                result = String(scalar) + result
            }
            index = (index - 1) / 26
        }
        return result.isEmpty ? "A" : result
    }

    public static func columnIndex(from letter: String) -> Int {
        var sum = 0
        let upper = letter.uppercased()
        for char in upper.unicodeScalars {
            let val = Int(char.value) - 64
            if val >= 1 && val <= 26 {
                sum = sum * 26 + val
            }
        }
        return max(0, sum - 1)
    }
}
