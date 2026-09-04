import SwiftUI

public struct InlineModelDownloadPopover: View {
    @State private var aiModelManager = AIModelManager.shared
    @State private var languageManager = LanguageManager.shared
    public var onDownloadCompleted: () -> Void

    public init(onDownloadCompleted: @escaping () -> Void) {
        self.onDownloadCompleted = onDownloadCompleted
    }

    private var defaultModel: AIModelId { .gemma3_1b }
    private var downloadState: AIModelDownloadState {
        aiModelManager.modelStates[defaultModel] ?? .notDownloaded
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            // Header
            HStack(spacing: 8) {
                ZStack {
                    Circle()
                        .fill(
                            LinearGradient(
                                colors: [Color.purple.opacity(0.8), Color.indigo.opacity(0.9)],
                                startPoint: .topLeading,
                                endPoint: .bottomTrailing
                            )
                        )
                        .frame(width: 28, height: 28)
                    Image(systemName: "sparkles")
                        .font(.system(size: 13, weight: .bold))
                        .foregroundColor(.white)
                }

                VStack(alignment: .leading, spacing: 2) {
                    Text(languageManager.text(.aiEnableOfflineTitle))
                        .font(.system(size: 13, weight: .bold))
                        .foregroundColor(.primary)
                    Text(languageManager.text(.aiEnableOfflineSubtitle))
                        .font(.system(size: 11))
                        .foregroundColor(.secondary)
                }
            }

            Divider()

            // Key Highlights
            VStack(alignment: .leading, spacing: 8) {
                highlightItem(icon: "lock.shield.fill", text: languageManager.text(.aiEnableOfflineFeature1), color: .green)
                highlightItem(icon: "bolt.fill", text: languageManager.text(.aiEnableOfflineFeature2), color: .orange)
            }
            .padding(.vertical, 2)

            Divider()

            // Download Action Area
            switch downloadState {
            case .notDownloaded:
                VStack(spacing: 6) {
                    Button {
                        aiModelManager.startDownload(model: defaultModel)
                    } label: {
                        HStack(spacing: 6) {
                            Image(systemName: "arrow.down.circle.fill")
                            Text(languageManager.text(.aiDownloadAndEnable))
                        }
                        .font(.system(size: 12, weight: .semibold))
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 4)
                    }
                    .buttonStyle(.borderedProminent)
                    .tint(
                        LinearGradient(
                            colors: [Color.purple, Color.indigo],
                            startPoint: .leading,
                            endPoint: .trailing
                        )
                    )
                    .controlSize(.regular)
                }

            case .downloading(let progress, let speed, let rem):
                VStack(alignment: .leading, spacing: 6) {
                    HStack {
                        Text(languageManager.text(.aiDownloading))
                            .font(.system(size: 11, weight: .medium))
                        Spacer()
                        Text("\(Int(progress * 100))%")
                            .font(.system(size: 11, weight: .bold, design: .monospaced))
                    }

                    ProgressView(value: progress)
                        .progressViewStyle(.linear)
                        .tint(.purple)

                    HStack {
                        Text(speed)
                        Spacer()
                        Text(rem)
                    }
                    .font(.system(size: 10, design: .monospaced))
                    .foregroundColor(.secondary)

                    Button(role: .cancel) {
                        aiModelManager.cancelDownload(for: defaultModel)
                    } label: {
                        Text(languageManager.text(.cancel))
                            .font(.system(size: 11))
                    }
                    .buttonStyle(.plain)
                    .foregroundColor(.secondary)
                    .padding(.top, 2)
                }

            case .ready:
                HStack(spacing: 6) {
                    Image(systemName: "checkmark.circle.fill")
                        .foregroundColor(.green)
                    Text(languageManager.text(.aiReady))
                        .font(.system(size: 12, weight: .semibold))
                        .foregroundColor(.green)
                    Spacer()
                    Button {
                        onDownloadCompleted()
                    } label: {
                        Text(languageManager.text(.aiExperienceNow))
                            .font(.system(size: 11, weight: .medium))
                    }
                    .buttonStyle(.borderedProminent)
                    .controlSize(.small)
                }
                .onAppear {
                    // Auto-dismiss and trigger after ready
                    DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
                        onDownloadCompleted()
                    }
                }

            case .error(let err):
                VStack(alignment: .leading, spacing: 4) {
                    Text(err)
                        .font(.system(size: 11))
                        .foregroundColor(.red)
                    Button {
                        aiModelManager.startDownload(model: defaultModel)
                    } label: {
                        Text(languageManager.text(.retry))
                    }
                    .controlSize(.small)
                }
            }
        }
        .padding(16)
        .frame(width: 320)
        .background(Material.regular)
    }

    private func highlightItem(icon: String, text: String, color: Color) -> some View {
        HStack(alignment: .top, spacing: 8) {
            Image(systemName: icon)
                .font(.system(size: 11))
                .foregroundColor(color)
                .frame(width: 14, height: 14)
            Text(text)
                .font(.system(size: 11))
                .foregroundColor(.secondary)
                .fixedSize(horizontal: false, vertical: true)
        }
    }
}
