import Foundation
import SwiftUI
import AppKit

// MARK: - Word View Modes

public enum WordViewMode: String, CaseIterable, Identifiable, Sendable {
    case structuredContent = "Structured Content"
    case formattingDiff = "Formatting Diff"
    case tableDiff = "Table Diff"
    case metadataDiff = "Metadata Diff"

    public var id: String { rawValue }

    public var iconName: String {
        switch self {
        case .structuredContent: return "doc.richtext"
        case .formattingDiff: return "textformat"
        case .tableDiff: return "tablecells"
        case .metadataDiff: return "info.circle"
        }
    }
}

// MARK: - Word Document Structure Models

public struct WordParagraphStyle: Sendable, Codable, Equatable {
    public var alignment: NSTextAlignment
    public var lineSpacing: CGFloat
    public var spaceBefore: CGFloat
    public var spaceAfter: CGFloat

    public init(
        alignment: NSTextAlignment = .left,
        lineSpacing: CGFloat = 0,
        spaceBefore: CGFloat = 0,
        spaceAfter: CGFloat = 0
    ) {
        self.alignment = alignment
        self.lineSpacing = lineSpacing
        self.spaceBefore = spaceBefore
        self.spaceAfter = spaceAfter
    }

    enum CodingKeys: String, CodingKey {
        case alignment
        case lineSpacing
        case spaceBefore
        case spaceAfter
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let rawAlign = try container.decode(Int.self, forKey: .alignment)
        self.alignment = NSTextAlignment(rawValue: rawAlign) ?? .left
        self.lineSpacing = try container.decode(CGFloat.self, forKey: .lineSpacing)
        self.spaceBefore = try container.decode(CGFloat.self, forKey: .spaceBefore)
        self.spaceAfter = try container.decode(CGFloat.self, forKey: .spaceAfter)
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(alignment.rawValue, forKey: .alignment)
        try container.encode(lineSpacing, forKey: .lineSpacing)
        try container.encode(spaceBefore, forKey: .spaceBefore)
        try container.encode(spaceAfter, forKey: .spaceAfter)
    }
}

public struct WordTextRun: Identifiable, Sendable, Codable, Equatable {
    public var id: String { "\(text.hashValue)-\(isBold)-\(isItalic)-\(fontSize ?? 0)-\(fontColorHex ?? "")" }
    public let text: String
    public let isBold: Bool
    public let isItalic: Bool
    public let isUnderline: Bool
    public let isStrikethrough: Bool
    public let fontSize: CGFloat?
    public let fontColorHex: String?
    public let fontName: String?
    public let highlightColorHex: String?

    public init(
        text: String,
        isBold: Bool = false,
        isItalic: Bool = false,
        isUnderline: Bool = false,
        isStrikethrough: Bool = false,
        fontSize: CGFloat? = nil,
        fontColorHex: String? = nil,
        fontName: String? = nil,
        highlightColorHex: String? = nil
    ) {
        self.text = text
        self.isBold = isBold
        self.isItalic = isItalic
        self.isUnderline = isUnderline
        self.isStrikethrough = isStrikethrough
        self.fontSize = fontSize
        self.fontColorHex = fontColorHex
        self.fontName = fontName
        self.highlightColorHex = highlightColorHex
    }
}

public struct WordParagraph: Identifiable, Sendable, Codable, Equatable {
    public let id: UUID
    public let index: Int
    public let headingLevel: Int? // 1...6 for H1-H6, nil for normal body text
    public let text: String
    public let runs: [WordTextRun]
    public let style: WordParagraphStyle
    public let bulletPrefix: String?

    public var isHeading: Bool { headingLevel != nil }

    public init(
        id: UUID = UUID(),
        index: Int,
        headingLevel: Int? = nil,
        text: String,
        runs: [WordTextRun] = [],
        style: WordParagraphStyle = WordParagraphStyle(),
        bulletPrefix: String? = nil
    ) {
        self.id = id
        self.index = index
        self.headingLevel = headingLevel
        self.text = text
        self.runs = runs
        self.style = style
        self.bulletPrefix = bulletPrefix
    }
}

// MARK: - Table Models

public struct WordTableCell: Identifiable, Sendable, Codable, Equatable {
    public var id: String { "\(rowIndex)-\(columnIndex)" }
    public let rowIndex: Int
    public let columnIndex: Int
    public let text: String
    public let runs: [WordTextRun]
    public let backgroundColorHex: String?

