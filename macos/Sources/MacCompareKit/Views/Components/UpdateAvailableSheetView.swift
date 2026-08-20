import SwiftUI
import AppKit

public struct UpdateAvailableSheetView: View {
    @State private var updateChecker = UpdateCheckerService.shared
    @State private var languageManager = LanguageManager.shared
    @State private var themeManager = ThemeManager.shared
    public var onDismiss: () -> Void

    public init(onDismiss: @escaping () -> Void) {
        self.onDismiss = onDismiss
    }

    public var body: some View {
        VStack(spacing: 0) {
            // Header
            HStack(spacing: 14) {
                ZStack {
                    Circle()
                        .fill(Color.accentColor.opacity(0.15))
                        .frame(width: 44, height: 44)
                    Image(systemName: "sparkles")
                        .font(.system(size: 22, weight: .semibold))
                        .foregroundColor(.accentColor)
                }

                VStack(alignment: .leading, spacing: 3) {
                    Text(languageManager.text(.newVersionAvailableTitle))
                        .font(.headline)
                        .foregroundColor(.primary)
                    Text("v\(updateChecker.latestReleaseVersion) • \(languageManager.text(.currentVersion)): v\(updateChecker.currentVersion)")
                        .font(.caption)
                        .foregroundColor(.secondary)
                }

                Spacer()

                Button(action: onDismiss) {
                    Image(systemName: "xmark.circle.fill")
                        .font(.system(size: 18))
                        .foregroundColor(.secondary.opacity(0.7))
                }
                .buttonStyle(.plain)
                .focusEffectDisabled()
                .help(languageManager.text(.close))
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 16)
            .background(Color(nsColor: .windowBackgroundColor))

            Divider()

            // Release Notes Content
            VStack(alignment: .leading, spacing: 10) {
                Text(languageManager.text(.releaseNotes))
                    .font(.system(size: 12, weight: .bold))
                    .foregroundColor(.secondary)

                ScrollView {
                    Text(updateChecker.latestReleaseNotes.isEmpty ? languageManager.text(.noReleaseNotes) : updateChecker.latestReleaseNotes)
                        .font(.system(size: 12))
                        .foregroundColor(.primary.opacity(0.9))
                        .lineSpacing(4)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(12)
                }
                .frame(maxHeight: 180)
                .background(
                    RoundedRectangle(cornerRadius: 8)
                        .fill(Color(nsColor: .textBackgroundColor).opacity(0.6))
                )
                .overlay(
                    RoundedRectangle(cornerRadius: 8)
                        .stroke(Color.secondary.opacity(0.15), lineWidth: 1)
                )
            }
            .padding(20)

            Divider()

            // Footer Actions
            HStack {
                Button(languageManager.text(.later)) {
                    onDismiss()
                }
                .keyboardShortcut(.cancelAction)

                Spacer()

                Button(action: {
                    updateChecker.openDownloadPage()
                    onDismiss()
                }) {
                    HStack(spacing: 6) {
                        Image(systemName: "arrow.down.circle.fill")
                        Text(languageManager.text(.downloadUpdate))
                    }
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.regular)
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 14)
            .background(Color(nsColor: .windowBackgroundColor))
        }
        .frame(width: 480, height: 360)
        .background(Color(nsColor: .controlBackgroundColor))
        .preferredColorScheme(themeManager.effectiveColorScheme)
    }
}
