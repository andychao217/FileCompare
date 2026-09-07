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
        Group {
            switch updateChecker.stage {
            case .prompt:
                promptView
            case .downloading(let progress, let downloaded, let total):
                downloadingView(progress: progress, downloaded: downloaded, total: total)
            case .extracting:
                extractingView
            case .readyToInstall(let extractedAppPath):
                readyToInstallView(extractedAppPath: extractedAppPath)
            case .failed(let message):
                failedView(message: message)
            }
        }
        .preferredColorScheme(themeManager.effectiveColorScheme)
        .animation(.easeInOut(duration: 0.25), value: updateChecker.stage)
    }

    // MARK: - Stage 1: Software Update Prompt

    private var promptView: some View {
        VStack(spacing: 0) {
            // Header Bar
            HStack {
                Text(languageManager.text(.softwareUpdateTitle))
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundColor(.secondary)
                Spacer()
                Button(action: onDismiss) {
                    Image(systemName: "xmark.circle.fill")
                        .font(.system(size: 15))
                        .foregroundColor(.secondary.opacity(0.6))
                }
                .buttonStyle(.plain)
                .focusEffectDisabled()
            }
            .padding(.horizontal, 18)
            .padding(.top, 14)
            .padding(.bottom, 6)

            // App Icon & Headline Info
            HStack(alignment: .top, spacing: 18) {
                AppIconView(size: 64)

                VStack(alignment: .leading, spacing: 6) {
                    Text("A new version of MacCompare is available!")
                        .font(.system(size: 15, weight: .bold))
                        .foregroundColor(.primary)

                    Text("MacCompare \(updateChecker.latestReleaseVersion) is now available—you have \(updateChecker.currentVersion). Would you like to download it now?")
                        .font(.system(size: 12.5))
                        .foregroundColor(.secondary)
                        .lineSpacing(2)
                }
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 14)

            // Release Notes Card
            VStack(alignment: .leading, spacing: 8) {
                Text(languageManager.text(.releaseNotes) + ":")
                    .font(.system(size: 12.5, weight: .bold))
                    .foregroundColor(.primary.opacity(0.9))

                ScrollView {
                    VStack(alignment: .leading, spacing: 8) {
                        Text(updateChecker.latestReleaseVersion)
                            .font(.system(size: 17, weight: .bold))
                            .foregroundColor(.primary)

                        let notes = updateChecker.localizedReleaseNotes(for: languageManager.effectiveLanguage)
                        Text(notes.isEmpty ? languageManager.text(.noReleaseNotes) : notes)
                            .font(.system(size: 12.5))
                            .foregroundColor(.primary.opacity(0.85))
                            .lineSpacing(3)
                            .textSelection(.enabled)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(14)
                }
                .frame(height: 180)
                .background(
                    RoundedRectangle(cornerRadius: 8)
                        .fill(Color(nsColor: .controlBackgroundColor).opacity(0.8))
                )
                .overlay(
                    RoundedRectangle(cornerRadius: 8)
                        .stroke(Color.secondary.opacity(0.2), lineWidth: 1)
                )
            }
            .padding(.horizontal, 20)

            Spacer(minLength: 14)

            Divider()

            // Footer Actions
            HStack {
                Button(languageManager.text(.skipThisVersion)) {
                    updateChecker.skipVersion()
                    onDismiss()
                }
                .buttonStyle(.bordered)
                .controlSize(.regular)

                Button(languageManager.text(.later)) {
                    onDismiss()
                }
                .buttonStyle(.plain)
                .foregroundColor(.secondary)
                .padding(.leading, 8)

                Spacer()

                Button(action: {
                    updateChecker.startDownload()
                }) {
                    Text(languageManager.text(.installUpdate))
                        .fontWeight(.medium)
                        .padding(.horizontal, 10)
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.regular)
                .keyboardShortcut(.defaultAction)
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 12)
            .background(Color(nsColor: .windowBackgroundColor))
        }
        .frame(width: 530, height: 395)
        .background(Color(nsColor: .windowBackgroundColor))
    }

    // MARK: - Stage 2: Downloading

    private func downloadingView(progress: Double, downloaded: Int64, total: Int64) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            // Window Header
            headerBar

            HStack(spacing: 18) {
                AppIconView(size: 52)

                VStack(alignment: .leading, spacing: 10) {
                    Text(languageManager.text(.downloadingUpdate))
                        .font(.system(size: 14, weight: .bold))
                        .foregroundColor(.primary)

                    // Progress Bar
                    ProgressBar(value: progress)
                        .frame(height: 5)

                    HStack {
                        Text("\(formatBytes(downloaded)) of \(formatBytes(total))")
                            .font(.system(size: 12))
                            .foregroundColor(.secondary)

                        Spacer()

                        Button(languageManager.text(.cancel)) {
                            updateChecker.cancelDownload()
                        }
                        .buttonStyle(.bordered)
                        .controlSize(.small)
                    }
                }
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 14)

            Spacer()
        }
        .frame(width: 480, height: 145)
        .background(Color(nsColor: .windowBackgroundColor))
    }

    // MARK: - Stage 3: Extracting

    private var extractingView: some View {
        VStack(alignment: .leading, spacing: 0) {
            headerBar

            HStack(spacing: 18) {
                AppIconView(size: 52)

                VStack(alignment: .leading, spacing: 10) {
                    Text(languageManager.text(.extractingUpdate))
                        .font(.system(size: 14, weight: .bold))
                        .foregroundColor(.primary)

                    IndeterminateProgressBar()
                        .frame(height: 5)

                    HStack {
                        Spacer()
                        Button(languageManager.text(.cancel)) {
                            updateChecker.cancelDownload()
                        }
                        .buttonStyle(.bordered)
                        .controlSize(.small)
                    }
                }
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 14)

            Spacer()
        }
        .frame(width: 480, height: 145)
        .background(Color(nsColor: .windowBackgroundColor))
    }

    // MARK: - Stage 4: Ready to Install

    private func readyToInstallView(extractedAppPath: URL) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            headerBar

            HStack(spacing: 18) {
                AppIconView(size: 52)

                VStack(alignment: .leading, spacing: 10) {
                    Text(languageManager.text(.readyToInstall))
                        .font(.system(size: 14, weight: .bold))
                        .foregroundColor(.primary)

                    ProgressBar(value: 1.0)
                        .frame(height: 5)

                    HStack {
                        Spacer()
                        Button(action: {
                            updateChecker.installAndRelaunch(extractedAppPath: extractedAppPath)
                        }) {
                            Text(languageManager.text(.installAndRelaunch))
                                .fontWeight(.medium)
                                .padding(.horizontal, 6)
                        }
                        .buttonStyle(.borderedProminent)
                        .controlSize(.regular)
                        .keyboardShortcut(.defaultAction)
                    }
                }
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 14)

            Spacer()
        }
        .frame(width: 480, height: 145)
        .background(Color(nsColor: .windowBackgroundColor))
    }

    // MARK: - Stage 5: Failed

    private func failedView(message: String) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            headerBar

            HStack(spacing: 18) {
                AppIconView(size: 52)

                VStack(alignment: .leading, spacing: 8) {
                    Text(languageManager.text(.updateFailed))
                        .font(.system(size: 14, weight: .bold))
                        .foregroundColor(.red)

                    Text(message)
                        .font(.system(size: 11))
                        .foregroundColor(.secondary)
                        .lineLimit(2)

                    HStack {
                        Button("Open in Browser") {
                            updateChecker.openDownloadPage()
                            onDismiss()
                        }
                        .buttonStyle(.bordered)
                        .controlSize(.small)

                        Spacer()

                        Button(languageManager.text(.cancel)) {
                            updateChecker.cancelDownload()
                        }
                        .buttonStyle(.bordered)
                        .controlSize(.small)
                    }
                }
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 14)

            Spacer()
        }
        .frame(width: 480, height: 145)
        .background(Color(nsColor: .windowBackgroundColor))
    }

    // MARK: - Subcomponents & Helpers

    private var headerBar: some View {
        HStack {
            Text(languageManager.text(.updatingApp))
                .font(.system(size: 13, weight: .semibold))
                .foregroundColor(.secondary)
            Spacer()
        }
        .padding(.horizontal, 18)
        .padding(.top, 14)
        .padding(.bottom, 6)
    }

    private func formatBytes(_ bytes: Int64) -> String {
        let formatter = ByteCountFormatter()
        formatter.allowedUnits = [.useMB, .useKB]
        formatter.countStyle = .file
        return formatter.string(fromByteCount: bytes)
    }
}

