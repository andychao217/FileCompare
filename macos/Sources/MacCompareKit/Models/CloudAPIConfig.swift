import Foundation

public enum CloudProviderPreset: String, CaseIterable, Identifiable, Codable, Sendable {
    case deepseek = "deepseek"
    case openai = "openai"
    case ollama = "ollama"
    case custom = "custom"

    public var id: String { rawValue }

    public var displayName: String {
        switch self {
        case .deepseek: return "DeepSeek"
        case .openai: return "OpenAI"
        case .ollama: return "Ollama (Local/LAN)"
        case .custom: return "Custom"
        }
    }

    public var defaultBaseURL: String {
        switch self {
        case .deepseek: return "https://api.deepseek.com/v1"
        case .openai: return "https://api.openai.com/v1"
        case .ollama: return "http://localhost:11434/v1"
        case .custom: return ""
        }
    }

    public var defaultModel: String {
        switch self {
        case .deepseek: return "deepseek-chat"
        case .openai: return "gpt-4o-mini"
        case .ollama: return "qwen2.5-coder:7b"
        case .custom: return ""
        }
    }
}

public struct CloudAPIConfig: Codable, Sendable, Equatable {
    public var isEnabled: Bool
    public var providerPreset: CloudProviderPreset
    public var baseURL: String
    public var apiKey: String
    public var modelName: String
    public var timeoutSeconds: TimeInterval

    public init(
        isEnabled: Bool = false,
        providerPreset: CloudProviderPreset = .deepseek,
        baseURL: String = "https://api.deepseek.com/v1",
        apiKey: String = "",
        modelName: String = "deepseek-chat",
        timeoutSeconds: TimeInterval = 120
    ) {
        self.isEnabled = isEnabled
        self.providerPreset = providerPreset
        self.baseURL = baseURL
        self.apiKey = apiKey
        self.modelName = modelName
        self.timeoutSeconds = timeoutSeconds
    }

    public static let `default` = CloudAPIConfig()

    private static let storageKey = "maccompare_cloud_api_config"

    public static func load() -> CloudAPIConfig {
        guard let data = UserDefaults.standard.data(forKey: storageKey),
              let config = try? JSONDecoder().decode(CloudAPIConfig.self, from: data) else {
            return .default
        }
        return config
    }

    public func save() {
        if let data = try? JSONEncoder().encode(self) {
            UserDefaults.standard.set(data, forKey: Self.storageKey)
        }
    }
}
