import Foundation

public enum CloudLLMError: LocalizedError, Sendable {
    case invalidURL
    case missingAPIKey
    case httpError(statusCode: Int, message: String)
    case invalidResponse
    case parseError(String)

    public var errorDescription: String? {
        switch self {
        case .invalidURL:
            return "无效的 API Base URL 地址"
        case .missingAPIKey:
            return "未配置 API Key"
        case .httpError(let statusCode, let message):
            return "云端服务错误 (HTTP \(statusCode)): \(message)"
        case .invalidResponse:
            return "云端服务返回数据格式无效"
        case .parseError(let msg):
            return "解析云端大模型响应失败: \(msg)"
        }
    }
}

public actor CloudLLMService {
    public static let shared = CloudLLMService()

    private init() {}

    /// 测试云端 API 连接连通性，返回响应耗时 (秒)
    public func testConnection(config: CloudAPIConfig) async throws -> TimeInterval {
        let trimmedBase = config.baseURL.trimmingCharacters(in: .whitespacesAndNewlines)
            .trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        guard let url = URL(string: "\(trimmedBase)/chat/completions") else {
            throw CloudLLMError.invalidURL
        }

        let model = config.modelName.trimmingCharacters(in: .whitespacesAndNewlines)
        let requestModel = model.isEmpty ? "deepseek-chat" : model

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        if !config.apiKey.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            request.setValue("Bearer \(config.apiKey.trimmingCharacters(in: .whitespacesAndNewlines))", forHTTPHeaderField: "Authorization")
        }
        request.timeoutInterval = 15

        let body: [String: Any] = [
            "model": requestModel,
            "messages": [
                ["role": "user", "content": "Hi"]
            ],
            "max_tokens": 5
        ]

        request.httpBody = try JSONSerialization.data(withJSONObject: body)

        let startTime = Date()
        let (data, response) = try await URLSession.shared.data(for: request)
        let elapsed = Date().timeIntervalSince(startTime)

        guard let httpResponse = response as? HTTPURLResponse else {
            throw CloudLLMError.invalidResponse
        }

        guard (200...299).contains(httpResponse.statusCode) else {
            let errorText = String(decoding: data, as: UTF8.self)
            throw CloudLLMError.httpError(statusCode: httpResponse.statusCode, message: errorText)
        }

        return elapsed
    }

    /// 向云端大模型发送成对 Diff 并获取结构化总结
    public func generateSummary(
        config: CloudAPIConfig,
        diffBody: String,
        language: AppLanguage,
        startTime: Date
    ) async throws -> AISummaryResult {
        let trimmedBase = config.baseURL.trimmingCharacters(in: .whitespacesAndNewlines)
            .trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        guard let url = URL(string: "\(trimmedBase)/chat/completions") else {
            throw CloudLLMError.invalidURL
        }

        let model = config.modelName.trimmingCharacters(in: .whitespacesAndNewlines)
        let requestModel = model.isEmpty ? "deepseek-chat" : model

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        if !config.apiKey.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            request.setValue("Bearer \(config.apiKey.trimmingCharacters(in: .whitespacesAndNewlines))", forHTTPHeaderField: "Authorization")
        }
        // Local models (like Ollama on CPU) or reasoning models need sufficient time to infer
        let effectiveTimeout: TimeInterval = max(120, config.timeoutSeconds)
        request.timeoutInterval = effectiveTimeout

        let systemInstruction: String
        let userInstruction: String

        switch language {
        case .zhHans:
            systemInstruction = "你是一位资深架构师与代码审查专家。请阅读用户提供的成对代码改动（Diff），分析修改意图并提炼出关键改动。请直接输出合法的 JSON 格式，不要进行冗长的思考或推导，不要添加额外的问候语或 Markdown 外部包裹。"
            userInstruction = """
            请阅读以下成对配对的代码改动（Diff）：
            \(diffBody)

            请输出一个合法的 JSON 对象，格式如下：
            {
              "intent": "用一句话概括本次修改的核心目的",
              "points": [
                "核心变更说明1（说明从旧值变更为新值，如：将 ARCH 由 arch64 更改为 arm64）",
                "核心变更说明2",
                "核心变更说明3"
              ],
              "commit": "规范的 Git 提交信息（如：chore(build): 升级构建版本并调整架构）"
            }
            规则：准确使用代码中的变量名，不得捏造不存在的名称，不得输出重复要点。
            """
        case .ja:
            systemInstruction = "あなたはシニアアーキテクト兼コードレビューの専門家です。コード差分を分析し、長考や過度な推論を行わずに、有効なJSON形式のみを直接出力してください。挨拶やMarkdownの外枠は含めないでください。"
            userInstruction = """
            成対差分内容：
            \(diffBody)

            以下の3つのフィールドを持つ単一のJSON形式で出力してください：
            {
              "intent": "変更意図の簡潔な日本語要約",
              "points": [
                "変更点1（旧値から新値への変更を明記）",
                "変更点2"
              ],
              "commit": "Gitのコミットメッセージ（例: chore(build): ...）"
            }
            規則: コード内の変数名を正確に使用し、重複した項目は出力しないでください。
            """
        default:
            systemInstruction = "You are a senior code reviewer. Analyze code diffs and directly output a valid JSON object without lengthy thinking or explanations. Do not include greetings or markdown fences."
            userInstruction = """
            Paired diff content:
            \(diffBody)

            Output a single JSON object with the following schema:
            {
              "intent": "concise summary of the core purpose",
              "points": [
                "key point 1 detailing change from old value to new value",
                "key point 2"
              ],
              "commit": "Git conventional commit message (e.g. chore(build): bump release version)"
            }
            Rules: Use exact variable names from the diff. Do not invent names or repeat items.
            """
        }

        let body: [String: Any] = [
            "model": requestModel,
            "messages": [
                ["role": "system", "content": systemInstruction],
                ["role": "user", "content": userInstruction]
            ],
            "temperature": 0.2,
            "response_format": ["type": "json_object"]
        ]

        request.httpBody = try JSONSerialization.data(withJSONObject: body)

        let (data, response) = try await URLSession.shared.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse else {
            throw CloudLLMError.invalidResponse
        }

        guard (200...299).contains(httpResponse.statusCode) else {
            let errorText = String(decoding: data, as: UTF8.self)
            throw CloudLLMError.httpError(statusCode: httpResponse.statusCode, message: errorText)
        }

        // 解析 Chat Completions 格式: choices[0].message.content
        guard let jsonObject = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let choices = jsonObject["choices"] as? [[String: Any]],
              let firstChoice = choices.first,
              let message = firstChoice["message"] as? [String: Any],
              let content = message["content"] as? String else {
            throw CloudLLMError.invalidResponse
        }

        // 提取并解析内部的结构化 JSON
        guard let payload = AIService.parseLLMJSON(from: content) else {
            throw CloudLLMError.parseError("大模型返回的内容无法解析为 JSON: \(content.prefix(100))")
        }

        var cleanPoints: [String] = []
        for p in payload.points ?? [] {
            let trimmed = p.trimmingCharacters(in: CharacterSet.whitespacesAndNewlines)
            if !trimmed.isEmpty && !cleanPoints.contains(trimmed) {
                cleanPoints.append(trimmed)
            }
        }

        let elapsed = Date().timeIntervalSince(startTime)
        return AISummaryResult(
            modelName: requestModel,
            oneLineIntent: payload.intent ?? "云端代码修改分析",
            keyModifications: cleanPoints.isEmpty ? (payload.points ?? []) : cleanPoints,
            conventionalCommit: payload.commit ?? "chore(diff): update implementation",
            rawMarkdown: content,
            executionTimeSeconds: elapsed
        )
    }
}
