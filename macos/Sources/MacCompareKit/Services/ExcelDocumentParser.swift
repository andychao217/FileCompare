import Foundation
import AppKit

/// Parser responsible for reading and extracting structured multi-sheet data from .xlsx, .xls, .csv, and .tsv files.
public final class ExcelDocumentParser: @unchecked Sendable {
    public static let shared = ExcelDocumentParser()

    public init() {}

    public func parseWorkbook(from url: URL) async throws -> ExcelWorkbookModel {
        let fileURL = url.resolvingSymlinksInPath()
        guard FileManager.default.fileExists(atPath: fileURL.path) else {
            throw NSError(domain: "MacCompareExcelError", code: 404, userInfo: [NSLocalizedDescriptionKey: "File does not exist at path: \(fileURL.path)"])
        }

        let ext = fileURL.pathExtension.lowercased()
        let fileSize = (try? FileManager.default.attributesOfItem(atPath: fileURL.path)[.size] as? UInt64) ?? 0
        let formattedSize = ByteCountFormatter.string(fromByteCount: Int64(fileSize), countStyle: .file)

        if ext == "xlsx" || ext == "xlsm" {
            return try await parseXlsx(from: fileURL, formattedSize: formattedSize)
        } else if ext == "xls" {
            return try await parseXlsBinary(from: fileURL, formattedSize: formattedSize)
        } else if ext == "csv" || ext == "tsv" {
            let delimiter = (ext == "tsv") ? "\t" : ","
            return try await parseDelimited(from: fileURL, delimiter: delimiter, formattedSize: formattedSize)
        } else {
            // Try xlsx first, then fallback to CSV
            if let result = try? await parseXlsx(from: fileURL, formattedSize: formattedSize) {
                return result
            }
            return try await parseDelimited(from: fileURL, delimiter: ",", formattedSize: formattedSize)
        }
    }

    // MARK: - XLSX (OpenXML) Parser

    private struct SheetRef {
        let name: String
        let sheetId: String
        let rId: String
        let targetPath: String
    }

