import Foundation
import SwiftUI

@MainActor
@Observable
public final class AIModelManager {
    public static let shared = AIModelManager()

    public var executionMode: AIExecutionMode {
        didSet {
            UserDefaults.standard.set(executionMode.rawValue, forKey: "ai_execution_mode")
        }
    }

    public var autoReleaseMemory: Bool {
        didSet {
            UserDefaults.standard.set(autoReleaseMemory, forKey: "ai_auto_release_memory")
        }
    }

    public var selectedModel: AIModelId {
        didSet {
            UserDefaults.standard.set(selectedModel.rawValue, forKey: "ai_selected_model")
        }
    }

    /// 获取当前实际可用于推理的模型（优先选中的且已下载的模型，否则返回任意已下载模型，若都未下载则返回选中的模型）
    public var effectiveModel: AIModelId {
        if isModelDownloaded(selectedModel) {
            return selectedModel
        }
        for model in AIModelId.allCases {
            if isModelDownloaded(model) {
                return model
            }
        }
        return selectedModel
    }

    /// 云端 API 配置
    public var cloudConfig: CloudAPIConfig {
        didSet {
            cloudConfig.save()
        }
    }

    /// 当前有效模型的简称（云端模式返回配置的模型名，本地模式返回本地已就绪模型的 shortName）
    public var activeModelName: String {
        if executionMode == .cloudAPI && !cloudConfig.modelName.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return cloudConfig.modelName.trimmingCharacters(in: .whitespacesAndNewlines)
        }
        return effectiveModel.shortName
    }

    // Model States
    public var modelStates: [AIModelId: AIModelDownloadState] = [:]

    // Active download task
    private var downloadTask: URLSessionDownloadTask?
    private var urlSession: URLSession?
    private var downloadDelegate: ModelDownloadDelegate?
    private var activeModelDownloading: AIModelId?
    private var lastBytesWritten: Int64 = 0
    private var lastSpeedCheckTime: Date?

    private init() {
        let savedMode = UserDefaults.standard.string(forKey: "ai_execution_mode") ?? AIExecutionMode.localOffline.rawValue
        self.executionMode = AIExecutionMode(rawValue: savedMode) ?? .localOffline

        self.autoReleaseMemory = UserDefaults.standard.object(forKey: "ai_auto_release_memory") as? Bool ?? true

        let savedModel = UserDefaults.standard.string(forKey: "ai_selected_model") ?? AIModelId.gemma3_1b.rawValue
        self.selectedModel = AIModelId(rawValue: savedModel) ?? .gemma3_1b

        self.cloudConfig = CloudAPIConfig.load()
        refreshAllModelStatuses()
    }

    public var modelsDirectoryURL: URL {
        let fileManager = FileManager.default
        let appSupport = fileManager.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        let dir = appSupport.appendingPathComponent("MacCompare/models", isDirectory: true)
        if !fileManager.fileExists(atPath: dir.path) {
            try? fileManager.createDirectory(at: dir, withIntermediateDirectories: true)
        }
        return dir
    }

    public func localFileURL(for model: AIModelId) -> URL {
        modelsDirectoryURL.appendingPathComponent(model.filename)
    }

    public func isModelDownloaded(_ model: AIModelId) -> Bool {
        let fileURL = localFileURL(for: model)
        guard FileManager.default.fileExists(atPath: fileURL.path) else {
            return false
        }
        // Basic check for file size > 10MB to avoid incomplete stub files
        if let attrs = try? FileManager.default.attributesOfItem(atPath: fileURL.path),
           let size = attrs[.size] as? Int64 {
            return size > 10 * 1024 * 1024
        }
        return false
    }

    public func refreshAllModelStatuses() {
        for model in AIModelId.allCases {
            if activeModelDownloading == model {
                continue
            }
            if isModelDownloaded(model) {
                modelStates[model] = .ready
            } else {
                modelStates[model] = .notDownloaded
            }
        }
    }

    public var isDefaultModelReady: Bool {
        isModelDownloaded(.gemma3_1b)
    }

    // MARK: - Download Actions

    public func startDownload(model: AIModelId, onCompleted: (() -> Void)? = nil) {
        guard activeModelDownloading == nil else { return }

        activeModelDownloading = model
        let preparingText = LanguageManager.shared.text(.aiPreparing)
        let estimatingText = LanguageManager.shared.text(.aiEstimating)
        modelStates[model] = .downloading(progress: 0.0, speedText: preparingText, remainingText: estimatingText)
        lastSpeedCheckTime = Date()
        lastBytesWritten = 0

        let destination = localFileURL(for: model)
        let delegate = ModelDownloadDelegate(manager: self, model: model, destinationURL: destination)
        self.downloadDelegate = delegate

        let config = URLSessionConfiguration.default
        let session = URLSession(configuration: config, delegate: delegate, delegateQueue: OperationQueue.main)
        self.urlSession = session

        let task = session.downloadTask(with: model.downloadURL)
        self.downloadTask = task
        task.resume()
    }

    public func cancelDownload(for model: AIModelId) {
        guard activeModelDownloading == model else { return }
        downloadTask?.cancel()
        downloadTask = nil
        urlSession?.invalidateAndCancel()
        urlSession = nil
        downloadDelegate = nil
        activeModelDownloading = nil
        modelStates[model] = .notDownloaded
    }

    public func deleteModel(model: AIModelId) {
        let fileURL = localFileURL(for: model)
        try? FileManager.default.removeItem(at: fileURL)
        modelStates[model] = .notDownloaded
        if selectedModel == model {
            for m in AIModelId.allCases where m != model && isModelDownloaded(m) {
                selectedModel = m
                break
            }
        }
    }

    // MARK: - Internal Delegate Callbacks (MainActor)

    fileprivate func handleDownloadProgress(
        bytesWritten: Int64,
        totalBytesWritten: Int64,
        totalBytesExpectedToWrite: Int64
    ) {
        guard let model = self.activeModelDownloading else { return }

        let progress: Double
        if totalBytesExpectedToWrite > 0 {
            progress = Double(totalBytesWritten) / Double(totalBytesExpectedToWrite)
        } else {
            let est = Int64(model.estimatedSizeMB) * 1024 * 1024
            progress = min(0.99, Double(totalBytesWritten) / Double(est))
        }

        let now = Date()
        let timeDiff = now.timeIntervalSince(self.lastSpeedCheckTime ?? now)
        let speedText: String
        let remainingText: String

        if timeDiff >= 0.5 {
            let bytesDiff = totalBytesWritten - self.lastBytesWritten
            let bytesPerSec = Double(bytesDiff) / max(0.1, timeDiff)
            let mbPerSec = bytesPerSec / (1024 * 1024)
            speedText = String(format: "%.1f MB/s", mbPerSec)

            if mbPerSec > 0.05, totalBytesExpectedToWrite > totalBytesWritten {
                let remBytes = Double(totalBytesExpectedToWrite - totalBytesWritten)
                let remSecs = Int(remBytes / bytesPerSec)
                if remSecs < 60 {
                    remainingText = String(format: LanguageManager.shared.text(.aiRemainingSeconds), remSecs)
                } else {
                    remainingText = String(format: LanguageManager.shared.text(.aiRemainingMinutes), max(1, remSecs / 60))
                }
            } else {
                remainingText = LanguageManager.shared.text(.aiCalculating)
            }

            self.lastSpeedCheckTime = now
            self.lastBytesWritten = totalBytesWritten
        } else {
            if case let .downloading(_, prevSpeed, prevRem) = self.modelStates[model] {
                speedText = prevSpeed
                remainingText = prevRem
            } else {
                speedText = "0 MB/s"
                remainingText = ""
            }
        }

        self.modelStates[model] = .downloading(
            progress: progress,
            speedText: speedText,
            remainingText: remainingText
        )
    }

    fileprivate func handleDownloadSuccess(for model: AIModelId) {
        guard self.activeModelDownloading == model else { return }
        self.modelStates[model] = .ready
        self.selectedModel = model
        self.activeModelDownloading = nil
        self.downloadTask = nil
        self.downloadDelegate = nil
        self.urlSession?.finishTasksAndInvalidate()
        self.urlSession = nil
    }

    fileprivate func handleSaveError(for model: AIModelId, error: Error) {
        guard self.activeModelDownloading == model else { return }
        let failText = LanguageManager.shared.text(.aiSaveModelFailed)
        self.modelStates[model] = .error("\(failText): \(error.localizedDescription)")
        self.activeModelDownloading = nil
        self.downloadTask = nil
        self.downloadDelegate = nil
        self.urlSession?.finishTasksAndInvalidate()
        self.urlSession = nil
    }

    fileprivate func handleDownloadError(_ error: Error) {
        guard let model = self.activeModelDownloading else { return }
        if let urlError = error as? URLError, urlError.code == .cancelled {
            self.modelStates[model] = .notDownloaded
            self.activeModelDownloading = nil
            self.downloadTask = nil
            self.downloadDelegate = nil
            self.urlSession?.finishTasksAndInvalidate()
            self.urlSession = nil
            return
        }

        let failText = LanguageManager.shared.text(.aiDownloadFailed)
        self.modelStates[model] = .error("\(failText): \(error.localizedDescription)")
        self.activeModelDownloading = nil
        self.downloadTask = nil
        self.downloadDelegate = nil
        self.urlSession?.finishTasksAndInvalidate()
        self.urlSession = nil
    }
}

