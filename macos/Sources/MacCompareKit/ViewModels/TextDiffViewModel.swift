import Foundation
import SwiftUI
import AppKit

@MainActor
@Observable
public final class TextDiffViewModel {
    public var leftTitle: String = "script.py (Original)"
    public var rightTitle: String = "script.py (Modified)"

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
        } else {
            loadSampleData()
        }
    }

    // MARK: - File Loading & Saving

    public func loadFiles(left: URL, right: URL) {
        self.leftFileURL = left
        self.rightFileURL = right
        self.leftTitle = left.lastPathComponent
        self.rightTitle = right.lastPathComponent

        do {
            self.leftContent = try diffEngine.loadFile(from: left, encoding: selectedEncoding)
            self.rightContent = try diffEngine.loadFile(from: right, encoding: selectedEncoding)
            self.isLeftDirty = false
            self.isRightDirty = false
            self.statusMessage = "Loaded \(left.lastPathComponent) & \(right.lastPathComponent)"
        } catch {
            self.statusMessage = "Error: \(error.localizedDescription)"
        }
    }

    public func openLeftFile() {
        let panel = NSOpenPanel()
        panel.title = "Select Left (Original) File"
        panel.canChooseFiles = true
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = false

        if panel.runModal() == .OK, let url = panel.url {
            self.leftFileURL = url
            self.leftTitle = url.lastPathComponent
            if let content = try? diffEngine.loadFile(from: url, encoding: selectedEncoding) {
                self.leftContent = content
                self.isLeftDirty = false
            }
        }
    }

    public func openRightFile() {
        let panel = NSOpenPanel()
        panel.title = "Select Right (Modified) File"
        panel.canChooseFiles = true
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = false

        if panel.runModal() == .OK, let url = panel.url {
            self.rightFileURL = url
            self.rightTitle = url.lastPathComponent
            if let content = try? diffEngine.loadFile(from: url, encoding: selectedEncoding) {
                self.rightContent = content
                self.isRightDirty = false
            }
        }
    }

    public func saveLeftFile() {
        guard let url = leftFileURL else {
            promptSaveAs(isLeft: true)
            return
        }
        do {
            try diffEngine.saveFile(to: url, content: leftContent, encoding: selectedEncoding, createBackup: true)
            isLeftDirty = false
            statusMessage = "Saved \(url.lastPathComponent)"
        } catch {
            statusMessage = "Save Error: \(error.localizedDescription)"
        }
    }

    public func saveRightFile() {
        guard let url = rightFileURL else {
            promptSaveAs(isLeft: false)
            return
        }
        do {
            try diffEngine.saveFile(to: url, content: rightContent, encoding: selectedEncoding, createBackup: true)
            isRightDirty = false
            statusMessage = "Saved \(url.lastPathComponent)"
        } catch {
            statusMessage = "Save Error: \(error.localizedDescription)"
        }
    }

    private func promptSaveAs(isLeft: Bool) {
        let panel = NSSavePanel()
        panel.title = isLeft ? "Save Left File" : "Save Right File"
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

    private func reloadWithCurrentEncoding() {
        if let l = leftFileURL, let content = try? diffEngine.loadFile(from: l, encoding: selectedEncoding) {
            self.leftContent = content
        }
        if let r = rightFileURL, let content = try? diffEngine.loadFile(from: r, encoding: selectedEncoding) {
            self.rightContent = content
        }
    }

    // MARK: - Diff Computation & Hunk Operations

    private func scheduleDiffComputation() {
        debounceTask?.cancel()
        debounceTask = Task {
            try? await Task.sleep(nanoseconds: 200_000_000) // 200ms debounce
            guard !Task.isCancelled else { return }
            await recomputeDiff()
        }
    }

    public func recomputeDiff() async {
        isLoading = true
        let res = await diffEngine.compareText(
            left: leftContent,
            right: rightContent,
            ignoreWhitespace: ignoreWhitespace,
            ignoreCase: ignoreCase
        )
        self.diffResult = res
        self.isLoading = false
    }

    public func previousDiff() {
        guard !diffResult.hunks.isEmpty else { return }
        if currentHunkIndex > 0 {
            currentHunkIndex -= 1
        } else {
            currentHunkIndex = diffResult.hunks.count - 1
        }
        scrollToHunk(index: currentHunkIndex)
    }

    public func nextDiff() {
        guard !diffResult.hunks.isEmpty else { return }
        if currentHunkIndex < diffResult.hunks.count - 1 {
            currentHunkIndex += 1
        } else {
            currentHunkIndex = 0
        }
        scrollToHunk(index: currentHunkIndex)
    }

    private func scrollToHunk(index: Int) {
        guard index < diffResult.hunks.count else { return }
        let hunk = diffResult.hunks[index]
        self.scrollToLineIndex = hunk.startLineIndex
    }

    /// Take currently focused or specific left hunk and copy to right side
    public func takeLeft(hunkIndex: Int? = nil) {
        let targetIndex = hunkIndex ?? currentHunkIndex
        guard targetIndex < diffResult.hunks.count else { return }
        let hunk = diffResult.hunks[targetIndex]

        let linesToCopy = (hunk.startLineIndex..<(hunk.startLineIndex + hunk.lineCount))
            .compactMap { idx -> String? in
                let line = diffResult.lines[idx]
                return line.isPhantomLeft ? nil : line.contentLeft
            }

        // Reconstruct right buffer with left hunk replacement
        var newRightLines = rightContent.components(separatedBy: .newlines)
        let rightStart = diffResult.lines[hunk.startLineIndex].rightLineNumber.map { Int($0 - 1) } ?? 0
        let rightCount = (hunk.startLineIndex..<(hunk.startLineIndex + hunk.lineCount))
            .filter { !$0.isPhantomRight(in: diffResult.lines) }.count

        if rightStart <= newRightLines.count {
            let replaceRange = rightStart..<min(rightStart + rightCount, newRightLines.count)
            newRightLines.replaceSubrange(replaceRange, with: linesToCopy)
            self.rightContent = newRightLines.joined(separator: "\n")
        }
    }

    /// Take currently focused or specific right hunk and copy to left side
    public func takeRight(hunkIndex: Int? = nil) {
        let targetIndex = hunkIndex ?? currentHunkIndex
        guard targetIndex < diffResult.hunks.count else { return }
        let hunk = diffResult.hunks[targetIndex]

        let linesToCopy = (hunk.startLineIndex..<(hunk.startLineIndex + hunk.lineCount))
            .compactMap { idx -> String? in
                let line = diffResult.lines[idx]
                return line.isPhantomRight ? nil : line.contentRight
            }

        var newLeftLines = leftContent.components(separatedBy: .newlines)
        let leftStart = diffResult.lines[hunk.startLineIndex].leftLineNumber.map { Int($0 - 1) } ?? 0
        let leftCount = (hunk.startLineIndex..<(hunk.startLineIndex + hunk.lineCount))
            .filter { !$0.isPhantomLeft(in: diffResult.lines) }.count

        if leftStart <= newLeftLines.count {
            let replaceRange = leftStart..<min(leftStart + leftCount, newLeftLines.count)
            newLeftLines.replaceSubrange(replaceRange, with: linesToCopy)
            self.leftContent = newLeftLines.joined(separator: "\n")
        }
    }

    public func loadSampleData() {
        self.leftContent = """
import synsc.sd

def funbic(basen):
    syntax = array[]
    syntax = self.town_strings>


def get_mith(atexs):
    token = schanged(ittoarname)
    return sensalion("scoze")

# Ignore Tokens
printult_io.recain("I The same necessarary $?")

if out in punt:
    # some punt = string[])
    sodeon.tokens = decoded[]

def autotname(selfI):
    return sampilleArray(sonfig + 10)
"""

        self.rightContent = """
import synsc.sd

def funbic(basen):
    syntax = array[]
    syntax = self.town_strings>


def get_mitt(atexs):
    token = schanged(fatsenzname>)
    return sensalion("score.1")

# Ignore Tokens
printult_io.recain("I The same necessarary $?")

if out in punt:
    # some punt = string[])
    sodeon.tokens = abcoded[]
    screen.claserining("$&ill")

def autotname(selfI):
    return sanoilleArray(sonfig + 10)
"""
        self.isLeftDirty = false
        self.isRightDirty = false
        Task {
            await recomputeDiff()
        }
    }
}

private extension Int {
    func isPhantomLeft(in lines: [DiffLine]) -> Bool {
        guard self < lines.count else { return false }
        return lines[self].isPhantomLeft
    }
    func isPhantomRight(in lines: [DiffLine]) -> Bool {
        guard self < lines.count else { return false }
        return lines[self].isPhantomRight
    }
}
