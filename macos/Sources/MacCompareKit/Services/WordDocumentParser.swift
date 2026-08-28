import Foundation
import AppKit
import UniformTypeIdentifiers

/// Parser responsible for extracting structured content, rich text runs, tables, and metadata from .docx and .doc files.
public final class WordDocumentParser: Sendable {
    public static let shared = WordDocumentParser()

    public init() {}

    /// Parse a Word document (.docx or .doc) from local file URL.
    public func parseDocument(from url: URL) async throws -> WordDocumentModel {
        let fileURL = url.resolvingSymlinksInPath()
        guard FileManager.default.fileExists(atPath: fileURL.path) else {
            throw NSError(domain: "MacCompareWordError", code: 404, userInfo: [NSLocalizedDescriptionKey: "File does not exist at path: \(fileURL.path)"])
        }

        let ext = fileURL.pathExtension.lowercased()
        let fileSize = (try? FileManager.default.attributesOfItem(atPath: fileURL.path)[.size] as? UInt64) ?? 0
        let formattedSize = ByteCountFormatter.string(fromByteCount: Int64(fileSize), countStyle: .file)

        if ext == "docx" {
            return try await parseDocx(from: fileURL, formattedSize: formattedSize)
        } else if ext == "doc" {
            return try await parseDocBinary(from: fileURL, formattedSize: formattedSize)
        } else {
            // Fallback general rich text parser
            return try await parseWithAttributedString(from: fileURL, fileFormat: "Rich Text Document", formattedSize: formattedSize)
        }
    }

    // MARK: - DOCX Parser (OpenXML + AppKit Hybrid)

    private func parseDocx(from url: URL, formattedSize: String) async throws -> WordDocumentModel {
        // Try XML-level extraction for tables and headings first, fallback to NSAttributedString
        var paragraphs: [WordParagraph] = []
        var tables: [WordTable] = []
        var comments: [WordComment] = []
        var metadata = WordMetadata(
            fileSizeFormatted: formattedSize,
            fileFormat: "Office Open XML (.docx)"
        )

        // 1. Primary: NSAttributedString for robust rich text styling
        let attributedStringResult = try? loadAttributedString(from: url, documentType: .officeOpenXML)

        if let (attrStr, docAttrs) = attributedStringResult {
            paragraphs = extractParagraphsFromAttributedString(attrStr)
            extractMetadataFromAttributes(docAttrs, into: &metadata)
        }

        // 2. XML Extraction for tables, comments, and structure enrichment
        if let xmlData = try? extractDocxXML(from: url) {
            if let extractedTables = parseDocxTablesFromXML(xmlData.documentXML), !extractedTables.isEmpty {
                tables = extractedTables
            }
            if let extractedComments = parseDocxCommentsFromXML(xmlData.commentsXML) {
                comments = extractedComments
            }
            if let coreMeta = parseDocxCorePropsXML(xmlData.corePropsXML) {
                if let title = coreMeta.title { metadata.title = title }
                if let author = coreMeta.author { metadata.author = author }
                if let modifiedBy = coreMeta.lastModifiedBy { metadata.lastModifiedBy = modifiedBy }
                if let created = coreMeta.createdAt { metadata.createdAt = created }
                if let modified = coreMeta.modifiedAt { metadata.modifiedAt = modified }
                if let revision = coreMeta.revision { metadata.revision = revision }
            }
        }

        // Calculate counts
        let totalWords = paragraphs.reduce(0) { $0 + countWords(in: $1.text) }
        metadata.wordCount = totalWords
        metadata.paragraphCount = paragraphs.count
        metadata.tableCount = tables.count

        return WordDocumentModel(
            fileURL: url,
            fileName: url.lastPathComponent,
            metadata: metadata,
            paragraphs: paragraphs,
            tables: tables,
            comments: comments
        )
    }

    // MARK: - DOC Parser (Word 97-2004 Binary)

