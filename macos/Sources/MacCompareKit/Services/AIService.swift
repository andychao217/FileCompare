import Foundation
import SwiftUI

@MainActor
@Observable
public final class AIService {
    public static let shared = AIService()

    public var isGenerating: Bool = false

    private init() {}

    /// 分析差异并生成结构化意图摘要
    public func summarizeDiff(
        leftContent: String,
        rightContent: String,
        leftTitle: String,
        rightTitle: String,
        language: AppLanguage = LanguageManager.shared.effectiveLanguage
    ) async -> AISummaryResult {
        isGenerating = true
        defer { isGenerating = false }

        let startTime = Date()

        // 提取变更行
        let leftLines = leftContent.components(separatedBy: .newlines)
        let rightLines = rightContent.components(separatedBy: .newlines)

        var addedLines: [String] = []
        var removedLines: [String] = []

        let maxSample = min(150, max(leftLines.count, rightLines.count))
        let leftSet = Set(leftLines)
        let rightSet = Set(rightLines)

        for line in rightLines.prefix(maxSample) {
            let trimmed = line.trimmingCharacters(in: .whitespaces)
            if !trimmed.isEmpty && !leftSet.contains(line) {
                addedLines.append(trimmed)
            }
        }
        for line in leftLines.prefix(maxSample) {
            let trimmed = line.trimmingCharacters(in: .whitespaces)
            if !trimmed.isEmpty && !rightSet.contains(line) {
                removedLines.append(trimmed)
            }
        }

        // 1. 若当前启用了自定义云端 API 模式，优先使用云端大模型进行深度语义推理
        if AIModelManager.shared.executionMode == .cloudAPI {
            let cloudConfig = AIModelManager.shared.cloudConfig
            let diffBody = buildPairwiseDiff(addedLines: addedLines, removedLines: removedLines)
            if !diffBody.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                do {
                    let cloudResult = try await CloudLLMService.shared.generateSummary(
                        config: cloudConfig,
                        diffBody: diffBody,
                        language: language,
                        startTime: startTime
                    )
                    return cloudResult
                } catch {
                    let elapsed = Date().timeIntervalSince(startTime)
                    let model = cloudConfig.modelName.isEmpty ? "Cloud API" : cloudConfig.modelName
                    return AISummaryResult(
                        modelName: model,
                        oneLineIntent: "云端大模型调用失败: \(error.localizedDescription)",
                        keyModifications: [
                            "请在偏好设置 -> AI Engine 中检查 API Base URL 与 API Key",
                            "可点击「测试连接」按钮检测接口连通性与余额状态",
                            "您也可以在设置中一键切换回「本地离线模型」模式"
                        ],
                        conventionalCommit: "chore(diff): update implementation",
                        rawMarkdown: error.localizedDescription,
                        executionTimeSeconds: elapsed
                    )
                }
            }
        }

        // 2. 尝试使用已下载的本地真实 LLM（Gemma 3 GGUF + llama-cli）执行端侧推理
        if let realResult = await tryLocalLLMInference(
            addedLines: addedLines,
            removedLines: removedLines,
            leftTitle: leftTitle,
            rightTitle: rightTitle,
            language: language,
            startTime: startTime
        ) {
            return realResult
        }

        // 若本地真实大模型尚未就绪，诚实返回未就绪引导状态，绝不伪造虚假规则
        let elapsed = Date().timeIntervalSince(startTime)
        let unreadyIntent: String
        let diffStats: [String]
        switch language {
        case .zhHans:
            unreadyIntent = "端侧大模型尚未就绪。请点击「下载模型」以启用 Gemma 3 真实大模型深度分析。"
            diffStats = [
                "检测到 \(addedLines.count) 处新增内容",
                "检测到 \(removedLines.count) 处移除内容",
                "下载约 800MB 离线模型后即可由本地神经网络自动提炼意图与 Commit"
            ]
        case .ja:
            unreadyIntent = "ローカルAIモデルが準備されていません。「モデルをダウンロード」をクリックして有効にしてください。"
            diffStats = [
                "\(addedLines.count) 件の追加内容を検出",
                "\(removedLines.count) 件の削除内容を検出",
                "モデルのダウンロード完了後、ローカルLLMによる自動要約が有効になります"
            ]
        default:
            unreadyIntent = "Local AI model is not ready. Please download the Gemma 3 model to enable on-device semantic analysis."
            diffStats = [
                "\(addedLines.count) additions detected",
                "\(removedLines.count) removals detected",
                "Local neural network will generate semantic intent after downloading the model"
            ]
        }