// MARK: - Isolated Session Delegate
private final class ModelDownloadDelegate: NSObject, URLSessionDownloadDelegate, @unchecked Sendable {
    private weak var manager: AIModelManager?
    private let model: AIModelId
    private let destinationURL: URL

    init(manager: AIModelManager, model: AIModelId, destinationURL: URL) {
        self.manager = manager
        self.model = model
        self.destinationURL = destinationURL
        super.init()
    }

    func urlSession(
        _ session: URLSession,
        downloadTask: URLSessionDownloadTask,
        didWriteData bytesWritten: Int64,
        totalBytesWritten: Int64,
        totalBytesExpectedToWrite: Int64
    ) {
        Task { @MainActor in
            self.manager?.handleDownloadProgress(
                bytesWritten: bytesWritten,
                totalBytesWritten: totalBytesWritten,
                totalBytesExpectedToWrite: totalBytesExpectedToWrite
            )
        }
    }

    func urlSession(_ session: URLSession, downloadTask: URLSessionDownloadTask, didFinishDownloadingTo location: URL) {
        // IMPORTANT: In URLSession, the temporary file at `location` is destroyed by the system
        // immediately upon this method returning. We MUST move the file synchronously here.
        do {
            let parentDir = destinationURL.deletingLastPathComponent()
            if !FileManager.default.fileExists(atPath: parentDir.path) {
                try FileManager.default.createDirectory(at: parentDir, withIntermediateDirectories: true)
            }
            if FileManager.default.fileExists(atPath: destinationURL.path) {
                try FileManager.default.removeItem(at: destinationURL)
            }
            try FileManager.default.moveItem(at: location, to: destinationURL)

            Task { @MainActor in
                self.manager?.handleDownloadSuccess(for: self.model)
            }
        } catch {
            Task { @MainActor in
                self.manager?.handleSaveError(for: self.model, error: error)
            }
        }
    }

    func urlSession(_ session: URLSession, task: URLSessionTask, didCompleteWithError error: (any Error)?) {
        if let error {
            Task { @MainActor in
                self.manager?.handleDownloadError(error)
            }
        }
    }
}