    private func parseDocBinary(from url: URL, formattedSize: String) async throws -> WordDocumentModel {
        var metadata = WordMetadata(
            fileSizeFormatted: formattedSize,
            fileFormat: "Word 97-2004 Document (.doc)"
        )

        do {
            let (attrStr, docAttrs) = try loadAttributedString(from: url, documentType: .docFormat)
            let paragraphs = extractParagraphsFromAttributedString(attrStr)
            extractMetadataFromAttributes(docAttrs, into: &metadata)

            let totalWords = paragraphs.reduce(0) { $0 + countWords(in: $1.text) }
            metadata.wordCount = totalWords
            metadata.paragraphCount = paragraphs.count

            return WordDocumentModel(
                fileURL: url,
                fileName: url.lastPathComponent,
                metadata: metadata,
                paragraphs: paragraphs,
                tables: [],
                comments: []
            )
        } catch {
            // Fallback to textutil conversion if direct load fails
            return try await parseWithTextutilConversion(from: url, formattedSize: formattedSize)
        }
    }

    private func parseWithAttributedString(from url: URL, fileFormat: String, formattedSize: String) async throws -> WordDocumentModel {
        var metadata = WordMetadata(fileSizeFormatted: formattedSize, fileFormat: fileFormat)
        let (attrStr, docAttrs) = try loadAttributedString(from: url, documentType: nil)
        let paragraphs = extractParagraphsFromAttributedString(attrStr)
        extractMetadataFromAttributes(docAttrs, into: &metadata)

        metadata.wordCount = paragraphs.reduce(0) { $0 + countWords(in: $1.text) }
        metadata.paragraphCount = paragraphs.count

        return WordDocumentModel(
            fileURL: url,
            fileName: url.lastPathComponent,
            metadata: metadata,
            paragraphs: paragraphs,
            tables: [],
            comments: []
        )
    }

    // MARK: - AttributedString Extraction Helpers

    private func loadAttributedString(from url: URL, documentType: NSAttributedString.DocumentType?) throws -> (NSAttributedString, [NSAttributedString.DocumentAttributeKey: Any]) {
        var options: [NSAttributedString.DocumentReadingOptionKey: Any] = [:]
        if let docType = documentType {
            options[.documentType] = docType
        }

        var docAttributes: NSDictionary?
        let attrString = try NSAttributedString(
            url: url,
            options: options,
            documentAttributes: &docAttributes
        )

        var swiftAttrs: [NSAttributedString.DocumentAttributeKey: Any] = [:]
        if let docAttrs = docAttributes as? [String: Any] {
            for (k, v) in docAttrs {
                swiftAttrs[NSAttributedString.DocumentAttributeKey(rawValue: k)] = v
            }
        }

        return (attrString, swiftAttrs)
    }