// MARK: - App Icon View

struct AppIconView: View {
    var size: CGFloat = 64

    var body: some View {
        if let icon = NSApp.applicationIconImage {
            Image(nsImage: icon)
                .resizable()
                .scaledToFit()
                .frame(width: size, height: size)
                .shadow(color: .black.opacity(0.18), radius: 3, x: 0, y: 2)
        } else {
            Image(systemName: "app.fill")
                .resizable()
                .scaledToFit()
                .frame(width: size, height: size)
                .foregroundColor(.accentColor)
        }
    }
}

// MARK: - Custom Progress Bars

struct ProgressBar: View {
    var value: Double

    var body: some View {
        GeometryReader { geometry in
            ZStack(alignment: .leading) {
                Capsule()
                    .fill(Color.secondary.opacity(0.2))
                    .frame(height: geometry.size.height)

                Capsule()
                    .fill(Color.accentColor)
                    .frame(width: max(0, min(CGFloat(value) * geometry.size.width, geometry.size.width)), height: geometry.size.height)
                    .animation(.linear(duration: 0.1), value: value)
            }
        }
    }
}

struct IndeterminateProgressBar: View {
    @State private var isAnimating = false

    var body: some View {
        GeometryReader { geometry in
            ZStack(alignment: .leading) {
                Capsule()
                    .fill(Color.secondary.opacity(0.2))
                    .frame(height: geometry.size.height)

                Capsule()
                    .fill(Color.accentColor)
                    .frame(width: geometry.size.width * 0.35, height: geometry.size.height)
                    .offset(x: isAnimating ? geometry.size.width * 0.65 : 0)
                    .animation(.easeInOut(duration: 1.0).repeatForever(autoreverses: true), value: isAnimating)
            }
        }
        .onAppear {
            isAnimating = true
        }
    }
}