    public init(
        rowIndex: Int,
        columnIndex: Int,
        text: String,
        runs: [WordTextRun] = [],
        backgroundColorHex: String? = nil
    ) {
        self.rowIndex = rowIndex
        self.columnIndex = columnIndex
        self.text = text
        self.runs = runs
        self.backgroundColorHex = backgroundColorHex
    }
}

public struct WordTableRow: Identifiable, Sendable, Codable, Equatable {
    public var id: Int { rowIndex }
    public let rowIndex: Int
    public let cells: [WordTableCell]
    public let isHeader: Bool

    public init(rowIndex: Int, cells: [WordTableCell], isHeader: Bool = false) {
        self.rowIndex = rowIndex
        self.cells = cells
        self.isHeader = isHeader
    }
}

public struct WordTable: Identifiable, Sendable, Codable, Equatable {
    public var id: Int { tableIndex }
    public let tableIndex: Int
    public let rows: [WordTableRow]
    public let columnCount: Int
    public let rowCount: Int

    public init(tableIndex: Int, rows: [WordTableRow]) {
        self.tableIndex = tableIndex
        self.rows = rows
        self.rowCount = rows.count
        self.columnCount = rows.map { $0.cells.count }.max() ?? 0
    }
}

// MARK: - Comments & Metadata

public struct WordComment: Identifiable, Sendable, Codable, Equatable {
    public let id: String
    public let author: String
    public let date: String?
    public let text: String
    public let paragraphIndex: Int?

    public init(id: String, author: String, date: String? = nil, text: String, paragraphIndex: Int? = nil) {
        self.id = id
        self.author = author
        self.date = date
        self.text = text
        self.paragraphIndex = paragraphIndex
    }
}

public struct WordMetadata: Sendable, Codable, Equatable {
    public var title: String?
    public var author: String?
    public var lastModifiedBy: String?
    public var createdAt: String?
    public var modifiedAt: String?
    public var revision: String?
    public var wordCount: Int
    public var paragraphCount: Int
    public var tableCount: Int
    public var fileSizeFormatted: String
    public var fileFormat: String

    public init(
        title: String? = nil,
        author: String? = nil,
        lastModifiedBy: String? = nil,
        createdAt: String? = nil,
        modifiedAt: String? = nil,
        revision: String? = nil,
        wordCount: Int = 0,
        paragraphCount: Int = 0,
        tableCount: Int = 0,
        fileSizeFormatted: String = "0 KB",
        fileFormat: String = "Word Document"
    ) {
        self.title = title
        self.author = author
        self.lastModifiedBy = lastModifiedBy
        self.createdAt = createdAt
        self.modifiedAt = modifiedAt
        self.revision = revision
        self.wordCount = wordCount
        self.paragraphCount = paragraphCount
        self.tableCount = tableCount
        self.fileSizeFormatted = fileSizeFormatted
        self.fileFormat = fileFormat
    }
}

public struct WordDocumentModel: Identifiable, Sendable {
    public var id: String { fileURL.path }
    public let fileURL: URL
    public let fileName: String
    public var metadata: WordMetadata
    public var paragraphs: [WordParagraph]
    public var tables: [WordTable]
    public var comments: [WordComment]

    public var headings: [WordParagraph] {
        paragraphs.filter { $0.isHeading }
    }

    public init(
        fileURL: URL,
        fileName: String,
        metadata: WordMetadata = WordMetadata(),
        paragraphs: [WordParagraph] = [],
        tables: [WordTable] = [],
        comments: [WordComment] = []
    ) {
        self.fileURL = fileURL
        self.fileName = fileName
        self.metadata = metadata
        self.paragraphs = paragraphs
        self.tables = tables
        self.comments = comments
    }
}

// MARK: - Diff & Format Comparison Models

public struct FormatDiffItem: Identifiable, Sendable, Codable, Equatable {
    public var id: String { "\(propertyName)-\(oldValue)-\(newValue)" }
    public let propertyName: String
    public let oldValue: String
    public let newValue: String

    public init(propertyName: String, oldValue: String, newValue: String) {
        self.propertyName = propertyName
        self.oldValue = oldValue
        self.newValue = newValue
    }
}

