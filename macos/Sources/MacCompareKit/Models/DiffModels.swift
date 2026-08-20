import Foundation

/// Supported text encodings for reading and writing files.
public enum FileEncoding: String, CaseIterable, Identifiable, Sendable {
    case utf8 = "UTF-8"
    case utf16 = "UTF-16"
    case gbk = "GBK"
    case ascii = "ASCII"
    case isoLatin1 = "ISO-8859-1"

    public var id: String { rawValue }

    public var stringEncoding: String.Encoding {
        switch self {
        case .utf8: return .utf8
        case .utf16: return .utf16
        case .gbk: return String.Encoding(rawValue: CFStringConvertEncodingToNSStringEncoding(CFStringEncoding(CFStringEncodings.GB_18030_2000.rawValue)))
        case .ascii: return .ascii
        case .isoLatin1: return .isoLatin1
        }
    }
}

public extension Notification.Name {
    static let mcNewTextCompare = Notification.Name("MCNewTextCompare")
    static let mcNewFolderCompare = Notification.Name("MCNewFolderCompare")
    static let mcNewThreeWayMerge = Notification.Name("MCNewThreeWayMerge")
    static let mcOpenFile = Notification.Name("MCOpenFile")
    static let mcSaveActive = Notification.Name("MCSaveActive")
    static let mcCloseActiveTab = Notification.Name("MCCloseActiveTab")
    static let mcNextDiff = Notification.Name("MCNextDiff")
    static let mcPrevDiff = Notification.Name("MCPrevDiff")
    static let mcTakeLeft = Notification.Name("MCTakeLeft")
    static let mcTakeRight = Notification.Name("MCTakeRight")
    static let mcToggleIgnoreWhitespace = Notification.Name("MCToggleIgnoreWhitespace")
    static let mcToggleIgnoreCase = Notification.Name("MCToggleIgnoreCase")
    static let mcOpenSettings = Notification.Name("MCOpenSettings")
    static let mcMoveTabToNewWindow = Notification.Name("MCMoveTabToNewWindow")
    static let mcMergeAllWindows = Notification.Name("MCMergeAllWindows")
    static let mcNewWindow = Notification.Name("MCNewWindow")
    static let mcOpenHelp = Notification.Name("MCOpenHelp")
    static let mcCheckForUpdates = Notification.Name("MCCheckForUpdates")
}

/// Type of change for line or token diff.
public enum ChangeType: String, Codable, Sendable {
    case unchanged = "Unchanged"
    case added = "Added"
    case deleted = "Deleted"
    case modified = "Modified"
}

/// Token or character-level fine difference within a line.
public struct DiffToken: Codable, Sendable, Identifiable {
    public var id: String { "\(startOffset)-\(length)-\(changeType.rawValue)" }
    public let startOffset: UInt32
    public let length: UInt32
    public let changeType: ChangeType

    public init(startOffset: UInt32, length: UInt32, changeType: ChangeType) {
        self.startOffset = startOffset
        self.length = length
        self.changeType = changeType
    }

    enum CodingKeys: String, CodingKey {
        case startOffset = "start_offset"
        case length
        case changeType = "change_type"
    }
}

/// A contiguous group of changed lines (Hunk).
public struct DiffHunk: Codable, Identifiable, Sendable {
    public let id: Int
    public let startLineIndex: Int
    public let lineCount: Int
    public let changeType: ChangeType

    public init(id: Int, startLineIndex: Int, lineCount: Int, changeType: ChangeType) {
        self.id = id
        self.startLineIndex = startLineIndex
        self.lineCount = lineCount
        self.changeType = changeType
    }

    enum CodingKeys: String, CodingKey {
        case id
        case startLineIndex = "start_line_index"
        case lineCount = "line_count"
        case changeType = "change_type"
    }
}

/// A visual diff line in a 2-way diff view.
public struct DiffLine: Codable, Sendable, Identifiable {
    public var id: String {
        "\(leftLineNumber?.description ?? "none")-\(rightLineNumber?.description ?? "none")-\(changeType.rawValue)"
    }
    public let leftLineNumber: UInt32?
    public let rightLineNumber: UInt32?
    public var contentLeft: String
    public var contentRight: String
    public var changeType: ChangeType
    public var tokensLeft: [DiffToken]
    public var tokensRight: [DiffToken]
    public var hunkIndex: Int?

