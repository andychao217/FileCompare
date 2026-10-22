import Foundation

public final class PdfReportExporter: Sendable {
    public static let shared = PdfReportExporter()

    public init() {}

    /// Generate a standalone HTML comparison report for two PDF documents.
    /// Adapts dynamically to the given language and theme appearance.
    public func generateHtmlReport(
        leftDoc: PdfDocumentModel,
        rightDoc: PdfDocumentModel,
        diffResult: PdfDiffResult,
        visualDiff: PdfVisualDiffResult? = nil,
        aiSummary: AISummaryResult? = nil,
        language: AppLanguage = .zhHans,
        theme: AppTheme = .system
    ) -> String {
        let langCode: String
        switch language {
        case .zhHans: langCode = "zh-CN"
        case .ja: langCode = "ja"
        case .en, .system: langCode = "en"
        }

        let strings = LocalizedStrings(language: language)
        let title = "\(strings.reportTitle) - \(leftDoc.fileName) vs \(rightDoc.fileName)"
        let dateStr = ISO8601DateFormatter().string(from: Date())
        let diffCount = diffResult.totalDiffCount
        let statusText = diffCount == 0 ? strings.identicalStatus : String(format: strings.diffsStatus, diffCount)

        let themeAttr: String
        switch theme {
        case .light: themeAttr = "light"
        case .dark: themeAttr = "dark"
        case .system: themeAttr = "auto"
        }

        var html = """
        <!DOCTYPE html>
        <html lang="\(langCode)" data-theme="\(themeAttr)">
        <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>\(escapeHtml(title))</title>
        <style>
            :root {
                --bg: #f8fafc;
                --card-bg: #ffffff;
                --text: #0f172a;
                --text-muted: #64748b;
                --border: #e2e8f0;
                --accent: #2563eb;
                --added-bg: #ecfdf5;
                --added-text: #047857;
                --deleted-bg: #fef2f2;
                --deleted-text: #b91c1c;
                --modified-bg: #fffbeb;
                --modified-text: #b45309;
                --btn-bg: #f1f5f9;
                --btn-border: #cbd5e1;
            }
            html[data-theme="dark"] {
                --bg: #0f172a;
                --card-bg: #1e293b;
                --text: #f1f5f9;
                --text-muted: #94a3b8;
                --border: #334155;
                --accent: #3b82f6;
                --added-bg: #064e3b;
                --added-text: #6ee7b7;
                --deleted-bg: #7f1d1d;
                --deleted-text: #fca5a5;
                --modified-bg: #78350f;
                --modified-text: #fde68a;
                --btn-bg: #334155;
                --btn-border: #475569;
            }
            @media (prefers-color-scheme: dark) {
                html[data-theme="auto"] {
                    --bg: #0f172a;
                    --card-bg: #1e293b;
                    --text: #f1f5f9;
                    --text-muted: #94a3b8;
                    --border: #334155;
                    --accent: #3b82f6;
                    --added-bg: #064e3b;
                    --added-text: #6ee7b7;
                    --deleted-bg: #7f1d1d;
                    --deleted-text: #fca5a5;
                    --modified-bg: #78350f;
                    --modified-text: #fde68a;
                    --btn-bg: #334155;
                    --btn-border: #475569;
                }
            }
            body {
                font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
                margin: 0;
                padding: 24px;
                background-color: var(--bg);
                color: var(--text);
                line-height: 1.5;
                transition: background-color 0.2s ease, color 0.2s ease;
            }
            .container {
                max-width: 1040px;
                margin: 0 auto;
            }
            .top-nav {
                display: flex;
                justify-content: flex-end;
                margin-bottom: 12px;
            }
            .theme-toggle-btn {
                background: var(--btn-bg);
                border: 1px solid var(--btn-border);
                color: var(--text);
                padding: 6px 14px;
                border-radius: 8px;
                font-size: 12px;
                font-weight: 500;
                cursor: pointer;
                display: inline-flex;
                align-items: center;
                gap: 6px;
                transition: all 0.15s ease;
            }
            .theme-toggle-btn:hover {
                filter: brightness(0.95);
            }
            .header-card {
                background: var(--card-bg);
                border: 1px solid var(--border);
                border-radius: 12px;
                padding: 24px;
                margin-bottom: 24px;
                box-shadow: 0 1px 3px rgba(0,0,0,0.05);
            }
            .header-title {
                margin: 0 0 8px 0;
                font-size: 22px;
                font-weight: 700;
            }
            .meta-grid {
                display: grid;
                grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
                gap: 16px;
                margin-top: 16px;
                padding-top: 16px;
                border-top: 1px solid var(--border);
                font-size: 13px;
            }
            .meta-item span {
                color: var(--text-muted);
                display: block;
                font-size: 11px;
                text-transform: uppercase;
                margin-bottom: 3px;
                font-weight: 600;
            }
            .badge {
                display: inline-block;
                padding: 2px 8px;
                border-radius: 6px;
                font-size: 12px;
                font-weight: 600;
            }
            .badge-added { background: var(--added-bg); color: var(--added-text); }
            .badge-deleted { background: var(--deleted-bg); color: var(--deleted-text); }
            .badge-modified { background: var(--modified-bg); color: var(--modified-text); }
            .badge-unchanged { background: var(--bg); color: var(--text-muted); }

            .ai-card {
                background: linear-gradient(135deg, rgba(147, 51, 234, 0.08), rgba(59, 130, 246, 0.08));
                border: 1px solid rgba(147, 51, 234, 0.25);
                border-radius: 12px;
                padding: 20px 24px;
                margin-bottom: 24px;
            }
            .ai-title {
                display: flex;
                align-items: center;
                gap: 8px;
                font-weight: 700;
                font-size: 15px;
                margin-bottom: 12px;
                color: var(--text);
            }
            .ai-intent {
                font-size: 14px;
                margin-bottom: 12px;
                line-height: 1.6;
            }
            .ai-points {
                margin: 0;
                padding-left: 20px;
                font-size: 13px;
            }
            .ai-points li {
                margin-bottom: 6px;
            }

            .diff-block {
                background: var(--card-bg);
                border: 1px solid var(--border);
                border-radius: 10px;
                margin-bottom: 20px;
                overflow: hidden;
                box-shadow: 0 1px 3px rgba(0,0,0,0.04);
            }
            .diff-block-header {
                display: flex;
                justify-content: space-between;
                align-items: center;
                padding: 12px 18px;
                background: var(--bg);
                border-bottom: 1px solid var(--border);
                font-size: 14px;
                font-weight: 600;
            }

            /* Detailed Difference Breakdown Callout */
            .diff-summary-card {
                background: var(--bg);
                border-bottom: 1px solid var(--border);
                padding: 14px 18px;
                display: flex;
                flex-direction: column;
                gap: 10px;
            }
            .summary-header {
                font-size: 13px;
                font-weight: 700;
                color: var(--text);
                display: flex;
                align-items: center;
                gap: 6px;
            }
            .summary-group {
                border-radius: 8px;
                padding: 10px 14px;
                font-size: 13px;
            }
            .group-deleted {
                background: var(--deleted-bg);
                border: 1px solid rgba(185, 28, 28, 0.2);
            }
            .group-added {
                background: var(--added-bg);
                border: 1px solid rgba(4, 120, 87, 0.2);
            }
            .group-title {
                font-weight: 600;
                font-size: 12px;
                margin-bottom: 6px;
            }
            .summary-list {
                margin: 0;
                padding-left: 20px;
                line-height: 1.6;
                word-break: break-word;
            }
            .summary-list li del {
                color: var(--deleted-text);
            }
            .summary-list li ins {
                color: var(--added-text);
                text-decoration: none;
                font-weight: 600;
            }
            .more-hint {
                color: var(--text-muted);
                font-style: italic;
                font-size: 11px;
            }

            .diff-content-grid {
                display: grid;
                grid-template-columns: 1fr 1fr;
                font-family: "SF Mono", Consolas, Menlo, monospace;
                font-size: 12px;
            }
            .diff-pane {
                padding: 14px 18px;
                overflow-x: auto;
                white-space: pre-wrap;
                word-break: break-word;
            }
            .diff-pane-left {
                border-right: 1px solid var(--border);
            }
            .token-del {
                background: var(--deleted-bg);
                color: var(--deleted-text);
                text-decoration: line-through;
                padding: 1px 3px;
                border-radius: 3px;
            }
            .token-ins {
                background: var(--added-bg);
                color: var(--added-text);
                font-weight: 600;
                padding: 1px 3px;
                border-radius: 3px;
            }
            footer {
                text-align: center;
                margin-top: 36px;
                font-size: 12px;
                color: var(--text-muted);
            }
        </style>
        </head>
        <body>
        <div class="container">
            <div class="top-nav">
                <button class="theme-toggle-btn" onclick="toggleTheme()" id="themeToggleBtn">
                    🌓 \(strings.themeToggleLabel)
                </button>
            </div>

            <div class="header-card">
                <h1 class="header-title">\(escapeHtml(strings.reportTitle))</h1>
                <div style="font-size: 13px; color: var(--text-muted);">
                    \(strings.generatedAt)：\(dateStr) · \(statusText)
                </div>
                <div class="meta-grid">
                    <div class="meta-item">
                        <span>\(strings.leftOriginalFile)</span>
                        <strong>\(escapeHtml(leftDoc.fileName))</strong><br>
                        <small style="color:var(--text-muted)">\(String(format: strings.pagesCountFormatted, leftDoc.pageCount)) · \(leftDoc.metadata.fileSizeFormatted)</small>
                    </div>
                    <div class="meta-item">
                        <span>\(strings.rightTargetFile)</span>
                        <strong>\(escapeHtml(rightDoc.fileName))</strong><br>
                        <small style="color:var(--text-muted)">\(String(format: strings.pagesCountFormatted, rightDoc.pageCount)) · \(rightDoc.metadata.fileSizeFormatted)</small>
                    </div>
                    <div class="meta-item">
                        <span>\(strings.pageOverview)</span>
                        <strong>\(String(format: strings.totalPagesAligned, max(leftDoc.pageCount, rightDoc.pageCount)))</strong><br>
                        <small style="color:var(--text-muted)">\(String(format: strings.changedPages, diffResult.blocks.filter { $0.changeType != .unchanged }.count))</small>
                    </div>
                </div>
            </div>
        """

        if let ai = aiSummary {
            let aiTitle = String(format: strings.aiSummaryTitle, ai.modelName)
            html.append("""
            <div class="ai-card">
                <div class="ai-title">\(escapeHtml(aiTitle))</div>
                <div class="ai-intent"><strong>\(strings.intentLabel)：</strong>\(escapeHtml(ai.oneLineIntent))</div>
                <ul class="ai-points">
            """)
            for point in ai.keyModifications {
                html.append("<li>\(escapeHtml(point))</li>")
            }
            html.append("</ul>")
            if !ai.conventionalCommit.isEmpty {
                html.append("""
                <div style="margin-top: 12px; font-size: 12px; color: var(--text-muted);">
                    <strong>\(strings.commitSuggestionLabel)：</strong> <code>\(escapeHtml(ai.conventionalCommit))</code>
                </div>
                """)
            }
            html.append("</div>")
        }

        html.append("""
        <h2 style="font-size: 16px; margin: 24px 0 14px 0;">\(strings.pageDetailsHeader)</h2>
        """)

        for block in diffResult.blocks {
            let leftPageDesc = block.leftPageIndex.map { "Page \($0 + 1)" } ?? strings.noneText
            let rightPageDesc = block.rightPageIndex.map { "Page \($0 + 1)" } ?? strings.noneText
            let badgeClass: String
            let badgeText: String

            switch block.changeType {
            case .unchanged:
                badgeClass = "badge-unchanged"
                badgeText = strings.badgeUnchanged
            case .inserted:
                badgeClass = "badge-added"
                badgeText = strings.badgeInserted
            case .deleted:
                badgeClass = "badge-deleted"
                badgeText = strings.badgeDeleted
            case .modified:
                badgeClass = "badge-modified"
                badgeText = strings.badgeModified
            }

            let summaryCardHtml = renderBreakdownSummary(block: block, visualDiff: visualDiff, strings: strings)

            html.append("""
            <div class="diff-block">
                <div class="diff-block-header">
                    <div>\(leftPageDesc) ⟷ \(rightPageDesc)</div>
                    <span class="badge \(badgeClass)">\(badgeText)</span>
                </div>
                \(summaryCardHtml)
                <div class="diff-content-grid">
                    <div class="diff-pane diff-pane-left">\(renderTokens(block.tokens, side: .left, emptyText: strings.blankPageText))</div>
                    <div class="diff-pane">\(renderTokens(block.tokens, side: .right, emptyText: strings.blankPageText))</div>
                </div>
            </div>
            """)
        }

        html.append("""
            <footer>
                \(escapeHtml(strings.footerText))
            </footer>
        </div>

        <script>
            function toggleTheme() {
                var doc = document.documentElement;
                var current = doc.getAttribute('data-theme');
                if (current === 'dark') {
                    doc.setAttribute('data-theme', 'light');
                } else if (current === 'light') {
                    doc.setAttribute('data-theme', 'dark');
                } else {
                    var isDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
                    doc.setAttribute('data-theme', isDark ? 'light' : 'dark');
                }
            }
        </script>
        </body>
        </html>
        """)

        return html
    }

