import SwiftUI

public struct FileValidationDialogView: View {
    public let result: ValidationResult
    public let onDismiss: () -> Void
    public let onSwitchMode: (TabContentType, URL) -> Void

    @State private var languageManager = LanguageManager.shared

    public init(
        result: ValidationResult,
        onDismiss: @escaping () -> Void,
        onSwitchMode: @escaping (TabContentType, URL) -> Void
    ) {
        self.result = result
        self.onDismiss = onDismiss
        self.onSwitchMode = onSwitchMode
    }

    public var body: some View {
        ZStack {
            // Semi-transparent backdrop
            Color.black.opacity(0.35)
                .ignoresSafeArea()
                .onTapGesture {
                    onDismiss()
                }

            // Main Modal Card
            VStack(spacing: 16) {
                // Top Icon Badge
                ZStack {
                    Circle()
                        .fill(iconBackgroundColor.opacity(0.15))
                        .frame(width: 48, height: 48)

                    Image(systemName: iconName)
                        .font(.system(size: 24, weight: .semibold))
                        .foregroundColor(iconColor)
                }
                .padding(.top, 4)

                // Title
                Text(languageManager.text(result.titleKey))
                    .font(.system(size: 16, weight: .bold))
                    .foregroundColor(.primary)
                    .multilineTextAlignment(.center)

                // Description Message
                Text(languageManager.text(result.messageKey))
                    .font(.system(size: 13))
                    .foregroundColor(.secondary)
                    .multilineTextAlignment(.center)
                    .lineSpacing(3)
                    .padding(.horizontal, 8)

                // Action Buttons
                HStack(spacing: 12) {
                    switch result.promptType {
                    case .suggestSwitch(let recMode):
                        Button(languageManager.text(.cancel)) {
                            onDismiss()
                        }
                        .buttonStyle(.bordered)
                        .controlSize(.regular)
                        .keyboardShortcut(.cancelAction)

                        Button(languageManager.text(.btnSwitchMode)) {
                            let url = result.targetURL
                            onDismiss()
                            onSwitchMode(recMode, url)
                        }
                        .buttonStyle(.borderedProminent)
                        .controlSize(.regular)
                        .keyboardShortcut(.defaultAction)

                    case .noticeOnly:
                        Button(languageManager.text(.btnGotIt)) {
                            onDismiss()
                        }
                        .buttonStyle(.borderedProminent)
                        .controlSize(.regular)
                        .keyboardShortcut(.defaultAction)
                    }
                }
                .padding(.top, 6)
            }
            .padding(22)
            .frame(width: 420)
            .background(
                RoundedRectangle(cornerRadius: 14, style: .continuous)
                    .fill(Color(nsColor: .windowBackgroundColor))
                    .shadow(color: Color.black.opacity(0.25), radius: 18, x: 0, y: 8)
            )
            .overlay(
                RoundedRectangle(cornerRadius: 14, style: .continuous)
                    .stroke(Color(nsColor: .separatorColor).opacity(0.8), lineWidth: 1)
            )
        }
    }

    private var iconName: String {
        switch result.promptType {
        case .suggestSwitch:
            return "arrow.triangle.swap"
        case .noticeOnly:
            return "exclamationmark.triangle.fill"
        }
    }

    private var iconColor: Color {
        switch result.promptType {
        case .suggestSwitch:
            return .accentColor
        case .noticeOnly:
            return .orange
        }
    }

    private var iconBackgroundColor: Color {
        switch result.promptType {
        case .suggestSwitch:
            return .accentColor
        case .noticeOnly:
            return .orange
        }
    }
}
