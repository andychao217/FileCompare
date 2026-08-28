import Foundation
import AppKit
import UniformTypeIdentifiers
import CryptoKit

/// Parser responsible for extracting structured content, rich text runs, tables, metadata and multimedia from .docx and .doc files.
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
        var paragraphs: [WordParagraph] = []
        var tables: [WordTable] = []
        var comments: [WordComment] = []
        var metadata = WordMetadata(
            fileSizeFormatted: formattedSize,
            fileFormat: "Office Open XML (.docx)"
        )

        // 1. Primary: NSAttributedString for robust rich text styling
        let attributedStringResult = try? loadAttributedString(from: url, documentType: .officeOpenXML)
        var fallbackParagraphs: [WordParagraph] = []
        if let (attrStr, docAttrs) = attributedStringResult {
            fallbackParagraphs = extractParagraphsFromAttributedString(attrStr)
            extractMetadataFromAttributes(docAttrs, into: &metadata)
        }

        // 2. XML Extraction for sequential blocks (tables, paragraphs, media and shapes)
        if let bundle = try? extractDocxXML(from: url) {
            let (seqParas, seqTables) = parseDocxSequentialBlocks(bundle: bundle, attrStrParagraphs: fallbackParagraphs)
            if !seqParas.isEmpty {
                paragraphs = seqParas
                tables = seqTables
            } else {
                paragraphs = fallbackParagraphs
                if let extractedTables = parseDocxTablesFromXML(bundle.documentXML), !extractedTables.isEmpty {
                    tables = extractedTables
                }
                enrichParagraphsWithDocxMedia(paragraphs: &paragraphs, bundle: bundle)
            }

            if let extractedComments = parseDocxCommentsFromXML(bundle.commentsXML) {
                comments = extractedComments
            }
            if let coreMeta = parseDocxCorePropsXML(bundle.corePropsXML) {
                if let title = coreMeta.title { metadata.title = title }
                if let author = coreMeta.author { metadata.author = author }
                if let modifiedBy = coreMeta.lastModifiedBy { metadata.lastModifiedBy = modifiedBy }
                if let created = coreMeta.createdAt { metadata.createdAt = created }
                if let modified = coreMeta.modifiedAt { metadata.modifiedAt = modified }
                if let revision = coreMeta.revision { metadata.revision = revision }
            }
        } else {
            paragraphs = fallbackParagraphs
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

            var foundAttachmentMedia: [WordMediaItem] = []

            if paraRange.length > 0 && paraRange.location + paraRange.length <= (attrStr.string as NSString).length {
                attrStr.enumerateAttributes(in: paraRange, options: []) { attrs, range, _ in
                    let subText = (attrStr.string as NSString).substring(with: range)

                    // Check for embedded text attachment images
                    if let attachment = attrs[.attachment] as? NSTextAttachment {
                        var imgData: Data? = nil
                        if let img = attachment.image {
                            if let tiff = img.tiffRepresentation,
                               let bitmap = NSBitmapImageRep(data: tiff) {
                                imgData = bitmap.representation(using: .png, properties: [:])
                            }
                        } else if let fileWrapper = attachment.fileWrapper, let contents = fileWrapper.regularFileContents {
                            imgData = contents
                        }

                        if let data = imgData {
                            let hash = self.computeSHA256Hex(data: data)
                            let mediaItem = WordMediaItem(
                                mediaType: .image,
                                fileName: "image_\(pIndex + 1).png",
                                fileExtension: "png",
                                fileSize: data.count,
                                hashSHA256: hash,
                                data: data,
                                paragraphIndex: pIndex + 1
                            )
                            foundAttachmentMedia.append(mediaItem)
                        }
                    }

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

            if runs.isEmpty && foundAttachmentMedia.isEmpty {
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
                bulletPrefix: bulletPrefix,
                mediaItems: foundAttachmentMedia
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
        var mediaDataMap: [String: Data] // "media/image1.png" -> Data
        var relationships: [String: String] // "rId4" -> "media/image1.png"
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

        // Extract media files (word/media/*)
        var mediaDataMap: [String: Data] = [:]
        let mediaDir = tempDir.appendingPathComponent("word/media")
        if let mediaFiles = try? FileManager.default.contentsOfDirectory(atPath: mediaDir.path) {
            for fileName in mediaFiles {
                let filePath = mediaDir.appendingPathComponent(fileName)
                if let data = try? Data(contentsOf: filePath) {
                    mediaDataMap["media/\(fileName)"] = data
                }
            }
        }

        // Extract relationships (word/_rels/document.xml.rels)
        var relationships: [String: String] = [:]
        let relsPath = tempDir.appendingPathComponent("word/_rels/document.xml.rels")
        if let relsContent = try? String(contentsOf: relsPath, encoding: .utf8) {
            let relPattern = "(?s)<Relationship[^>]*Id=\"([^\"]+)\"[^>]*Target=\"([^\"]+)\""
            if let relRegex = try? NSRegularExpression(pattern: relPattern) {
                let matches = relRegex.matches(in: relsContent, range: NSRange(relsContent.startIndex..., in: relsContent))
                for m in matches {
                    if let idRange = Range(m.range(at: 1), in: relsContent),
                       let targetRange = Range(m.range(at: 2), in: relsContent) {
                        let rId = String(relsContent[idRange])
                        let target = String(relsContent[targetRange])
                        relationships[rId] = target
                    }
                }
            }
        }

        return DocxXMLBundle(
            documentXML: docContent,
            commentsXML: commentsContent,
            corePropsXML: corePropsContent,
            mediaDataMap: mediaDataMap,
            relationships: relationships
        )
    }

    private func enrichParagraphsWithDocxMedia(paragraphs: inout [WordParagraph], bundle: DocxXMLBundle) {
        guard !bundle.mediaDataMap.isEmpty else { return }

        // Find all <w:p>...</w:p> blocks in document.xml
        let pPattern = "(?s)<w:p(?: [^>]*)?>(.*?)</w:p>"
        guard let pRegex = try? NSRegularExpression(pattern: pPattern) else { return }
        let pMatches = pRegex.matches(in: bundle.documentXML, range: NSRange(bundle.documentXML.startIndex..., in: bundle.documentXML))

        // Regexes for image embeddings and extents
        let blipRegex = try? NSRegularExpression(pattern: "<a:blip[^>]*r:embed=\"([^\"]+)\"")
        let vmlRegex = try? NSRegularExpression(pattern: "<v:imagedata[^>]*r:id=\"([^\"]+)\"")
        let extentRegex = try? NSRegularExpression(pattern: "<wp:extent[^>]*cx=\"([0-9]+)\"[^>]*cy=\"([0-9]+)\"")

        var nonTablePIndex = 0
        for pMatch in pMatches {
            guard let pRange = Range(pMatch.range(at: 1), in: bundle.documentXML) else { continue }
            let pContent = String(bundle.documentXML[pRange])

            var foundMedia: [WordMediaItem] = []

            // Check drawing extents (EMU / 12700 = pt)
            var widthPt: CGFloat? = nil
            var heightPt: CGFloat? = nil
            if let extentMatch = extentRegex?.firstMatch(in: pContent, range: NSRange(pContent.startIndex..., in: pContent)) {
                if let cxRange = Range(extentMatch.range(at: 1), in: pContent),
                   let cyRange = Range(extentMatch.range(at: 2), in: pContent),
                   let cx = Double(pContent[cxRange]),
                   let cy = Double(pContent[cyRange]) {
                    widthPt = CGFloat(cx / 12700.0)
                    heightPt = CGFloat(cy / 12700.0)
                }
            }

            // Check OpenXML Drawing Blip
            var rIds: [String] = []
            if let blipMatches = blipRegex?.matches(in: pContent, range: NSRange(pContent.startIndex..., in: pContent)) {
                for bm in blipMatches {
                    if let rRange = Range(bm.range(at: 1), in: pContent) {
                        rIds.append(String(pContent[rRange]))
                    }
                }
            }

            // Check VML legacy Image
            if let vmlMatches = vmlRegex?.matches(in: pContent, range: NSRange(pContent.startIndex..., in: pContent)) {
                for vm in vmlMatches {
                    if let rRange = Range(vm.range(at: 1), in: pContent) {
                        rIds.append(String(pContent[rRange]))
                    }
                }
            }

            for rId in rIds {
                guard let targetPath = bundle.relationships[rId] else { continue }
                let cleanTarget = targetPath.replacingOccurrences(of: "../", with: "")
                guard let data = bundle.mediaDataMap[cleanTarget] else { continue }

                let fileName = (cleanTarget as NSString).lastPathComponent
                let fileExt = (fileName as NSString).pathExtension.lowercased()
                let hash = computeSHA256Hex(data: data)
                let mediaType = determineMediaType(forExtension: fileExt)

                let mediaItem = WordMediaItem(
                    mediaType: mediaType,
                    fileName: fileName,
                    fileExtension: fileExt,
                    fileSize: data.count,
                    hashSHA256: hash,
                    data: data,
                    widthPoints: widthPt,
                    heightPoints: heightPt,
                    relationshipId: rId,
                    paragraphIndex: nonTablePIndex + 1
                )
                foundMedia.append(mediaItem)
            }

            // Check OpenXML Drawing Shapes (wps:wsp) and VML Shapes (v:shape / v:rect)
            let shapeRegex = try? NSRegularExpression(pattern: "(?s)<(?:wps:wsp|v:rect|v:shape)(?: [^>]*)?>.*?</(?:wps:wsp|v:rect|v:shape)>")
            if let shapeMatches = shapeRegex?.matches(in: pContent, range: NSRange(pContent.startIndex..., in: pContent)) {
                for sm in shapeMatches {
                    guard let sRange = Range(sm.range, in: pContent) else { continue }
                    let shapeContent = String(pContent[sRange])

                    // Determine shape geometry
                    var shapeType = "rect"
                    if let geomMatch = try? NSRegularExpression(pattern: "prst=\"([a-zA-Z0-9]+)\"").firstMatch(in: shapeContent, range: NSRange(shapeContent.startIndex..., in: shapeContent)),
                       let gRange = Range(geomMatch.range(at: 1), in: shapeContent) {
                        shapeType = String(shapeContent[gRange])
                    }

                    // Determine fill color
                    var fillColor = "#5B9BD5" // default accent1 blue
                    if let srgbMatch = try? NSRegularExpression(pattern: "(?:srgbClr val|fillcolor)=\"?#?([0-9A-Fa-f]{6})").firstMatch(in: shapeContent, range: NSRange(shapeContent.startIndex..., in: shapeContent)),
                       let cRange = Range(srgbMatch.range(at: 1), in: shapeContent) {
                        fillColor = "#" + String(shapeContent[cRange])
                    } else if shapeContent.contains("schemeClr val=\"accent1\"") {
                        fillColor = "#5B9BD5"
                    } else if shapeContent.contains("schemeClr val=\"accent2\"") {
                        fillColor = "#ED7D31"
                    } else if shapeContent.contains("schemeClr val=\"accent3\"") {
                        fillColor = "#A5A5A5"
                    } else if shapeContent.contains("schemeClr val=\"accent4\"") {
                        fillColor = "#FFC000"
                    } else if shapeContent.contains("schemeClr val=\"accent5\"") {
                        fillColor = "#4472C4"
                    } else if shapeContent.contains("schemeClr val=\"accent6\"") {
                        fillColor = "#70AD47"
                    }

                    // Determine stroke color
                    var strokeColor: String? = nil
                    if let strokeMatch = try? NSRegularExpression(pattern: "(?:color|stroke color)=\"?#?([0-9A-Fa-f]{6})").firstMatch(in: shapeContent, range: NSRange(shapeContent.startIndex..., in: shapeContent)),
                       let stRange = Range(strokeMatch.range(at: 1), in: shapeContent) {
                        strokeColor = "#" + String(shapeContent[stRange])
                    }

                    let shapeHash = computeSHA256Hex(data: "\(shapeType)-\(fillColor)-\(widthPt ?? 60)-\(heightPt ?? 24)".data(using: .utf8)!)

                    let shapeMedia = WordMediaItem(
                        mediaType: .shape,
                        fileName: "shape_\(shapeType).vector",
                        fileExtension: "shape",
                        fileSize: 1024,
                        hashSHA256: shapeHash,
                        widthPoints: widthPt ?? 60,
                        heightPoints: heightPt ?? 24,
                        paragraphIndex: nonTablePIndex + 1,
                        shapeType: shapeType,
                        fillColorHex: fillColor,
                        strokeColorHex: strokeColor
                    )
                    foundMedia.append(shapeMedia)
                }
            }

            if !foundMedia.isEmpty && nonTablePIndex < paragraphs.count {
                paragraphs[nonTablePIndex].mediaItems.append(contentsOf: foundMedia)
            }

            let textContent = extractTextFromXML(pContent).trimmingCharacters(in: .whitespacesAndNewlines)
            if !textContent.isEmpty || !foundMedia.isEmpty {
                nonTablePIndex += 1
            }
        }
    }

    private func determineMediaType(forExtension ext: String) -> WordMediaType {
        switch ext {
        case "png", "jpg", "jpeg", "gif", "webp", "tiff", "bmp", "svg", "ico":
            return .image
        case "mp4", "mov", "avi", "m4v", "mkv", "webm":
            return .video
        case "mp3", "m4a", "wav", "aac", "ogg", "flac":
            return .audio
        default:
            return .attachment
        }
    }

    private func computeSHA256Hex(data: Data) -> String {
        let digest = SHA256.hash(data: data)
        return digest.map { String(format: "%02x", $0) }.joined()
    }

    private func parseSingleDocxTable(_ tableXML: String, tableIndex: Int) -> WordTable? {
        let rowPattern = "(?s)<w:tr(?: [^>]*)?>(.*?)</w:tr>"
        guard let rowRegex = try? NSRegularExpression(pattern: rowPattern) else { return nil }
        let rowMatches = rowRegex.matches(in: tableXML, range: NSRange(tableXML.startIndex..., in: tableXML))

        var rows: [WordTableRow] = []
        for (rowIdx, rowMatch) in rowMatches.enumerated() {
            guard let rowRange = Range(rowMatch.range(at: 1), in: tableXML) else { continue }
            let rowContent = String(tableXML[rowRange])

            let cellPattern = "(?s)<w:tc(?: [^>]*)?>(.*?)</w:tc>"
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
            return WordTable(tableIndex: tableIndex, rows: rows)
        }
        return nil
    }

    private func parseDocxSequentialBlocks(bundle: DocxXMLBundle, attrStrParagraphs: [WordParagraph]) -> (paragraphs: [WordParagraph], tables: [WordTable]) {
        guard let bodyRange = bundle.documentXML.range(of: "<w:body>") else {
            return ([], [])
        }

        let bodyXML = String(bundle.documentXML[bodyRange.upperBound...])
        let blockPattern = "(?s)(<w:tbl(?: [^>]*)?>.*?</w:tbl>|<w:p(?: [^>]*)?>.*?</w:p>)"
        guard let regex = try? NSRegularExpression(pattern: blockPattern) else {
            return ([], [])
        }

        let matches = regex.matches(in: bodyXML, range: NSRange(bodyXML.startIndex..., in: bodyXML))
        var orderedParagraphs: [WordParagraph] = []
        var extractedTables: [WordTable] = []
        var pIndex = 1
        var tblIndex = 1

        let extentRegex = try? NSRegularExpression(pattern: "<wp:extent[^>]*cx=\"([0-9]+)\"[^>]*cy=\"([0-9]+)\"")
        let blipRegex = try? NSRegularExpression(pattern: "<a:blip[^>]*r:embed=\"([^\"]+)\"")
        let vmlRegex = try? NSRegularExpression(pattern: "<v:imagedata[^>]*r:id=\"([^\"]+)\"")
        let shapeRegex = try? NSRegularExpression(pattern: "(?s)<(?:wps:wsp|v:rect|v:shape)(?: [^>]*)?>.*?</(?:wps:wsp|v:rect|v:shape)>")

        for match in matches {
            guard let blockRange = Range(match.range, in: bodyXML) else { continue }
            let blockContent = String(bodyXML[blockRange])

            if blockContent.hasPrefix("<w:tbl") {
                if let tbl = parseSingleDocxTable(blockContent, tableIndex: tblIndex) {
                    extractedTables.append(tbl)
                    let summary = "表格 (\(tbl.rows.count)行 × \(tbl.columnCount)列)"
                    let tablePara = WordParagraph(
                        index: pIndex,
                        text: summary,
                        runs: [WordTextRun(text: summary, isBold: true)],
                        table: tbl
                    )
                    orderedParagraphs.append(tablePara)
                    pIndex += 1
                    tblIndex += 1
                }
            } else if blockContent.hasPrefix("<w:p") {
                var mediaItems: [WordMediaItem] = []
                let text = extractTextFromXML(blockContent)

                var widthPt: CGFloat? = nil
                var heightPt: CGFloat? = nil
                if let extentMatch = extentRegex?.firstMatch(in: blockContent, range: NSRange(blockContent.startIndex..., in: blockContent)) {
                    if let cxRange = Range(extentMatch.range(at: 1), in: blockContent),
                       let cyRange = Range(extentMatch.range(at: 2), in: blockContent),
                       let cx = Double(blockContent[cxRange]),
                       let cy = Double(blockContent[cyRange]) {
                        widthPt = CGFloat(cx / 12700.0)
                        heightPt = CGFloat(cy / 12700.0)
                    }
                }

                // Bitmaps
                var rIds: [String] = []
                if let blipMatches = blipRegex?.matches(in: blockContent, range: NSRange(blockContent.startIndex..., in: blockContent)) {
                    for bm in blipMatches {
                        if let rRange = Range(bm.range(at: 1), in: blockContent) {
                            rIds.append(String(blockContent[rRange]))
                        }
                    }
                }
                if let vmlMatches = vmlRegex?.matches(in: blockContent, range: NSRange(blockContent.startIndex..., in: blockContent)) {
                    for vm in vmlMatches {
                        if let rRange = Range(vm.range(at: 1), in: blockContent) {
                            rIds.append(String(blockContent[rRange]))
                        }
                    }
                }

                for rId in rIds {
                    guard let targetPath = bundle.relationships[rId] else { continue }
                    let cleanTarget = targetPath.replacingOccurrences(of: "../", with: "")
                    guard let data = bundle.mediaDataMap[cleanTarget] else { continue }

                    let fileName = (cleanTarget as NSString).lastPathComponent
                    let fileExt = (fileName as NSString).pathExtension.lowercased()
                    let hash = computeSHA256Hex(data: data)
                    let mediaType = determineMediaType(forExtension: fileExt)

                    let mediaItem = WordMediaItem(
                        mediaType: mediaType,
                        fileName: fileName,
                        fileExtension: fileExt,
                        fileSize: data.count,
                        hashSHA256: hash,
                        data: data,
                        widthPoints: widthPt,
                        heightPoints: heightPt,
                        relationshipId: rId,
                        paragraphIndex: pIndex
                    )
                    mediaItems.append(mediaItem)
                }

                // Shapes
                if let shapeMatches = shapeRegex?.matches(in: blockContent, range: NSRange(blockContent.startIndex..., in: blockContent)) {
                    for sm in shapeMatches {
                        guard let sRange = Range(sm.range, in: blockContent) else { continue }
                        let shapeContent = String(blockContent[sRange])

                        var shapeType = "rect"
                        if let geomMatch = try? NSRegularExpression(pattern: "prst=\"([a-zA-Z0-9]+)\"").firstMatch(in: shapeContent, range: NSRange(shapeContent.startIndex..., in: shapeContent)),
                           let gRange = Range(geomMatch.range(at: 1), in: shapeContent) {
                            shapeType = String(shapeContent[gRange])
                        }

                        var fillColor = "#5B9BD5"
                        if let srgbMatch = try? NSRegularExpression(pattern: "(?:srgbClr val|fillcolor)=\"?#?([0-9A-Fa-f]{6})").firstMatch(in: shapeContent, range: NSRange(shapeContent.startIndex..., in: shapeContent)),
                           let cRange = Range(srgbMatch.range(at: 1), in: shapeContent) {
                            fillColor = "#" + String(shapeContent[cRange])
                        } else if shapeContent.contains("schemeClr val=\"accent1\"") {
                            fillColor = "#5B9BD5"
                        } else if shapeContent.contains("schemeClr val=\"accent2\"") {
                            fillColor = "#ED7D31"
                        } else if shapeContent.contains("schemeClr val=\"accent3\"") {
                            fillColor = "#A5A5A5"
                        } else if shapeContent.contains("schemeClr val=\"accent4\"") {
                            fillColor = "#FFC000"
                        } else if shapeContent.contains("schemeClr val=\"accent5\"") {
                            fillColor = "#4472C4"
                        } else if shapeContent.contains("schemeClr val=\"accent6\"") {
                            fillColor = "#70AD47"
                        }

                        var strokeColor: String? = nil
                        if let strokeMatch = try? NSRegularExpression(pattern: "(?:color|stroke color)=\"?#?([0-9A-Fa-f]{6})").firstMatch(in: shapeContent, range: NSRange(shapeContent.startIndex..., in: shapeContent)),
                           let stRange = Range(strokeMatch.range(at: 1), in: shapeContent) {
                            strokeColor = "#" + String(shapeContent[stRange])
                        }

                        let shapeHash = computeSHA256Hex(data: "\(shapeType)-\(fillColor)-\(widthPt ?? 60)-\(heightPt ?? 24)".data(using: .utf8)!)
                        let shapeMedia = WordMediaItem(
                            mediaType: .shape,
                            fileName: "shape_\(shapeType).vector",
                            fileExtension: "shape",
                            fileSize: 1024,
                            hashSHA256: shapeHash,
                            widthPoints: widthPt ?? 60,
                            heightPoints: heightPt ?? 24,
                            paragraphIndex: pIndex,
                            shapeType: shapeType,
                            fillColorHex: fillColor,
                            strokeColorHex: strokeColor
                        )
                        mediaItems.append(shapeMedia)
                    }
                }

                if !text.isEmpty || !mediaItems.isEmpty {
                    // Try to extract rich text runs from block
                    let runs = parseDocxParagraphRunsFromXML(blockContent)
                    let para = WordParagraph(
                        index: pIndex,
                        text: text,
                        runs: runs.isEmpty ? [WordTextRun(text: text)] : runs,
                        mediaItems: mediaItems
                    )
                    orderedParagraphs.append(para)
                    pIndex += 1
                }
            }
        }

        return (orderedParagraphs, extractedTables)
    }

    private func parseDocxParagraphRunsFromXML(_ pXML: String) -> [WordTextRun] {
        let rPattern = "(?s)<w:r(?: [^>]*)?>(.*?)</w:r>"
        guard let rRegex = try? NSRegularExpression(pattern: rPattern) else { return [] }
        let rMatches = rRegex.matches(in: pXML, range: NSRange(pXML.startIndex..., in: pXML))

        var runs: [WordTextRun] = []
        for rMatch in rMatches {
            guard let rRange = Range(rMatch.range(at: 1), in: pXML) else { continue }
            let rContent = String(pXML[rRange])
            let rText = extractTextFromXML(rContent)
            if rText.isEmpty { continue }

            let isBold = rContent.contains("<w:b/>") || rContent.contains("<w:b ")
            let isItalic = rContent.contains("<w:i/>") || rContent.contains("<w:i ")
            let isUnderline = rContent.contains("<w:u ") || rContent.contains("<w:u/>")
            let isStrike = rContent.contains("<w:strike")

            var colorHex: String? = nil
            if let colorMatch = try? NSRegularExpression(pattern: "<w:color [^>]*w:val=\"([0-9A-Fa-f]{6})\"").firstMatch(in: rContent, range: NSRange(rContent.startIndex..., in: rContent)),
               let cRange = Range(colorMatch.range(at: 1), in: rContent) {
                colorHex = "#" + String(rContent[cRange])
            }

            var szPt: CGFloat? = nil
            if let szMatch = try? NSRegularExpression(pattern: "<w:sz [^>]*w:val=\"([0-9]+)\"").firstMatch(in: rContent, range: NSRange(rContent.startIndex..., in: rContent)),
               let sRange = Range(szMatch.range(at: 1), in: rContent),
               let halfPts = Double(rContent[sRange]) {
                szPt = CGFloat(halfPts / 2.0)
            }

            let run = WordTextRun(
                text: rText,
                isBold: isBold,
                isItalic: isItalic,
                isUnderline: isUnderline,
                isStrikethrough: isStrike,
                fontSize: szPt,
                fontColorHex: colorHex
            )
            runs.append(run)
        }
        return runs
    }

    private func parseDocxTablesFromXML(_ xml: String) -> [WordTable]? {
        let tablePattern = "(?s)<w:tbl>(.*?)</w:tbl>"
        guard let tableRegex = try? NSRegularExpression(pattern: tablePattern) else { return nil }
        let matches = tableRegex.matches(in: xml, range: NSRange(xml.startIndex..., in: xml))

        var tables: [WordTable] = []
        for (tblIdx, match) in matches.enumerated() {
            guard let tableRange = Range(match.range, in: xml) else { continue }
            let tableXML = String(xml[tableRange])
            if let tbl = parseSingleDocxTable(tableXML, tableIndex: tblIdx + 1) {
                tables.append(tbl)
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
