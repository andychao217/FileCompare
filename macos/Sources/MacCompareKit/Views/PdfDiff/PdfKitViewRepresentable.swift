import SwiftUI
import AppKit
import PDFKit

extension Notification.Name {
    static let pdfLiveSyncScroll = Notification.Name("MacComparePdfLiveSyncScroll")
    static let pdfFocusRegion = Notification.Name("MacComparePdfFocusRegion")
}

public struct PdfKitViewRepresentable: NSViewRepresentable {
    public let url: URL?
    public let pageIndex: Int
    public let zoomScale: CGFloat
    public let syncScroll: Bool
    public let isLeft: Bool
    public let diffBlocks: [PdfPageDiffBlock]
    public let visualDiff: PdfVisualDiffResult?
    public let showDiffMaskOverlay: Bool
    @Binding public var scrollFraction: CGFloat
    public var onScrollFractionChanged: ((CGFloat) -> Void)?

    public init(
        url: URL?,
        pageIndex: Int,
        zoomScale: CGFloat = 1.0,
        syncScroll: Bool = true,
        isLeft: Bool = true,
        diffBlocks: [PdfPageDiffBlock] = [],
        visualDiff: PdfVisualDiffResult? = nil,
        showDiffMaskOverlay: Bool = true,
        scrollFraction: Binding<CGFloat>,
        onScrollFractionChanged: ((CGFloat) -> Void)? = nil
    ) {
        self.url = url
        self.pageIndex = pageIndex
        self.zoomScale = zoomScale
        self.syncScroll = syncScroll
        self.isLeft = isLeft
        self.diffBlocks = diffBlocks
        self.visualDiff = visualDiff
        self.showDiffMaskOverlay = showDiffMaskOverlay
        self._scrollFraction = scrollFraction
        self.onScrollFractionChanged = onScrollFractionChanged
    }

    public func makeCoordinator() -> Coordinator {
        Coordinator(self)
    }

    public func makeNSView(context: Context) -> PDFView {
        let pdfView = PDFView()
        pdfView.autoScales = true
        pdfView.displayMode = .singlePageContinuous
        pdfView.displayDirection = .vertical
        pdfView.backgroundColor = .windowBackgroundColor

        if let docURL = url, let doc = PDFDocument(url: docURL) {
            pdfView.document = doc
            context.coordinator.currentURL = docURL
            context.coordinator.applyAnnotations(to: pdfView, doc: doc)
        }

        context.coordinator.setupScrollNotification(for: pdfView)
        return pdfView
    }

    public func updateNSView(_ nsView: PDFView, context: Context) {
        context.coordinator.parent = self

        // 1. Document URL update
        if context.coordinator.currentURL != url {
            context.coordinator.currentURL = url
            if let docURL = url, let doc = PDFDocument(url: docURL) {
                nsView.document = doc
                context.coordinator.lastAppliedPageIndex = -1
                context.coordinator.applyAnnotations(to: nsView, doc: doc)
            } else {
                nsView.document = nil
            }
        }

        // 2. Page navigation update - Only trigger go(to:) when explicitly changed from outside
        if let doc = nsView.document, pageIndex >= 0 && pageIndex < doc.pageCount {
            if context.coordinator.lastAppliedPageIndex != pageIndex {
                context.coordinator.lastAppliedPageIndex = pageIndex
                if let targetPage = doc.page(at: pageIndex), nsView.currentPage != targetPage {
                    context.coordinator.isApplyingProgrammaticScroll = true
                    nsView.go(to: targetPage)
                    Task { @MainActor in
                        try? await Task.sleep(nanoseconds: 120_000_000)
                        context.coordinator.isApplyingProgrammaticScroll = false
                    }
                }
            }
        }

        // 3. Zoom update
        if abs(nsView.scaleFactor - zoomScale) > 0.05 && zoomScale >= 0.4 && zoomScale <= 4.0 {
            nsView.scaleFactor = zoomScale
        }

        // 4. Update difference annotations on page
        if let doc = nsView.document {
            context.coordinator.applyAnnotations(to: nsView, doc: doc)
        }

        // 5. Synchronized scroll update (guarded against feedback loop)
        if syncScroll && !context.coordinator.isUserScrolling && !context.coordinator.isApplyingProgrammaticScroll {
            context.coordinator.applyScrollFraction(scrollFraction, to: nsView)
        }
    }

