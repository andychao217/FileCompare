import Foundation
import SwiftUI
import AppKit

import PDFKit
import UniformTypeIdentifiers

@MainActor
@Observable
public final class PdfDiffViewModel {
    public var leftFileURL: URL?
    public var rightFileURL: URL?

    public var leftDocument: PdfDocumentModel?
    public var rightDocument: PdfDocumentModel?
    public var diffResult: PdfDiffResult = PdfDiffResult()

    public var currentPageIndex: Int = 0 {
        didSet { updateVisualDiff() }
    }
    public var zoomScale: CGFloat = 1.0
    public var syncScroll: Bool = true
    public var viewMode: PdfViewMode = .sideBySide
    public var ignoreWhitespace: Bool = false {
        didSet { recomputeDiff() }
    }
    public var overlayBlendAlpha: Double = 0.5
    public var pixelTolerance: Double = 0.08 {
        didSet { updateVisualDiff() }
    }
    public var showDiffMaskOverlay: Bool = true

    public var currentVisualDiff: PdfVisualDiffResult?
    public var currentDiffMaskImage: CGImage?

    public var currentDiffIndex: Int = 0
    public var currentRegionIndex: Int = 0
    public var isLoading: Bool = false
    public var errorMessage: String?

    public var isAISummaryVisible: Bool = false
    public var isAIGenerating: Bool = false
    public var aiSummaryResult: AISummaryResult?

    public var currentScrollFraction: CGFloat = 0.0

    private let parser = PdfDocumentParser.shared
    private let diffEngine = PdfDiffEngine.shared
    private let rasterizer = PdfRasterizer.shared
    private let pixelDiffEngine = PdfPixelDiffEngine.shared

    public var hasDocumentsLoaded: Bool {
        leftDocument != nil || rightDocument != nil
    }

    public var maxPageCount: Int {
        max(leftDocument?.pageCount ?? 0, rightDocument?.pageCount ?? 0)
    }

    public var modifiedBlocks: [PdfPageDiffBlock] {
        diffResult.blocks.filter { $0.changeType != .unchanged }
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
                self.currentPageIndex = 0

                await runDiff()
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

        Task {
            do {
                let parsed = try await parser.parseDocument(from: url)
                if isLeft {
                    self.leftFileURL = url
                    self.leftDocument = parsed
                } else {
                    self.rightFileURL = url
                    self.rightDocument = parsed
                }

                if self.leftDocument != nil && self.rightDocument != nil {
                    await runDiff()
                } else {
                    self.diffResult = PdfDiffResult()
                }
                self.isLoading = false
            } catch {
                self.errorMessage = error.localizedDescription
                self.isLoading = false
            }
        }
    }

    public func recomputeDiff() {
        guard leftDocument != nil, rightDocument != nil else { return }
        Task {
            await runDiff()
        }
    }

    public var currentSortedRegions: [CGRect] {
        guard let boxes = currentVisualDiff?.diffBoundingBoxes, !boxes.isEmpty else { return [] }
        return boxes.sorted { $0.origin.y < $1.origin.y }
    }

    public var currentDiffStatusText: String {
        let regions = currentSortedRegions
        if !regions.isEmpty {
            return String(format: LanguageManager.shared.text(.pdfDiffRegionNavFormat), currentRegionIndex + 1, regions.count)
        }
        let count = modifiedBlocks.count
        if count == 0 {
            return LanguageManager.shared.text(.pdfDiffNoDiff)
        } else {
            return String(format: LanguageManager.shared.text(.pdfDiffCountFormat), count)
        }
    }

    private func runDiff() async {
        guard let left = leftDocument, let right = rightDocument else { return }
        let result = await diffEngine.compareDocuments(
            left: left,
            right: right,
            ignoreWhitespace: ignoreWhitespace
        )
        self.diffResult = result
        self.currentDiffIndex = 0
        self.currentRegionIndex = 0
        if let l = leftFileURL, let r = rightFileURL {
            RecentHistoryManager.shared.addRecord(left: l, right: r, type: .pdfDiff)
        }
        updateVisualDiff()
    }

    public func updateVisualDiff() {
        guard let leftURL = leftFileURL, let rightURL = rightFileURL else {
            currentVisualDiff = nil
            currentDiffMaskImage = nil
            return
        }

        let pageIdx = currentPageIndex
        let tol = pixelTolerance

        Task {
            guard let leftPdf = PDFDocument(url: leftURL),
                  let rightPdf = PDFDocument(url: rightURL) else {
                return
            }

            guard let leftImg = self.rasterizer.rasterizePage(from: leftPdf, at: pageIdx),
                  let rightImg = self.rasterizer.rasterizePage(from: rightPdf, at: pageIdx) else {
                self.currentVisualDiff = nil
                self.currentDiffMaskImage = nil
                return
            }

            let output = self.pixelDiffEngine.compareImages(
                left: leftImg,
                right: rightImg,
                pageIndex: pageIdx,
                tolerance: tol
            )

            self.currentVisualDiff = output.result
            self.currentDiffMaskImage = output.diffMaskImage
            self.currentRegionIndex = 0
        }
    }

    // MARK: - Navigation & Zoom

    public func nextPage() {
        if currentPageIndex + 1 < maxPageCount {
            currentPageIndex += 1
        }
    }

    public func prevPage() {
        if currentPageIndex > 0 {
            currentPageIndex -= 1
        }
    }

