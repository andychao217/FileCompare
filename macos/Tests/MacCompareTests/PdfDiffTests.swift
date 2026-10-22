import XCTest
@testable import MacCompareKit

final class PdfDiffTests: XCTestCase {

    func testPdfDiffEnginePageDiff() async {
        let leftPages = [
            PdfPageModel(pageIndex: 0, pageSize: CGSize(width: 595, height: 842), text: "Hello World Standard Text"),
            PdfPageModel(pageIndex: 1, pageSize: CGSize(width: 595, height: 842), text: "Section A: Terms and Conditions"),
            PdfPageModel(pageIndex: 2, pageSize: CGSize(width: 595, height: 842), text: "Page to be deleted")
        ]

        let rightPages = [
            PdfPageModel(pageIndex: 0, pageSize: CGSize(width: 595, height: 842), text: "Hello World Standard Text"),
            PdfPageModel(pageIndex: 1, pageSize: CGSize(width: 595, height: 842), text: "Section A: Modified Terms and Conditions"),
            PdfPageModel(pageIndex: 2, pageSize: CGSize(width: 595, height: 842), text: "Page to be deleted"),
            PdfPageModel(pageIndex: 3, pageSize: CGSize(width: 595, height: 842), text: "Brand new added page")
        ]

        let leftDoc = PdfDocumentModel(
            fileURL: URL(fileURLWithPath: "/tmp/left.pdf"),
            fileName: "left.pdf",
            pageCount: 3,
            pages: leftPages
        )

        let rightDoc = PdfDocumentModel(
            fileURL: URL(fileURLWithPath: "/tmp/right.pdf"),
            fileName: "right.pdf",
            pageCount: 4,
            pages: rightPages
        )

        let result = await PdfDiffEngine.shared.compareDocuments(left: leftDoc, right: rightDoc)

        XCTAssertEqual(result.blocks.count, 4)
        XCTAssertEqual(result.blocks[0].changeType, .unchanged)
        XCTAssertEqual(result.blocks[1].changeType, .modified)
        XCTAssertFalse(result.blocks[1].tokens.isEmpty)
        XCTAssertEqual(result.blocks[2].changeType, .unchanged)
        XCTAssertEqual(result.blocks[3].changeType, .inserted)
        XCTAssertEqual(result.totalDiffCount, 2)
    }

    func testPdfFileFormatValidation() {
        let pdfURL = URL(fileURLWithPath: "/Users/test/contract.pdf")

        // 1. In textDiff mode, dropping a pdf should suggest switching to pdfDiff
        let textResult = FileFormatValidator.validate(url: pdfURL, expectedMode: .textDiff)
        // Note: URL might not exist on disk in unit test, but let's check mock file
        XCTAssertFalse(textResult.isValid)
    }

    @MainActor
    func testPdfDiffViewModelNavigation() {
        let vm = PdfDiffViewModel()
        XCTAssertEqual(vm.currentPageIndex, 0)
        XCTAssertEqual(vm.zoomScale, 1.0)

        vm.zoomIn()
        XCTAssertEqual(vm.zoomScale, 1.2, accuracy: 0.01)

        vm.zoomOut()
        XCTAssertEqual(vm.zoomScale, 1.0, accuracy: 0.01)

        vm.resetZoom()
        XCTAssertEqual(vm.zoomScale, 1.0)
    }

    func testPixelDiffEngineWithSyntheticImages() {
        let width = 64
        let height = 64
        let colorSpace = CGColorSpaceCreateDeviceRGB()
        let bitmapInfo = CGImageAlphaInfo.premultipliedLast.rawValue

        // Create image A: pure white
        guard let ctxA = CGContext(
            data: nil,
            width: width,
            height: height,
            bitsPerComponent: 8,
            bytesPerRow: width * 4,
            space: colorSpace,
            bitmapInfo: bitmapInfo
        ) else {
            XCTFail("Failed to create context A")
            return
        }
        ctxA.setFillColor(CGColor(red: 1, green: 1, blue: 1, alpha: 1))
        ctxA.fill(CGRect(x: 0, y: 0, width: width, height: height))
        guard let imageA = ctxA.makeImage() else {
            XCTFail("Failed to make image A")
            return
        }

        // Compare identical images
        let identicalOutput = PdfPixelDiffEngine.shared.compareImages(
            left: imageA,
            right: imageA,
            pageIndex: 0,
            tolerance: 0.05
        )
        XCTAssertEqual(identicalOutput.result.diffPixelCount, 0)
        XCTAssertTrue(identicalOutput.result.diffBoundingBoxes.isEmpty)
        XCTAssertNil(identicalOutput.diffMaskImage)

        // Create image B: white with a black square
        guard let ctxB = CGContext(
            data: nil,
            width: width,
            height: height,
            bitsPerComponent: 8,
            bytesPerRow: width * 4,
            space: colorSpace,
            bitmapInfo: bitmapInfo
        ) else {
            XCTFail("Failed to create context B")
            return
        }
        ctxB.setFillColor(CGColor(red: 1, green: 1, blue: 1, alpha: 1))
        ctxB.fill(CGRect(x: 0, y: 0, width: width, height: height))
        ctxB.setFillColor(CGColor(red: 0, green: 0, blue: 0, alpha: 1))
        ctxB.fill(CGRect(x: 10, y: 10, width: 20, height: 20)) // 400 pixels difference
        guard let imageB = ctxB.makeImage() else {
            XCTFail("Failed to make image B")
            return
        }

        // Compare different images
        let diffOutput = PdfPixelDiffEngine.shared.compareImages(
            left: imageA,
            right: imageB,
            pageIndex: 0,
            tolerance: 0.05
        )
        XCTAssertGreaterThan(diffOutput.result.diffPixelCount, 300)
        XCTAssertFalse(diffOutput.result.diffBoundingBoxes.isEmpty)
        XCTAssertNotNil(diffOutput.diffMaskImage)
        XCTAssertGreaterThan(diffOutput.result.diffPercentage, 5.0)
    }