    private func extractParagraphsFromAttributedString(_ attrStr: NSAttributedString) -> [WordParagraph] {
        let fullText = attrStr.string
        if fullText.isEmpty { return [] }

        var paragraphs: [WordParagraph] = []
        let rawParagraphs = fullText.components(separatedBy: "\n")
        var currentOffset = 0

        for (pIndex, rawPara) in rawParagraphs.enumerated() {
            let paraLength = (rawPara as NSString).length
            let paraRange = NSRange(location: currentOffset, length: paraLength)
            currentOffset += paraLength + 1 // +1 for the newline

            if rawPara.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && pIndex == rawParagraphs.count - 1 {
                continue
            }

            var runs: [WordTextRun] = []
            var paraStyle = WordParagraphStyle()
            var detectedHeadingLevel: Int? = nil

            // Heuristic heading detection based on prefix or size/bold
            let trimmed = rawPara.trimmingCharacters(in: .whitespaces)
            if trimmed.hasPrefix("# ") || trimmed.hasPrefix("一、") || trimmed.hasPrefix("第1章") || trimmed.hasPrefix("1. ") {
                detectedHeadingLevel = 1
            } else if trimmed.hasPrefix("## ") || trimmed.hasPrefix("（一）") || trimmed.hasPrefix("1.1 ") {
                detectedHeadingLevel = 2
            } else if trimmed.hasPrefix("### ") || trimmed.hasPrefix("1.1.1 ") {
                detectedHeadingLevel = 3
            }

            if paraRange.length > 0 && paraRange.location + paraRange.length <= (attrStr.string as NSString).length {
                attrStr.enumerateAttributes(in: paraRange, options: []) { attrs, range, _ in
                    let subText = (attrStr.string as NSString).substring(with: range)
                    if subText.isEmpty { return }

                    var isBold = false
                    var isItalic = false
                    var fontSize: CGFloat? = nil
                    var fontName: String? = nil
                    var isUnderline = false
                    var isStrikethrough = false
                    var colorHex: String? = nil
                    var highlightHex: String? = nil

                    if let font = attrs[.font] as? NSFont {
                        let traits = font.fontDescriptor.symbolicTraits
                        isBold = traits.contains(.bold)
                        isItalic = traits.contains(.italic)
                        fontSize = font.pointSize
                        fontName = font.fontName

                        // Check large font as heading
                        if detectedHeadingLevel == nil {
                            if font.pointSize >= 22 { detectedHeadingLevel = 1 }
                            else if font.pointSize >= 18 { detectedHeadingLevel = 2 }
                            else if font.pointSize >= 15 && isBold { detectedHeadingLevel = 3 }
                        }
                    }

                    if let underline = attrs[.underlineStyle] as? Int, underline != 0 {
                        isUnderline = true
                    }
                    if let strike = attrs[.strikethroughStyle] as? Int, strike != 0 {
                        isStrikethrough = true
                    }
                    if let color = attrs[.foregroundColor] as? NSColor {
                        colorHex = color.toHexString()
                    }
                    if let bg = attrs[.backgroundColor] as? NSColor {
                        highlightHex = bg.toHexString()
                    }
                    if let style = attrs[.paragraphStyle] as? NSParagraphStyle {
                        paraStyle.alignment = style.alignment
                        paraStyle.lineSpacing = style.lineSpacing
                        paraStyle.spaceBefore = style.paragraphSpacingBefore
                        paraStyle.spaceAfter = style.paragraphSpacing
                    }

                    let run = WordTextRun(
                        text: subText,
                        isBold: isBold,
                        isItalic: isItalic,
                        isUnderline: isUnderline,
                        isStrikethrough: isStrikethrough,
                        fontSize: fontSize,
                        fontColorHex: colorHex,
                        fontName: fontName,
                        highlightColorHex: highlightHex
                    )
                    runs.append(run)
                }
            }

            if runs.isEmpty {
                runs.append(WordTextRun(text: rawPara))
            }

            var bulletPrefix: String? = nil
            if trimmed.hasPrefix("• ") || trimmed.hasPrefix("- ") || trimmed.hasPrefix("* ") {
                bulletPrefix = "•"
            }

            let paragraph = WordParagraph(
                index: pIndex + 1,
                headingLevel: detectedHeadingLevel,
                text: rawPara,
                runs: runs,
                style: paraStyle,
                bulletPrefix: bulletPrefix
            )
            paragraphs.append(paragraph)
        }

        return paragraphs
    }

    private func extractMetadataFromAttributes(_ attrs: [NSAttributedString.DocumentAttributeKey: Any], into metadata: inout WordMetadata) {
        if let title = attrs[.title] as? String { metadata.title = title }
        if let author = attrs[.author] as? String { metadata.author = author }
        if let comment = attrs[.comment] as? String, metadata.title == nil { metadata.title = comment }
        if let creationDate = attrs[.creationTime] as? Date {
            let formatter = DateFormatter()
            formatter.dateFormat = "yyyy-MM-dd HH:mm:ss"
            metadata.createdAt = formatter.string(from: creationDate)
        }
        if let modDate = attrs[.modificationTime] as? Date {
            let formatter = DateFormatter()
            formatter.dateFormat = "yyyy-MM-dd HH:mm:ss"
            metadata.modifiedAt = formatter.string(from: modDate)
        }
    }

    // MARK: - Docx XML Unzip & DOM Parsing

    private struct DocxXMLBundle {
        var documentXML: String
        var commentsXML: String?
        var corePropsXML: String?
    }

