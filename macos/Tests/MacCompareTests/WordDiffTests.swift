import XCTest
@testable import MacCompareKit

final class WordDiffTests: XCTestCase {
    var tempDirectory: URL!

    override func setUp() {
        super.setUp()
        tempDirectory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try? FileManager.default.createDirectory(at: tempDirectory, withIntermediateDirectories: true)
    }

    override func tearDown() {
        try? FileManager.default.removeItem(at: tempDirectory)
        super.tearDown()
    }

    func testParagraphAlignmentAndDiff() async {
        let leftParas = [
            WordParagraph(index: 1, text: "Chapter 1: Introduction", runs: [WordTextRun(text: "Chapter 1: Introduction", isBold: true)]),
            WordParagraph(index: 2, text: "This is the first version of the text."),
            WordParagraph(index: 3, text: "This paragraph will be deleted.")
        ]

        let rightParas = [
            WordParagraph(index: 1, text: "Chapter 1: Introduction", runs: [WordTextRun(text: "Chapter 1: Introduction", isBold: true)]),
            WordParagraph(index: 2, text: "This is the second revised version of the text."),
            WordParagraph(index: 3, text: "This is a brand new added paragraph.")
        ]

        let leftDoc = WordDocumentModel(fileURL: URL(fileURLWithPath: "/test/left.docx"), fileName: "left.docx", paragraphs: leftParas)
        let rightDoc = WordDocumentModel(fileURL: URL(fileURLWithPath: "/test/right.docx"), fileName: "right.docx", paragraphs: rightParas)

        let result = await WordDiffEngine.shared.compareDocuments(left: leftDoc, right: rightDoc)

        XCTAssertEqual(result.blocks.count, 4)
        XCTAssertEqual(result.blocks[0].changeType, .unchanged)
        XCTAssertEqual(result.blocks[1].changeType, .modified)
        XCTAssertFalse(result.blocks[1].tokensLeft.isEmpty)
        XCTAssertFalse(result.blocks[1].tokensRight.isEmpty)

        XCTAssertEqual(result.blocks[2].changeType, .deleted)
        XCTAssertNil(result.blocks[2].rightParagraph)

        XCTAssertEqual(result.blocks[3].changeType, .added)
        XCTAssertNil(result.blocks[3].leftParagraph)

        XCTAssertEqual(result.totalModifications, 1)
        XCTAssertEqual(result.totalDeletions, 1)
        XCTAssertEqual(result.totalAdditions, 1)
    }

    func testFormattingDifferenceDetection() async {
        let leftPara = WordParagraph(
            index: 1,
            text: "Important Clause",
            runs: [WordTextRun(text: "Important Clause", isBold: false, fontSize: 12.0, fontColorHex: "#000000")]
        )

        let rightPara = WordParagraph(
            index: 1,
            text: "Important Clause",
            runs: [WordTextRun(text: "Important Clause", isBold: true, fontSize: 14.0, fontColorHex: "#FF0000")]
        )

        let leftDoc = WordDocumentModel(fileURL: URL(fileURLWithPath: "/test/doc1.docx"), fileName: "doc1.docx", paragraphs: [leftPara])
        let rightDoc = WordDocumentModel(fileURL: URL(fileURLWithPath: "/test/doc2.docx"), fileName: "doc2.docx", paragraphs: [rightPara])

        let result = await WordDiffEngine.shared.compareDocuments(left: leftDoc, right: rightDoc, ignoreFormatting: false)

        XCTAssertEqual(result.blocks.count, 1)
        let block = result.blocks[0]
        XCTAssertEqual(block.changeType, .modified)
        XCTAssertTrue(block.isFormatOnly)
        XCTAssertEqual(block.formatDifferences.count, 3)

        let propNames = Set(block.formatDifferences.map { $0.propertyName })
        XCTAssertTrue(propNames.contains("Bold"))
        XCTAssertTrue(propNames.contains("Font Size"))
        XCTAssertTrue(propNames.contains("Font Color"))
        XCTAssertEqual(result.totalFormatChanges, 1)
    }