        let unreadyModelName: String
        switch language {
        case .zhHans:
            unreadyModelName = "未就绪"
        case .ja:
            unreadyModelName = "未準備"
        default:
            unreadyModelName = "Not Ready"
        }

        return AISummaryResult(
            modelName: unreadyModelName,
            oneLineIntent: unreadyIntent,
            keyModifications: diffStats,
            conventionalCommit: "chore: update changes",
            rawMarkdown: unreadyIntent,
            executionTimeSeconds: elapsed
        )
    }

    // MARK: - Real Local LLM Inference (Gemma 3)

    /// 尝试调用本地 llama-cli 驱动已下载的 Gemma 3 GGUF 模型进行真实端侧推理
    private func tryLocalLLMInference(
        addedLines: [String],
        removedLines: [String],
        leftTitle: String,
        rightTitle: String,
        language: AppLanguage,
        startTime: Date
    ) async -> AISummaryResult? {
        // 1. 查找模型路径 (优先用户选中的模型，若未下载则回退到任意已下载模型)
        let usedModel: AIModelId
        if AIModelManager.shared.isModelDownloaded(AIModelManager.shared.selectedModel) {
            usedModel = AIModelManager.shared.selectedModel
        } else if AIModelManager.shared.isModelDownloaded(.gemma3_4b) {
            usedModel = .gemma3_4b
        } else if AIModelManager.shared.isModelDownloaded(.gemma3_1b) {
            usedModel = .gemma3_1b
        } else {
            return nil
        }
        let modelURL = AIModelManager.shared.localFileURL(for: usedModel)

        // 2. 查找可用的 llama 执行器
        guard let executablePath = findLlamaExecutable() else {
            return nil
        }

        // 3. 构建成对对齐的 Diff 文本（让端侧小模型能清晰感知从旧值到新值的对应变化）
        let diffBody = buildPairwiseDiff(addedLines: addedLines, removedLines: removedLines)
        guard !diffBody.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return nil
        }

        // 4. 构建针对当前语言的 Prompt (针对成对改动设计，要求说明从旧值到新值的演进)
        let userInstruction: String
        switch language {
        case .zhHans:
            userInstruction = """
            请阅读以下成对配对的代码改动（Diff），分析修改意图并提炼出关键改动。

            成对改动内容：
            \(diffBody)

            请输出一个合法的 JSON 对象，包含：
            - intent: 用中文概括本次修改的核心目的
            - points: 3到5条核心变更说明，每条必须说明从旧值变更为新值（例如："将 ARCH 由 arch64 更改为 arm64"）
            - commit: 规范的 Git 提交信息（例如：chore(build): 升级构建版本并调整架构）
            规则：准确使用代码中的变量名，不得捏造不存在的名称，不得输出重复要点。
            """
        case .ja:
            userInstruction = """
            以下のペアリングされたコード差分（Diff）を分析し、変更意図と重要な変更点を要約してください。

            成対差分内容：
            \(diffBody)

            以下の3つのフィールドを持つ単一のJSON形式で出力してください：
            - intent: 変更意図の簡潔な日本語要約
            - points: 3〜5個の変更点（旧値から新値への変更を明記、例: "ARCHをarch64からarm64に変更"）
            - commit: Gitのコミットメッセージ（例: chore(build): ...）
            規則: コード内の変数名を正確に使用し、重複した項目は出力しないでください。
            """
        default:
            userInstruction = """
            Analyze the following paired code diff and summarize the intent and key changes.

            Paired diff content:
            \(diffBody)

            Output a single JSON object with:
            - intent: concise summary of the core purpose
            - points: 3 to 5 key points detailing changes from old value to new value (e.g., "Change ARCH from arch64 to arm64")
            - commit: Git conventional commit message (e.g., chore(build): bump release version and adjust architecture)
            Rules: Use exact variable names from the diff. Do not invent names or repeat items.
            """
        }

        let fullPrompt = "<start_of_turn>user\n\(userInstruction)<end_of_turn>\n<start_of_turn>model\n"
        let modelPath = modelURL.path

        // 5. 在后台线程运行本地进程
        let output = await Task.detached(priority: .userInitiated) { () -> String? in
            let process = Process()
            process.executableURL = URL(fileURLWithPath: executablePath)
            process.arguments = [
                "-m", modelPath,
                "-p", fullPrompt,
                "-n", "280",
                "-st",
                "--no-display-prompt"
            ]

            let stdoutPipe = Pipe()
            let stderrPipe = Pipe()

            process.standardOutput = stdoutPipe
            process.standardError = stderrPipe

            do {
                try process.run()
                let data = stdoutPipe.fileHandleForReading.readDataToEndOfFile()
                process.waitUntilExit()

                return String(decoding: data, as: UTF8.self)
            } catch {
                return nil
            }
        }.value

        guard let rawOutput = output, !rawOutput.isEmpty else {
            return nil
        }

        // 6. 解析提取 JSON
        guard let payload = Self.parseLLMJSON(from: rawOutput) else {
            return nil
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
            modelName: usedModel.shortName,
            oneLineIntent: payload.intent ?? "代码修改分析",
            keyModifications: cleanPoints.isEmpty ? (payload.points ?? []) : cleanPoints,
            conventionalCommit: payload.commit ?? "chore(diff): update implementation",
            rawMarkdown: rawOutput,
            executionTimeSeconds: elapsed
        )
    }

    /// 将零散的增删行进行语义成对配对（Pairwise Alignment），让端侧小模型一眼看懂前后对应演进
    private func buildPairwiseDiff(addedLines: [String], removedLines: [String]) -> String {
        var remainingAdded = addedLines.prefix(20).map { $0 }
        var pairs: [(old: String, new: String)] = []
        var singleRemoved: [String] = []

        for oldLine in removedLines.prefix(20) {
            let oldPrefix = String(oldLine.prefix(10))
            // 优先查找前缀相同的行（如 export ARCH=... 对应 export ARCH=...）
            if let matchIdx = remainingAdded.firstIndex(where: {
                !oldPrefix.isEmpty && $0.hasPrefix(oldPrefix)
            }) {
                pairs.append((old: oldLine, new: remainingAdded.remove(at: matchIdx)))
            } else if let matchIdx = remainingAdded.firstIndex(where: {
                let commonChars = Set(oldLine).intersection(Set($0)).count
                return commonChars > max(oldLine.count, $0.count) / 2
            }) {
                pairs.append((old: oldLine, new: remainingAdded.remove(at: matchIdx)))
            } else {
                singleRemoved.append(oldLine)
            }
        }

        var snippets: [String] = []
        var index = 1

        // 1. 成对修改（最有价值的信息）
        for pair in pairs.prefix(6) {
            snippets.append("[改动项 \(index)]\n- \(pair.old)\n+ \(pair.new)")
            index += 1
        }

        // 2. 单纯新增
        for added in remainingAdded.prefix(3) {
            snippets.append("[新增项 \(index)]\n+ \(added)")
            index += 1
        }

        // 3. 单纯删除
        for removed in singleRemoved.prefix(3) {
            snippets.append("[删除项 \(index)]\n- \(removed)")
            index += 1
        }

        return snippets.joined(separator: "\n\n")
    }

    /// 查找可用的 llama 推理执行器（优先 App 自带 Bundle 内置引擎 mc-llama，其次兼容系统开发者环境）
    private func findLlamaExecutable() -> String? {
        var candidates: [String] = []

        // 1. 最高优先级：App 自身的 Bundle 内置引擎（自包含分发，用户开箱即用，无需安装任何环境）
        let macosPath = Bundle.main.bundleURL.appendingPathComponent("Contents/MacOS/mc-llama").path
        candidates.append(macosPath)

        if let resourcePath = Bundle.main.resourcePath {
            candidates.append("\(resourcePath)/llama/bin/mc-llama")
            candidates.append("\(resourcePath)/bin/mc-llama")
        }
        if let bundleBin = Bundle.main.url(forResource: "mc-llama", withExtension: nil)?.path {
            candidates.append(bundleBin)
        }
        candidates.append(Bundle.main.bundleURL.appendingPathComponent("Contents/MacOS/llama-cli").path)

        // 2. 兼容优先级：若本机已配置全局开发者环境，自动兼容
        candidates.append(contentsOf: [
            "/usr/local/bin/llama-cli",
            "/opt/homebrew/bin/llama-cli",
            "/usr/bin/llama-cli",
            FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".cargo/bin/llama-cli").path
        ])

        return candidates.first { FileManager.default.isExecutableFile(atPath: $0) }
    }

    /// 解析 LLM 输出文本中包含的 JSON 块（支持容错清理与正则提取）
    nonisolated static func parseLLMJSON(from text: String) -> LLMJSONPayload? {
        guard let firstBrace = text.firstIndex(of: "{"),
              let lastBrace = text.lastIndex(of: "}"),
              firstBrace < lastBrace else {
            return nil
        }

        var jsonString = String(text[firstBrace...lastBrace])

        // 1. 清理 LLM 常见的尾随逗号 (trailing comma)，例如: ["a", "b",] 或 {"a": 1,}
        if let regex = try? NSRegularExpression(pattern: ",\\s*([\\]\\}])", options: []) {
            let range = NSRange(jsonString.startIndex..., in: jsonString)
            jsonString = regex.stringByReplacingMatches(in: jsonString, options: [], range: range, withTemplate: "$1")
        }

        // 2. 尝试标准解码
        if let data = jsonString.data(using: .utf8),
           let payload = try? JSONDecoder().decode(LLMJSONPayload.self, from: data),
           (payload.intent != nil || payload.commit != nil) {
            return payload
        }

        // 3. 正则兜底解析器：即使 JSON 结构被某些特殊字符破坏，也能提取出关键字段
        var extractedIntent: String?
        var extractedCommit: String?
        var extractedPoints: [String] = []

        if let intentRegex = try? NSRegularExpression(pattern: "\"intent\"\\s*:\\s*\"([^\"]+)\"", options: []) {
            if let match = intentRegex.firstMatch(in: text, options: [], range: NSRange(text.startIndex..., in: text)),
               let r = Range(match.range(at: 1), in: text) {
                extractedIntent = String(text[r])
            }
        }

        if let commitRegex = try? NSRegularExpression(pattern: "\"commit\"\\s*:\\s*\"([^\"]+)\"", options: []) {
            if let match = commitRegex.firstMatch(in: text, options: [], range: NSRange(text.startIndex..., in: text)),
               let r = Range(match.range(at: 1), in: text) {
                extractedCommit = String(text[r])
            }
        }

        if let pointsRegex = try? NSRegularExpression(pattern: "\"points\"\\s*:\\s*\\[([^\\]]+)\\]", options: []) {
            if let match = pointsRegex.firstMatch(in: text, options: [], range: NSRange(text.startIndex..., in: text)),
               let r = Range(match.range(at: 1), in: text) {
                let inner = String(text[r])
                let itemRegex = try? NSRegularExpression(pattern: "\"([^\"]+)\"", options: [])
                let itemMatches = itemRegex?.matches(in: inner, options: [], range: NSRange(inner.startIndex..., in: inner)) ?? []
                for m in itemMatches {
                    if let itemRange = Range(m.range(at: 1), in: inner) {
                        let itemStr = String(inner[itemRange]).trimmingCharacters(in: .whitespaces)
                        if !itemStr.isEmpty {
                            extractedPoints.append(itemStr)
                        }
                    }
                }
            }
        }

        if extractedIntent != nil || !extractedPoints.isEmpty || extractedCommit != nil {
            return LLMJSONPayload(
                intent: extractedIntent,
                points: extractedPoints.isEmpty ? nil : extractedPoints,
                commit: extractedCommit
            )
        }

        return nil
    }
}

struct LLMJSONPayload: Codable {
    let intent: String?
    let points: [String]?
    let commit: String?
}
