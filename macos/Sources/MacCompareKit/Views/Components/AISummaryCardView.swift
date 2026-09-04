import SwiftUI
import AppKit

public struct AISummaryCardView: View {
    public let result: AISummaryResult?
    public let isGenerating: Bool
    public let onReanalyze: () -> Void
    public let onClose: () -> Void

    @State private var copiedCommit: Bool = false
    @State private var copiedText: Bool = false
    @State private var languageManager = LanguageManager.shared

    public init(
        result: AISummaryResult?,
        isGenerating: Bool,
        onReanalyze: @escaping () -> Void,
        onClose: @escaping () -> Void
    ) {
        self.result = result
        self.isGenerating = isGenerating
        self.onReanalyze = onReanalyze
        self.onClose = onClose
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            // Header bar
            HStack {
                HStack(spacing: 6) {
                    Image(systemName: "sparkles")
                        .foregroundColor(.purple)
                        .font(.system(size: 13, weight: .bold))
                    Text("\(languageManager.text(.aiSummaryTitle)) (\(result?.modelName ?? AIModelManager.shared.activeModelName))")
                        .font(.system(size: 12, weight: .bold))
                        .foregroundColor(.primary)
                }

                Spacer()

                if isGenerating {
                    HStack(spacing: 6) {
                        ProgressView()
                            .controlSize(.mini)
                        Text(languageManager.text(.aiAnalyzing))
                            .font(.system(size: 11))
                            .foregroundColor(.secondary)
                    }
                } else if let time = result?.executionTimeSeconds {
                    HStack(spacing: 4) {
                        Circle().fill(Color.green).frame(width: 5, height: 5)
                        let badgeText = AIModelManager.shared.executionMode == .cloudAPI
                            ? languageManager.text(.aiCloudBadge)
                            : languageManager.text(.aiLocalOfflineBadge)
                        Text(String(format: "%.2fs · %@", time, badgeText))
                            .font(.system(size: 10, design: .monospaced))
                            .foregroundColor(.secondary)
                    }
                }

                Button(action: onClose) {
                    Image(systemName: "xmark.circle.fill")
                        .font(.system(size: 14))
                        .foregroundColor(.secondary.opacity(0.6))
                }
                .buttonStyle(.plain)
                .padding(.leading, 8)
            }

            Divider()

            if isGenerating {
                // Skeleton loading state
                VStack(alignment: .leading, spacing: 8) {
                    HStack(spacing: 8) {
                        Image(systemName: "lightbulb.fill")
                            .foregroundColor(.yellow.opacity(0.8))
                        let activeModel = result?.modelName ?? AIModelManager.shared.activeModelName
                        Text(languageManager.text(.aiAnalyzingHint, activeModel))
                            .font(.system(size: 12))
                            .foregroundColor(.secondary)
                    }
                    .padding(.vertical, 4)
                }
            } else if let summary = result {
                // One-line Intent
                HStack(alignment: .top, spacing: 8) {
                    Image(systemName: "lightbulb.fill")
                        .foregroundColor(.yellow)
                        .font(.system(size: 12))
                        .padding(.top, 1)

                    Text(summary.oneLineIntent)
                        .font(.system(size: 12, weight: .medium))
                        .foregroundColor(.primary)
                        .fixedSize(horizontal: false, vertical: true)
                }
                .padding(8)
                .frame(maxWidth: .infinity, alignment: .leading)
                .background(Color.yellow.opacity(0.08))
                .clipShape(RoundedRectangle(cornerRadius: 6, style: .continuous))

                // Key points
                if !summary.keyModifications.isEmpty {
                    VStack(alignment: .leading, spacing: 5) {
                        ForEach(summary.keyModifications, id: \.self) { point in
                            HStack(alignment: .top, spacing: 6) {
                                Circle()
                                    .fill(Color.purple.opacity(0.8))
                                    .frame(width: 4, height: 4)
                                    .padding(.top, 6)

                                Text(point)
                                    .font(.system(size: 11))
                                    .foregroundColor(.secondary)
                                    .fixedSize(horizontal: false, vertical: true)
                            }
                        }
                    }
                    .padding(.leading, 4)
                }

                Divider()

                // Conventional Commit Preview
                HStack(spacing: 6) {
                    Image(systemName: "arrow.triangle.branch")
                        .font(.system(size: 10))
                        .foregroundColor(.purple)
                    Text(summary.conventionalCommit)
                        .font(.system(size: 11, design: .monospaced))
                        .foregroundColor(.primary)
                        .lineLimit(2)
                    Spacer()
                }
                .padding(.horizontal, 8)
                .padding(.vertical, 5)
                .background(Color(nsColor: .controlBackgroundColor).opacity(0.7))
                .cornerRadius(5)
                .overlay(
                    RoundedRectangle(cornerRadius: 5)
                        .stroke(Color.secondary.opacity(0.2), lineWidth: 0.8)
                )

                // Action buttons
                HStack(spacing: 10) {
                    Button {
                        NSPasteboard.general.clearContents()
                        NSPasteboard.general.setString(summary.conventionalCommit, forType: .string)
                        copiedCommit = true
                        DispatchQueue.main.asyncAfter(deadline: .now() + 2) {
                            copiedCommit = false
                        }
                    } label: {
                        HStack(spacing: 4) {
                            Image(systemName: copiedCommit ? "checkmark" : "doc.on.doc")
                            Text(copiedCommit ? languageManager.text(.aiCopied) : languageManager.text(.aiCopyCommitMessage))
                        }
                        .font(.system(size: 11))
                    }
                    .buttonStyle(.bordered)
                    .controlSize(.small)

                    Button {
                        let text = "\(summary.oneLineIntent)\n" + summary.keyModifications.map { "• \($0)" }.joined(separator: "\n")
                        NSPasteboard.general.clearContents()
                        NSPasteboard.general.setString(text, forType: .string)
                        copiedText = true
                        DispatchQueue.main.asyncAfter(deadline: .now() + 2) {
                            copiedText = false
                        }
                    } label: {
                        HStack(spacing: 4) {
                            Image(systemName: copiedText ? "checkmark" : "doc.on.clipboard")
                            Text(copiedText ? languageManager.text(.aiCopied) : languageManager.text(.aiCopyText))
                        }
                        .font(.system(size: 11))
                    }
                    .buttonStyle(.bordered)
                    .controlSize(.small)

                    Spacer()

                    Button(action: onReanalyze) {
                        HStack(spacing: 4) {
                            Image(systemName: "arrow.clockwise")
                            Text(languageManager.text(.aiReanalyze))
                        }
                        .font(.system(size: 11))
                    }
                    .buttonStyle(.plain)
                    .foregroundColor(.secondary)
                }
            }
        }
        .padding(14)
        .background(Material.regular)
        .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .stroke(
                    LinearGradient(
                        colors: [Color.purple.opacity(0.4), Color.indigo.opacity(0.2), Color.clear],
                        startPoint: .topLeading,
                        endPoint: .bottomTrailing
                    ),
                    lineWidth: 1
                )
        )
        .shadow(color: Color.black.opacity(0.15), radius: 10, x: 0, y: 4)
        .padding(.horizontal, 14)
        .padding(.top, 8)
        .padding(.bottom, 6)
    }
}