    @MainActor
    public final class Coordinator: NSObject {
        var parent: PdfKitViewRepresentable
        var currentURL: URL?
        var lastAppliedPageIndex: Int = -1
        var isUserScrolling: Bool = false
        var isApplyingProgrammaticScroll: Bool = false
        weak var observedScrollView: NSScrollView?
        weak var pdfView: PDFView?
        private var lastAnnotatedDiffHash: Int = 0
        private var isHandlingRemoteScroll: Bool = false

        init(_ parent: PdfKitViewRepresentable) {
            self.parent = parent
            super.init()
            NotificationCenter.default.addObserver(
                self,
                selector: #selector(onRemoteSyncScroll(_:)),
                name: .pdfLiveSyncScroll,
                object: nil
            )
            NotificationCenter.default.addObserver(
                self,
                selector: #selector(onFocusRegion(_:)),
                name: .pdfFocusRegion,
                object: nil
            )
        }

        deinit {
            NotificationCenter.default.removeObserver(self)
        }

        func setupScrollNotification(for pdfView: PDFView) {
            self.pdfView = pdfView
            guard let scrollView = findScrollView(in: pdfView) else { return }
            observedScrollView = scrollView

            NotificationCenter.default.removeObserver(self, name: NSScrollView.didLiveScrollNotification, object: nil)
            NotificationCenter.default.addObserver(
                self,
                selector: #selector(onDidScroll(_:)),
                name: NSScrollView.didLiveScrollNotification,
                object: scrollView
            )
        }

        @objc private func onDidScroll(_ notification: Notification) {
            guard let sv = notification.object as? NSScrollView, parent.syncScroll else { return }
            guard !isHandlingRemoteScroll && !isApplyingProgrammaticScroll else { return }

            let maxOffset = (sv.documentView?.frame.height ?? 0) - sv.contentView.bounds.height
            guard maxOffset > 0 else { return }

            let fraction = max(0, min(1, sv.contentView.bounds.origin.y / maxOffset))
            parent.onScrollFractionChanged?(fraction)

            // Direct instant broadcast to opposite pane with zero latency
            NotificationCenter.default.post(
                name: .pdfLiveSyncScroll,
                object: nil,
                userInfo: ["fraction": fraction, "isLeft": parent.isLeft]
            )
        }

        @objc private func onRemoteSyncScroll(_ notif: Notification) {
            guard let info = notif.userInfo,
                  let fraction = info["fraction"] as? CGFloat,
                  let fromLeft = info["isLeft"] as? Bool,
                  fromLeft != parent.isLeft,
                  parent.syncScroll,
                  let sv = observedScrollView else { return }

            let maxOffset = (sv.documentView?.frame.height ?? 0) - sv.contentView.bounds.height
            guard maxOffset > 0 else { return }

            let targetY = max(0, min(maxOffset, maxOffset * fraction))
            let currentY = sv.contentView.bounds.origin.y

            if abs(Double(targetY - currentY)) >= 0.5 {
                isHandlingRemoteScroll = true
                sv.contentView.scroll(to: NSPoint(x: sv.contentView.bounds.origin.x, y: targetY))
                sv.reflectScrolledClipView(sv.contentView)
                isHandlingRemoteScroll = false
            }
        }

        @objc private func onFocusRegion(_ notif: Notification) {
            guard let info = notif.userInfo,
                  let pageIndex = info["pageIndex"] as? Int,
                  let pdfRect = info["pdfRect"] as? CGRect,
                  let pv = pdfView,
                  let doc = pv.document,
                  pageIndex >= 0 && pageIndex < doc.pageCount,
                  let page = doc.page(at: pageIndex) else { return }

            isApplyingProgrammaticScroll = true
            pv.go(to: pdfRect, on: page)
            Task { @MainActor in
                try? await Task.sleep(nanoseconds: 120_000_000)
                self.isApplyingProgrammaticScroll = false
            }
        }

        func applyScrollFraction(_ fraction: CGFloat, to pdfView: PDFView) {
            // Handled directly via onRemoteSyncScroll for 120Hz zero-latency synchronization
        }

