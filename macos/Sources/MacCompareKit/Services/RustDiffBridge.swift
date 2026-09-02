import Foundation
import CMacCompareCore

// MARK: - Big-Data Excel Diff Models from Rust Engine

public struct RustExcelSheetSummary: Codable, Sendable {
    public let sheetNameLeft: String
    public let sheetNameRight: String
    public let totalRows: Int
    public let totalColumns: Int
    public let differenceRowCount: Int
    public let equalRowCount: Int
    public let leftOnlyRowCount: Int
    public let rightOnlyRowCount: Int
    public let diffRowIndices: [Int]

    enum CodingKeys: String, CodingKey {
        case sheetNameLeft = "sheet_name_left"
        case sheetNameRight = "sheet_name_right"
        case totalRows = "total_rows"
        case totalColumns = "total_columns"
        case differenceRowCount = "difference_row_count"
        case equalRowCount = "equal_row_count"
        case leftOnlyRowCount = "left_only_row_count"
        case rightOnlyRowCount = "right_only_row_count"
        case diffRowIndices = "diff_row_indices"
    }
}

public struct RustExcelWorkbookSummary: Codable, Sendable {
    public let sheetSummaries: [RustExcelSheetSummary]
    public let totalDifferences: Int
    public let durationMs: UInt64

    enum CodingKeys: String, CodingKey {
        case sheetSummaries = "sheet_summaries"
        case totalDifferences = "total_differences"
        case durationMs = "duration_ms"
    }
}

public struct RustExcelCellData: Codable, Sendable {
    public let columnIndex: Int
    public let columnLetter: String
    public let rawValue: String
    public let formattedValue: String
    public let formula: String?
    public let cellType: String

    enum CodingKeys: String, CodingKey {
        case columnIndex = "column_index"
        case columnLetter = "column_letter"
        case rawValue = "raw_value"
        case formattedValue = "formatted_value"
        case formula
        case cellType = "cell_type"
    }
}

public struct RustCellDiffDetail: Codable, Sendable {
    public let columnIndex: Int
    public let columnLetter: String
    public let leftCell: RustExcelCellData?
    public let rightCell: RustExcelCellData?
    public let status: String
    public let diffReason: String?

    enum CodingKeys: String, CodingKey {
        case columnIndex = "column_index"
        case columnLetter = "column_letter"
        case leftCell = "left_cell"
        case rightCell = "right_cell"
        case status
        case diffReason = "diff_reason"
    }
}

public struct RustExcelRowDiff: Codable, Sendable {
    public let rowIndexLeft: Int?
    public let rowIndexRight: Int?
    public let status: String
    public let cellDiffs: [RustCellDiffDetail]
    public let hasDiff: Bool

    enum CodingKeys: String, CodingKey {
        case rowIndexLeft = "row_index_left"
        case rowIndexRight = "row_index_right"
        case status
        case cellDiffs = "cell_diffs"
        case hasDiff = "has_diff"
    }
}

// MARK: - Big-Data Excel Diff Session

/// Big-Data Excel Diff Session managed by Rust engine with Viewport Paging.
public final class RustExcelSession: @unchecked Sendable {
    private var rawPointer: UnsafeMutableRawPointer?

    public init?(leftPath: String, rightPath: String, rules: ExcelCompareRules = ExcelCompareRules()) {
        guard RustDiffBridge.isAvailable else { return nil }

        struct RustRulesPayload: Codable {
            let key_columns: [String]
            let numeric_tolerance: Double
            let ignore_case: Bool
            let ignore_whitespace: Bool
            let ignore_empty_rows: Bool
            let compare_formulas: Bool
        }

        let payload = RustRulesPayload(
            key_columns: rules.keyColumnIndices.map { String($0) },
            numeric_tolerance: rules.numericTolerance,
            ignore_case: rules.ignoreCase,
            ignore_whitespace: rules.ignoreWhitespace,
            ignore_empty_rows: true,
            compare_formulas: false
        )

        let jsonString = (try? JSONEncoder().encode(payload)).flatMap { String(data: $0, encoding: .utf8) } ?? "{}"

        let ptr = leftPath.withCString { leftPtr in
            rightPath.withCString { rightPtr in
                jsonString.withCString { rulesPtr in
                    maccompare_excel_create_session(leftPtr, rightPtr, rulesPtr)
                }
            }
        }

        guard let validPtr = ptr else { return nil }
        self.rawPointer = validPtr
    }

    deinit {
        if let ptr = rawPointer {
            maccompare_excel_free_session(ptr)
            rawPointer = nil
        }
    }

    /// Fetches lightweight summary and difference row distribution bitmap.
    public func getSummary() -> RustExcelWorkbookSummary? {
        guard let ptr = rawPointer else { return nil }
        guard let cStr = maccompare_excel_get_summary(ptr) else { return nil }
        defer { free_rust_string(cStr) }
        let jsonString = String(cString: cStr)
        guard let data = jsonString.data(using: .utf8) else { return nil }
        return try? JSONDecoder().decode(RustExcelWorkbookSummary.self, from: data)
    }

