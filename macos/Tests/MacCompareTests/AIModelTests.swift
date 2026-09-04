import XCTest
@testable import MacCompareKit

@MainActor
final class AIModelTests: XCTestCase {
    func testModelMetadataAndPaths() {
        let manager = AIModelManager.shared

        let b1 = AIModelId.gemma3_1b
        XCTAssertEqual(b1.shortName, "Gemma 3 1B")
        XCTAssertEqual(b1.estimatedSizeMB, 780)
        XCTAssertTrue(b1.filename.contains("gemma-3-1b"))

        let fileURL = manager.localFileURL(for: b1)
        XCTAssertTrue(fileURL.path.contains("MacCompare/models"))
        XCTAssertTrue(fileURL.lastPathComponent.hasSuffix(".gguf"))
        XCTAssertTrue(b1.downloadURL.absoluteString.contains("modelscope.cn"))
    }

    func testAIServiceSemanticSummary() async {
        let aiService = AIService.shared

        let leftCode = """
        func fetchUser() -> User? {
            let lock = NSLock()
            lock.lock()
            defer { lock.unlock() }
            return self.cachedUser
        }
        """

        let rightCode = """
        func fetchUser() async -> User? {
            guard let user = await self.userActor.getUser() else {
                return nil
            }
            return user
        }
        """

        let result = await aiService.summarizeDiff(
            leftContent: leftCode,
            rightContent: rightCode,
            leftTitle: "Old.swift",
            rightTitle: "New.swift",
            language: .zhHans
        )

        XCTAssertTrue(result.modelName.contains("Gemma 3 1B"))
        XCTAssertFalse(result.modelName.contains("本地离线"))
        XCTAssertFalse(result.oneLineIntent.isEmpty)
        XCTAssertFalse(result.keyModifications.isEmpty)
        XCTAssertTrue(!result.conventionalCommit.isEmpty)
    }

    func testModelMultiLanguageLocalization() {
        let b1 = AIModelId.gemma3_1b
        let b4 = AIModelId.gemma3_4b

        // Chinese
        XCTAssertTrue(b1.localizedDisplayName(for: .zhHans).contains("推荐"))
        XCTAssertTrue(b1.localizedSubtitle(for: .zhHans).contains("32K"))
        XCTAssertTrue(b4.localizedDisplayName(for: .zhHans).contains("进阶"))

        // English
        XCTAssertTrue(b1.localizedDisplayName(for: .en).contains("Recommended"))
        XCTAssertTrue(b1.localizedSubtitle(for: .en).contains("32K"))
        XCTAssertTrue(b4.localizedDisplayName(for: .en).contains("Advanced"))

        // Japanese
        XCTAssertTrue(b1.localizedDisplayName(for: .ja).contains("推奨"))
        XCTAssertTrue(b1.localizedSubtitle(for: .ja).contains("32K"))
        XCTAssertTrue(b4.localizedDisplayName(for: .ja).contains("高度"))
    }

    func testAIServiceJapaneseSummary() async {
        let aiService = AIService.shared
        let result = await aiService.summarizeDiff(
            leftContent: "let a = 1",
            rightContent: "let a = 2\nlet b = 3",
            leftTitle: "old",
            rightTitle: "new",
            language: .ja
        )
        XCTAssertFalse(result.oneLineIntent.isEmpty)
        XCTAssertFalse(result.modelName.isEmpty)
    }

    func testAIServiceEnvShellConfigDiff() async {
        let aiService = AIService.shared
        let left = """
        export ARCH=arch64
        export RELEASE=1
        """
        let right = """
        export ARCH=arm64
        export RELEASE=2
        """
        let result = await aiService.summarizeDiff(
            leftContent: left,
            rightContent: right,
            leftTitle: "env.sh",
            rightTitle: "env のコピー.sh",
            language: .zhHans
        )
        XCTAssertTrue(result.modelName.contains("Gemma 3 1B"))
        XCTAssertFalse(result.oneLineIntent.isEmpty)
        XCTAssertFalse(result.conventionalCommit.isEmpty)
    }

    func testCloudProviderPresets() {
        let deepseek = CloudProviderPreset.deepseek
        XCTAssertEqual(deepseek.defaultBaseURL, "https://api.deepseek.com/v1")
        XCTAssertEqual(deepseek.defaultModel, "deepseek-chat")

        let openai = CloudProviderPreset.openai
        XCTAssertEqual(openai.defaultBaseURL, "https://api.openai.com/v1")
        XCTAssertEqual(openai.defaultModel, "gpt-4o-mini")

        let ollama = CloudProviderPreset.ollama
        XCTAssertEqual(ollama.defaultBaseURL, "http://localhost:11434/v1")
        XCTAssertEqual(ollama.defaultModel, "qwen2.5-coder:7b")

        let custom = CloudProviderPreset.custom
        XCTAssertTrue(custom.defaultBaseURL.isEmpty)
    }

    func testCloudAPIConfigPersistence() {
        var config = CloudAPIConfig.default
        config.baseURL = "https://custom.api.com/v1"
        config.apiKey = "sk-test-12345"
        config.modelName = "custom-model"
        config.save()

        let loaded = CloudAPIConfig.load()
        XCTAssertEqual(loaded.baseURL, "https://custom.api.com/v1")
        XCTAssertEqual(loaded.apiKey, "sk-test-12345")
        XCTAssertEqual(loaded.modelName, "custom-model")
    }

    func testCloudAPIResponseParsing() {
        let sampleOutput = """
        ```json
        {
          "intent": "升级系统构建参数以支持新硬件架构",
          "points": [
            "将 ARCH 从 arch64 修改为 arm64",
            "将 RELEASE 版本从 1 提升至 2"
          ],
          "commit": "chore(build): bump release to 2 and switch arch to arm64"
        }
        ```
        """
        let payload = AIService.parseLLMJSON(from: sampleOutput)
        XCTAssertNotNil(payload)
        XCTAssertEqual(payload?.intent, "升级系统构建参数以支持新硬件架构")
        XCTAssertEqual(payload?.points?.count, 2)
        XCTAssertEqual(payload?.commit, "chore(build): bump release to 2 and switch arch to arm64")
    }
}