    public var isPhantomLeft: Bool { leftLineNumber == nil }
    public var isPhantomRight: Bool { rightLineNumber == nil }

    public init(
        leftLineNumber: UInt32?,
        rightLineNumber: UInt32?,
        contentLeft: String,
        contentRight: String,
        changeType: ChangeType,
        tokensLeft: [DiffToken] = [],
        tokensRight: [DiffToken] = [],
        hunkIndex: Int? = nil
    ) {
        self.leftLineNumber = leftLineNumber
        self.rightLineNumber = rightLineNumber
        self.contentLeft = contentLeft
        self.contentRight = contentRight
        self.changeType = changeType
        self.tokensLeft = tokensLeft
        self.tokensRight = tokensRight
        self.hunkIndex = hunkIndex
    }

    enum CodingKeys: String, CodingKey {
        case leftLineNumber = "left_line_number"
        case rightLineNumber = "right_line_number"
        case contentLeft = "content_left"
        case contentRight = "content_right"
        case changeType = "change_type"
        case tokensLeft = "tokens_left"
        case tokensRight = "tokens_right"
        case hunkIndex = "hunk_index"
    }
}

/// Overall 2-way text diff result summary.
public struct TextDiffResult: Codable, Sendable {
    public var lines: [DiffLine]
    public var totalAdditions: UInt32
    public var totalDeletions: UInt32
    public var totalModifications: UInt32
    public var hunks: [DiffHunk]

    public init(
        lines: [DiffLine] = [],
        totalAdditions: UInt32 = 0,
        totalDeletions: UInt32 = 0,
        totalModifications: UInt32 = 0,
        hunks: [DiffHunk] = []
    ) {
        self.lines = lines
        self.totalAdditions = totalAdditions
        self.totalDeletions = totalDeletions
        self.totalModifications = totalModifications
        self.hunks = hunks
    }

    enum CodingKeys: String, CodingKey {
        case lines
        case totalAdditions = "total_additions"
        case totalDeletions = "total_deletions"
        case totalModifications = "total_modifications"
        case hunks
    }
}

/// Status of an item when comparing two directories.
public enum FolderItemStatus: String, Codable, Sendable {
    case equal = "Equal"
    case leftOnly = "LeftOnly"
    case rightOnly = "RightOnly"
    case contentDifferent = "ContentDifferent"
    case metadataDifferent = "MetadataDifferent"
}

/// A node in the aligned directory diff tree.
public struct FolderDiffEntry: Codable, Sendable, Identifiable {
    public var id: String { relativePath }
    public let relativePath: String
    public let isDirectory: Bool
    public let status: FolderItemStatus
    public let leftSize: UInt64?
    public let rightSize: UInt64?
    public let leftModifiedTimestamp: UInt64?
    public let rightModifiedTimestamp: UInt64?
    public let leftHash: String?
    public let rightHash: String?

    public var leftURL: URL?
    public var rightURL: URL?

    public init(
        relativePath: String,
        isDirectory: Bool,
        status: FolderItemStatus,
        leftSize: UInt64? = nil,
        rightSize: UInt64? = nil,
        leftModifiedTimestamp: UInt64? = nil,
        rightModifiedTimestamp: UInt64? = nil,
        leftHash: String? = nil,
        rightHash: String? = nil,
        leftURL: URL? = nil,
        rightURL: URL? = nil
    ) {
        self.relativePath = relativePath
        self.isDirectory = isDirectory
        self.status = status
        self.leftSize = leftSize
        self.rightSize = rightSize
        self.leftModifiedTimestamp = leftModifiedTimestamp
        self.rightModifiedTimestamp = rightModifiedTimestamp
        self.leftHash = leftHash
        self.rightHash = rightHash
        self.leftURL = leftURL
        self.rightURL = rightURL
    }

