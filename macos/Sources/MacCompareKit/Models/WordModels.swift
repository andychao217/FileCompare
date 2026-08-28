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

// MARK: - Word Media Models (Images, Video, Audio, Attachments, Shapes)

public enum WordMediaType: String, Sendable, Codable, Equatable, CaseIterable {
    case image = "Image"
    case video = "Video"
    case audio = "Audio"
    case attachment = "Attachment"
    case shape = "Shape"

    public var iconName: String {
        switch self {
        case .image: return "photo.fill"
        case .video: return "video.fill"
        case .audio: return "waveform"
        case .attachment: return "paperclip"
        case .shape: return "rectangle.fill"
        }
    }
}

public struct WordMediaItem: Identifiable, Sendable, Codable, Equatable {
    public let id: UUID
    public let mediaType: WordMediaType
    public let fileName: String
    public let fileExtension: String
    public let fileSize: Int
    public let hashSHA256: String
    public let data: Data?
    public let widthPoints: CGFloat?
    public let heightPoints: CGFloat?
    public let relationshipId: String?
    public let paragraphIndex: Int
    public let shapeType: String?
    public let fillColorHex: String?
    public let strokeColorHex: String?

    public var formattedSize: String {
        if mediaType == .shape {
            if let w = widthPoints, let h = heightPoints {
                return "\(Int(w))×\(Int(h)) pt"
            }
            return "Vector Shape"
        }
        return ByteCountFormatter.string(fromByteCount: Int64(fileSize), countStyle: .file)
    }

    public init(
        id: UUID = UUID(),
        mediaType: WordMediaType = .image,
        fileName: String,
        fileExtension: String,
        fileSize: Int,
        hashSHA256: String,
        data: Data? = nil,
        widthPoints: CGFloat? = nil,
        heightPoints: CGFloat? = nil,
        relationshipId: String? = nil,
        paragraphIndex: Int = 0,
        shapeType: String? = nil,
        fillColorHex: String? = nil,
        strokeColorHex: String? = nil
    ) {
        self.id = id
        self.mediaType = mediaType
        self.fileName = fileName
        self.fileExtension = fileExtension
        self.fileSize = fileSize
        self.hashSHA256 = hashSHA256
        self.data = data
        self.widthPoints = widthPoints
        self.heightPoints = heightPoints
        self.relationshipId = relationshipId
        self.paragraphIndex = paragraphIndex
        self.shapeType = shapeType
        self.fillColorHex = fillColorHex
        self.strokeColorHex = strokeColorHex
    }
}

public struct WordMediaDiffItem: Identifiable, Sendable, Equatable {
    public let id: UUID
    public let changeType: ChangeType
    public let mediaType: WordMediaType
    public let leftMedia: WordMediaItem?
    public let rightMedia: WordMediaItem?
    public let changeDescriptions: [String]

    public init(
        id: UUID = UUID(),
        changeType: ChangeType,
        mediaType: WordMediaType = .image,
        leftMedia: WordMediaItem? = nil,
        rightMedia: WordMediaItem? = nil,
        changeDescriptions: [String] = []
    ) {
        self.id = id
        self.changeType = changeType
        self.mediaType = mediaType
        self.leftMedia = leftMedia
        self.rightMedia = rightMedia
        self.changeDescriptions = changeDescriptions
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
    public var mediaItems: [WordMediaItem]
    public var table: WordTable?

    public var isHeading: Bool { headingLevel != nil }
    public var hasMedia: Bool { !mediaItems.isEmpty }
    public var isTableBlock: Bool { table != nil }

    public init(
        id: UUID = UUID(),
        index: Int,
        headingLevel: Int? = nil,
        text: String,
        runs: [WordTextRun] = [],
        style: WordParagraphStyle = WordParagraphStyle(),
        bulletPrefix: String? = nil,
        mediaItems: [WordMediaItem] = [],
        table: WordTable? = nil
    ) {
        self.id = id
        self.index = index
        self.headingLevel = headingLevel
        self.text = text
        self.runs = runs
        self.style = style
        self.bulletPrefix = bulletPrefix
        self.mediaItems = mediaItems
        self.table = table
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
    public let mediaDifferences: [WordMediaDiffItem]
    public let tableDiff: WordTableDiffResult?
    public let isFormatOnly: Bool

    public var isPhantomLeft: Bool { leftParagraph == nil }
    public var isPhantomRight: Bool { rightParagraph == nil }
    public var isTableBlock: Bool {
        leftParagraph?.isTableBlock == true || rightParagraph?.isTableBlock == true || tableDiff != nil
    }
    public var hasMedia: Bool {
        !(leftParagraph?.mediaItems.isEmpty ?? true) ||
        !(rightParagraph?.mediaItems.isEmpty ?? true) ||
        !mediaDifferences.isEmpty
    }

    public init(
        id: UUID = UUID(),
        blockIndex: Int,
        leftParagraph: WordParagraph?,
        rightParagraph: WordParagraph?,
        changeType: ChangeType,
        tokensLeft: [DiffToken] = [],
        tokensRight: [DiffToken] = [],
        formatDifferences: [FormatDiffItem] = [],
        mediaDifferences: [WordMediaDiffItem] = [],
        tableDiff: WordTableDiffResult? = nil,
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
        self.mediaDifferences = mediaDifferences
        self.tableDiff = tableDiff
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

public struct WordDiffResult: Identifiable, Sendable {
    public let id: UUID
    public var blocks: [WordDiffBlock]
    public var tableDiffs: [WordTableDiffResult]
    public var metadataDiffs: [MetadataDiffItem]
    public var totalAdditions: Int
    public var totalDeletions: Int
    public var totalModifications: Int
    public var totalFormatChanges: Int
    public var totalMediaChanges: Int

    public var totalDifferences: Int {
        totalAdditions + totalDeletions + totalModifications + totalFormatChanges + totalMediaChanges
    }

    public init(
        id: UUID = UUID(),
        blocks: [WordDiffBlock] = [],
        tableDiffs: [WordTableDiffResult] = [],
        metadataDiffs: [MetadataDiffItem] = [],
        totalAdditions: Int = 0,
        totalDeletions: Int = 0,
        totalModifications: Int = 0,
        totalFormatChanges: Int = 0,
        totalMediaChanges: Int = 0
    ) {
        self.id = id
        self.blocks = blocks
        self.tableDiffs = tableDiffs
        self.metadataDiffs = metadataDiffs
        self.totalAdditions = totalAdditions
        self.totalDeletions = totalDeletions
        self.totalModifications = totalModifications
        self.totalFormatChanges = totalFormatChanges
        self.totalMediaChanges = totalMediaChanges
    }
}
