import Foundation
import SwiftUI
import AppKit

public struct GitHubAsset: Codable, Sendable {
    public let name: String
    public let browserDownloadUrl: String
    public let size: Int64

    enum CodingKeys: String, CodingKey {
        case name
        case browserDownloadUrl = "browser_download_url"
        case size
    }
}

public struct GitHubRelease: Codable, Sendable {
    public let tagName: String
    public let name: String?
    public let body: String?
    public let htmlUrl: String
    public let publishedAt: String?
    public let draft: Bool?
    public let prerelease: Bool?
    public let assets: [GitHubAsset]?

    enum CodingKeys: String, CodingKey {
        case tagName = "tag_name"
        case name
        case body
        case htmlUrl = "html_url"
        case publishedAt = "published_at"
        case draft
        case prerelease
        case assets
    }
}

public enum UpdateCheckStatus: Equatable, Sendable {
    case idle
    case checking
    case updateAvailable(version: String, releaseNotes: String, releaseUrl: URL)
    case upToDate(version: String)
    case failed(message: String)
}

public enum UpdateStage: Equatable, Sendable {
    case prompt
    case downloading(progress: Double, downloadedBytes: Int64, totalBytes: Int64)
    case extracting
    case readyToInstall(extractedAppPath: URL)
    case failed(message: String)
}

// MARK: - Download Delegate (Streamed Progress)

final class UpdateDownloadDelegate: NSObject, URLSessionDownloadDelegate, @unchecked Sendable {
    var onProgress: (@Sendable (Double, Int64, Int64) -> Void)?
    var onFinish: (@Sendable (URL) -> Void)?
    var onError: (@Sendable (Error) -> Void)?

    func urlSession(
        _ session: URLSession,
        downloadTask: URLSessionDownloadTask,
        didWriteData bytesWritten: Int64,
        totalBytesWritten: Int64,
        totalBytesExpectedToWrite: Int64
    ) {
        let expected = totalBytesExpectedToWrite > 0
            ? totalBytesExpectedToWrite
            : (downloadTask.countOfBytesExpectedToReceive > 0 ? downloadTask.countOfBytesExpectedToReceive : 1)
        let progress = max(0.0, min(1.0, Double(totalBytesWritten) / Double(expected)))
        onProgress?(progress, totalBytesWritten, expected)
    }

    func urlSession(
        _ session: URLSession,
        downloadTask: URLSessionDownloadTask,
        didFinishDownloadingTo location: URL
    ) {
        let tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("MacCompareUpdate", isDirectory: true)
        try? FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
        let destination = tempDir.appendingPathComponent("update.zip")
        try? FileManager.default.removeItem(at: destination)
        do {
            try FileManager.default.moveItem(at: location, to: destination)
            onFinish?(destination)
        } catch {
            onError?(error)
        }
    }

    func urlSession(
        _ session: URLSession,
        task: URLSessionTask,
        didCompleteWithError error: Error?
    ) {
        if let error = error {
            let nsError = error as NSError
            if nsError.code != NSURLErrorCancelled {
                onError?(error)
            }
        }
    }
}

// MARK: - Update Checker Service

@MainActor
@Observable
public final class UpdateCheckerService {
    public static let shared = UpdateCheckerService()

    public var status: UpdateCheckStatus = .idle
    public var stage: UpdateStage = .prompt
    public var showUpdateSheet: Bool = false
    public var showUpToDateAlert: Bool = false

    public var lastCheckedDate: Date? {
        get {
            let timestamp = UserDefaults.standard.double(forKey: "last_update_check_timestamp")
            return timestamp > 0 ? Date(timeIntervalSince1970: timestamp) : nil
        }
        set {
            if let date = newValue {
                UserDefaults.standard.set(date.timeIntervalSince1970, forKey: "last_update_check_timestamp")
            }
        }
    }

    public var isAutoCheckEnabled: Bool {
        get {
            if UserDefaults.standard.object(forKey: "auto_check_updates_on_launch") == nil {
                return true
            }
            return UserDefaults.standard.bool(forKey: "auto_check_updates_on_launch")
        }
        set {
            UserDefaults.standard.set(newValue, forKey: "auto_check_updates_on_launch")
        }
    }

