import Foundation
import CoreGraphics

public enum PdfViewMode: String, CaseIterable, Sendable {
    case sideBySide = "Side by Side"
    case overlay = "Overlay Blend"
    case curtain = "Curtain Split"
}

public enum PdfDiffChangeType: String, Sendable, Codable {
    case unchanged
    case modified
    case inserted
    case deleted
}

public struct PdfTextLine: Identifiable, Sendable {
    public var id: String { "\(lineNumber)-\(bounds.origin.x)-\(bounds.origin.y)" }
    public let lineNumber: Int
    public let text: String
    public let bounds: CGRect

    public init(lineNumber: Int, text: String, bounds: CGRect) {
        self.lineNumber = lineNumber
        self.text = text
        self.bounds = bounds
    }
}

public struct PdfPageModel: Identifiable, Sendable {
    public var id: Int { pageIndex }
    public let pageIndex: Int
    public let pageSize: CGSize
    public let text: String
    public let lines: [PdfTextLine]

    public init(pageIndex: Int, pageSize: CGSize, text: String, lines: [PdfTextLine] = []) {
        self.pageIndex = pageIndex
        self.pageSize = pageSize
        self.text = text
        self.lines = lines
    }
}

public struct PdfMetadata: Sendable {
    public var title: String?
    public var author: String?
    public var subject: String?
    public var creator: String?
    public var producer: String?
    public var creationDate: Date?
    public var modificationDate: Date?
    public var fileSizeFormatted: String

    public init(
        title: String? = nil,
        author: String? = nil,
        subject: String? = nil,
        creator: String? = nil,
        producer: String? = nil,
        creationDate: Date? = nil,
        modificationDate: Date? = nil,
        fileSizeFormatted: String = ""
    ) {
        self.title = title
        self.author = author
        self.subject = subject
        self.creator = creator
        self.producer = producer
        self.creationDate = creationDate
        self.modificationDate = modificationDate
        self.fileSizeFormatted = fileSizeFormatted
    }
}

public struct PdfDocumentModel: Identifiable, Sendable {
    public var id: String { fileURL.path }
    public let fileURL: URL
    public let fileName: String
    public let pageCount: Int
    public var metadata: PdfMetadata
    public var pages: [PdfPageModel]

    public init(
        fileURL: URL,
        fileName: String,
        pageCount: Int,
        metadata: PdfMetadata = PdfMetadata(),
        pages: [PdfPageModel] = []
    ) {
        self.fileURL = fileURL
        self.fileName = fileName
        self.pageCount = pageCount
        self.metadata = metadata
        self.pages = pages
    }
}

public struct PdfDiffToken: Identifiable, Sendable {
    public var id: UUID = UUID()
    public let text: String
    public let changeType: PdfDiffChangeType

    public init(text: String, changeType: PdfDiffChangeType) {
        self.text = text
        self.changeType = changeType
    }
}

public struct PdfPageDiffBlock: Identifiable, Sendable {
    public var id: String { "\(leftPageIndex ?? -1)-\(rightPageIndex ?? -1)" }
    public let leftPageIndex: Int?
    public let rightPageIndex: Int?
    public let changeType: PdfDiffChangeType
    public let leftText: String?
    public let rightText: String?
    public let tokens: [PdfDiffToken]

    public init(
        leftPageIndex: Int?,
        rightPageIndex: Int?,
        changeType: PdfDiffChangeType,
        leftText: String? = nil,
        rightText: String? = nil,
        tokens: [PdfDiffToken] = []
    ) {
        self.leftPageIndex = leftPageIndex
        self.rightPageIndex = rightPageIndex
        self.changeType = changeType
        self.leftText = leftText
        self.rightText = rightText
        self.tokens = tokens
    }
}

public struct PdfDiffResult: Sendable {
    public var blocks: [PdfPageDiffBlock] = []
    public var totalDiffCount: Int = 0

    public init(blocks: [PdfPageDiffBlock] = [], totalDiffCount: Int = 0) {
        self.blocks = blocks
        self.totalDiffCount = totalDiffCount
    }
}

public struct PdfVisualDiffResult: Sendable {
    public let pageIndex: Int
    public let diffBoundingBoxes: [CGRect]
    public let diffPixelCount: Int
    public let totalPixelCount: Int
    public let diffPercentage: Double

    public init(
        pageIndex: Int,
        diffBoundingBoxes: [CGRect] = [],
        diffPixelCount: Int = 0,
        totalPixelCount: Int = 0
    ) {
        self.pageIndex = pageIndex
        self.diffBoundingBoxes = diffBoundingBoxes
        self.diffPixelCount = diffPixelCount
        self.totalPixelCount = totalPixelCount
        self.diffPercentage = totalPixelCount > 0 ? (Double(diffPixelCount) / Double(totalPixelCount)) * 100.0 : 0.0
    }
}