    private func renderBreakdownSummary(
        block: PdfPageDiffBlock,
        visualDiff: PdfVisualDiffResult?,
        strings: LocalizedStrings
    ) -> String {
        guard block.changeType != .unchanged else { return "" }

        var deletedSnippets: [String] = []
        var addedSnippets: [String] = []

        for token in block.tokens {
            let text = token.text.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !text.isEmpty else { continue }
            if token.changeType == .deleted {
                if !deletedSnippets.contains(text) {
                    deletedSnippets.append(text)
                }
            } else if token.changeType == .inserted {
                if !addedSnippets.contains(text) {
                    addedSnippets.append(text)
                }
            }
        }

        let hasVisual = visualDiff != nil && (visualDiff?.diffPixelCount ?? 0) > 0 &&
            (block.leftPageIndex == visualDiff?.pageIndex || block.rightPageIndex == visualDiff?.pageIndex)

        if deletedSnippets.isEmpty && addedSnippets.isEmpty && !hasVisual {
            return ""
        }

        var output = "<div class='diff-summary-card'>"
        output.append("<div class='summary-header'>📋 \(escapeHtml(strings.changeSummaryTitle))</div>")

        if !deletedSnippets.isEmpty {
            output.append("<div class='summary-group group-deleted'>")
            output.append("<div class='group-title' style='color:var(--deleted-text);'>\(escapeHtml(strings.deletedWhereTitle)) (\(deletedSnippets.count))</div>")
            output.append("<ul class='summary-list'>")
            for item in deletedSnippets.prefix(8) {
                output.append("<li><del>\(escapeHtml(item))</del></li>")
            }
            if deletedSnippets.count > 8 {
                output.append("<li class='more-hint'>\(escapeHtml(String(format: strings.moreItemsFormat, deletedSnippets.count - 8)))</li>")
            }
            output.append("</ul></div>")
        }

        if !addedSnippets.isEmpty {
            output.append("<div class='summary-group group-added'>")
            output.append("<div class='group-title' style='color:var(--added-text);'>\(escapeHtml(strings.addedWhereTitle)) (\(addedSnippets.count))</div>")
            output.append("<ul class='summary-list'>")
            for item in addedSnippets.prefix(8) {
                output.append("<li><ins>\(escapeHtml(item))</ins></li>")
            }
            if addedSnippets.count > 8 {
                output.append("<li class='more-hint'>\(escapeHtml(String(format: strings.moreItemsFormat, addedSnippets.count - 8)))</li>")
            }
            output.append("</ul></div>")
        }

        if let visual = visualDiff, hasVisual {
            output.append("""
            <div class='summary-group group-visual' style='background:rgba(234, 88, 12, 0.08); border:1px solid rgba(234, 88, 12, 0.25);'>
                <div class='group-title' style='color:#ea580c; font-weight:700;'>\(escapeHtml(strings.visualDiffWhereTitle))</div>
                <div style='font-size:12px; color:var(--text); line-height:1.5;'>\(escapeHtml(String(format: strings.visualDiffDescFormat, visual.diffPercentage)))</div>
            </div>
            """)
        }

        output.append("</div>")
        return output
    }