    private func extractDocxXML(from url: URL) throws -> DocxXMLBundle? {
        let tempDir = FileManager.default.temporaryDirectory.appendingPathComponent("mc_docx_\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
        defer {
            _ = try? FileManager.default.removeItem(at: tempDir)
        }

        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/unzip")
        process.arguments = ["-q", url.path, "-d", tempDir.path]

        try process.run()
        process.waitUntilExit()

        let docXMLPath = tempDir.appendingPathComponent("word/document.xml")
        guard FileManager.default.fileExists(atPath: docXMLPath.path),
              let docContent = try? String(contentsOf: docXMLPath, encoding: .utf8) else {
            return nil
        }

        var commentsContent: String?
        let commentsPath = tempDir.appendingPathComponent("word/comments.xml")
        if FileManager.default.fileExists(atPath: commentsPath.path) {
            commentsContent = try? String(contentsOf: commentsPath, encoding: .utf8)
        }

        var corePropsContent: String?
        let corePropsPath = tempDir.appendingPathComponent("docProps/core.xml")
        if FileManager.default.fileExists(atPath: corePropsPath.path) {
            corePropsContent = try? String(contentsOf: corePropsPath, encoding: .utf8)
        }

        return DocxXMLBundle(
            documentXML: docContent,
            commentsXML: commentsContent,
            corePropsXML: corePropsContent
        )
    }

    private func parseDocxTablesFromXML(_ xml: String) -> [WordTable]? {
        // Find all <w:tbl>...</w:tbl> blocks
        let tablePattern = "(?s)<w:tbl>(.*?)</w:tbl>"
        guard let tableRegex = try? NSRegularExpression(pattern: tablePattern) else { return nil }
        let matches = tableRegex.matches(in: xml, range: NSRange(xml.startIndex..., in: xml))

        var tables: [WordTable] = []
        for (tblIdx, match) in matches.enumerated() {
            guard let tableRange = Range(match.range(at: 1), in: xml) else { continue }
            let tableContent = String(xml[tableRange])

            // Find all <w:tr>...</w:tr> rows
            let rowPattern = "(?s)<w:tr>(.*?)</w:tr>"
            guard let rowRegex = try? NSRegularExpression(pattern: rowPattern) else { continue }
            let rowMatches = rowRegex.matches(in: tableContent, range: NSRange(tableContent.startIndex..., in: tableContent))

            var rows: [WordTableRow] = []
            for (rowIdx, rowMatch) in rowMatches.enumerated() {
                guard let rowRange = Range(rowMatch.range(at: 1), in: tableContent) else { continue }
                let rowContent = String(tableContent[rowRange])

                // Find all <w:tc>...</w:tc> cells
                let cellPattern = "(?s)<w:tc>(.*?)</w:tc>"
                guard let cellRegex = try? NSRegularExpression(pattern: cellPattern) else { continue }
                let cellMatches = cellRegex.matches(in: rowContent, range: NSRange(rowContent.startIndex..., in: rowContent))

                var cells: [WordTableCell] = []
                for (colIdx, cellMatch) in cellMatches.enumerated() {
                    guard let cellRange = Range(cellMatch.range(at: 1), in: rowContent) else { continue }
                    let cellContent = String(rowContent[cellRange])
                    let cellText = extractTextFromXML(cellContent)

                    let cell = WordTableCell(
                        rowIndex: rowIdx,
                        columnIndex: colIdx,
                        text: cellText
                    )
                    cells.append(cell)
                }

                let row = WordTableRow(rowIndex: rowIdx, cells: cells, isHeader: rowIdx == 0)
                rows.append(row)
            }

            if !rows.isEmpty {
                tables.append(WordTable(tableIndex: tblIdx + 1, rows: rows))
            }
        }

        return tables
    }

    private func parseDocxCommentsFromXML(_ xml: String?) -> [WordComment]? {
        guard let xml else { return nil }
        let commentPattern = "(?s)<w:comment w:id=\"([^\"]+)\" w:author=\"([^\"]+)\"(?: w:date=\"([^\"]+)\")?>(.*?)</w:comment>"
        guard let regex = try? NSRegularExpression(pattern: commentPattern) else { return nil }
        let matches = regex.matches(in: xml, range: NSRange(xml.startIndex..., in: xml))

        var comments: [WordComment] = []
        for match in matches {
            guard let idRange = Range(match.range(at: 1), in: xml),
                  let authorRange = Range(match.range(at: 2), in: xml),
                  let contentRange = Range(match.range(at: 4), in: xml) else { continue }

            let id = String(xml[idRange])
            let author = String(xml[authorRange])
            let text = extractTextFromXML(String(xml[contentRange]))

            var dateStr: String? = nil
            if match.range(at: 3).location != NSNotFound, let dateRange = Range(match.range(at: 3), in: xml) {
                dateStr = String(xml[dateRange])
            }

            comments.append(WordComment(id: id, author: author, date: dateStr, text: text))
        }

        return comments
    }

    private struct CoreProps {
        var title: String?
        var author: String?
        var lastModifiedBy: String?
        var createdAt: String?
        var modifiedAt: String?
        var revision: String?
    }

    private func parseDocxCorePropsXML(_ xml: String?) -> CoreProps? {
        guard let xml else { return nil }
        var props = CoreProps()

        props.title = extractTagValue(from: xml, tag: "dc:title")
        props.author = extractTagValue(from: xml, tag: "dc:creator")
        props.lastModifiedBy = extractTagValue(from: xml, tag: "cp:lastModifiedBy")
        props.createdAt = extractTagValue(from: xml, tag: "dcterms:created")
        props.modifiedAt = extractTagValue(from: xml, tag: "dcterms:modified")
        props.revision = extractTagValue(from: xml, tag: "cp:revision")

        return props
    }

    private func extractTagValue(from xml: String, tag: String) -> String? {
        let pattern = "(?s)<\(tag)[^>]*>(.*?)</\(tag)>"
        guard let regex = try? NSRegularExpression(pattern: pattern),
              let match = regex.firstMatch(in: xml, range: NSRange(xml.startIndex..., in: xml)),
              let range = Range(match.range(at: 1), in: xml) else { return nil }
        return String(xml[range]).trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private func extractTextFromXML(_ xml: String) -> String {
        let pattern = "(?s)<w:t[^>]*>(.*?)</w:t>"
        guard let regex = try? NSRegularExpression(pattern: pattern) else { return "" }
        let matches = regex.matches(in: xml, range: NSRange(xml.startIndex..., in: xml))
        var text = ""
        for match in matches {
            if let range = Range(match.range(at: 1), in: xml) {
                text += String(xml[range])
            }
        }
        return text.replacingOccurrences(of: "&amp;", with: "&")
            .replacingOccurrences(of: "&lt;", with: "<")
            .replacingOccurrences(of: "&gt;", with: ">")
            .replacingOccurrences(of: "&quot;", with: "\"")
    }

    private func parseWithTextutilConversion(from url: URL, formattedSize: String) async throws -> WordDocumentModel {
        let tempTxt = FileManager.default.temporaryDirectory.appendingPathComponent("conv_\(UUID().uuidString).txt")
        defer { _ = try? FileManager.default.removeItem(at: tempTxt) }

        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/textutil")
        process.arguments = ["-convert", "txt", "-output", tempTxt.path, url.path]

        try process.run()
        process.waitUntilExit()

        guard let content = try? String(contentsOf: tempTxt, encoding: .utf8) else {
            throw NSError(domain: "MacCompareWordError", code: 500, userInfo: [NSLocalizedDescriptionKey: "Failed to convert .doc document"])
        }

        let lines = content.components(separatedBy: .newlines)
        let paragraphs = lines.enumerated().map { index, line in
            WordParagraph(index: index + 1, text: line, runs: [WordTextRun(text: line)])
        }

        let metadata = WordMetadata(
            wordCount: paragraphs.reduce(0) { $0 + countWords(in: $1.text) },
            paragraphCount: paragraphs.count,
            fileSizeFormatted: formattedSize,
            fileFormat: "Word 97-2004 Document (.doc)"
        )

        return WordDocumentModel(
            fileURL: url,
            fileName: url.lastPathComponent,
            metadata: metadata,
            paragraphs: paragraphs
        )
    }

    private func countWords(in text: String) -> Int {
        var count = 0
        let range = CFRangeMake(0, CFStringGetLength(text as CFString))
        let tokenizer = CFStringTokenizerCreate(kCFAllocatorDefault, text as CFString, range, kCFStringTokenizerUnitWord, nil)
        var tokenType = CFStringTokenizerAdvanceToNextToken(tokenizer)
        while tokenType != [] {
            count += 1
            tokenType = CFStringTokenizerAdvanceToNextToken(tokenizer)
        }
        return max(count, text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? 0 : 1)
    }
}

// MARK: - Color Hex Extensions

private extension NSColor {
    func toHexString() -> String {
        guard let rgbColor = usingColorSpace(.deviceRGB) else { return "#000000" }
        let r = Int(rgbColor.redComponent * 255.0)
        let g = Int(rgbColor.greenComponent * 255.0)
        let b = Int(rgbColor.blueComponent * 255.0)
        return String(format: "#%02X%02X%02X", r, g, b)
    }
}
