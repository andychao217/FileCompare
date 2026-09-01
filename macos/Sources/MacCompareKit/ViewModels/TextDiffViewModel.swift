import Foundation
import SwiftUI
import AppKit

@MainActor
@Observable
public final class TextDiffViewModel {
    public var leftTitle: String = "Source File (Left)"
    public var rightTitle: String = "Target File (Right)"

    public var leftFileURL: URL?
    public var rightFileURL: URL?

    public var leftContent: String = "" {
        didSet {
            isLeftDirty = true
            scheduleDiffComputation()
        }
    }
    public var rightContent: String = "" {
        didSet {
            isRightDirty = true
            scheduleDiffComputation()
        }
    }

    public var isLeftDirty: Bool = false
    public var isRightDirty: Bool = false

    public var diffResult: TextDiffResult = TextDiffResult()
    public var isLoading: Bool = false
    public var statusMessage: String?

    public var ignoreWhitespace: Bool = false {
        didSet { Task { await recomputeDiff() } }
    }
    public var ignoreCase: Bool = false {
        didSet { Task { await recomputeDiff() } }
    }
    public var selectedEncoding: FileEncoding = .utf8 {
        didSet { reloadWithCurrentEncoding() }
    }
    public var cursorPosition: String = "Ln 1, Col 1"
    public var currentHunkIndex: Int = 0
    public var scrollToLineIndex: Int?

    private let diffEngine: DiffEngineProtocol
    private var debounceTask: Task<Void, Never>?

    public var hasFilesLoaded: Bool {
        leftFileURL != nil || rightFileURL != nil || !leftContent.isEmpty || !rightContent.isEmpty
    }

    public init(
        diffEngine: DiffEngineProtocol = DiffEngineService.shared,
        leftURL: URL? = nil,
        rightURL: URL? = nil
    ) {
        self.diffEngine = diffEngine
        self.leftFileURL = leftURL
        self.rightFileURL = rightURL

        if let l = leftURL, let r = rightURL {
            loadFiles(left: l, right: r)
        } else if let l = leftURL {
            loadSingleFile(from: l, isLeft: true)
        } else if let r = rightURL {
            loadSingleFile(from: r, isLeft: false)
        }
    }

    public func loadFiles(left: URL, right: URL) {
        loadSingleFile(from: left, isLeft: true)
        loadSingleFile(from: right, isLeft: false)
    }

    public func clearAll() {
        leftFileURL = nil
        rightFileURL = nil
        leftTitle = LanguageManager.shared.text(.sourceFile)
        rightTitle = LanguageManager.shared.text(.targetFile)
        leftContent = ""
        rightContent = ""
        isLeftDirty = false
        isRightDirty = false
        diffResult = TextDiffResult()
        statusMessage = nil
        currentHunkIndex = 0
        scrollToLineIndex = nil
    }

    public func loadSingleFile(from url: URL, isLeft: Bool) {
        do {
            let content = try diffEngine.loadFile(from: url, encoding: selectedEncoding)
            if isLeft {
                self.leftFileURL = url
                self.leftTitle = url.lastPathComponent
                self.leftContent = content
                self.isLeftDirty = false
            } else {
                self.rightFileURL = url
                self.rightTitle = url.lastPathComponent
                self.rightContent = content
                self.isRightDirty = false
            }
            Task { await recomputeDiff() }
        } catch {
            self.statusMessage = "Error loading \(url.lastPathComponent): \(error.localizedDescription)"
        }
    }

    public func openLeftFile() {
        let panel = NSOpenPanel()
        panel.title = "Select Left File"
        panel.canChooseFiles = true
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = false

        if panel.runModal() == .OK, let url = panel.url {
            loadSingleFile(from: url, isLeft: true)
        }
    }

    public func openRightFile() {
        let panel = NSOpenPanel()
        panel.title = "Select Right File"
        panel.canChooseFiles = true
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = false

        if panel.runModal() == .OK, let url = panel.url {
            loadSingleFile(from: url, isLeft: false)
        }
    }

    public func saveLeftFile() {
        guard let url = leftFileURL else {
            promptSavePanel(isLeft: true)
            return
        }
        let createBackup = UserDefaults.standard.bool(forKey: "create_bak_backup_on_save")
        do {
            try diffEngine.saveFile(to: url, content: leftContent, encoding: selectedEncoding, createBackup: createBackup)
            isLeftDirty = false
            statusMessage = "Saved \(url.lastPathComponent)"
        } catch {
            statusMessage = "Save Error: \(error.localizedDescription)"
        }
    }

    public func saveRightFile() {
        guard let url = rightFileURL else {
            promptSavePanel(isLeft: false)
            return
        }
        let createBackup = UserDefaults.standard.bool(forKey: "create_bak_backup_on_save")
        do {
            try diffEngine.saveFile(to: url, content: rightContent, encoding: selectedEncoding, createBackup: createBackup)
            isRightDirty = false
            statusMessage = "Saved \(url.lastPathComponent)"
        } catch {
            statusMessage = "Save Error: \(error.localizedDescription)"
        }
    }

    private func promptSavePanel(isLeft: Bool) {
        let panel = NSSavePanel()
        panel.title = "Save \(isLeft ? "Left" : "Right") File"
        if panel.runModal() == .OK, let url = panel.url {
            if isLeft {
                leftFileURL = url
                leftTitle = url.lastPathComponent
                saveLeftFile()
            } else {
                rightFileURL = url
                rightTitle = url.lastPathComponent
                saveRightFile()
            }
        }
    }