    public var skippedVersion: String? {
        get {
            UserDefaults.standard.string(forKey: "skipped_update_version")
        }
        set {
            UserDefaults.standard.set(newValue, forKey: "skipped_update_version")
        }
    }

    public var currentVersion: String {
        Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "0.5.0"
    }

    public var latestReleaseVersion: String = ""
    public var latestReleaseNotes: String = ""
    public var latestReleaseUrl: URL = URL(string: "https://github.com/andychao217/FileCompare/releases")!
    public var downloadAssetUrl: URL?
    public var downloadAssetSize: Int64 = 0
    public var downloadAssetName: String = ""

    private var activeDownloadSession: URLSession?
    private var activeDownloadTask: URLSessionDownloadTask?
    private var downloadDelegate: UpdateDownloadDelegate?

    private init() {}

    // MARK: - Check For Updates

    public func checkForUpdates(isUserInitiated: Bool = false) {
        guard status != .checking else { return }
        status = .checking
        stage = .prompt

        let repo = "andychao217/FileCompare"
        guard let url = URL(string: "https://api.github.com/repos/\(repo)/releases/latest") else {
            status = .failed(message: "Invalid update URL")
            return
        }

        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.setValue("application/vnd.github.v3+json", forHTTPHeaderField: "Accept")
        request.setValue("MacCompare/\(currentVersion)", forHTTPHeaderField: "User-Agent")
        request.timeoutInterval = 10

        Task {
            do {
                let (data, response) = try await URLSession.shared.data(for: request)
                guard let httpResponse = response as? HTTPURLResponse else {
                    self.status = .failed(message: "Invalid network response")
                    return
                }

                if httpResponse.statusCode == 200 {
                    let release = try JSONDecoder().decode(GitHubRelease.self, from: data)
                    let latestVersion = release.tagName.trimmingCharacters(in: CharacterSet(charactersIn: "vV"))
                    self.lastCheckedDate = Date()

                    if self.isNewerVersion(latest: latestVersion, current: self.currentVersion) {
                        // Check if user skipped this version
                        if !isUserInitiated && self.skippedVersion == latestVersion {
                            self.status = .idle
                            return
                        }

                        self.latestReleaseVersion = latestVersion
                        self.latestReleaseNotes = release.body ?? ""
                        self.latestReleaseUrl = URL(string: release.htmlUrl) ?? URL(string: "https://github.com/andychao217/FileCompare/releases")!

                        // Locate zip asset
                        self.resolveDownloadAsset(release: release, version: latestVersion)

                        self.status = .updateAvailable(
                            version: latestVersion,
                            releaseNotes: self.latestReleaseNotes,
                            releaseUrl: self.latestReleaseUrl
                        )
                        self.stage = .prompt
                        self.showUpdateSheet = true
                    } else {
                        self.status = .upToDate(version: self.currentVersion)
                        if isUserInitiated {
                            self.showUpToDateAlert = true
                        }
                    }
                } else if httpResponse.statusCode == 404 {
                    self.status = .upToDate(version: self.currentVersion)
                    if isUserInitiated {
                        self.showUpToDateAlert = true
                    }
                } else {
                    self.status = .failed(message: "HTTP \(httpResponse.statusCode)")
                }
            } catch {
                self.status = .failed(message: error.localizedDescription)
            }
        }
    }

    private func resolveDownloadAsset(release: GitHubRelease, version: String) {
        if let assets = release.assets {
            // Prioritize zip containing MacCompare
            if let zipAsset = assets.first(where: { $0.name.hasSuffix(".zip") && $0.name.contains("MacCompare") })
                ?? assets.first(where: { $0.name.hasSuffix(".zip") }) {
                self.downloadAssetUrl = URL(string: zipAsset.browserDownloadUrl)
                self.downloadAssetSize = zipAsset.size
                self.downloadAssetName = zipAsset.name
                return
            }
        }
        // Construct standard fallback download URL for newly created release assets
        let fallback = "https://github.com/andychao217/FileCompare/releases/download/v\(version)/MacCompare-\(version)-macos.zip"
        self.downloadAssetUrl = URL(string: fallback)
        self.downloadAssetSize = 25 * 1024 * 1024 // ~25MB estimated
        self.downloadAssetName = "MacCompare-\(version)-macos.zip"
    }