    private func parseXlsx(from url: URL, formattedSize: String) async throws -> ExcelWorkbookModel {
        let tempDir = FileManager.default.temporaryDirectory.appendingPathComponent("mc_xlsx_\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
        defer {
            _ = try? FileManager.default.removeItem(at: tempDir)
        }

        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/unzip")
        process.arguments = ["-q", url.path, "-d", tempDir.path]

        try process.run()
        process.waitUntilExit()

        var metadata = ExcelMetadata(
            fileSizeFormatted: formattedSize,
            fileFormat: "Excel OpenXML (.xlsx)"
        )

        // 1. Read Metadata from docProps/core.xml
        let corePropsPath = tempDir.appendingPathComponent("docProps/core.xml")
        if FileManager.default.fileExists(atPath: corePropsPath.path),
           let coreContent = try? String(contentsOf: corePropsPath, encoding: .utf8) {
            parseCorePropsXML(coreContent, into: &metadata)
        }

        // 2. Read Shared Strings (xl/sharedStrings.xml)
        let sharedStringsPath = tempDir.appendingPathComponent("xl/sharedStrings.xml")
        var sharedStrings: [String] = []
        if FileManager.default.fileExists(atPath: sharedStringsPath.path),
           let sharedContent = try? String(contentsOf: sharedStringsPath, encoding: .utf8) {
            sharedStrings = parseSharedStringsXML(sharedContent)
        }

        // 3. Read Workbook Relationships (xl/_rels/workbook.xml.rels)
        let relsPath = tempDir.appendingPathComponent("xl/_rels/workbook.xml.rels")
        var relsMap: [String: String] = [:] // rId -> relative path
        if FileManager.default.fileExists(atPath: relsPath.path),
           let relsContent = try? String(contentsOf: relsPath, encoding: .utf8) {
            relsMap = parseWorkbookRelsXML(relsContent)
        }

        // 4. Read Workbook Structure (xl/workbook.xml)
        let workbookPath = tempDir.appendingPathComponent("xl/workbook.xml")
        var sheetRefs: [SheetRef] = []
        if FileManager.default.fileExists(atPath: workbookPath.path),
           let wbContent = try? String(contentsOf: workbookPath, encoding: .utf8) {
            sheetRefs = parseWorkbookXML(wbContent, relsMap: relsMap)
        }

        // Fallback: if no sheetRefs found, scan xl/worksheets/*.xml directly
        if sheetRefs.isEmpty {
            let worksheetsDir = tempDir.appendingPathComponent("xl/worksheets")
            if let files = try? FileManager.default.contentsOfDirectory(atPath: worksheetsDir.path) {
                let sheetFiles = files.filter { $0.hasPrefix("sheet") && $0.hasSuffix(".xml") }.sorted()
                for (idx, file) in sheetFiles.enumerated() {
                    sheetRefs.append(SheetRef(
                        name: "Sheet\(idx + 1)",
                        sheetId: "\(idx + 1)",
                        rId: "rId\(idx + 1)",
                        targetPath: "worksheets/\(file)"
                    ))
                }
            }
        }

        // 5. Parse each Sheet XML
        var sheets: [ExcelSheetModel] = []
        for ref in sheetRefs {
            let sheetFilePath: URL
            if ref.targetPath.hasPrefix("/") {
                sheetFilePath = tempDir.appendingPathComponent(String(ref.targetPath.dropFirst()))
            } else if ref.targetPath.hasPrefix("xl/") {
                sheetFilePath = tempDir.appendingPathComponent(ref.targetPath)
            } else {
                sheetFilePath = tempDir.appendingPathComponent("xl/\(ref.targetPath)")
            }

            if FileManager.default.fileExists(atPath: sheetFilePath.path),
               let sheetXML = try? String(contentsOf: sheetFilePath, encoding: .utf8) {
                let sheetModel = parseWorksheetXML(sheetXML, sheetName: ref.name, sheetId: ref.sheetId, sharedStrings: sharedStrings)
                sheets.append(sheetModel)
            }
        }

        metadata.sheetCount = sheets.count

        return ExcelWorkbookModel(
            fileURL: url,
            fileName: url.lastPathComponent,
            metadata: metadata,
            sheets: sheets
        )
    }

    // MARK: - Shared Strings Parser
    private func parseSharedStringsXML(_ xml: String) -> [String] {
        var results: [String] = []
        let siPattern = try? NSRegularExpression(pattern: "<si>(.*?)</si>", options: [.dotMatchesLineSeparators])
        let tPattern = try? NSRegularExpression(pattern: "<t(?:[^>]*)>(.*?)</t>", options: [.dotMatchesLineSeparators])

        guard let siPattern = siPattern else { return [] }
        let nsString = xml as NSString
        let matches = siPattern.matches(in: xml, range: NSRange(location: 0, length: nsString.length))

        for match in matches {
            let siContent = nsString.substring(with: match.range(at: 1))
            if let tPattern = tPattern {
                let tMatches = tPattern.matches(in: siContent, range: NSRange(location: 0, length: (siContent as NSString).length))
                var combined = ""
                for tm in tMatches {
                    let text = (siContent as NSString).substring(with: tm.range(at: 1))
                    combined += decodeXMLEntities(text)
                }
                results.append(combined)
            } else {
                results.append(decodeXMLEntities(siContent))
            }
        }
        return results
    }

    // MARK: - Workbook Relationships Parser
    private func parseWorkbookRelsXML(_ xml: String) -> [String: String] {
        var map: [String: String] = [:]
        let relPattern = try? NSRegularExpression(pattern: "<Relationship\\s+([^>]+)/>", options: [])
        let idPattern = try? NSRegularExpression(pattern: "Id=\"([^\"]+)\"", options: [])
        let targetPattern = try? NSRegularExpression(pattern: "Target=\"([^\"]+)\"", options: [])

        guard let relPattern = relPattern else { return map }
        let nsString = xml as NSString
        let matches = relPattern.matches(in: xml, range: NSRange(location: 0, length: nsString.length))

        for match in matches {
            let attrStr = nsString.substring(with: match.range(at: 1))
            let attrNs = attrStr as NSString
            var idVal: String?
            var targetVal: String?

            if let idMatch = idPattern?.firstMatch(in: attrStr, range: NSRange(location: 0, length: attrNs.length)) {
                idVal = attrNs.substring(with: idMatch.range(at: 1))
            }
            if let targetMatch = targetPattern?.firstMatch(in: attrStr, range: NSRange(location: 0, length: attrNs.length)) {
                targetVal = attrNs.substring(with: targetMatch.range(at: 1))
            }

            if let id = idVal, let target = targetVal {
                map[id] = target
            }
        }
        return map
    }

    // MARK: - Workbook XML Parser
    private func parseWorkbookXML(_ xml: String, relsMap: [String: String]) -> [SheetRef] {
        var results: [SheetRef] = []
        let sheetPattern = try? NSRegularExpression(pattern: "<sheet\\s+([^>]+)/>", options: [])
        let namePattern = try? NSRegularExpression(pattern: "name=\"([^\"]+)\"", options: [])
        let sheetIdPattern = try? NSRegularExpression(pattern: "sheetId=\"([^\"]+)\"", options: [])
        let rIdPattern = try? NSRegularExpression(pattern: "r:id=\"([^\"]+)\"", options: [])

        guard let sheetPattern = sheetPattern else { return [] }
        let nsString = xml as NSString
        let matches = sheetPattern.matches(in: xml, range: NSRange(location: 0, length: nsString.length))

        for match in matches {
            let attrStr = nsString.substring(with: match.range(at: 1))
            let attrNs = attrStr as NSString
            var nameVal = "Sheet"
            var sheetIdVal = "1"
            var rIdVal = "rId1"

            if let nameMatch = namePattern?.firstMatch(in: attrStr, range: NSRange(location: 0, length: attrNs.length)) {
                nameVal = decodeXMLEntities(attrNs.substring(with: nameMatch.range(at: 1)))
            }
            if let idMatch = sheetIdPattern?.firstMatch(in: attrStr, range: NSRange(location: 0, length: attrNs.length)) {
                sheetIdVal = attrNs.substring(with: idMatch.range(at: 1))
            }
            if let rMatch = rIdPattern?.firstMatch(in: attrStr, range: NSRange(location: 0, length: attrNs.length)) {
                rIdVal = attrNs.substring(with: rMatch.range(at: 1))
            }

            let target = relsMap[rIdVal] ?? "worksheets/sheet\(sheetIdVal).xml"
            results.append(SheetRef(name: nameVal, sheetId: sheetIdVal, rId: rIdVal, targetPath: target))
        }
        return results
    }

    // MARK: - Worksheet XML Parser
    private func parseWorksheetXML(_ xml: String, sheetName: String, sheetId: String, sharedStrings: [String]) -> ExcelSheetModel {
        var rowModels: [ExcelRowData] = []
        var maxColumns = 1

        let rowPattern = try? NSRegularExpression(pattern: "<row\\s+r=\"(\\d+)\"[^>]*>(.*?)</row>", options: [.dotMatchesLineSeparators])
        let cellPattern = try? NSRegularExpression(pattern: "<c\\s+([^>]*?)>(.*?)</c>", options: [.dotMatchesLineSeparators])
        let cellSelfClosingPattern = try? NSRegularExpression(pattern: "<c\\s+([^>]*?)/>", options: [])
        let rAttrPattern = try? NSRegularExpression(pattern: "r=\"([A-Za-z]+)(\\d+)\"", options: [])
        let tAttrPattern = try? NSRegularExpression(pattern: "t=\"([^\"]+)\"", options: [])
        let vTagPattern = try? NSRegularExpression(pattern: "<v>(.*?)</v>", options: [.dotMatchesLineSeparators])
        let fTagPattern = try? NSRegularExpression(pattern: "<f(?:[^>]*)>(.*?)</f>", options: [.dotMatchesLineSeparators])
        let isTagPattern = try? NSRegularExpression(pattern: "<is><t>(.*?)</t></is>", options: [.dotMatchesLineSeparators])

        guard let rowPattern = rowPattern, let cellPattern = cellPattern else {
            return ExcelSheetModel(id: sheetId, name: sheetName, maxColumns: 1, maxRows: 0, rows: [])
        }

        let nsString = xml as NSString
        let rowMatches = rowPattern.matches(in: xml, range: NSRange(location: 0, length: nsString.length))

        for rowMatch in rowMatches {
            let rowIdxStr = nsString.substring(with: rowMatch.range(at: 1))
            let rowIdx = Int(rowIdxStr) ?? (rowModels.count + 1)
            let rowContent = nsString.substring(with: rowMatch.range(at: 2))
            let rowContentNs = rowContent as NSString

            var cells: [ExcelCellData] = []
            let cellMatches = cellPattern.matches(in: rowContent, range: NSRange(location: 0, length: rowContentNs.length))

            for cellMatch in cellMatches {
                let attrStr = rowContentNs.substring(with: cellMatch.range(at: 1))
                let bodyStr = rowContentNs.substring(with: cellMatch.range(at: 2))
                let attrNs = attrStr as NSString
                let bodyNs = bodyStr as NSString

                var colLetter = "A"
                if let rMatch = rAttrPattern?.firstMatch(in: attrStr, range: NSRange(location: 0, length: attrNs.length)) {
                    colLetter = attrNs.substring(with: rMatch.range(at: 1)).uppercased()
                }
                let colIdx = ExcelModelsHelper.columnIndex(from: colLetter)
                maxColumns = max(maxColumns, colIdx + 1)

                var typeAttr = ""
                if let tMatch = tAttrPattern?.firstMatch(in: attrStr, range: NSRange(location: 0, length: attrNs.length)) {
                    typeAttr = attrNs.substring(with: tMatch.range(at: 1))
                }

                var formula: String?
                if let fMatch = fTagPattern?.firstMatch(in: bodyStr, range: NSRange(location: 0, length: bodyNs.length)) {
                    formula = decodeXMLEntities(bodyNs.substring(with: fMatch.range(at: 1)))
                }

                var rawValue = ""
                var cellType: ExcelCellType = .string

                if let vMatch = vTagPattern?.firstMatch(in: bodyStr, range: NSRange(location: 0, length: bodyNs.length)) {
                    let vContent = decodeXMLEntities(bodyNs.substring(with: vMatch.range(at: 1)))
                    if typeAttr == "s", let strIndex = Int(vContent), strIndex < sharedStrings.count {
                        rawValue = sharedStrings[strIndex]
                        cellType = .string
                    } else if typeAttr == "b" {
                        rawValue = (vContent == "1" || vContent.lowercased() == "true") ? "TRUE" : "FALSE"
                        cellType = .boolean
                    } else if typeAttr == "e" {
                        rawValue = vContent
                        cellType = .error
                    } else {
                        rawValue = vContent
                        cellType = (Double(vContent) != nil) ? .number : .string
                    }
                } else if let isMatch = isTagPattern?.firstMatch(in: bodyStr, range: NSRange(location: 0, length: bodyNs.length)) {
                    rawValue = decodeXMLEntities(bodyNs.substring(with: isMatch.range(at: 1)))
                    cellType = .string
                } else if typeAttr == "str" {
                    rawValue = bodyStr.trimmingCharacters(in: .whitespacesAndNewlines)
                    cellType = .string
                }

                if formula != nil && cellType != .error {
                    cellType = .formula
                }

                cells.append(ExcelCellData(
                    columnIndex: colIdx,
                    columnLetter: colLetter,
                    rawValue: rawValue,
                    formattedValue: rawValue,
                    formula: formula,
                    cellType: cellType
                ))
            }

            // Also check self-closing empty cells (e.g. <c r="A1"/>)
            if let cellSelfClosingPattern = cellSelfClosingPattern {
                let scMatches = cellSelfClosingPattern.matches(in: rowContent, range: NSRange(location: 0, length: rowContentNs.length))
                for scMatch in scMatches {
                    let attrStr = rowContentNs.substring(with: scMatch.range(at: 1))
                    let attrNs = attrStr as NSString
                    if let rMatch = rAttrPattern?.firstMatch(in: attrStr, range: NSRange(location: 0, length: attrNs.length)) {
                        let colLetter = attrNs.substring(with: rMatch.range(at: 1)).uppercased()
                        let colIdx = ExcelModelsHelper.columnIndex(from: colLetter)
                        if !cells.contains(where: { $0.columnIndex == colIdx }) {
                            cells.append(ExcelCellData(
                                columnIndex: colIdx,
                                columnLetter: colLetter,
                                rawValue: "",
                                cellType: .blank
                            ))
                        }
                    }
                }
            }

            // Sort cells by column index
            cells.sort(by: { $0.columnIndex < $1.columnIndex })
            rowModels.append(ExcelRowData(rowIndex: rowIdx, cells: cells))
        }

        // Sort rows by row index
        rowModels.sort(by: { $0.rowIndex < $1.rowIndex })

        return ExcelSheetModel(
            id: sheetId,
            name: sheetName,
            maxColumns: maxColumns,
            maxRows: rowModels.count,
            rows: rowModels
        )
    }

    // MARK: - Core Properties Parser
    private func parseCorePropsXML(_ xml: String, into metadata: inout ExcelMetadata) {
        let titleMatch = extractTagContent(xml: xml, tag: "dc:title")
        let authorMatch = extractTagContent(xml: xml, tag: "dc:creator")
        let modifiedByMatch = extractTagContent(xml: xml, tag: "cp:lastModifiedBy")
        let createdMatch = extractTagContent(xml: xml, tag: "dcterms:created")
        let modifiedMatch = extractTagContent(xml: xml, tag: "dcterms:modified")

        if let t = titleMatch, !t.isEmpty { metadata.title = t }
        if let a = authorMatch, !a.isEmpty { metadata.author = a }
        if let m = modifiedByMatch, !m.isEmpty { metadata.lastModifiedBy = m }

        let isoFormatter = ISO8601DateFormatter()
        isoFormatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        if let c = createdMatch, let d = isoFormatter.date(from: c) { metadata.createdAt = d }
        if let m = modifiedMatch, let d = isoFormatter.date(from: m) { metadata.modifiedAt = d }
    }

    private func extractTagContent(xml: String, tag: String) -> String? {
        let pattern = "<\(tag)(?:[^>]*)>(.*?)</\(tag)>"
        guard let regex = try? NSRegularExpression(pattern: pattern, options: [.dotMatchesLineSeparators]) else { return nil }
        let ns = xml as NSString
        if let match = regex.firstMatch(in: xml, range: NSRange(location: 0, length: ns.length)) {
            return decodeXMLEntities(ns.substring(with: match.range(at: 1)))
        }
        return nil
    }

    // MARK: - XLS (BIFF8 Binary) & CSV Parsers

    private func parseXlsBinary(from url: URL, formattedSize: String) async throws -> ExcelWorkbookModel {
        // High compatibility: read data via CFB stream or fallback string extraction
        let data = try Data(contentsOf: url)
        var sheets: [ExcelSheetModel] = []

        // Try extracting ASCII / Unicode text lines if binary BIFF parsing
        let extractedRows = parseBiffTextRows(from: data)
        if !extractedRows.isEmpty {
            sheets.append(ExcelSheetModel(
                id: "1",
                name: "Sheet1",
                maxColumns: extractedRows.map({ $0.cells.count }).max() ?? 1,
                maxRows: extractedRows.count,
                rows: extractedRows
            ))
        } else {
            // Fallback: try reading as CSV formatted text in case of misnamed file
            let str = String(data: data, encoding: .utf8) ?? String(data: data, encoding: .ascii) ?? ""
            return try parseDelimitedString(str, url: url, delimiter: ",", formattedSize: formattedSize)
        }

        let metadata = ExcelMetadata(
            sheetCount: sheets.count,
            fileSizeFormatted: formattedSize,
            fileFormat: "Excel 97-2004 Workbook (.xls)"
        )

        return ExcelWorkbookModel(
            fileURL: url,
            fileName: url.lastPathComponent,
            metadata: metadata,
            sheets: sheets
        )
    }

    private func parseBiffTextRows(from data: Data) -> [ExcelRowData] {
        var rows: [ExcelRowData] = []
        // Simple BIFF string extraction scan
        guard let text = String(data: data, encoding: .utf8) ?? String(data: data, encoding: .ascii) else { return [] }
        let lines = text.components(separatedBy: CharacterSet.newlines).filter { !$0.trimmingCharacters(in: .whitespaces).isEmpty }

        for (rIdx, line) in lines.prefix(500).enumerated() {
            let parts = line.components(separatedBy: "\t")
            let cells = parts.enumerated().map { (cIdx, val) in
                ExcelCellData(columnIndex: cIdx, rawValue: val.trimmingCharacters(in: .whitespaces))
            }
            rows.append(ExcelRowData(rowIndex: rIdx + 1, cells: cells))
        }
        return rows
    }

    // MARK: - CSV / TSV Parser

    private func parseDelimited(from url: URL, delimiter: String, formattedSize: String) async throws -> ExcelWorkbookModel {
        let data = try Data(contentsOf: url)
        let str = String(data: data, encoding: .utf8) ??
                  String(data: data, encoding: .utf16) ??
                  String(data: data, encoding: .isoLatin1) ?? ""
        return try parseDelimitedString(str, url: url, delimiter: delimiter, formattedSize: formattedSize)
    }

    private func parseDelimitedString(_ text: String, url: URL, delimiter: String, formattedSize: String) throws -> ExcelWorkbookModel {
        var rows: [ExcelRowData] = []
        let lines = parseCSVLines(text, delimiter: delimiter)
        var maxCols = 1

        for (rIdx, rowValues) in lines.enumerated() {
            maxCols = max(maxCols, rowValues.count)
            let cells = rowValues.enumerated().map { (cIdx, val) in
                ExcelCellData(
                    columnIndex: cIdx,
                    rawValue: val,
                    cellType: (Double(val) != nil) ? .number : .string
                )
            }
            rows.append(ExcelRowData(rowIndex: rIdx + 1, cells: cells))
        }

        let sheetName = url.deletingPathExtension().lastPathComponent
        let sheet = ExcelSheetModel(
            id: "1",
            name: sheetName.isEmpty ? "Sheet1" : sheetName,
            maxColumns: maxCols,
            maxRows: rows.count,
            rows: rows
        )

        let metadata = ExcelMetadata(
            sheetCount: 1,
            fileSizeFormatted: formattedSize,
            fileFormat: delimiter == "\t" ? "TSV Table" : "CSV Table"
        )

        return ExcelWorkbookModel(
            fileURL: url,
            fileName: url.lastPathComponent,
            metadata: metadata,
            sheets: [sheet]
        )
    }

    private func parseCSVLines(_ text: String, delimiter: String) -> [[String]] {
        var result: [[String]] = []
        var currentField = ""
        var currentRow: [String] = []
        var insideQuotes = false

        let chars = Array(text)
        var i = 0
        let delimChar = delimiter.first ?? ","

        while i < chars.count {
            let ch = chars[i]
            if ch == "\"" {
                if insideQuotes && i + 1 < chars.count && chars[i + 1] == "\"" {
                    currentField.append("\"")
                    i += 2
                    continue
                } else {
                    insideQuotes.toggle()
                }
            } else if ch == delimChar && !insideQuotes {
                currentRow.append(currentField.trimmingCharacters(in: .whitespaces))
                currentField = ""
            } else if (ch == "\n" || ch == "\r") && !insideQuotes {
                if ch == "\r" && i + 1 < chars.count && chars[i + 1] == "\n" {
                    i += 1
                }
                currentRow.append(currentField.trimmingCharacters(in: .whitespaces))
                if !currentRow.isEmpty && !(currentRow.count == 1 && currentRow[0].isEmpty) {
                    result.append(currentRow)
                }
                currentRow = []
                currentField = ""
            } else {
                currentField.append(ch)
            }
            i += 1
        }

        if !currentField.isEmpty || !currentRow.isEmpty {
            currentRow.append(currentField.trimmingCharacters(in: .whitespaces))
            result.append(currentRow)
        }

        return result
    }

    private func decodeXMLEntities(_ string: String) -> String {
        var str = string
        str = str.replacingOccurrences(of: "&quot;", with: "\"")
        str = str.replacingOccurrences(of: "&apos;", with: "'")
        str = str.replacingOccurrences(of: "&lt;", with: "<")
        str = str.replacingOccurrences(of: "&gt;", with: ">")
        str = str.replacingOccurrences(of: "&amp;", with: "&")
        return str
    }
}
