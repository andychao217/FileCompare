import Foundation

public enum ValidationPromptType: Sendable, Equatable {
    case noticeOnly
    case suggestSwitch(recommendedMode: TabContentType)
}

public struct ValidationResult: Sendable, Equatable {
    public let isValid: Bool
    public let titleKey: L10nKey
    public let messageKey: L10nKey
    public let promptType: ValidationPromptType
    public let targetURL: URL

    public static func valid(for url: URL) -> ValidationResult {
        ValidationResult(
            isValid: true,
            titleKey: .dialogNoticeTitle,
            messageKey: .dialogNoticeTitle,
            promptType: .noticeOnly,
            targetURL: url
        )
    }
}

public enum FileFormatValidator {
    private static let binaryExtensions: Set<String> = [
        "png", "jpg", "jpeg", "gif", "webp", "bmp", "ico", "tiff", "psd", "ai",
        "zip", "tar", "gz", "tgz", "bz2", "7z", "rar", "xz", "dmg", "pkg", "iso",
        "exe", "dll", "dylib", "so", "a", "bin", "o", "obj", "class", "jar",
        "pdf", "mp3", "mp4", "mov", "wav", "m4a", "flac", "avi", "mkv",
        "ttf", "otf", "woff", "woff2", "eot", "db", "sqlite", "sqlite3"
    ]

    private static let wordExtensions: Set<String> = ["docx", "doc", "rtf"]
    private static let excelExtensions: Set<String> = ["xlsx", "xls", "xlsm", "csv", "tsv"]

    public static func validate(url: URL, expectedMode: TabContentType) -> ValidationResult {
        let resolved = url.resolvingSymlinksInPath()
        var isDir: ObjCBool = false
        let exists = FileManager.default.fileExists(atPath: resolved.path, isDirectory: &isDir)

        if !exists {
            return ValidationResult(
                isValid: false,
                titleKey: .dialogNoticeTitle,
                messageKey: .errFolderDroppedInFileMode,
                promptType: .noticeOnly,
                targetURL: url
            )
        }

        // 1. Directory Checks
        if isDir.boolValue {
            if expectedMode == .folderDiff {
                return .valid(for: url)
            } else {
                return ValidationResult(
                    isValid: false,
                    titleKey: .suggestSwitchModeTitle,
                    messageKey: .errFolderDroppedInFileMode,
                    promptType: .suggestSwitch(recommendedMode: .folderDiff),
                    targetURL: url
                )
            }
        }

        // 2. Single File in FolderDiff Mode
        if expectedMode == .folderDiff {
            return ValidationResult(
                isValid: false,
                titleKey: .dialogNoticeTitle,
                messageKey: .errFileDroppedInFolderMode,
                promptType: .noticeOnly,
                targetURL: url
            )
        }

        let ext = resolved.pathExtension.lowercased()

        // 3. Mode Specific Validations
        switch expectedMode {
        case .textDiff, .threeWayMerge:
            if wordExtensions.contains(ext) {
                return ValidationResult(
                    isValid: false,
                    titleKey: .suggestSwitchModeTitle,
                    messageKey: .suggestSwitchToWord,
                    promptType: .suggestSwitch(recommendedMode: .wordDiff),
                    targetURL: url
                )
            }
            if excelExtensions.contains(ext) && ext != "csv" && ext != "tsv" {
                return ValidationResult(
                    isValid: false,
                    titleKey: .suggestSwitchModeTitle,
                    messageKey: .suggestSwitchToExcel,
                    promptType: .suggestSwitch(recommendedMode: .excelDiff),
                    targetURL: url
                )
            }
            if binaryExtensions.contains(ext) {
                return ValidationResult(
                    isValid: false,
                    titleKey: .dialogNoticeTitle,
                    messageKey: .errBinaryFileNotSupported,
                    promptType: .noticeOnly,
                    targetURL: url
                )
            }
            return .valid(for: url)

        case .wordDiff:
            if wordExtensions.contains(ext) {
                return .valid(for: url)
            }
            if excelExtensions.contains(ext) {
                return ValidationResult(
                    isValid: false,
                    titleKey: .suggestSwitchModeTitle,
                    messageKey: .suggestSwitchToExcel,
                    promptType: .suggestSwitch(recommendedMode: .excelDiff),
                    targetURL: url
                )
            }
            return ValidationResult(
                isValid: false,
                titleKey: .dialogNoticeTitle,
                messageKey: .errWordInvalidFormat,
                promptType: .noticeOnly,
                targetURL: url
            )

        case .excelDiff:
            if excelExtensions.contains(ext) {
                return .valid(for: url)
            }
            if wordExtensions.contains(ext) {
                return ValidationResult(
                    isValid: false,
                    titleKey: .suggestSwitchModeTitle,
                    messageKey: .suggestSwitchToWord,
                    promptType: .suggestSwitch(recommendedMode: .wordDiff),
                    targetURL: url
                )
            }
            return ValidationResult(
                isValid: false,
                titleKey: .dialogNoticeTitle,
                messageKey: .errExcelInvalidFormat,
                promptType: .noticeOnly,
                targetURL: url
            )

        case .folderDiff:
            return .valid(for: url)
        }
    }
}
