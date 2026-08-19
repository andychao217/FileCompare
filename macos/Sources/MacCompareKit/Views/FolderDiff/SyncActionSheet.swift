import SwiftUI

public struct SyncActionSheet: View {
    @Bindable public var viewModel: FolderDiffViewModel
    @Environment(\.dismiss) private var dismiss
    @State private var isExecuting = false
    @State private var languageManager = LanguageManager.shared

    public init(viewModel: FolderDiffViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        VStack(spacing: 16) {
            HStack {
                Image(systemName: "arrow.triangle.2.circlepath")
                    .font(.system(size: 24))
                    .foregroundColor(.accentColor)
                VStack(alignment: .leading, spacing: 2) {
                    Text(languageManager.text(.dryRunTitle))
                        .font(.headline)
                    Text(languageManager.text(.dryRunSubtitle))
                        .font(.subheadline)
                        .foregroundColor(.secondary)
                }
                Spacer()
            }

            Divider()

            if viewModel.pendingSyncPlan.isEmpty {
                VStack(spacing: 8) {
                    Image(systemName: "checkmark.circle.fill")
                        .font(.system(size: 32))
                        .foregroundColor(.green)
                    Text(languageManager.text(.completelyInSync))
                        .font(.subheadline)
                        .foregroundColor(.secondary)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                VStack(alignment: .leading, spacing: 6) {
                    Text("\(languageManager.text(.pendingOperations)) (\(viewModel.pendingSyncPlan.count) \(languageManager.text(.itemsCount))):")
                        .font(.system(size: 12, weight: .semibold))

                    ScrollView {
                        VStack(spacing: 4) {
                            ForEach(viewModel.pendingSyncPlan) { item in
                                HStack(spacing: 8) {
                                    icon(for: item.action)
                                    Text(item.relativePath)
                                        .font(.system(size: 11, design: .monospaced))
                                        .lineLimit(1)
                                    Spacer()
                                    Text(item.action.rawValue)
                                        .font(.system(size: 10))
                                        .foregroundColor(.secondary)
                                }
                                .padding(.horizontal, 8)
                                .padding(.vertical, 4)
                                .background(RoundedRectangle(cornerRadius: 4).fill(Color(nsColor: .windowBackgroundColor).opacity(0.5)))
                            }
                        }
                    }
                    .frame(height: 160)
                }
                .padding(8)
                .background(RoundedRectangle(cornerRadius: 6).fill(Color(nsColor: .controlBackgroundColor)))
            }

            Spacer()

            HStack {
                Button(languageManager.text(.cancel)) {
                    dismiss()
                }
                .keyboardShortcut(.cancelAction)

                Spacer()

                Button(isExecuting ? languageManager.text(.executing) : languageManager.text(.executeSync)) {
                    isExecuting = true
                    Task {
                        await viewModel.executePendingSync()
                        isExecuting = false
                        dismiss()
                    }
                }
                .buttonStyle(.borderedProminent)
                .disabled(viewModel.pendingSyncPlan.isEmpty || isExecuting)
            }
        }
        .padding(20)
        .frame(width: 520, height: 340)
    }

    @ViewBuilder
    private func icon(for action: SyncActionType) -> some View {
        switch action {
        case .copyLeftToRight, .copyRightToLeft:
            Image(systemName: "plus.circle.fill").foregroundColor(.green)
        case .overwriteLeftToRight, .overwriteRightToLeft:
            Image(systemName: "arrow.triangle.2.circlepath.circle.fill").foregroundColor(.blue)
        case .deleteRight, .deleteLeft:
            Image(systemName: "trash.circle.fill").foregroundColor(.red)
        }
    }
}
