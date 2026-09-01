import XCTest
@testable import MacCompareKit

final class ExcelDiffTests: XCTestCase {

    func testColumnLetterConversion() {
        XCTAssertEqual(ExcelModelsHelper.columnLetter(for: 0), "A")
        XCTAssertEqual(ExcelModelsHelper.columnLetter(for: 1), "B")
        XCTAssertEqual(ExcelModelsHelper.columnLetter(for: 25), "Z")
        XCTAssertEqual(ExcelModelsHelper.columnLetter(for: 26), "AA")
        XCTAssertEqual(ExcelModelsHelper.columnLetter(for: 27), "AB")

        XCTAssertEqual(ExcelModelsHelper.columnIndex(from: "A"), 0)
        XCTAssertEqual(ExcelModelsHelper.columnIndex(from: "B"), 1)
        XCTAssertEqual(ExcelModelsHelper.columnIndex(from: "Z"), 25)
        XCTAssertEqual(ExcelModelsHelper.columnIndex(from: "AA"), 26)
        XCTAssertEqual(ExcelModelsHelper.columnIndex(from: "AB"), 27)
    }

    func testDelimitedParsing() async throws {
        let tempCSV = FileManager.default.temporaryDirectory.appendingPathComponent("test_\(UUID().uuidString).csv")
        let csvContent = """
        Qty,Value,Device,Package
        15k,30K,AT2630,AB36
        15k,50K,AT2640,AB2C
        """
        try csvContent.write(to: tempCSV, atomically: true, encoding: .utf8)
        defer { try? FileManager.default.removeItem(at: tempCSV) }

        let model = try await ExcelDocumentParser.shared.parseWorkbook(from: tempCSV)
        XCTAssertEqual(model.sheets.count, 1)
        let sheet = model.sheets[0]
        XCTAssertEqual(sheet.rows.count, 3)
        XCTAssertEqual(sheet.maxColumns, 4)

        let firstRow = sheet.rows[0]
        XCTAssertEqual(firstRow.cells[0].rawValue, "Qty")
        XCTAssertEqual(firstRow.cells[1].rawValue, "Value")
    }

    func testExcelDiffEngineLCSAlignment() async {
        let leftRows = [
            ExcelRowData(rowIndex: 1, cells: [ExcelCellData(columnIndex: 0, rawValue: "Qty"), ExcelCellData(columnIndex: 1, rawValue: "Value")]),
            ExcelRowData(rowIndex: 2, cells: [ExcelCellData(columnIndex: 0, rawValue: "15k"), ExcelCellData(columnIndex: 1, rawValue: "30K")]),
            ExcelRowData(rowIndex: 3, cells: [ExcelCellData(columnIndex: 0, rawValue: "10k"), ExcelCellData(columnIndex: 1, rawValue: "20K")])
        ]
        let rightRows = [
            ExcelRowData(rowIndex: 1, cells: [ExcelCellData(columnIndex: 0, rawValue: "Qty"), ExcelCellData(columnIndex: 1, rawValue: "Value")]),
            ExcelRowData(rowIndex: 2, cells: [ExcelCellData(columnIndex: 0, rawValue: "15k"), ExcelCellData(columnIndex: 1, rawValue: "35K")]), // Modified
            ExcelRowData(rowIndex: 3, cells: [ExcelCellData(columnIndex: 0, rawValue: "10k"), ExcelCellData(columnIndex: 1, rawValue: "20K")]),
            ExcelRowData(rowIndex: 4, cells: [ExcelCellData(columnIndex: 0, rawValue: "5k"), ExcelCellData(columnIndex: 1, rawValue: "10K")])   // Added
        ]

        let leftSheet = ExcelSheetModel(id: "1", name: "Sheet1", maxColumns: 2, maxRows: 3, rows: leftRows)
        let rightSheet = ExcelSheetModel(id: "1", name: "Sheet1", maxColumns: 2, maxRows: 4, rows: rightRows)

        let rules = ExcelCompareRules(firstRowAsHeader: true)
        let diffResult = ExcelDiffEngine.shared.compareSheets(left: leftSheet, right: rightSheet, rules: rules)

        XCTAssertEqual(diffResult.differenceRowCount, 2)
        XCTAssertEqual(diffResult.sameRowCount, 1)
        XCTAssertEqual(diffResult.status, .modified)
        XCTAssertEqual(diffResult.columnHeaders, ["Qty", "Value"])
    }

    func testExcelDiffToleranceAndCaseInsensitive() async {
        let leftRows = [
            ExcelRowData(rowIndex: 1, cells: [ExcelCellData(columnIndex: 0, rawValue: "RESISTOR"), ExcelCellData(columnIndex: 1, rawValue: "10.005")])
        ]
        let rightRows = [
            ExcelRowData(rowIndex: 1, cells: [ExcelCellData(columnIndex: 0, rawValue: "resistor"), ExcelCellData(columnIndex: 1, rawValue: "10.008")])
        ]

        let leftSheet = ExcelSheetModel(id: "1", name: "Sheet1", maxColumns: 2, maxRows: 1, rows: leftRows)
        let rightSheet = ExcelSheetModel(id: "1", name: "Sheet1", maxColumns: 2, maxRows: 1, rows: rightRows)

        // With tolerance and ignore case
        let rules = ExcelCompareRules(firstRowAsHeader: false, ignoreCase: true, ignoreWhitespace: true, numericTolerance: 0.01)
        let diffResult = ExcelDiffEngine.shared.compareSheets(left: leftSheet, right: rightSheet, rules: rules)

        XCTAssertEqual(diffResult.differenceRowCount, 0)
        XCTAssertEqual(diffResult.sameRowCount, 1)
        XCTAssertEqual(diffResult.status, .same)
    }
}