    public func goToPage(_ page: Int) {
        if page >= 0 && page < maxPageCount {
            currentPageIndex = page
        }
    }

    public func nextDiff() {
        let regions = currentSortedRegions
        if !regions.isEmpty {
            if currentRegionIndex + 1 < regions.count {
                currentRegionIndex += 1
                focusCurrentRegion()
                return
            }
        }

        // Advance to next page with diffs
        let diffs = modifiedBlocks
        guard !diffs.isEmpty else { return }
        currentDiffIndex = (currentDiffIndex + 1) % diffs.count
        if let targetPage = diffs[currentDiffIndex].leftPageIndex ?? diffs[currentDiffIndex].rightPageIndex {
            currentRegionIndex = 0
            goToPage(targetPage)
            Task { @MainActor in
                try? await Task.sleep(nanoseconds: 120_000_000)
                self.focusCurrentRegion()
            }
        }
    }

    public func prevDiff() {
        let regions = currentSortedRegions
        if !regions.isEmpty {
            if currentRegionIndex > 0 {
                currentRegionIndex -= 1
                focusCurrentRegion()
                return
            }
        }

        // Go back to previous page with diffs
        let diffs = modifiedBlocks
        guard !diffs.isEmpty else { return }
        currentDiffIndex = (currentDiffIndex - 1 + diffs.count) % diffs.count
        if let targetPage = diffs[currentDiffIndex].leftPageIndex ?? diffs[currentDiffIndex].rightPageIndex {
            currentRegionIndex = 0
            goToPage(targetPage)
            Task { @MainActor in
                try? await Task.sleep(nanoseconds: 120_000_000)
                self.focusCurrentRegion()
            }
        }
    }

    public func focusCurrentRegion() {
        let regions = currentSortedRegions
        guard !regions.isEmpty, currentRegionIndex >= 0 && currentRegionIndex < regions.count else { return }
        let box = regions[currentRegionIndex]

        // Convert box to PDF coordinates
        let scale: CGFloat = 1.5
        let pageHeight: CGFloat = (currentPageIndex < (leftDocument?.pages.count ?? 0))
            ? (leftDocument?.pages[currentPageIndex].pageSize.height ?? 842.0)
            : 842.0
        let pdfX = box.origin.x / scale
        let pdfY = pageHeight - ((box.origin.y + box.height) / scale)
        let pdfW = box.width / scale
        let pdfH = box.height / scale
        let pdfRect = CGRect(x: pdfX, y: pdfY, width: pdfW, height: pdfH)

        NotificationCenter.default.post(
            name: .pdfFocusRegion,
            object: nil,
            userInfo: [
                "pageIndex": currentPageIndex,
                "pdfRect": pdfRect
            ]
        )
    }

    public func zoomIn() {
        zoomScale = min(zoomScale + 0.2, 4.0)
    }

    public func zoomOut() {
        zoomScale = max(zoomScale - 0.2, 0.4)
    }

    public func resetZoom() {
        zoomScale = 1.0
    }

    // MARK: - AI Summary & Report Export

    public func toggleAISummary() {
        isAISummaryVisible.toggle()
        if isAISummaryVisible && aiSummaryResult == nil && !isAIGenerating {
            generateAISummary()
        }
    }

    public func generateAISummary() {
        guard let leftDoc = leftDocument, let rightDoc = rightDocument else { return }
        isAISummaryVisible = true
        isAIGenerating = true

        Task {
            let leftModifiedText = diffResult.blocks
                .compactMap { block -> String? in
                    guard block.changeType != .unchanged,
                          let idx = block.leftPageIndex,
                          idx < leftDoc.pages.count else { return nil }
                    return "--- Page \(idx + 1) ---\n" + leftDoc.pages[idx].text
                }
                .joined(separator: "\n\n")

            let rightModifiedText = diffResult.blocks
                .compactMap { block -> String? in
                    guard block.changeType != .unchanged,
                          let idx = block.rightPageIndex,
                          idx < rightDoc.pages.count else { return nil }
                    return "+++ Page \(idx + 1) +++\n" + rightDoc.pages[idx].text
                }
                .joined(separator: "\n\n")

            let summary = await AIService.shared.summarizeDiff(
                leftContent: leftModifiedText,
                rightContent: rightModifiedText,
                leftTitle: leftDoc.fileName,
                rightTitle: rightDoc.fileName
            )

            self.aiSummaryResult = summary
            self.isAIGenerating = false
        }
    }

    public func exportHtmlReport() {
        guard let leftDoc = leftDocument, let rightDoc = rightDocument else { return }
        let panel = NSSavePanel()
        panel.allowedContentTypes = [.html]
        panel.nameFieldStringValue = "PDF_Diff_\(leftDoc.fileName)_vs_\(rightDoc.fileName).html"

        if panel.runModal() == .OK, let targetURL = panel.url {
            let currentLanguage = LanguageManager.shared.effectiveLanguage
            let currentTheme = ThemeManager.shared.currentTheme

            let html = PdfReportExporter.shared.generateHtmlReport(
                leftDoc: leftDoc,
                rightDoc: rightDoc,
                diffResult: diffResult,
                visualDiff: currentVisualDiff,
                aiSummary: aiSummaryResult,
                language: currentLanguage,
                theme: currentTheme
            )
            do {
                try html.write(to: targetURL, atomically: true, encoding: .utf8)
                NSWorkspace.shared.activateFileViewerSelecting([targetURL])
            } catch {
                self.errorMessage = error.localizedDescription
            }
        }
    }
}