public struct WordDiffBlock: Identifiable, Sendable {
    public let id: UUID
    public let blockIndex: Int
    public let leftParagraph: WordParagraph?
    public let rightParagraph: WordParagraph?
    public let changeType: ChangeType
    public let tokensLeft: [DiffToken]
    public let tokensRight: [DiffToken]
    public let formatDifferences: [FormatDiffItem]
    public let isFormatOnly: Bool

    public var isPhantomLeft: Bool { leftParagraph == nil }
    public var isPhantomRight: Bool { rightParagraph == nil }

    public init(
        id: UUID = UUID(),
        blockIndex: Int,
        leftParagraph: WordParagraph?,
        rightParagraph: WordParagraph?,
        changeType: ChangeType,
        tokensLeft: [DiffToken] = [],
        tokensRight: [DiffToken] = [],
        formatDifferences: [FormatDiffItem] = [],
        isFormatOnly: Bool = false
    ) {
        self.id = id
        self.blockIndex = blockIndex
        self.leftParagraph = leftParagraph
        self.rightParagraph = rightParagraph
        self.changeType = changeType
        self.tokensLeft = tokensLeft
        self.tokensRight = tokensRight
        self.formatDifferences = formatDifferences
        self.isFormatOnly = isFormatOnly
    }
}

public struct WordTableCellDiff: Identifiable, Sendable {
    public var id: String { "\(rowIndex)-\(colIndex)" }
    public let rowIndex: Int
    public let colIndex: Int
    public let leftCell: WordTableCell?
    public let rightCell: WordTableCell?
    public let changeType: ChangeType
    public let tokensLeft: [DiffToken]
    public let tokensRight: [DiffToken]

    public init(
        rowIndex: Int,
        colIndex: Int,
        leftCell: WordTableCell?,
        rightCell: WordTableCell?,
        changeType: ChangeType,
        tokensLeft: [DiffToken] = [],
        tokensRight: [DiffToken] = []
    ) {
        self.rowIndex = rowIndex
        self.colIndex = colIndex
        self.leftCell = leftCell
        self.rightCell = rightCell
        self.changeType = changeType
        self.tokensLeft = tokensLeft
        self.tokensRight = tokensRight
    }
}

public struct WordTableDiffResult: Identifiable, Sendable {
    public var id: Int { tableIndex }
    public let tableIndex: Int
    public let leftTable: WordTable?
    public let rightTable: WordTable?
    public let cellDiffs: [WordTableCellDiff]
    public let maxRows: Int
    public let maxCols: Int
    public let changeType: ChangeType

    public init(
        tableIndex: Int,
        leftTable: WordTable?,
        rightTable: WordTable?,
        cellDiffs: [WordTableCellDiff],
        maxRows: Int,
        maxCols: Int,
        changeType: ChangeType
    ) {
        self.tableIndex = tableIndex
        self.leftTable = leftTable
        self.rightTable = rightTable
        self.cellDiffs = cellDiffs
        self.maxRows = maxRows
        self.maxCols = maxCols
        self.changeType = changeType
    }
}

public struct MetadataDiffItem: Identifiable, Sendable {
    public var id: String { fieldName }
    public let fieldName: String
    public let leftValue: String
    public let rightValue: String
    public let isDifferent: Bool

    public init(fieldName: String, leftValue: String, rightValue: String) {
        self.fieldName = fieldName
        self.leftValue = leftValue
        self.rightValue = rightValue
        self.isDifferent = leftValue != rightValue
    }
}

public struct WordDiffResult: Sendable {
    public var blocks: [WordDiffBlock]
    public var tableDiffs: [WordTableDiffResult]
    public var metadataDiffs: [MetadataDiffItem]
    public var totalAdditions: Int
    public var totalDeletions: Int
    public var totalModifications: Int
    public var totalFormatChanges: Int

    public var totalDifferences: Int {
        totalAdditions + totalDeletions + totalModifications + totalFormatChanges
    }

    public init(
        blocks: [WordDiffBlock] = [],
        tableDiffs: [WordTableDiffResult] = [],
        metadataDiffs: [MetadataDiffItem] = [],
        totalAdditions: Int = 0,
        totalDeletions: Int = 0,
        totalModifications: Int = 0,
        totalFormatChanges: Int = 0
    ) {
        self.blocks = blocks
        self.tableDiffs = tableDiffs
        self.metadataDiffs = metadataDiffs
        self.totalAdditions = totalAdditions
        self.totalDeletions = totalDeletions
        self.totalModifications = totalModifications
        self.totalFormatChanges = totalFormatChanges
    }
}
