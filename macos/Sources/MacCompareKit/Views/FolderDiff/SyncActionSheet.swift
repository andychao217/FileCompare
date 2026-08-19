import SwiftUI

public struct SyncActionSheet: View {
    @Environment(\.dismiss) private var dismiss

    public var body: some View {
        VStack(spacing: 16) {
            HStack {
                Image(systemName: "arrow.triangle.2.circlepath")
                    .font(.system(size: 24))
                    .foregroundColor(.accentColor)
                VStack(alignment: .leading, spacing: 2) {
                    Text("Dry Run: Sync Actions Preview")
                        .font(.headline)
                    Text("No changes will be written to disk in preview mode.")
                        .font(.subheadline)
                        .foregroundColor(.secondary)
                }
                Spacer()
            }

            Divider()

            VStack(alignment: .leading, spacing: 8) {
                Text("Pending Sync Operations:")
                    .font(.system(size: 12, weight: .semibold))

                HStack {
                    Image(systemName: "plus.circle.fill")
                        .foregroundColor(.green)
                    Text("Copy 5 files from Source to Target")
                        .font(.system(size: 12))
                }
                HStack {
                    Image(systemName: "arrow.triangle.2.circlepath.circle.fill")
                        .foregroundColor(.blue)
                    Text("Overwrite 18 modified files")
                        .font(.system(size: 12))
                }
                HStack {
                    Image(systemName: "trash.circle.fill")
                        .foregroundColor(.red)
                    Text("Delete 2 orphan files on Target")
                        .font(.system(size: 12))
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(12)
            .background(RoundedRectangle(cornerRadius: 6).fill(Color(nsColor: .controlBackgroundColor)))

            Spacer()

            HStack {
                Button("Cancel") {
                    dismiss()
                }
                .keyboardShortcut(.cancelAction)

                Spacer()

                Button("Execute Sync") {
                    dismiss()
                }
                .buttonStyle(.borderedProminent)
            }
        }
        .padding(20)
        .frame(width: 480, height: 280)
    }
}