    public func previousDiff() {
        guard !diffResult.hunks.isEmpty else { return }
        if currentHunkIndex > 0 {
            currentHunkIndex -= 1
        } else {
            currentHunkIndex = diffResult.hunks.count - 1
        }
        scrollToLineIndex = diffResult.hunks[currentHunkIndex].startLineIndex
    }

    public func nextDiff() {
        guard !diffResult.hunks.isEmpty else { return }
        if currentHunkIndex < diffResult.hunks.count - 1 {
            currentHunkIndex += 1
        } else {
            currentHunkIndex = 0
        }
        scrollToLineIndex = diffResult.hunks[currentHunkIndex].startLineIndex
    }

    public func takeLeft(hunkIndex: Int? = nil) {
        let idx = hunkIndex ?? currentHunkIndex
        guard diffResult.hunks.indices.contains(idx) else { return }
        let hunk = diffResult.hunks[idx]

        var rightLines = rightContent.components(separatedBy: .newlines)
        let leftLines = leftContent.components(separatedBy: .newlines)

        let targetStart = min(hunk.startLineIndex, rightLines.count)
        let targetEnd = min(targetStart + hunk.lineCount, rightLines.count)
        let replacement = Array(leftLines[min(hunk.startLineIndex, leftLines.count)..<min(hunk.startLineIndex + hunk.lineCount, leftLines.count)])

        if targetStart <= targetEnd {
            rightLines.replaceSubrange(targetStart..<targetEnd, with: replacement)
            self.rightContent = rightLines.joined(separator: "\n")
        }
    }

    public func takeRight(hunkIndex: Int? = nil) {
        let idx = hunkIndex ?? currentHunkIndex
        guard diffResult.hunks.indices.contains(idx) else { return }
        let hunk = diffResult.hunks[idx]

        var leftLines = leftContent.components(separatedBy: .newlines)
        let rightLines = rightContent.components(separatedBy: .newlines)

        let targetStart = min(hunk.startLineIndex, leftLines.count)
        let targetEnd = min(targetStart + hunk.lineCount, leftLines.count)
        let replacement = Array(rightLines[min(hunk.startLineIndex, rightLines.count)..<min(hunk.startLineIndex + hunk.lineCount, rightLines.count)])

        if targetStart <= targetEnd {
            leftLines.replaceSubrange(targetStart..<targetEnd, with: replacement)
            self.leftContent = leftLines.joined(separator: "\n")
        }
    }

    private func reloadWithCurrentEncoding() {
        if let l = leftFileURL {
            leftContent = (try? diffEngine.loadFile(from: l, encoding: selectedEncoding)) ?? leftContent
        }
        if let r = rightFileURL {
            rightContent = (try? diffEngine.loadFile(from: r, encoding: selectedEncoding)) ?? rightContent
        }
    }

    public var hasBothFiles: Bool {
        (leftFileURL != nil || !leftContent.isEmpty) && (rightFileURL != nil || !rightContent.isEmpty)
    }

    public var hasLeftFile: Bool {
        leftFileURL != nil || !leftContent.isEmpty
    }

    public var hasRightFile: Bool {
        rightFileURL != nil || !rightContent.isEmpty
    }

    private func scheduleDiffComputation() {
        debounceTask?.cancel()
        debounceTask = Task {
            try? await Task.sleep(nanoseconds: 200_000_000)
            guard !Task.isCancelled else { return }
            await recomputeDiff()
        }
    }

    public func recomputeDiff() async {
        guard !leftContent.isEmpty || !rightContent.isEmpty else {
            self.diffResult = TextDiffResult()
            return
        }

        if hasBothFiles {
            let res = await diffEngine.compareText(
                left: leftContent,
                right: rightContent,
                ignoreWhitespace: ignoreWhitespace,
                ignoreCase: ignoreCase
            )
            self.diffResult = res
            if let l = leftFileURL, let r = rightFileURL {
                RecentHistoryManager.shared.addRecord(left: l, right: r, type: .textDiff)
            }
        } else if hasLeftFile {
            let lines = leftContent.components(separatedBy: .newlines)
            let diffLines = lines.enumerated().map { idx, line in
                DiffLine(
                    leftLineNumber: UInt32(idx + 1),
                    rightLineNumber: nil,
                    contentLeft: line,
                    contentRight: "",
                    changeType: .unchanged
                )
            }
            self.diffResult = TextDiffResult(
                lines: diffLines,
                totalAdditions: 0,
                totalDeletions: 0,
                totalModifications: 0,
                hunks: []
            )
        } else if hasRightFile {
            let lines = rightContent.components(separatedBy: .newlines)
            let diffLines = lines.enumerated().map { idx, line in
                DiffLine(
                    leftLineNumber: nil,
                    rightLineNumber: UInt32(idx + 1),
                    contentLeft: "",
                    contentRight: line,
                    changeType: .unchanged
                )
            }
            self.diffResult = TextDiffResult(
                lines: diffLines,
                totalAdditions: 0,
                totalDeletions: 0,
                totalModifications: 0,
                hunks: []
            )
        }
    }
}