    private enum TokenSide {
        case left
        case right
    }

    private func renderTokens(_ tokens: [PdfDiffToken], side: TokenSide, emptyText: String) -> String {
        guard !tokens.isEmpty else { return "<span style='color:var(--text-muted);'>\(escapeHtml(emptyText))</span>" }

        var output = ""
        for token in tokens {
            switch token.changeType {
            case .unchanged:
                output.append(escapeHtml(token.text))
            case .deleted:
                if side == .left {
                    output.append("<span class='token-del'>\(escapeHtml(token.text))</span>")
                }
            case .inserted:
                if side == .right {
                    output.append("<span class='token-ins'>\(escapeHtml(token.text))</span>")
                }
            case .modified:
                output.append(escapeHtml(token.text))
            }
        }
        return output
    }

    private func escapeHtml(_ text: String) -> String {
        return text
            .replacingOccurrences(of: "&", with: "&amp;")
            .replacingOccurrences(of: "<", with: "&lt;")
            .replacingOccurrences(of: ">", with: "&gt;")
            .replacingOccurrences(of: "\"", with: "&quot;")
            .replacingOccurrences(of: "'", with: "&#39;")
    }

    // MARK: - Localized Text Map

    private struct LocalizedStrings {
        let reportTitle: String
        let generatedAt: String
        let identicalStatus: String
        let diffsStatus: String
        let leftOriginalFile: String
        let rightTargetFile: String
        let pageOverview: String
        let totalPagesAligned: String
        let changedPages: String
        let pagesCountFormatted: String
        let aiSummaryTitle: String
        let intentLabel: String
        let commitSuggestionLabel: String
        let pageDetailsHeader: String
        let badgeUnchanged: String
        let badgeInserted: String
        let badgeDeleted: String
        let badgeModified: String
        let blankPageText: String
        let footerText: String
        let noneText: String
        let themeToggleLabel: String
        let changeSummaryTitle: String
        let deletedWhereTitle: String
        let addedWhereTitle: String
        let moreItemsFormat: String
        let visualDiffWhereTitle: String
        let visualDiffDescFormat: String

