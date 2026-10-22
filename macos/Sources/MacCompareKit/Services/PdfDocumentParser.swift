import Foundation
import PDFKit

public final class PdfDocumentParser: Sendable {
    public static let shared = PdfDocumentParser()

    public init() {}

    public func parseDocument(from url: URL) async throws -> PdfDocumentModel {
        let fileURL = url.resolvingSymlinksInPath()
        guard FileManager.default.fileExists(atPath: fileURL.path) else {
            throw NSError(
                domain: "MacComparePdfError",
                code: 404,
                userInfo: [NSLocalizedDescriptionKey: "File does not exist: \(fileURL.path)"]
            )
        }

        guard let pdfDoc = PDFDocument(url: fileURL) else {
            throw NSError(
                domain: "MacComparePdfError",
                code: 422,
                userInfo: [NSLocalizedDescriptionKey: "Failed to load PDF document: \(fileURL.lastPathComponent)"]
            )
        }

        let fileSize = (try? FileManager.default.attributesOfItem(atPath: fileURL.path)[.size] as? UInt64) ?? 0
        let formattedSize = ByteCountFormatter.string(fromByteCount: Int64(fileSize), countStyle: .file)

        var metadata = PdfMetadata(fileSizeFormatted: formattedSize)
        if let attrs = pdfDoc.documentAttributes {
            metadata.title = attrs[PDFDocumentAttribute.titleAttribute] as? String
            metadata.author = attrs[PDFDocumentAttribute.authorAttribute] as? String
            metadata.subject = attrs[PDFDocumentAttribute.subjectAttribute] as? String
            metadata.creator = attrs[PDFDocumentAttribute.creatorAttribute] as? String
            metadata.producer = attrs[PDFDocumentAttribute.producerAttribute] as? String
            metadata.creationDate = attrs[PDFDocumentAttribute.creationDateAttribute] as? Date
            metadata.modificationDate = attrs[PDFDocumentAttribute.modificationDateAttribute] as? Date
        }

        let pageCount = pdfDoc.pageCount
        var pages: [PdfPageModel] = []

        for index in 0..<pageCount {
            if let page = pdfDoc.page(at: index) {
                let pageSize = page.bounds(for: .mediaBox).size
                let text = page.string ?? ""
                
                var lines: [PdfTextLine] = []
                let rawLines = text.components(separatedBy: .newlines)
                for (lineIdx, rawLine) in rawLines.enumerated() {
                    let trimmed = rawLine.trimmingCharacters(in: .whitespaces)
                    if !trimmed.isEmpty {
                        lines.append(PdfTextLine(lineNumber: lineIdx + 1, text: rawLine, bounds: .zero))
                    }
                }
                
                pages.append(PdfPageModel(pageIndex: index, pageSize: pageSize, text: text, lines: lines))
            }
        }

        return PdfDocumentModel(
            fileURL: fileURL,
            fileName: fileURL.lastPathComponent,
            pageCount: pageCount,
            metadata: metadata,
            pages: pages
        )
    }
}
