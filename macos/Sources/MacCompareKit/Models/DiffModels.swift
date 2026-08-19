import Foundation

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

/// A visual diff line in a 2-way diff view.
public struct DiffLine: Codable, Sendable, Identifiable {
    public var id: String {
        "\(leftLineNumber?.description ?? "none")-\(rightLineNumber?.description ?? "none")-\(changeType.rawValue)"
    }
    public let leftLineNumber: UInt32?
    public let rightLineNumber: UInt32?
    public let contentLeft: String
    public let contentRight: String
    public let changeType: ChangeType
    public let tokensLeft: [DiffToken]
    public let tokensRight: [DiffToken]

    public var isPhantomLeft: Bool { leftLineNumber == nil }
    public var isPhantomRight: Bool { rightLineNumber == nil }

    public init(
        leftLineNumber: UInt32?,
        rightLineNumber: UInt32?,
        contentLeft: String,
        contentRight: String,
        changeType: ChangeType,
        tokensLeft: [DiffToken] = [],
        tokensRight: [DiffToken] = []
    ) {
        self.leftLineNumber = leftLineNumber
        self.rightLineNumber = rightLineNumber
        self.contentLeft = contentLeft
        self.contentRight = contentRight
        self.changeType = changeType
        self.tokensLeft = tokensLeft
        self.tokensRight = tokensRight
    }

    enum CodingKeys: String, CodingKey {
        case leftLineNumber = "left_line_number"
        case rightLineNumber = "right_line_number"
        case contentLeft = "content_left"
        case contentRight = "content_right"
        case changeType = "change_type"
        case tokensLeft = "tokens_left"
        case tokensRight = "tokens_right"
    }
}

/// Overall 2-way text diff result summary.
public struct TextDiffResult: Codable, Sendable {
    public let lines: [DiffLine]
    public let totalAdditions: UInt32
    public let totalDeletions: UInt32
    public let totalModifications: UInt32

    public init(
        lines: [DiffLine] = [],
        totalAdditions: UInt32 = 0,
        totalDeletions: UInt32 = 0,
        totalModifications: UInt32 = 0
    ) {
        self.lines = lines
        self.totalAdditions = totalAdditions
        self.totalDeletions = totalDeletions
        self.totalModifications = totalModifications
    }

    enum CodingKeys: String, CodingKey {
        case lines
        case totalAdditions = "total_additions"
        case totalDeletions = "total_deletions"
        case totalModifications = "total_modifications"
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

    public init(
        relativePath: String,
        isDirectory: Bool,
        status: FolderItemStatus,
        leftSize: UInt64? = nil,
        rightSize: UInt64? = nil,
        leftModifiedTimestamp: UInt64? = nil,
        rightModifiedTimestamp: UInt64? = nil,
        leftHash: String? = nil,
        rightHash: String? = nil
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
    public let status: MergeHunkStatus
    public var resolvedContent: String?

    public init(
        localLineNumber: UInt32?,
        baseLineNumber: UInt32?,
        remoteLineNumber: UInt32?,
        contentLocal: String,
        contentBase: String,
        contentRemote: String,
        status: MergeHunkStatus,
        resolvedContent: String? = nil
    ) {
        self.localLineNumber = localLineNumber
        self.baseLineNumber = baseLineNumber
        self.remoteLineNumber = remoteLineNumber
        self.contentLocal = contentLocal
        self.contentBase = contentBase
        self.contentRemote = contentRemote
        self.status = status
        self.resolvedContent = resolvedContent
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