        init(language: AppLanguage) {
            switch language {
            case .zhHans:
                reportTitle = "PDF 对比审阅报告"
                generatedAt = "生成时间"
                identicalStatus = "两份文档内容完全一致"
                diffsStatus = "存在 %d 处差异"
                leftOriginalFile = "左侧原文件"
                rightTargetFile = "右侧目标文件"
                pageOverview = "页面总览"
                totalPagesAligned = "共 %d 对齐页面"
                changedPages = "变更页: %d"
                pagesCountFormatted = "%d 页"
                aiSummaryTitle = "✨ AI 变更意图智能总结 (%@)"
                intentLabel = "修改意图"
                commitSuggestionLabel = "推荐 Commit 信息"
                pageDetailsHeader = "逐页文本对比详情"
                badgeUnchanged = "无变化"
                badgeInserted = "新增页面"
                badgeDeleted = "已删除页面"
                badgeModified = "内容变更"
                blankPageText = "[空白页面]"
                footerText = "由 MacCompare PDF 对比引擎生成 · 基于 Apple PDFKit & Rust"
                noneText = "无"
                themeToggleLabel = "切换深浅色"
                changeSummaryTitle = "本页具体变更明细"
                deletedWhereTitle = "🔴 删除了哪里 (Deleted)"
                addedWhereTitle = "🟢 新增了哪里 (Added)"
                moreItemsFormat = "... 还有 %d 项变更未完全列出"
                visualDiffWhereTitle = "🖼️ 图像与排版变动 (Visual & Layout Diff)"
                visualDiffDescFormat = "检测到本页包含显著图像或排版变动（差异区域约占 %.1f%%）"

            case .ja:
                reportTitle = "PDF 比較レビューレポート"
                generatedAt = "生成日時"
                identicalStatus = "両方のドキュメントは完全に一致しています"
                diffsStatus = "%d 件の差分が見つかりました"
                leftOriginalFile = "左側の元ファイル"
                rightTargetFile = "右側の比較ファイル"
                pageOverview = "ページ概要"
                totalPagesAligned = "合計 %d ページ"
                changedPages = "変更ページ: %d"
                pagesCountFormatted = "%d ページ"
                aiSummaryTitle = "✨ AI 変更意図の要約 (%@)"
                intentLabel = "変更意図"
                commitSuggestionLabel = "推奨コミットメッセージ"
                pageDetailsHeader = "ページごとのテキスト差分詳細"
                badgeUnchanged = "変更なし"
                badgeInserted = "追加ページ"
                badgeDeleted = "削除ページ"
                badgeModified = "内容変更"
                blankPageText = "[空白ページ]"
                footerText = "MacCompare PDF 差分エンジンにより生成 · Apple PDFKit & Rust 搭載"
                noneText = "なし"
                themeToggleLabel = "テーマ切替"
                changeSummaryTitle = "このページの変更詳細"
                deletedWhereTitle = "🔴 削除された箇所 (Deleted)"
                addedWhereTitle = "🟢 追加された箇所 (Added)"
                moreItemsFormat = "... 他 %d 件の変更"
                visualDiffWhereTitle = "🖼️ 画像・レイアウトの変更 (Visual & Layout Diff)"
                visualDiffDescFormat = "ページ内に画像またはレイアウトの顕著な変更を検知（差分比率 約 %.1f%%）"

            case .en, .system:
                reportTitle = "PDF Comparison Report"
                generatedAt = "Generated At"
                identicalStatus = "Both documents are completely identical"
                diffsStatus = "Found %d difference(s)"
                leftOriginalFile = "Left Original File"
                rightTargetFile = "Right Target File"
                pageOverview = "Page Overview"
                totalPagesAligned = "%d Aligned Pages"
                changedPages = "Changed Pages: %d"
                pagesCountFormatted = "%d pages"
                aiSummaryTitle = "✨ AI Change Intent Summary (%@)"
                intentLabel = "Intent"
                commitSuggestionLabel = "Suggested Commit Message"
                pageDetailsHeader = "Page-by-Page Comparison Details"
                badgeUnchanged = "Unchanged"
                badgeInserted = "Added Page"
                badgeDeleted = "Deleted Page"
                badgeModified = "Modified"
                blankPageText = "[Blank Page]"
                footerText = "Generated by MacCompare PDF Diff Engine · Powered by Apple PDFKit & Rust"
                noneText = "None"
                themeToggleLabel = "Toggle Theme"
                changeSummaryTitle = "Page Differences Breakdown"
                deletedWhereTitle = "🔴 Deleted Content"
                addedWhereTitle = "🟢 Added Content"
                moreItemsFormat = "... and %d more changes"
                visualDiffWhereTitle = "🖼️ Visual & Layout Differences"
                visualDiffDescFormat = "Significant image or layout differences detected (~%.1f%% of page area)"
            }
        }
    }
}
