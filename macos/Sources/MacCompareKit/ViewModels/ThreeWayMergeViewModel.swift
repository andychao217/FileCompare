import Foundation
import SwiftUI
import AppKit

@MainActor
@Observable
public final class ThreeWayMergeViewModel {
    public var filePath: String = "No Files Selected"

    public var localBranchName: String = "Local (Current)"
    public var baseBranchName: String = "Base (Ancestor)"
    public var remoteBranchName: String = "Remote (Incoming)"

    public var localFileURL: URL?
    public var baseFileURL: URL?
    public var remoteFileURL: URL?
    public var outputFileURL: URL?

    public var localContent: String = "" {
        didSet { scheduleMergeComputation() }
    }
    public var baseContent: String = "" {
        didSet { scheduleMergeComputation() }
    }
    public var remoteContent: String = "" {
        didSet { scheduleMergeComputation() }
    }

    public var mergeResult: MergeResult = MergeResult()
    public var currentConflictIndex: Int = 0
    public var totalConflicts: Int = 0
    public var statusMessage: String?

    public var conflictHunks: [[MergeLine]] = []

    private let diffEngine: DiffEngineProtocol
    private var debounceTask: Task<Void, Never>?

    public var hasFilesLoaded: Bool {
        localFileURL != nil || baseFileURL != nil || remoteFileURL != nil || !localContent.isEmpty || !baseContent.isEmpty || !remoteContent.isEmpty
    }

    public init(
        diffEngine: DiffEngineProtocol = DiffEngineService.shared,
        localURL: URL? = nil,
        baseURL: URL? = nil,
        remoteURL: URL? = nil,
        outputURL: URL? = nil
    ) {
        self.diffEngine = diffEngine
        self.localFileURL = localURL
        self.baseFileURL = baseURL
        self.remoteFileURL = remoteURL
        self.outputFileURL = outputURL

        if let l = localURL, let b = baseURL, let r = remoteURL {
            loadFiles(local: l, base: b, remote: r, output: outputURL)
        }
    }

    public func clearAll() {
        localFileURL = nil
        baseFileURL = nil
        remoteFileURL = nil
        outputFileURL = nil
        filePath = LanguageManager.shared.text(.noFilesSelected)
        localContent = ""
        baseContent = ""
        remoteContent = ""
        mergeResult = MergeResult()
        conflictHunks = []
        currentConflictIndex = 0
        totalConflicts = 0
        statusMessage = nil
    }

    public func loadFiles(local: URL, base: URL, remote: URL, output: URL? = nil) {
        self.localFileURL = local
        self.baseFileURL = base
        self.remoteFileURL = remote
        self.outputFileURL = output ?? local
        self.filePath = local.lastPathComponent

        do {
            self.localContent = try diffEngine.loadFile(from: local, encoding: .utf8)
            self.baseContent = try diffEngine.loadFile(from: base, encoding: .utf8)
            self.remoteContent = try diffEngine.loadFile(from: remote, encoding: .utf8)
            Task { await recomputeMerge() }
        } catch {
            self.statusMessage = "Error: \(error.localizedDescription)"
        }
    }

    public enum MergeFileType: Sendable {
        case local, base, remote
    }

    public func loadSingleFile(from url: URL, type: MergeFileType) {
        do {
            let content = try diffEngine.loadFile(from: url, encoding: .utf8)
            switch type {
            case .local:
                self.localFileURL = url
                self.localContent = content
                self.filePath = url.lastPathComponent
            case .base:
                self.baseFileURL = url
                self.baseContent = content
            case .remote:
                self.remoteFileURL = url
                self.remoteContent = content
            }
            Task { await recomputeMerge() }
        } catch {
            self.statusMessage = "Error loading \(url.lastPathComponent): \(error.localizedDescription)"
        }
    }

    public func openLocalFile() {
        let panel = NSOpenPanel()
        panel.title = "Select Local (Current Branch) File"
        if panel.runModal() == .OK, let url = panel.url {
            loadSingleFile(from: url, type: .local)
        }
    }

    public func openBaseFile() {
        let panel = NSOpenPanel()
        panel.title = "Select Base (Common Ancestor) File"
        if panel.runModal() == .OK, let url = panel.url {
            loadSingleFile(from: url, type: .base)
        }
    }

    public func openRemoteFile() {
        let panel = NSOpenPanel()
        panel.title = "Select Remote (Incoming Branch) File"
        if panel.runModal() == .OK, let url = panel.url {
            loadSingleFile(from: url, type: .remote)
        }
    }

