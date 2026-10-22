using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using WinCompare.Core.Models;

namespace WinCompare.Core.Services;

public static class PdfReportExporter
{
    public static string GenerateHtmlReport(
        PdfDocument leftDoc, 
        PdfDocument rightDoc, 
        PdfDiffResult diffResult,
        PdfVisualDiffResult? visualDiff = null,
        AppLanguage? language = null,
        string? theme = null)
    {
        var effLang = language ?? LanguageManager.Instance.EffectiveLanguage;
        var strings = new PdfReportStrings(effLang);
        string langCode = effLang switch
        {
            AppLanguage.ZhHans => "zh-CN",
            AppLanguage.Ja => "ja",
            _ => "en"
        };

        var effTheme = theme ?? SettingsManager.Instance.Theme;
        string themeAttr = effTheme switch
        {
            "Dark" => "dark",
            "Light" => "light",
            _ => "auto"
        };

        string title = $"{strings.ReportTitle} - {leftDoc.FileName} vs {rightDoc.FileName}";
        string dateStr = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        int diffCount = diffResult.TotalDiffCount;
        string statusText = diffCount == 0 
            ? strings.IdenticalStatus 
            : string.Format(strings.DiffsStatusFormat, diffCount);

        int totalPages = Math.Max(leftDoc.PageCount, rightDoc.PageCount);
        int changedPagesCount = diffResult.Blocks.Count(b => b.ChangeType != PdfDiffChangeType.Unchanged);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine($"<html lang=\"{langCode}\" data-theme=\"{themeAttr}\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"<title>{EscapeHtml(title)}</title>");
        sb.AppendLine(@"<style>
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
            html[data-theme=""dark""] {
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
                html[data-theme=""auto""] {
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
                font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;
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
            .group-visual {
                background: rgba(234, 88, 12, 0.08);
                border: 1px solid rgba(234, 88, 12, 0.25);
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
                font-family: ""SF Mono"", Consolas, Menlo, monospace;
                font-size: 12px;
            }
            .diff-pane {
                padding: 14px 18px;
                overflow-x: auto;
                white-space: pre-wrap;
                word-break: break-word;
                line-height: 1.6;
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
        </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"container\">");
        sb.AppendLine("    <div class=\"top-nav\">");
        sb.AppendLine($"        <button class=\"theme-toggle-btn\" onclick=\"toggleTheme()\" id=\"themeToggleBtn\">🌓 {EscapeHtml(strings.ThemeToggleLabel)}</button>");
        sb.AppendLine("    </div>");

        sb.AppendLine("    <div class=\"header-card\">");
        sb.AppendLine($"        <h1 class=\"header-title\">{EscapeHtml(strings.ReportTitle)}</h1>");
        sb.AppendLine($"        <div style=\"font-size: 13px; color: var(--text-muted);\">{EscapeHtml(strings.GeneratedAt)}: {dateStr} · {EscapeHtml(statusText)}</div>");
        sb.AppendLine("        <div class=\"meta-grid\">");
        sb.AppendLine("            <div class=\"meta-item\">");
        sb.AppendLine($"                <span>{EscapeHtml(strings.LeftOriginalFile)}</span>");
        sb.AppendLine($"                <strong>{EscapeHtml(leftDoc.FileName)}</strong><br>");
        sb.AppendLine($"                <small style=\"color:var(--text-muted)\">{string.Format(strings.PagesCountFormatted, leftDoc.PageCount)} · {leftDoc.Metadata.FileSizeFormatted}</small>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div class=\"meta-item\">");
        sb.AppendLine($"                <span>{EscapeHtml(strings.RightTargetFile)}</span>");
        sb.AppendLine($"                <strong>{EscapeHtml(rightDoc.FileName)}</strong><br>");
        sb.AppendLine($"                <small style=\"color:var(--text-muted)\">{string.Format(strings.PagesCountFormatted, rightDoc.PageCount)} · {rightDoc.Metadata.FileSizeFormatted}</small>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div class=\"meta-item\">");
        sb.AppendLine($"                <span>{EscapeHtml(strings.PageOverview)}</span>");
        sb.AppendLine($"                <strong>{string.Format(strings.TotalPagesAlignedFormat, totalPages)}</strong><br>");
        sb.AppendLine($"                <small style=\"color:var(--text-muted)\">{string.Format(strings.ChangedPagesFormat, changedPagesCount)}</small>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("    </div>");

        sb.AppendLine($"    <h2 style=\"font-size: 16px; margin: 24px 0 14px 0;\">{EscapeHtml(strings.PageDetailsHeader)}</h2>");

        foreach (var block in diffResult.Blocks)
        {
            string leftPageDesc = block.LeftPageIndex.HasValue ? $"Page {block.LeftPageIndex.Value + 1}" : strings.NoneText;
            string rightPageDesc = block.RightPageIndex.HasValue ? $"Page {block.RightPageIndex.Value + 1}" : strings.NoneText;
            string badgeClass;
            string badgeText;

            switch (block.ChangeType)
            {
                case PdfDiffChangeType.Unchanged:
                    badgeClass = "badge-unchanged";
                    badgeText = strings.BadgeUnchanged;
                    break;
                case PdfDiffChangeType.Inserted:
                    badgeClass = "badge-added";
                    badgeText = strings.BadgeInserted;
                    break;
                case PdfDiffChangeType.Deleted:
                    badgeClass = "badge-deleted";
                    badgeText = strings.BadgeDeleted;
                    break;
                case PdfDiffChangeType.Modified:
                default:
                    badgeClass = "badge-modified";
                    badgeText = strings.BadgeModified;
                    break;
            }

            string summaryCardHtml = RenderBreakdownSummary(block, visualDiff, strings);

            sb.AppendLine("    <div class=\"diff-block\">");
            sb.AppendLine("        <div class=\"diff-block-header\">");
            sb.AppendLine($"            <div>{leftPageDesc} ⟷ {rightPageDesc}</div>");
            sb.AppendLine($"            <span class=\"badge {badgeClass}\">{badgeText}</span>");
            sb.AppendLine("        </div>");
            sb.Append(summaryCardHtml);
            sb.AppendLine("        <div class=\"diff-content-grid\">");
            sb.AppendLine($"            <div class=\"diff-pane diff-pane-left\">{RenderTokens(block.Tokens, TokenSide.Left, strings.BlankPageText)}</div>");
            sb.AppendLine($"            <div class=\"diff-pane\">{RenderTokens(block.Tokens, TokenSide.Right, strings.BlankPageText)}</div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("    </div>");
        }

        sb.AppendLine("    <footer>");
        sb.AppendLine($"        {EscapeHtml(strings.FooterText)}");
        sb.AppendLine("    </footer>");
        sb.AppendLine("</div>");

        sb.AppendLine(@"<script>
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
        </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string RenderBreakdownSummary(
        PdfPageDiffBlock block,
        PdfVisualDiffResult? visualDiff,
        PdfReportStrings strings)
    {
        if (block.ChangeType == PdfDiffChangeType.Unchanged) return string.Empty;

        var deletedSnippets = new List<string>();
        var addedSnippets = new List<string>();

        foreach (var token in block.Tokens)
        {
            string text = token.Text.Trim();
            if (string.IsNullOrEmpty(text)) continue;

            if (token.ChangeType == PdfDiffChangeType.Deleted)
            {
                if (!deletedSnippets.Contains(text))
                {
                    deletedSnippets.Add(text);
                }
            }
            else if (token.ChangeType == PdfDiffChangeType.Inserted)
            {
                if (!addedSnippets.Contains(text))
                {
                    addedSnippets.Add(text);
                }
            }
        }

        bool hasVisual = visualDiff != null && visualDiff.DiffPixelCount > 0 &&
            (block.LeftPageIndex == visualDiff.PageIndex || block.RightPageIndex == visualDiff.PageIndex);

        if (deletedSnippets.Count == 0 && addedSnippets.Count == 0 && !hasVisual)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("        <div class=\"diff-summary-card\">");
        sb.AppendLine($"            <div class=\"summary-header\">📋 {EscapeHtml(strings.ChangeSummaryTitle)}</div>");

        if (deletedSnippets.Count > 0)
        {
            sb.AppendLine("            <div class=\"summary-group group-deleted\">");
            sb.AppendLine($"                <div class=\"group-title\" style=\"color:var(--deleted-text);\">{EscapeHtml(strings.DeletedWhereTitle)} ({deletedSnippets.Count})</div>");
            sb.AppendLine("                <ul class=\"summary-list\">");
            foreach (var item in deletedSnippets.Take(8))
            {
                sb.AppendLine($"                    <li><del>{EscapeHtml(item)}</del></li>");
            }
            if (deletedSnippets.Count > 8)
            {
                sb.AppendLine($"                    <li class=\"more-hint\">{EscapeHtml(string.Format(strings.MoreItemsFormat, deletedSnippets.Count - 8))}</li>");
            }
            sb.AppendLine("                </ul></div>");
        }

        if (addedSnippets.Count > 0)
        {
            sb.AppendLine("            <div class=\"summary-group group-added\">");
            sb.AppendLine($"                <div class=\"group-title\" style=\"color:var(--added-text);\">{EscapeHtml(strings.AddedWhereTitle)} ({addedSnippets.Count})</div>");
            sb.AppendLine("                <ul class=\"summary-list\">");
            foreach (var item in addedSnippets.Take(8))
            {
                sb.AppendLine($"                    <li><ins>{EscapeHtml(item)}</ins></li>");
            }
            if (addedSnippets.Count > 8)
            {
                sb.AppendLine($"                    <li class=\"more-hint\">{EscapeHtml(string.Format(strings.MoreItemsFormat, addedSnippets.Count - 8))}</li>");
            }
            sb.AppendLine("                </ul></div>");
        }

        if (visualDiff != null && hasVisual)
        {
            sb.AppendLine("            <div class=\"summary-group group-visual\">");
            sb.AppendLine($"                <div class=\"group-title\" style=\"color:#ea580c; font-weight:700;\">{EscapeHtml(strings.VisualDiffWhereTitle)}</div>");
            sb.AppendLine($"                <div style=\"font-size:12px; color:var(--text); line-height:1.5;\">{EscapeHtml(string.Format(strings.VisualDiffDescFormat, visualDiff.DiffPercentage))}</div>");
            sb.AppendLine("            </div>");
        }

        sb.AppendLine("        </div>");
        return sb.ToString();
    }

    private enum TokenSide
    {
        Left,
        Right
    }

    private static string RenderTokens(List<PdfDiffToken> tokens, TokenSide side, string emptyText)
    {
        if (tokens.Count == 0)
        {
            return $"<span style=\"color:var(--text-muted);\">{EscapeHtml(emptyText)}</span>";
        }

        var sb = new StringBuilder();
        foreach (var token in tokens)
        {
            switch (token.ChangeType)
            {
                case PdfDiffChangeType.Unchanged:
                    sb.Append(EscapeHtml(token.Text));
                    break;
                case PdfDiffChangeType.Deleted:
                    if (side == TokenSide.Left)
                    {
                        sb.Append($"<span class=\"token-del\">{EscapeHtml(token.Text)}</span>");
                    }
                    break;
                case PdfDiffChangeType.Inserted:
                    if (side == TokenSide.Right)
                    {
                        sb.Append($"<span class=\"token-ins\">{EscapeHtml(token.Text)}</span>");
                    }
                    break;
                case PdfDiffChangeType.Modified:
                    sb.Append(EscapeHtml(token.Text));
                    break;
            }
        }
        return sb.ToString();
    }

    private static string EscapeHtml(string text)
    {
        return WebUtility.HtmlEncode(text ?? string.Empty);
    }

    private sealed class PdfReportStrings
    {
        public string ReportTitle { get; }
        public string GeneratedAt { get; }
        public string IdenticalStatus { get; }
        public string DiffsStatusFormat { get; }
        public string LeftOriginalFile { get; }
        public string RightTargetFile { get; }
        public string PageOverview { get; }
        public string TotalPagesAlignedFormat { get; }
        public string ChangedPagesFormat { get; }
        public string PagesCountFormatted { get; }
        public string PageDetailsHeader { get; }
        public string BadgeUnchanged { get; }
        public string BadgeInserted { get; }
        public string BadgeDeleted { get; }
        public string BadgeModified { get; }
        public string BlankPageText { get; }
        public string FooterText { get; }
        public string NoneText { get; }
        public string ThemeToggleLabel { get; }
        public string ChangeSummaryTitle { get; }
        public string DeletedWhereTitle { get; }
        public string AddedWhereTitle { get; }
        public string MoreItemsFormat { get; }
        public string VisualDiffWhereTitle { get; }
        public string VisualDiffDescFormat { get; }

        public PdfReportStrings(AppLanguage lang)
        {
            switch (lang)
            {
                case AppLanguage.ZhHans:
                    ReportTitle = "PDF 对比审阅报告";
                    GeneratedAt = "生成时间";
                    IdenticalStatus = "两份文档内容完全一致";
                    DiffsStatusFormat = "存在 %d 处差异";
                    LeftOriginalFile = "左侧原文件";
                    RightTargetFile = "右侧目标文件";
                    PageOverview = "页面总览";
                    TotalPagesAlignedFormat = "共 %d 对齐页面";
                    ChangedPagesFormat = "变更页: %d";
                    PagesCountFormatted = "%d 页";
                    PageDetailsHeader = "逐页文本对比详情";
                    BadgeUnchanged = "无变化";
                    BadgeInserted = "新增页面";
                    BadgeDeleted = "已删除页面";
                    BadgeModified = "内容变更";
                    BlankPageText = "[空白页面]";
                    FooterText = "由 WinCompare PDF 差异引擎生成 · 搭载 C# & Fluent Design";
                    NoneText = "无";
                    ThemeToggleLabel = "切换深浅色";
                    ChangeSummaryTitle = "本页具体变更明细";
                    DeletedWhereTitle = "🔴 删除了哪里 (Deleted)";
                    AddedWhereTitle = "🟢 新增了哪里 (Added)";
                    MoreItemsFormat = "... 还有 %d 项变更未完全列出";
                    VisualDiffWhereTitle = "🖼️ 图像与排版变动 (Visual & Layout Diff)";
                    VisualDiffDescFormat = "检测到本页包含显著图像或排版变动（差异区域约占 %.1f%%）";
                    break;

                case AppLanguage.Ja:
                    ReportTitle = "PDF 比較レビューレポート";
                    GeneratedAt = "生成日時";
                    IdenticalStatus = "両方のドキュメントは完全に一致しています";
                    DiffsStatusFormat = "%d 件の差分が見つかりました";
                    LeftOriginalFile = "左側の元ファイル";
                    RightTargetFile = "右側の比較ファイル";
                    PageOverview = "ページ概要";
                    TotalPagesAlignedFormat = "合計 %d ページ";
                    ChangedPagesFormat = "変更ページ: %d";
                    PagesCountFormatted = "%d ページ";
                    PageDetailsHeader = "ページごとのテキスト差分詳細";
                    BadgeUnchanged = "変更なし";
                    BadgeInserted = "追加ページ";
                    BadgeDeleted = "削除ページ";
                    BadgeModified = "内容変更";
                    BlankPageText = "[空白ページ]";
                    FooterText = "WinCompare PDF 差分エンジンにより生成 · C# & Fluent Design 搭載";
                    NoneText = "なし";
                    ThemeToggleLabel = "テーマ切替";
                    ChangeSummaryTitle = "このページの変更詳細";
                    DeletedWhereTitle = "🔴 削除された箇所 (Deleted)";
                    AddedWhereTitle = "🟢 追加された箇所 (Added)";
                    MoreItemsFormat = "... 他 %d 件の変更";
                    VisualDiffWhereTitle = "🖼️ 画像・レイアウトの変更 (Visual & Layout Diff)";
                    VisualDiffDescFormat = "ページ内に画像またはレイアウトの顕著な変更を検知（差分比率 約 %.1f%%）";
                    break;

                default: // En
                    ReportTitle = "PDF Comparison Report";
                    GeneratedAt = "Generated At";
                    IdenticalStatus = "Both documents are completely identical";
                    DiffsStatusFormat = "Found %d difference(s)";
                    LeftOriginalFile = "Left Original File";
                    RightTargetFile = "Right Target File";
                    PageOverview = "Page Overview";
                    TotalPagesAlignedFormat = "%d Aligned Pages";
                    ChangedPagesFormat = "Changed Pages: %d";
                    PagesCountFormatted = "%d pages";
                    PageDetailsHeader = "Page-by-Page Comparison Details";
                    BadgeUnchanged = "Unchanged";
                    BadgeInserted = "Added Page";
                    BadgeDeleted = "Deleted Page";
                    BadgeModified = "Modified";
                    BlankPageText = "[Blank Page]";
                    FooterText = "Generated by WinCompare PDF Diff Engine · Powered by C# & Fluent Design";
                    NoneText = "None";
                    ThemeToggleLabel = "Toggle Theme";
                    ChangeSummaryTitle = "Page Differences Breakdown";
                    DeletedWhereTitle = "🔴 Deleted Content";
                    AddedWhereTitle = "🟢 Added Content";
                    MoreItemsFormat = "... and %d more changes";
                    VisualDiffWhereTitle = "🖼️ Visual & Layout Differences";
                    VisualDiffDescFormat = "Significant image or layout differences detected (~%.1f%% of page area)";
                    break;
            }

            // Convert printf-style %d / %.1f to C# {0} format
            DiffsStatusFormat = DiffsStatusFormat.Replace("%d", "{0}");
            TotalPagesAlignedFormat = TotalPagesAlignedFormat.Replace("%d", "{0}");
            ChangedPagesFormat = ChangedPagesFormat.Replace("%d", "{0}");
            PagesCountFormatted = PagesCountFormatted.Replace("%d", "{0}");
            MoreItemsFormat = MoreItemsFormat.Replace("%d", "{0}");
            VisualDiffDescFormat = VisualDiffDescFormat.Replace("%.1f%%", "{0:F1}%");
        }
    }
}