    func testPdfReportExporterHtmlGeneration() {
        let leftDoc = PdfDocumentModel(
            fileURL: URL(fileURLWithPath: "/tmp/contract_v1.pdf"),
            fileName: "contract_v1.pdf",
            pageCount: 1,
            pages: [PdfPageModel(pageIndex: 0, pageSize: CGSize(width: 595, height: 842), text: "Old Clause")]
        )
        let rightDoc = PdfDocumentModel(
            fileURL: URL(fileURLWithPath: "/tmp/contract_v2.pdf"),
            fileName: "contract_v2.pdf",
            pageCount: 1,
            pages: [PdfPageModel(pageIndex: 0, pageSize: CGSize(width: 595, height: 842), text: "New Clause")]
        )

        let block = PdfPageDiffBlock(
            leftPageIndex: 0,
            rightPageIndex: 0,
            changeType: .modified,
            tokens: [
                PdfDiffToken(text: "Old Clause", changeType: .deleted),
                PdfDiffToken(text: "New Clause", changeType: .inserted)
            ]
        )
        let diffResult = PdfDiffResult(blocks: [block], totalDiffCount: 1)

        let ai = AISummaryResult(
            modelName: "Gemma-3-1B",
            oneLineIntent: "Update payment terms",
            keyModifications: ["Changed Old Clause to New Clause"],
            conventionalCommit: "feat: update contract clauses",
            executionTimeSeconds: 0.45
        )

        // 1. Test Simplified Chinese & Dark Theme
        let chineseDarkHtml = PdfReportExporter.shared.generateHtmlReport(
            leftDoc: leftDoc,
            rightDoc: rightDoc,
            diffResult: diffResult,
            aiSummary: ai,
            language: .zhHans,
            theme: .dark
        )
        XCTAssertTrue(chineseDarkHtml.contains("lang=\"zh-CN\""))
        XCTAssertTrue(chineseDarkHtml.contains("data-theme=\"dark\""))
        XCTAssertTrue(chineseDarkHtml.contains("PDF 对比审阅报告"))
        XCTAssertTrue(chineseDarkHtml.contains("左侧原文件"))
        XCTAssertTrue(chineseDarkHtml.contains("contract_v1.pdf"))
        XCTAssertTrue(chineseDarkHtml.contains("Update payment terms"))
        XCTAssertTrue(chineseDarkHtml.contains("feat: update contract clauses"))
        XCTAssertTrue(chineseDarkHtml.contains("token-del"))
        XCTAssertTrue(chineseDarkHtml.contains("token-ins"))

        // 2. Test Japanese & Light Theme
        let japaneseLightHtml = PdfReportExporter.shared.generateHtmlReport(
            leftDoc: leftDoc,
            rightDoc: rightDoc,
            diffResult: diffResult,
            aiSummary: ai,
            language: .ja,
            theme: .light
        )
        XCTAssertTrue(japaneseLightHtml.contains("lang=\"ja\""))
        XCTAssertTrue(japaneseLightHtml.contains("data-theme=\"light\""))
        XCTAssertTrue(japaneseLightHtml.contains("PDF 比較レビューレポート"))
        XCTAssertTrue(japaneseLightHtml.contains("左側の元ファイル"))

        // 3. Test English & Auto/System Theme
        let englishAutoHtml = PdfReportExporter.shared.generateHtmlReport(
            leftDoc: leftDoc,
            rightDoc: rightDoc,
            diffResult: diffResult,
            aiSummary: ai,
            language: .en,
            theme: .system
        )
        XCTAssertTrue(englishAutoHtml.contains("lang=\"en\""))
        XCTAssertTrue(englishAutoHtml.contains("data-theme=\"auto\""))
        XCTAssertTrue(englishAutoHtml.contains("PDF Comparison Report"))
        XCTAssertTrue(englishAutoHtml.contains("Left Original File"))
    }