    // MARK: - Download & Extract Flow

    public func startDownload() {
        guard let downloadUrl = downloadAssetUrl else {
            openDownloadPage()
            return
        }

        stage = .downloading(progress: 0.0, downloadedBytes: 0, totalBytes: downloadAssetSize)

        let delegate = UpdateDownloadDelegate()
        self.downloadDelegate = delegate

        delegate.onProgress = { [weak self] progress, written, expected in
            Task { @MainActor in
                guard let self = self else { return }
                self.stage = .downloading(progress: progress, downloadedBytes: written, totalBytes: expected)
            }
        }

        delegate.onFinish = { [weak self] downloadedZipUrl in
            Task { @MainActor in
                guard let self = self else { return }
                self.extractArchive(zipUrl: downloadedZipUrl)
            }
        }

        delegate.onError = { [weak self] error in
            Task { @MainActor in
                guard let self = self else { return }
                self.stage = .failed(message: error.localizedDescription)
            }
        }

        let config = URLSessionConfiguration.default
        let session = URLSession(configuration: config, delegate: delegate, delegateQueue: nil)
        self.activeDownloadSession = session

        var request = URLRequest(url: downloadUrl)
        request.setValue("MacCompare/\(currentVersion)", forHTTPHeaderField: "User-Agent")
        let task = session.downloadTask(with: request)
        self.activeDownloadTask = task
        task.resume()
    }

    public func cancelDownload() {
        activeDownloadTask?.cancel()
        activeDownloadTask = nil
        activeDownloadSession?.invalidateAndCancel()
        activeDownloadSession = nil
        downloadDelegate = nil

        cleanupTempDirectory()
        stage = .prompt
        showUpdateSheet = false
    }

    public func skipVersion() {
        skippedVersion = latestReleaseVersion
        cancelDownload()
    }

    private func extractArchive(zipUrl: URL) {
        stage = .extracting

        Task.detached(priority: .userInitiated) {
            let tempExtractDir = FileManager.default.temporaryDirectory
                .appendingPathComponent("MacCompareExtracted", isDirectory: true)

            try? FileManager.default.removeItem(at: tempExtractDir)
            try? FileManager.default.createDirectory(at: tempExtractDir, withIntermediateDirectories: true)

            // Use macOS native ditto tool to extract zip preserving permissions and attributes
            let dittoProcess = Process()
            dittoProcess.executableURL = URL(fileURLWithPath: "/usr/bin/ditto")
            dittoProcess.arguments = ["-x", "-k", zipUrl.path, tempExtractDir.path]

            do {
                try dittoProcess.run()
                dittoProcess.waitUntilExit()

                guard dittoProcess.terminationStatus == 0 else {
                    await MainActor.run {
                        self.stage = .failed(message: "Failed to extract update package.")
                    }
                    return
                }

                // Locate MacCompare.app in extracted directory
                let appUrl = tempExtractDir.appendingPathComponent("MacCompare.app")
                guard FileManager.default.fileExists(atPath: appUrl.path) else {
                    await MainActor.run {
                        self.stage = .failed(message: "Extracted package does not contain MacCompare.app.")
                    }
                    return
                }

                // Remove quarantine flag on extracted app
                let xattrProcess = Process()
                xattrProcess.executableURL = URL(fileURLWithPath: "/usr/bin/xattr")
                xattrProcess.arguments = ["-cr", appUrl.path]
                try? xattrProcess.run()
                xattrProcess.waitUntilExit()

                await MainActor.run {
                    self.stage = .readyToInstall(extractedAppPath: appUrl)
                }
            } catch {
                await MainActor.run {
                    self.stage = .failed(message: "Extraction error: \(error.localizedDescription)")
                }
            }
        }
    }

    // MARK: - Install & Relaunch

