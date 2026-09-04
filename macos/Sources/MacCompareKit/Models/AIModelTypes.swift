import Foundation

public enum AIExecutionMode: String, CaseIterable, Identifiable, Sendable {
    case localOffline = "localOffline"
    case cloudAPI = "cloudAPI"

    public var id: String { rawValue }
}

public enum AIModelId: String, CaseIterable, Identifiable, Sendable {
    case gemma3_1b = "gemma-3-1b-it-q4_k_m"
    case gemma3_4b = "gemma-3-4b-it-q4_k_m"

    public var id: String { rawValue }

    @MainActor
    public var displayName: String {
        localizedDisplayName(for: LanguageManager.shared.effectiveLanguage)
    }

    @MainActor
    public func localizedDisplayName(for language: AppLanguage) -> String {
        switch self {
        case .gemma3_1b:
            return LanguageManager.shared.text(.aiModelGemma3_1B, language: language)
        case .gemma3_4b:
            return LanguageManager.shared.text(.aiModelGemma3_4B, language: language)
        }
    }

    public var shortName: String {
        switch self {
        case .gemma3_1b: return "Gemma 3 1B"
        case .gemma3_4b: return "Gemma 3 4B"
        }
    }

    @MainActor
    public var subtitle: String {
        localizedSubtitle(for: LanguageManager.shared.effectiveLanguage)
    }

    @MainActor
    public func localizedSubtitle(for language: AppLanguage) -> String {
        switch self {
        case .gemma3_1b:
            return LanguageManager.shared.text(.aiModelGemma3_1BDesc, language: language)
        case .gemma3_4b:
            return LanguageManager.shared.text(.aiModelGemma3_4BDesc, language: language)
        }
    }

    public var estimatedSizeMB: Int {
        switch self {
        case .gemma3_1b: return 780
        case .gemma3_4b: return 2560
        }
    }

    public var filename: String {
        "\(rawValue).gguf"
    }

    public var downloadURL: URL {
        switch self {
        case .gemma3_1b:
            // ModelScope (China mirror, high-speed & public without login)
            return URL(string: "https://modelscope.cn/models/lmstudio-community/gemma-3-1b-it-GGUF/resolve/master/gemma-3-1b-it-Q4_K_M.gguf")!
        case .gemma3_4b:
            return URL(string: "https://modelscope.cn/models/lmstudio-community/gemma-3-4b-it-GGUF/resolve/master/gemma-3-4b-it-Q4_K_M.gguf")!
        }
    }
}

public enum AIModelDownloadState: Sendable, Equatable {
    case notDownloaded
    case downloading(progress: Double, speedText: String, remainingText: String)
    case ready
    case error(String)

    public var isDownloading: Bool {
        if case .downloading = self { return true }
        return false
    }

    public var isReady: Bool {
        if case .ready = self { return true }
        return false
    }
}

public struct AISummaryResult: Sendable, Equatable {
    public var modelName: String
    public var oneLineIntent: String
    public var keyModifications: [String]
    public var conventionalCommit: String
    public var rawMarkdown: String
    public var executionTimeSeconds: Double

    public init(
        modelName: String = "Gemma 3 1B",
        oneLineIntent: String = "",
        keyModifications: [String] = [],
        conventionalCommit: String = "",
        rawMarkdown: String = "",
        executionTimeSeconds: Double = 0.8
    ) {
        self.modelName = modelName
        self.oneLineIntent = oneLineIntent
        self.keyModifications = keyModifications
        self.conventionalCommit = conventionalCommit
        self.rawMarkdown = rawMarkdown
        self.executionTimeSeconds = executionTimeSeconds
    }
}