        func applyAnnotations(to pdfView: PDFView, doc: PDFDocument) {
            self.pdfView = pdfView

            // Compute change fingerprint to avoid expensive re-annotation per frame
            let blocksCount = parent.diffBlocks.count
            let visualCount = parent.visualDiff?.diffBoundingBoxes.count ?? 0
            let showMask = parent.showDiffMaskOverlay ? 1 : 0
            let pageIdx = parent.pageIndex
            let currentHash = blocksCount.hashValue ^ visualCount.hashValue ^ showMask.hashValue ^ pageIdx.hashValue

            if currentHash == lastAnnotatedDiffHash { return }
            lastAnnotatedDiffHash = currentHash

            // Clear previous diff annotations
            for pIdx in 0..<doc.pageCount {
                guard let page = doc.page(at: pIdx) else { continue }
                let diffAnnotations = page.annotations.filter { $0.userName == "MacCompareDiff" }
                for ann in diffAnnotations {
                    page.removeAnnotation(ann)
                }
            }

            guard parent.showDiffMaskOverlay else { return }

            // 1. Text Differences Highlight - Context & Range Accurate (Per-side token stream)
            for block in parent.diffBlocks where block.changeType != .unchanged {
                let targetPageIdx = parent.isLeft ? block.leftPageIndex : block.rightPageIndex
                guard let pIdx = targetPageIdx, pIdx >= 0 && pIdx < doc.pageCount,
                      let page = doc.page(at: pIdx) else { continue }

                let targetType: PdfDiffChangeType = parent.isLeft ? .deleted : .inserted
                let highlightColor: NSColor = parent.isLeft
                    ? NSColor.systemRed.withAlphaComponent(0.35)
                    : NSColor.systemGreen.withAlphaComponent(0.35)

                var charOffset = 0
                for token in block.tokens {
                    let tokenLength = (token.text as NSString).length

                    if token.changeType == targetType {
                        let trimmed = token.text.trimmingCharacters(in: .whitespacesAndNewlines)
                        if !trimmed.isEmpty && tokenLength > 0 {
                            let range = NSRange(location: charOffset, length: tokenLength)
                            if let selection = page.selection(for: range) {
                                let lineSelections = selection.selectionsByLine()
                                let targets = lineSelections.isEmpty ? [selection] : lineSelections
                                for lineSel in targets {
                                    let bounds = lineSel.bounds(for: page)
                                    if bounds.width > 0 && bounds.height > 0 {
                                        let ann = PDFAnnotation(bounds: bounds, forType: .highlight, withProperties: nil)
                                        ann.color = highlightColor
                                        ann.userName = "MacCompareDiff"
                                        page.addAnnotation(ann)
                                    }
                                }
                            }
                        }
                        charOffset += tokenLength
                    } else if token.changeType == .unchanged {
                        charOffset += tokenLength
                    }
                    // Opposite tokens (inserted on left side, or deleted on right side) do not exist in this side's text stream!
                }
            }

            // 2. Visual Pixel Difference Bounding Boxes - Clean Border, Clear Interior (Never color-tint whole text blocks)
            if let visual = parent.visualDiff, visual.diffPixelCount > 0 {
                let pIdx = visual.pageIndex
                if pIdx >= 0 && pIdx < doc.pageCount, let page = doc.page(at: pIdx) {
                    let mediaBox = page.bounds(for: .mediaBox)
                    let scale: CGFloat = 1.5
                    let strokeColor = parent.isLeft
                        ? NSColor.systemRed.withAlphaComponent(0.85)
                        : NSColor.systemOrange.withAlphaComponent(0.85)

                    for box in visual.diffBoundingBoxes {
                        let pdfX = mediaBox.origin.x + (box.origin.x / scale)
                        let pdfY = mediaBox.origin.y + mediaBox.height - ((box.origin.y + box.height) / scale)
                        let pdfW = box.width / scale
                        let pdfH = box.height / scale
                        let pdfRect = CGRect(x: pdfX, y: pdfY, width: pdfW, height: pdfH)

                        let ann = PDFAnnotation(bounds: pdfRect, forType: .square, withProperties: nil)
                        ann.color = strokeColor
                        ann.interiorColor = NSColor.clear // Transparent interior prevents confusing users with full-box pink/orange tint
                        let border = PDFBorder()
                        border.lineWidth = 1.5
                        ann.border = border
                        ann.userName = "MacCompareDiff"
                        page.addAnnotation(ann)
                    }
                }
            }
        }

        private func findScrollView(in view: NSView) -> NSScrollView? {
            if let sv = view as? NSScrollView {
                return sv
            }
            for sub in view.subviews {
                if let found = findScrollView(in: sub) {
                    return found
                }
            }
            return nil
        }
    }
}