    public func installAndRelaunch(extractedAppPath: URL) {
        let currentAppUrl = Bundle.main.bundleURL
        let targetPath = currentAppUrl.path
        let newAppPath = extractedAppPath.path
        let pid = ProcessInfo.processInfo.processIdentifier

        // Detached shell script to wait for current process exit, replace app bundle, and relaunch
        let script = """
        while kill -0 \(pid) 2>/dev/null; do
            sleep 0.2
        done
        rm -rf "\(targetPath)"
        cp -R "\(newAppPath)" "\(targetPath)"
        open "\(targetPath)"
        rm -rf "\(extractedAppPath.deletingLastPathComponent().path)"
        """

        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/bin/sh")
        process.arguments = ["-c", script]

        do {
            try process.run()
            NSApp.terminate(nil)
        } catch {
            self.stage = .failed(message: "Failed to relaunch: \(error.localizedDescription)")
        }
    }

    private func cleanupTempDirectory() {
        let tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("MacCompareUpdate", isDirectory: true)
        let extractDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("MacCompareExtracted", isDirectory: true)
        try? FileManager.default.removeItem(at: tempDir)
        try? FileManager.default.removeItem(at: extractDir)
    }

    // MARK: - Version Comparison

    public func isNewerVersion(latest: String, current: String) -> Bool {
        let latestComponents = latest.split(separator: ".").compactMap { Int($0) }
        let currentComponents = current.split(separator: ".").compactMap { Int($0) }

        for i in 0..<max(latestComponents.count, currentComponents.count) {
            let l = i < latestComponents.count ? latestComponents[i] : 0
            let c = i < currentComponents.count ? currentComponents[i] : 0
            if l > c { return true }
            if l < c { return false }
        }
        return false
    }

    public func openDownloadPage() {
        NSWorkspace.shared.open(latestReleaseUrl)
    }

    // MARK: - Multi-Language Release Notes Parsing

    public func localizedReleaseNotes(for language: AppLanguage) -> String {
        let raw = latestReleaseNotes.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !raw.isEmpty else { return "" }

        let targetTags: [String]
        switch language {
        case .zhHans:
            targetTags = ["<!-- zh-Hans -->", "<!-- zh_CN -->", "<!-- zh -->", "## 简体中文", "## 中文"]
        case .ja:
            targetTags = ["<!-- ja -->", "<!-- ja_JP -->", "## 日本語", "## 日本语"]
        case .en, .system:
            targetTags = ["<!-- en -->", "<!-- en_US -->", "## English"]
        }

        let sectionDelimiter = "(<!--\\s*[a-zA-Z0-9_-]+\\s*-->|##\\s+[^\\n\\r]+)"

        // 1. Try finding matching section for current language
        for tag in targetTags {
            if let range = raw.range(of: tag, options: .caseInsensitive) {
                let afterTag = raw[range.upperBound...]
                if let nextMatch = afterTag.range(of: sectionDelimiter, options: .regularExpression) {
                    let section = afterTag[..<nextMatch.lowerBound].trimmingCharacters(in: .whitespacesAndNewlines)
                    if !section.isEmpty { return section }
                } else {
                    let section = afterTag.trimmingCharacters(in: .whitespacesAndNewlines)
                    if !section.isEmpty { return section }
                }
            }
        }

        // 2. Fallback to English if non-English requested but missing
        if language != .en && language != .system {
            for tag in ["<!-- en -->", "<!-- en_US -->", "## English"] {
                if let range = raw.range(of: tag, options: .caseInsensitive) {
                    let afterTag = raw[range.upperBound...]
                    if let nextMatch = afterTag.range(of: sectionDelimiter, options: .regularExpression) {
                        let section = afterTag[..<nextMatch.lowerBound].trimmingCharacters(in: .whitespacesAndNewlines)
                        if !section.isEmpty { return section }
                    } else {
                        let section = afterTag.trimmingCharacters(in: .whitespacesAndNewlines)
                        if !section.isEmpty { return section }
                    }
                }
            }
        }

        // 3. Fallback: Return raw notes if no section tags found
        return raw
    }
}
