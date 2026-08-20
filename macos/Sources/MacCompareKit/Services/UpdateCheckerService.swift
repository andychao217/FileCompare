import Foundation
import SwiftUI
import AppKit

public struct GitHubRelease: Codable, Sendable {
    public let tagName: String
    public let name: String?
    public let body: String?
    public let htmlUrl: String
    public let publishedAt: String?
    public let draft: Bool?
    public let prerelease: Bool?

    enum CodingKeys: String, CodingKey {
        case tagName = "tag_name"
        case name
        case body
        case htmlUrl = "html_url"
        case publishedAt = "published_at"
        case draft
        case prerelease
    }
}

public enum UpdateCheckStatus: Equatable, Sendable {
    case idle
    case checking
    case updateAvailable(version: String, releaseNotes: String, releaseUrl: URL)
    case upToDate(version: String)
    case failed(message: String)
}

@MainActor
@Observable
public final class UpdateCheckerService {
    public static let shared = UpdateCheckerService()

    public var status: UpdateCheckStatus = .idle
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
                return true // default enabled
            }
            return UserDefaults.standard.bool(forKey: "auto_check_updates_on_launch")
        }
        set {
            UserDefaults.standard.set(newValue, forKey: "auto_check_updates_on_launch")
        }
    }

    public var currentVersion: String {
        Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "0.1.0"
    }

    public var latestReleaseVersion: String = ""
    public var latestReleaseNotes: String = ""
    public var latestReleaseUrl: URL = URL(string: "https://github.com/andychao217/FileCompare/releases")!

    private init() {}

    public func checkForUpdates(isUserInitiated: Bool = false) {
        guard status != .checking else { return }
        status = .checking

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
                        self.latestReleaseVersion = latestVersion
                        self.latestReleaseNotes = release.body ?? ""
                        self.latestReleaseUrl = URL(string: release.htmlUrl) ?? URL(string: "https://github.com/andychao217/FileCompare/releases")!
                        self.status = .updateAvailable(version: latestVersion, releaseNotes: self.latestReleaseNotes, releaseUrl: self.latestReleaseUrl)
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
}
