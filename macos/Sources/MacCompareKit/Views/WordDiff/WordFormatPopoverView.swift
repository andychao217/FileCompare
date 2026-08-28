import SwiftUI

public struct WordFormatPopoverView: View {
    public let formatDifferences: [FormatDiffItem]

    public init(formatDifferences: [FormatDiffItem]) {
        self.formatDifferences = formatDifferences
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 6) {
                Image(systemName: "paintpalette.fill")
                    .foregroundColor(.purple)
                    .font(.system(size: 13))
                Text(LanguageManager.shared.text(.formatChanged))
                    .font(.headline)
                    .foregroundColor(.primary)
            }

            Divider()

            ForEach(formatDifferences) { item in
                HStack(spacing: 8) {
                    Text(item.propertyName)
                        .font(.system(size: 12, weight: .semibold))
                        .foregroundColor(.secondary)
                        .frame(width: 90, alignment: .leading)

                    Text(item.oldValue)
                        .font(.system(size: 12))
                        .foregroundColor(.red)
                        .strikethrough()

                    Image(systemName: "arrow.right")
                        .font(.system(size: 10))
                        .foregroundColor(.secondary)

                    Text(item.newValue)
                        .font(.system(size: 12, weight: .semibold))
                        .foregroundColor(.green)
                }
            }
        }
        .padding(14)
        .frame(minWidth: 260)
        .background(Color(nsColor: .windowBackgroundColor))
    }
}
