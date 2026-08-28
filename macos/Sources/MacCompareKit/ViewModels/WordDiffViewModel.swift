import Foundation
import SwiftUI
import AppKit

@MainActor
@Observable
public final class WordDiffViewModel {
    public var leftFileURL: URL?
    public var rightFileURL: URL?

    public var leftDocument: WordDocumentModel?
    public var rightDocument: WordDocumentModel?
    public var diffResult: WordDiffResult = WordDiffResult()

    public var viewMode: WordViewMode = .structuredContent
    public var ignoreWhitespace: Bool = false {
        didSet { recomputeDiff() }
    }
    public var ignoreFormatting: Bool = false {
        didSet { recomputeDiff() }
    }
    public var filterText: String = ""

    public var selectedBlockIndex: Int?
    public var currentDiffIndex: Int = 0

    public var isLoading: Bool = false
    public var errorMessage: String?
    public var isExporting: Bool = false

    private let parser = WordDocumentParser.shared
    private let diffEngine = WordDiffEngine.shared

    public var hasDocumentsLoaded: Bool {
        leftDocument != nil || rightDocument != nil
    }

    public var activeDifferencesBlocks: [WordDiffBlock] {
        diffResult.blocks.filter { $0.changeType != .unchanged }
    }

    public var filteredBlocks: [WordDiffBlock] {
        if filterText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return diffResult.blocks
        }
        let term = filterText.lowercased()
        return diffResult.blocks.filter { block in
            (block.leftParagraph?.text.lowercased().contains(term) ?? false) ||
            (block.rightParagraph?.text.lowercased().contains(term) ?? false)
        }
    }

    public init(leftURL: URL? = nil, rightURL: URL? = nil) {
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

    // MARK: - Document Loading

    public func loadFiles(left: URL, right: URL) {
        isLoading = true
        errorMessage = nil
        self.leftFileURL = left
        self.rightFileURL = right

        Task {
            do {
                async let leftDoc = parser.parseDocument(from: left)
                async let rightDoc = parser.parseDocument(from: right)

                let (parsedLeft, parsedRight) = try await (leftDoc, rightDoc)
                self.leftDocument = parsedLeft
                self.rightDocument = parsedRight
                await self.performDiff()
                self.isLoading = false
            } catch {
                self.errorMessage = error.localizedDescription
                self.isLoading = false
            }
        }
    }

    public func loadSingleFile(from url: URL, isLeft: Bool) {
        isLoading = true
        errorMessage = nil
        if isLeft {
            self.leftFileURL = url
        } else {
            self.rightFileURL = url
        }

        Task {
            do {
                let parsed = try await parser.parseDocument(from: url)
                if isLeft {
                    self.leftDocument = parsed
                } else {
                    self.rightDocument = parsed
                }
                await self.performDiff()
                self.isLoading = false
            } catch {
                self.errorMessage = error.localizedDescription
                self.isLoading = false
            }
        }
    }

    public func clearAll() {
        leftFileURL = nil
        rightFileURL = nil
        leftDocument = nil
        rightDocument = nil
        diffResult = WordDiffResult()
        selectedBlockIndex = nil
        currentDiffIndex = 0
        errorMessage = nil
    }

    // MARK: - Diff Computation

    public func recomputeDiff() {
        Task {
            await performDiff()
        }
    }

    private func performDiff() async {
        guard let left = leftDocument, let right = rightDocument else {
            if let left = leftDocument {
                // Left only presentation
                let blocks = left.paragraphs.enumerated().map { index, p in
                    WordDiffBlock(blockIndex: index, leftParagraph: p, rightParagraph: nil, changeType: .deleted)
                }
                diffResult = WordDiffResult(blocks: blocks, totalDeletions: blocks.count)
            } else if let right = rightDocument {
                // Right only presentation
                let blocks = right.paragraphs.enumerated().map { index, p in
                    WordDiffBlock(blockIndex: index, leftParagraph: nil, rightParagraph: p, changeType: .added)
                }
                diffResult = WordDiffResult(blocks: blocks, totalAdditions: blocks.count)
            }
            return
        }

        let result = await diffEngine.compareDocuments(
            left: left,
            right: right,
            ignoreWhitespace: ignoreWhitespace,
            ignoreFormatting: ignoreFormatting
        )
        self.diffResult = result
    }

    // MARK: - Navigation

    public func nextDiff() {
        let diffBlocks = activeDifferencesBlocks
        guard !diffBlocks.isEmpty else { return }
        currentDiffIndex = (currentDiffIndex + 1) % diffBlocks.count
        selectedBlockIndex = diffBlocks[currentDiffIndex].blockIndex
    }

    public func prevDiff() {
        let diffBlocks = activeDifferencesBlocks
        guard !diffBlocks.isEmpty else { return }
        currentDiffIndex = (currentDiffIndex - 1 + diffBlocks.count) % diffBlocks.count
        selectedBlockIndex = diffBlocks[currentDiffIndex].blockIndex
    }

    public func jumpToHeading(headingIndex: Int) {
        if let block = diffResult.blocks.first(where: {
            $0.leftParagraph?.index == headingIndex || $0.rightParagraph?.index == headingIndex
        }) {
            selectedBlockIndex = block.blockIndex
        }
    }

    // MARK: - Export Report

    public func exportHTMLReport() -> String {
        var html = """
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="utf-8">
            <title>Word Diff Report - \(leftDocument?.fileName ?? "Document 1") vs \(rightDocument?.fileName ?? "Document 2")</title>
            <style>
                body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 40px; background: #f8fafc; color: #1e293b; }
                h1 { font-size: 24px; margin-bottom: 8px; }
                .summary-card { background: white; border-radius: 8px; padding: 16px; margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
                .stats { display: flex; gap: 20px; font-weight: bold; margin-top: 10px; }
                .stat-add { color: #16a34a; }
                .stat-del { color: #dc2626; }
                .stat-mod { color: #d97706; }
                .stat-fmt { color: #7c3aed; }
                table.diff-table { width: 100%; border-collapse: collapse; background: white; border-radius: 8px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
                th, td { padding: 12px 16px; text-align: left; vertical-align: top; border-bottom: 1px solid #e2e8f0; }
                th { background: #f1f5f9; font-weight: 600; font-size: 13px; color: #64748b; }
                tr.added { background-color: #f0fdf4; }
                tr.deleted { background-color: #fef2f2; }
                tr.modified { background-color: #fffbeb; }
                .token-add { background: #bbf7d0; color: #14532d; border-radius: 2px; padding: 1px 3px; font-weight: 500; }
                .token-del { background: #fecaca; color: #7f1d1d; text-decoration: line-through; border-radius: 2px; padding: 1px 3px; }
                .badge { display: inline-block; padding: 2px 8px; border-radius: 12px; font-size: 11px; font-weight: 600; margin-right: 6px; }
                .badge-add { background: #dcfce7; color: #15803d; }
                .badge-del { background: #fee2e2; color: #b91c1c; }
                .badge-mod { background: #fef3c7; color: #b45309; }
                .badge-fmt { background: #f3e8ff; color: #6b21a8; }
                .format-note { font-size: 12px; color: #6b21a8; margin-top: 4px; font-style: italic; }
            </style>
        </head>
        <body>
            <h1>Word Document Comparison Report</h1>
            <div class="summary-card">
                <div><strong>Left Document:</strong> \(leftDocument?.fileName ?? "None") (\(leftDocument?.metadata.fileSizeFormatted ?? "0 KB"))</div>
                <div><strong>Right Document:</strong> \(rightDocument?.fileName ?? "None") (\(rightDocument?.metadata.fileSizeFormatted ?? "0 KB"))</div>
                <div class="stats">
                    <span class="stat-add">+ \(diffResult.totalAdditions) Added</span>
                    <span class="stat-del">- \(diffResult.totalDeletions) Deleted</span>
                    <span class="stat-mod">~ \(diffResult.totalModifications) Modified</span>
                    <span class="stat-fmt">* \(diffResult.totalFormatChanges) Format Changes</span>
                </div>
            </div>

            <table class="diff-table">
                <thead>
                    <tr>
                        <th style="width: 50px;">#</th>
                        <th style="width: 45%;">\(leftDocument?.fileName ?? "Left")</th>
                        <th style="width: 45%;">\(rightDocument?.fileName ?? "Right")</th>
                    </tr>
                </thead>
                <tbody>
        """

        for block in diffResult.blocks {
            let rowClass: String
            let badge: String
            switch block.changeType {
            case .added:
                rowClass = "added"
                badge = "<span class='badge badge-add'>+ ADD</span>"
            case .deleted:
                rowClass = "deleted"
                badge = "<span class='badge badge-del'>- DEL</span>"
            case .modified:
                rowClass = "modified"
                badge = block.isFormatOnly ? "<span class='badge badge-fmt'>* FMT</span>" : "<span class='badge badge-mod'>~ MOD</span>"
            case .unchanged:
                rowClass = ""
                badge = ""
            }

            let leftText = block.leftParagraph?.text ?? "<span style='color: #94a3b8;'>—</span>"
            let rightText = block.rightParagraph?.text ?? "<span style='color: #94a3b8;'>—</span>"

            var formatNotes = ""
            if !block.formatDifferences.isEmpty {
                let noteList = block.formatDifferences.map { "\($0.propertyName): \($0.oldValue) &rarr; \($0.newValue)" }.joined(separator: ", ")
                formatNotes = "<div class='format-note'>🎨 Format Changed: \(noteList)</div>"
            }

            html += """
                <tr class="\(rowClass)">
                    <td style="color: #94a3b8; font-size: 11px;">\(block.blockIndex + 1)</td>
                    <td>\(leftText)</td>
                    <td>\(badge)\(rightText)\(formatNotes)</td>
                </tr>
            """
        }

        html += """
                </tbody>
            </table>
        </body>
        </html>
        """
        return html
    }
}