    private func scheduleMergeComputation() {
        debounceTask?.cancel()
        debounceTask = Task {
            try? await Task.sleep(nanoseconds: 200_000_000)
            guard !Task.isCancelled else { return }
            await recomputeMerge()
        }
    }

    public func recomputeMerge() async {
        guard !localContent.isEmpty || !baseContent.isEmpty || !remoteContent.isEmpty else {
            self.mergeResult = MergeResult()
            self.totalConflicts = 0
            return
        }

        let res = await diffEngine.mergeThreeWay(
            local: localContent,
            base: baseContent,
            remote: remoteContent
        )
        self.mergeResult = res
        self.totalConflicts = Int(res.conflictCount)
    }

    // MARK: - Conflict Resolution Actions

    public func nextConflict() {
        guard totalConflicts > 0 else { return }
        if currentConflictIndex < totalConflicts - 1 {
            currentConflictIndex += 1
        } else {
            currentConflictIndex = 0
        }
    }

    public func previousConflict() {
        guard totalConflicts > 0 else { return }
        if currentConflictIndex > 0 {
            currentConflictIndex -= 1
        } else {
            currentConflictIndex = totalConflicts - 1
        }
    }

    public func acceptLocal(conflictIndex: Int? = nil) {
        let idx = conflictIndex ?? currentConflictIndex
        for i in 0..<mergeResult.lines.count {
            if mergeResult.lines[i].conflictIndex == idx {
                mergeResult.lines[i].resolvedContent = mergeResult.lines[i].contentLocal
                mergeResult.lines[i].status = .cleanLocal
            }
        }
        rebuildMergedText()
    }

    public func acceptRemote(conflictIndex: Int? = nil) {
        let idx = conflictIndex ?? currentConflictIndex
        for i in 0..<mergeResult.lines.count {
            if mergeResult.lines[i].conflictIndex == idx {
                mergeResult.lines[i].resolvedContent = mergeResult.lines[i].contentRemote
                mergeResult.lines[i].status = .cleanRemote
            }
        }
        rebuildMergedText()
    }

    public func takeBoth(conflictIndex: Int? = nil) {
        let idx = conflictIndex ?? currentConflictIndex
        for i in 0..<mergeResult.lines.count {
            if mergeResult.lines[i].conflictIndex == idx {
                let combined = "\(mergeResult.lines[i].contentLocal)\n\(mergeResult.lines[i].contentRemote)"
                mergeResult.lines[i].resolvedContent = combined
                mergeResult.lines[i].status = .unchanged
            }
        }
        rebuildMergedText()
    }

    public func autoResolveNonConflicts() {
        for i in 0..<mergeResult.lines.count {
            if mergeResult.lines[i].status == .cleanLocal {
                mergeResult.lines[i].resolvedContent = mergeResult.lines[i].contentLocal
            } else if mergeResult.lines[i].status == .cleanRemote {
                mergeResult.lines[i].resolvedContent = mergeResult.lines[i].contentRemote
            }
        }
        rebuildMergedText()
        statusMessage = "Auto-resolved \(mergeResult.autoResolvedCount) non-conflicting changes."
    }

    private func rebuildMergedText() {
        var lines: [String] = []
        var remainingConflicts = 0

        for line in mergeResult.lines {
            if line.status == .conflict {
                remainingConflicts += 1
                lines.append("<<<<<<< Local\n\(line.contentLocal)\n=======\n\(line.contentRemote)\n>>>>>>> Remote")
            } else if let resolved = line.resolvedContent {
                lines.append(resolved)
            } else {
                lines.append(line.contentBase)
            }
        }

        self.mergeResult.mergedText = lines.joined(separator: "\n")
        self.totalConflicts = remainingConflicts
    }

    public func saveAndCompleteMerge() {
        guard let url = outputFileURL ?? localFileURL else {
            let panel = NSSavePanel()
            panel.title = "Save Merged File"
            if panel.runModal() == .OK, let saveURL = panel.url {
                outputFileURL = saveURL
                saveAndCompleteMerge()
            }
            return
        }

        do {
            try diffEngine.saveFile(to: url, content: mergeResult.mergedText, encoding: .utf8, createBackup: false)
            statusMessage = "Merged file successfully saved to \(url.lastPathComponent)!"
        } catch {
            statusMessage = "Save Error: \(error.localizedDescription)"
        }
    }
}
