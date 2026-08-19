import Foundation
import SwiftUI

public enum FolderViewMode: String, CaseIterable, Identifiable, Sendable {
    case quick = "Quick Compare (Timestamp/Size)"
    case deepHash = "Deep Hash Compare (CRC32)"

    public var id: String { rawValue }
}

public struct AlignedFolderRow: Identifiable, Sendable {
    public var id: String { relativePath }
    public let relativePath: String
    public let name: String
    public let isDirectory: Bool
    public let status: FolderItemStatus

    public let leftSizeFormatted: String
    public let leftModifiedFormatted: String
    public let rightSizeFormatted: String
    public let rightModifiedFormatted: String

    public var isLeftMissing: Bool { status == .rightOnly }
    public var isRightMissing: Bool { status == .leftOnly }
}

@MainActor
@Observable
public final class FolderDiffViewModel {
    public var leftFolderPath: String = "Source Folder (with SF Symbols)"
    public var rightFolderPath: String = "Remote Target Folder"

    public var selectedMode: FolderViewMode = .quick
    public var filterRulesSummary: String = "*.swift, exclude node_modules"
    public var isDryRunPresented: Bool = false

    public var entries: [AlignedFolderRow] = []
    public var totalScanned: Int = 1420
    public var modifiedCount: Int = 18
    public var addedCount: Int = 5
    public var deletedCount: Int = 2

    private let diffEngine: DiffEngineProtocol

    public init(diffEngine: DiffEngineProtocol = DiffEngineService.shared) {
        self.diffEngine = diffEngine
        loadSampleData()
    }

    public func loadSampleData() {
        self.entries = [
            AlignedFolderRow(
                relativePath: "blts",
                name: "blts",
                isDirectory: true,
                status: .equal,
                leftSizeFormatted: "",
                leftModifiedFormatted: "Nov 3, 2021 at 22:35",
                rightSizeFormatted: "",
                rightModifiedFormatted: "Nov 3, 2021 at 12:38"
            ),
            AlignedFolderRow(
                relativePath: "source/config",
                name: "config",
                isDirectory: true,
                status: .equal,
                leftSizeFormatted: "",
                leftModifiedFormatted: "Nov 3, 2021 at 22:35",
                rightSizeFormatted: "",
                rightModifiedFormatted: "Nov 3, 2021 at 12:30"
            ),
            AlignedFolderRow(
                relativePath: "source/config/.gitigmode.swift",
                name: ".gitigmode.swift",
                isDirectory: false,
                status: .equal,
                leftSizeFormatted: "1.5 KB",
                leftModifiedFormatted: "Nov 3, 2021 at 22:35",
                rightSizeFormatted: "1.5 KB",
                rightModifiedFormatted: "Nov 3, 2021 at 12:39"
            ),
            AlignedFolderRow(
                relativePath: "source/config/.gitignore.swift",
                name: ".gitignore.swift",
                isDirectory: false,
                status: .contentDifferent,
                leftSizeFormatted: "159 KB",
                leftModifiedFormatted: "Nov 3, 2021 at 22:35",
                rightSizeFormatted: "153 KB",
                rightModifiedFormatted: "Dec 3, 2021 at 12:37"
            ),
            AlignedFolderRow(
                relativePath: "source/config/macCompare.swift",
                name: "macCompare.swift",
                isDirectory: false,
                status: .contentDifferent,
                leftSizeFormatted: "15.4 KB",
                leftModifiedFormatted: "Dec 3, 2021 at 20:56",
                rightSizeFormatted: "---",
                rightModifiedFormatted: "---"
            ),
            AlignedFolderRow(
                relativePath: "source/config/licensed.swift",
                name: "licensed.swift",
                isDirectory: false,
                status: .leftOnly,
                leftSizeFormatted: "3.3 KB",
                leftModifiedFormatted: "Dec 3, 2021 at 13:38",
                rightSizeFormatted: "---",
                rightModifiedFormatted: "---"
            ),
            AlignedFolderRow(
                relativePath: "source/config/result.swift",
                name: "result.swift",
                isDirectory: false,
                status: .rightOnly,
                leftSizeFormatted: "---",
                leftModifiedFormatted: "---",
                rightSizeFormatted: "---",
                rightModifiedFormatted: "---"
            )
        ]
    }

    public func syncLeftToRight() {
        isDryRunPresented = true
    }

    public func syncRightToLeft() {
        isDryRunPresented = true
    }
}