    func testRealWorkReportDiff() async throws {
        let p1 = "/Users/andychao217/Works/FileCompare/test_files/工作日报.pdf"
        let p2 = "/Users/andychao217/Works/FileCompare/test_files/工作日报1.pdf"
        guard FileManager.default.fileExists(atPath: p1), FileManager.default.fileExists(atPath: p2) else { return }

        let doc1 = try await PdfDocumentParser.shared.parseDocument(from: URL(fileURLWithPath: p1))
        let doc2 = try await PdfDocumentParser.shared.parseDocument(from: URL(fileURLWithPath: p2))
        let diff = await PdfDiffEngine.shared.compareDocuments(left: doc1, right: doc2)

        XCTAssertEqual(diff.blocks.count, 2)
        XCTAssertEqual(diff.blocks[0].changeType, PdfDiffChangeType.modified)
        XCTAssertEqual(diff.blocks[1].changeType, PdfDiffChangeType.unchanged)

        let tokens = diff.blocks[0].tokens
        let added = tokens.filter { $0.changeType == PdfDiffChangeType.inserted }.map { $0.text }
        let deleted = tokens.filter { $0.changeType == PdfDiffChangeType.deleted }.map { $0.text }

        // Must accurately detect "22" added
        XCTAssertTrue(added.contains(where: { $0.contains("22") }), "Added tokens should include 22, got: \(added)")
        // Must accurately detect "是" added to "工作日报"
        XCTAssertTrue(added.contains(where: { $0.contains("是") }), "Added tokens should include 是, got: \(added)")
        // Must accurately detect "2" added
        XCTAssertTrue(added.contains(where: { $0.contains("2") }), "Added tokens should include 2, got: \(added)")
        // Must accurately detect deleted "项" or "作事项"
        XCTAssertTrue(deleted.contains(where: { $0.contains("项") || $0.contains("作事项") }), "Deleted tokens should include 项, got: \(deleted)")

        let html = PdfReportExporter.shared.generateHtmlReport(
            leftDoc: doc1,
            rightDoc: doc2,
            diffResult: diff,
            visualDiff: PdfVisualDiffResult(pageIndex: 0, diffPixelCount: 15000, totalPixelCount: 60000),
            language: .zhHans,
            theme: .dark
        )
        try html.write(toFile: "/tmp/work_report_diff.html", atomically: true, encoding: .utf8)
        XCTAssertTrue(html.contains("22"))
        XCTAssertTrue(html.contains("工作日报"))
        XCTAssertTrue(html.contains("图像与排版变动"))
    }

    @MainActor
    func testPdfRecentHistoryAndRegionNavigation() async throws {
        let p1 = "/Users/andychao217/Works/FileCompare/test_files/工作日报.pdf"
        let p2 = "/Users/andychao217/Works/FileCompare/test_files/工作日报1.pdf"
        guard FileManager.default.fileExists(atPath: p1), FileManager.default.fileExists(atPath: p2) else { return }

        let u1 = URL(fileURLWithPath: p1)
        let u2 = URL(fileURLWithPath: p2)

        let vm = PdfDiffViewModel(leftURL: u1, rightURL: u2)
        // Wait for diff calculation to finish
        try? await Task.sleep(nanoseconds: 300_000_000)

        // Verify recent history record added
        let records = RecentHistoryManager.shared.records
        XCTAssertTrue(records.contains { $0.type == .pdfDiff && $0.leftPath == p1 && $0.rightPath == p2 })

        // Verify region navigation and status text
        let regions = vm.currentSortedRegions
        XCTAssertEqual(regions.count, 3)
        XCTAssertEqual(vm.currentRegionIndex, 0)
        XCTAssertTrue(vm.currentDiffStatusText.contains("1 / 3") || vm.currentDiffStatusText.contains("1/3"))

        vm.nextDiff()
        XCTAssertEqual(vm.currentRegionIndex, 1)
        XCTAssertTrue(vm.currentDiffStatusText.contains("2 / 3") || vm.currentDiffStatusText.contains("2/3"))

        vm.nextDiff()
        XCTAssertEqual(vm.currentRegionIndex, 2)
        XCTAssertTrue(vm.currentDiffStatusText.contains("3 / 3") || vm.currentDiffStatusText.contains("3/3"))

        vm.prevDiff()
        XCTAssertEqual(vm.currentRegionIndex, 1)
    }
}