    func testTableDiffGrid() async {
        let leftRows = [
            WordTableRow(rowIndex: 0, cells: [
                WordTableCell(rowIndex: 0, columnIndex: 0, text: "Item"),
                WordTableCell(rowIndex: 0, columnIndex: 1, text: "Price")
            ]),
            WordTableRow(rowIndex: 1, cells: [
                WordTableCell(rowIndex: 1, columnIndex: 0, text: "Widget A"),
                WordTableCell(rowIndex: 1, columnIndex: 1, text: "$100")
            ])
        ]

        let rightRows = [
            WordTableRow(rowIndex: 0, cells: [
                WordTableCell(rowIndex: 0, columnIndex: 0, text: "Item"),
                WordTableCell(rowIndex: 0, columnIndex: 1, text: "Price")
            ]),
            WordTableRow(rowIndex: 1, cells: [
                WordTableCell(rowIndex: 1, columnIndex: 0, text: "Widget A"),
                WordTableCell(rowIndex: 1, columnIndex: 1, text: "$120") // Modified price
            ]),
            WordTableRow(rowIndex: 2, cells: [
                WordTableCell(rowIndex: 2, columnIndex: 0, text: "Widget B"), // Added row
                WordTableCell(rowIndex: 2, columnIndex: 1, text: "$200")
            ])
        ]

        let leftTable = WordTable(tableIndex: 1, rows: leftRows)
        let rightTable = WordTable(tableIndex: 1, rows: rightRows)

        let leftDoc = WordDocumentModel(fileURL: URL(fileURLWithPath: "/test/l.docx"), fileName: "l.docx", tables: [leftTable])
        let rightDoc = WordDocumentModel(fileURL: URL(fileURLWithPath: "/test/r.docx"), fileName: "r.docx", tables: [rightTable])

        let result = await WordDiffEngine.shared.compareDocuments(left: leftDoc, right: rightDoc)

        XCTAssertEqual(result.tableDiffs.count, 1)
        let tblDiff = result.tableDiffs[0]
        XCTAssertEqual(tblDiff.maxRows, 3)
        XCTAssertEqual(tblDiff.maxCols, 2)
        XCTAssertEqual(tblDiff.changeType, .modified)

        let modifiedCell = tblDiff.cellDiffs.first(where: { $0.rowIndex == 1 && $0.colIndex == 1 })
        XCTAssertEqual(modifiedCell?.changeType, .modified)
        XCTAssertEqual(modifiedCell?.leftCell?.text, "$100")
        XCTAssertEqual(modifiedCell?.rightCell?.text, "$120")

        let addedCell = tblDiff.cellDiffs.first(where: { $0.rowIndex == 2 && $0.colIndex == 0 })
        XCTAssertEqual(addedCell?.changeType, .added)
        XCTAssertEqual(addedCell?.rightCell?.text, "Widget B")
    }

    @MainActor
    func testWordDiffViewModelAndReportExport() async {
        let leftParas = [WordParagraph(index: 1, text: "First paragraph in document.")]
        let rightParas = [WordParagraph(index: 1, text: "First modified paragraph in document.")]

        let leftDoc = WordDocumentModel(fileURL: URL(fileURLWithPath: "/test/contract_v1.docx"), fileName: "contract_v1.docx", paragraphs: leftParas)
        let rightDoc = WordDocumentModel(fileURL: URL(fileURLWithPath: "/test/contract_v2.docx"), fileName: "contract_v2.docx", paragraphs: rightParas)

        let viewModel = WordDiffViewModel()
        viewModel.leftDocument = leftDoc
        viewModel.rightDocument = rightDoc

        viewModel.recomputeDiff()
        try? await Task.sleep(nanoseconds: 100_000_000)

        XCTAssertEqual(viewModel.diffResult.blocks.count, 1)
        XCTAssertEqual(viewModel.diffResult.blocks[0].changeType, .modified)

        let htmlReport = viewModel.exportHTMLReport()
        XCTAssertTrue(htmlReport.contains("Word Document Comparison Report"))
        XCTAssertTrue(htmlReport.contains("contract_v1.docx"))
        XCTAssertTrue(htmlReport.contains("contract_v2.docx"))
        XCTAssertTrue(htmlReport.contains("First paragraph"))
        XCTAssertTrue(htmlReport.contains("First modified paragraph"))
    }