    enum CodingKeys: String, CodingKey {
        case relativePath = "relative_path"
        case isDirectory = "is_directory"
        case status
        case leftSize = "left_size"
        case rightSize = "right_size"
        case leftModifiedTimestamp = "left_modified_timestamp"
        case rightModifiedTimestamp = "right_modified_timestamp"
        case leftHash = "left_hash"
        case rightHash = "right_hash"
    }
}

/// Types of file sync actions.
public enum SyncActionType: String, Sendable, Identifiable {
    case copyLeftToRight = "Copy from Source to Target"
    case copyRightToLeft = "Copy from Target to Source"
    case overwriteLeftToRight = "Overwrite Target with Source"
    case overwriteRightToLeft = "Overwrite Source with Target"
    case deleteRight = "Delete orphan on Target"
    case deleteLeft = "Delete orphan on Source"

    public var id: String { rawValue }
}

/// A specific sync operation for dry run.
public struct SyncPlanItem: Identifiable, Sendable {
    public var id: String { "\(action.rawValue)-\(relativePath)" }
    public let action: SyncActionType
    public let relativePath: String
    public let sourceURL: URL?
    public let targetURL: URL?
    public let size: UInt64?

    public init(action: SyncActionType, relativePath: String, sourceURL: URL?, targetURL: URL?, size: UInt64?) {
        self.action = action
        self.relativePath = relativePath
        self.sourceURL = sourceURL
        self.targetURL = targetURL
        self.size = size
    }
}

/// 3-Way Merge conflict hunk status.
public enum MergeHunkStatus: String, Codable, Sendable {
    case cleanLocal = "CleanLocal"
    case cleanRemote = "CleanRemote"
    case conflict = "Conflict"
    case unchanged = "Unchanged"
}

/// A line in a 3-way merge view.
public struct MergeLine: Codable, Sendable, Identifiable {
    public var id: String {
        "\(localLineNumber?.description ?? "x")-\(baseLineNumber?.description ?? "x")-\(remoteLineNumber?.description ?? "x")"
    }
    public let localLineNumber: UInt32?
    public let baseLineNumber: UInt32?
    public let remoteLineNumber: UInt32?
    public let contentLocal: String
    public let contentBase: String
    public let contentRemote: String
    public var status: MergeHunkStatus
    public var resolvedContent: String?
    public var conflictIndex: Int?

    public init(
        localLineNumber: UInt32?,
        baseLineNumber: UInt32?,
        remoteLineNumber: UInt32?,
        contentLocal: String,
        contentBase: String,
        contentRemote: String,
        status: MergeHunkStatus,
        resolvedContent: String? = nil,
        conflictIndex: Int? = nil
    ) {
        self.localLineNumber = localLineNumber
        self.baseLineNumber = baseLineNumber
        self.remoteLineNumber = remoteLineNumber
        self.contentLocal = contentLocal
        self.contentBase = contentBase
        self.contentRemote = contentRemote
        self.status = status
        self.resolvedContent = resolvedContent
        self.conflictIndex = conflictIndex
    }

    enum CodingKeys: String, CodingKey {
        case localLineNumber = "local_line_number"
        case baseLineNumber = "base_line_number"
        case remoteLineNumber = "remote_line_number"
        case contentLocal = "content_local"
        case contentBase = "content_base"
        case contentRemote = "content_remote"
        case status
        case resolvedContent = "resolved_content"
        case conflictIndex = "conflict_index"
    }
}

/// Overall 3-way merge result.
public struct MergeResult: Codable, Sendable {
    public var lines: [MergeLine]
    public var conflictCount: UInt32
    public var autoResolvedCount: UInt32
    public var mergedText: String

    public init(
        lines: [MergeLine] = [],
        conflictCount: UInt32 = 0,
        autoResolvedCount: UInt32 = 0,
        mergedText: String = ""
    ) {
        self.lines = lines
        self.conflictCount = conflictCount
        self.autoResolvedCount = autoResolvedCount
        self.mergedText = mergedText
    }

    enum CodingKeys: String, CodingKey {
        case lines
        case conflictCount = "conflict_count"
        case autoResolvedCount = "auto_resolved_count"
        case mergedText = "merged_text"
    }
}