    /// Fetches a small page of aligned rows for viewport rendering.
    public func getViewport(sheetIndex: Int, startRow: Int, count: Int) -> [RustExcelRowDiff]? {
        guard let ptr = rawPointer else { return nil }
        guard let cStr = maccompare_excel_get_viewport_json(ptr, sheetIndex, startRow, count) else { return nil }
        defer { free_rust_string(cStr) }
        let jsonString = String(cString: cStr)
        guard let data = jsonString.data(using: .utf8) else { return nil }
        return try? JSONDecoder().decode([RustExcelRowDiff].self, from: data)
    }

    /// O(log N) jump to next/previous diff row index.
    public func findNextDiffRow(sheetIndex: Int, currentRow: Int, backwards: Bool = false) -> Int? {
        guard let ptr = rawPointer else { return nil }
        let res = maccompare_excel_find_next_diff_row(ptr, sheetIndex, currentRow, backwards)
        return res >= 0 ? Int(res) : nil
    }
}

// MARK: - RustDiffBridge

/// High-performance FFI Bridge to Rust core diff engine.
public enum RustDiffBridge: Sendable {
    /// Checks if Rust Core Engine is available and operational.
    public static var isAvailable: Bool {
        #if canImport(CMacCompareCore)
        return maccompare_is_available()
        #else
        return false
        #endif
    }

    /// Returns Rust Core version string.
    public static var version: String {
        guard isAvailable else { return "Unavailable" }
        guard let cStr = maccompare_version() else { return "Unknown" }
        defer { free_rust_string(cStr) }
        return String(cString: cStr)
    }

    /// Performs 2-way text diff via Rust Myers algorithm.
    public static func compareText(
        left: String,
        right: String,
        ignoreWhitespace: Bool = false,
        ignoreCase: Bool = false
    ) -> TextDiffResult? {
        guard isAvailable else { return nil }

        return left.withCString { leftPtr in
            right.withCString { rightPtr in
                guard let jsonPtr = maccompare_compare_text_json(leftPtr, rightPtr, ignoreWhitespace, ignoreCase) else {
                    return nil
                }
                defer { free_rust_string(jsonPtr) }
                let jsonString = String(cString: jsonPtr)
                guard let data = jsonString.data(using: .utf8) else { return nil }
                return try? JSONDecoder().decode(TextDiffResult.self, from: data)
            }
        }
    }

    /// Performs 3-way merge via Rust engine.
    public static func mergeThreeWay(
        local: String,
        base: String,
        remote: String
    ) -> MergeResult? {
        guard isAvailable else { return nil }

        return local.withCString { localPtr in
            base.withCString { basePtr in
                remote.withCString { remotePtr in
                    guard let jsonPtr = maccompare_merge_three_way_json(localPtr, basePtr, remotePtr) else {
                        return nil
                    }
                    defer { free_rust_string(jsonPtr) }
                    let jsonString = String(cString: jsonPtr)
                    guard let data = jsonString.data(using: .utf8) else { return nil }
                    return try? JSONDecoder().decode(MergeResult.self, from: data)
                }
            }
        }
    }

    /// Performs directory comparison via Rust parallel scanner (rayon + crc32fast).
    public static func compareFolders(
        leftPath: String,
        rightPath: String,
        mode: Int,
        excludePatterns: [String] = []
    ) -> [FolderDiffEntry]? {
        guard isAvailable else { return nil }

        let excludeJsonData = try? JSONEncoder().encode(excludePatterns)
        let excludeJsonString = excludeJsonData.flatMap { String(data: $0, encoding: .utf8) }

        return leftPath.withCString { leftPtr in
            rightPath.withCString { rightPtr in
                let performCall = { (exPtr: UnsafePointer<CChar>?) -> [FolderDiffEntry]? in
                    guard let jsonPtr = maccompare_compare_folders_json(leftPtr, rightPtr, Int32(mode), exPtr) else {
                        return nil
                    }
                    defer { free_rust_string(jsonPtr) }
                    let jsonString = String(cString: jsonPtr)
                    guard let data = jsonString.data(using: .utf8) else { return nil }
                    return try? JSONDecoder().decode([FolderDiffEntry].self, from: data)
                }

                if let excludeJsonString = excludeJsonString {
                    return excludeJsonString.withCString { exPtr in
                        performCall(exPtr)
                    }
                } else {
                    return performCall(nil)
                }
            }
        }
    }

    /// Creates a Big-Data Excel/CSV Diff Session.
    public static func createExcelSession(
        leftPath: String,
        rightPath: String,
        rules: ExcelCompareRules = ExcelCompareRules()
    ) -> RustExcelSession? {
        RustExcelSession(leftPath: leftPath, rightPath: rightPath, rules: rules)
    }
}