    func testRealAssetsDocxDiff() async throws {
        let u1 = URL(fileURLWithPath: "/Users/andychao217/Works/FileCompare/macos/Assets/工作日报.docx")
        let u2 = URL(fileURLWithPath: "/Users/andychao217/Works/FileCompare/macos/Assets/工作日报1.docx")

        guard FileManager.default.fileExists(atPath: u1.path),
              FileManager.default.fileExists(atPath: u2.path) else {
            return
        }

        let d1 = try await WordDocumentParser.shared.parseDocument(from: u1)
        let d2 = try await WordDocumentParser.shared.parseDocument(from: u2)

        let diff = await WordDiffEngine.shared.compareDocuments(left: d1, right: d2)

        XCTAssertEqual(diff.blocks.count, 23)
        
        // Block 0: Embedded Table Grid (3 rows x 4 cols)
        XCTAssertTrue(diff.blocks[0].isTableBlock)
        XCTAssertNotNil(diff.blocks[0].tableDiff)

        // Block 1: '工作日报' vs '工作日报是' -> Modified
        XCTAssertEqual(diff.blocks[1].changeType, .modified)
        XCTAssertEqual(diff.blocks[1].leftParagraph?.text, "工作日报")
        XCTAssertEqual(diff.blocks[1].rightParagraph?.text, "工作日报是")

        // Block 4: Vector Shape Block
        XCTAssertTrue(diff.blocks[4].leftParagraph?.mediaItems.contains { $0.mediaType == .shape } ?? false)
    }

    func testMediaDiffDetectionAndImageHash() async {
        let dummyImg1 = "PNG_DATA_VERSION_1".data(using: .utf8)!
        let dummyImg2 = "PNG_DATA_VERSION_2".data(using: .utf8)!

        let leftMedia = WordMediaItem(
            mediaType: .image,
            fileName: "architecture.png",
            fileExtension: "png",
            fileSize: dummyImg1.count,
            hashSHA256: "hash_v1",
            data: dummyImg1,
            widthPoints: 200,
            heightPoints: 150,
            paragraphIndex: 1
        )

        let rightMedia = WordMediaItem(
            mediaType: .image,
            fileName: "architecture.png",
            fileExtension: "png",
            fileSize: dummyImg2.count,
            hashSHA256: "hash_v2",
            data: dummyImg2,
            widthPoints: 300,
            heightPoints: 200,
            paragraphIndex: 1
        )

        let leftPara = WordParagraph(index: 1, text: "System Architecture", mediaItems: [leftMedia])
        let rightPara = WordParagraph(index: 1, text: "System Architecture", mediaItems: [rightMedia])

        let leftDoc = WordDocumentModel(fileURL: URL(fileURLWithPath: "/test/doc1.docx"), fileName: "doc1.docx", paragraphs: [leftPara])
        let rightDoc = WordDocumentModel(fileURL: URL(fileURLWithPath: "/test/doc2.docx"), fileName: "doc2.docx", paragraphs: [rightPara])

        let diff = await WordDiffEngine.shared.compareDocuments(left: leftDoc, right: rightDoc)

        XCTAssertEqual(diff.blocks.count, 1)
        let block = diff.blocks[0]
        XCTAssertEqual(block.changeType, .modified)
        XCTAssertEqual(block.mediaDifferences.count, 1)

        let mediaDiff = block.mediaDifferences[0]
        XCTAssertEqual(mediaDiff.changeType, .modified)
        XCTAssertEqual(mediaDiff.mediaType, .image)
        XCTAssertTrue(mediaDiff.changeDescriptions.contains { $0.contains("尺寸") })
        XCTAssertEqual(diff.totalMediaChanges, 1)
    }
}
