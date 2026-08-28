import SwiftUI

public struct WordMetadataDiffView: View {
    @Bindable public var viewModel: WordDiffViewModel

    public init(viewModel: WordDiffViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                // Summary cards
                HStack(spacing: 16) {
                    docInfoCard(
                        title: viewModel.leftDocument?.fileName ?? LanguageManager.shared.text(.sourceFile),
                        doc: viewModel.leftDocument,
                        badgeColor: .blue
                    )
                    docInfoCard(
                        title: viewModel.rightDocument?.fileName ?? LanguageManager.shared.text(.targetFile),
                        doc: viewModel.rightDocument,
                        badgeColor: .purple
                    )
                }

                // Detailed Key-Value Comparison Table
                VStack(alignment: .leading, spacing: 0) {
                    HStack {
                        Text(LanguageManager.shared.text(.property))
                            .font(.system(size: 12, weight: .bold))
                            .frame(width: 140, alignment: .leading)
                        Divider()
                        Text(viewModel.leftDocument?.fileName ?? LanguageManager.shared.text(.sourceFile))
                            .font(.system(size: 12, weight: .bold))
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .padding(.leading, 8)
                        Divider()
                        Text(viewModel.rightDocument?.fileName ?? LanguageManager.shared.text(.targetFile))
                            .font(.system(size: 12, weight: .bold))
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .padding(.leading, 8)
                    }
                    .padding(.horizontal, 14)
                    .padding(.vertical, 10)
                    .background(Color(nsColor: .controlBackgroundColor))

                    Divider()

                    ForEach(viewModel.diffResult.metadataDiffs) { item in
                        HStack {
                            HStack(spacing: 6) {
                                if item.isDifferent {
                                    Circle()
                                        .fill(Color.orange)
                                        .frame(width: 6, height: 6)
                                }
                                Text(item.fieldName)
                                    .font(.system(size: 12, weight: item.isDifferent ? .semibold : .regular))
                                    .foregroundColor(item.isDifferent ? .primary : .secondary)
                            }
                            .frame(width: 140, alignment: .leading)

                            Divider()

                            Text(item.leftValue)
                                .font(.system(size: 12))
                                .foregroundColor(item.isDifferent ? .red : .primary)
                                .frame(maxWidth: .infinity, alignment: .leading)
                                .padding(.leading, 8)

                            Divider()

                            Text(item.rightValue)
                                .font(.system(size: 12, weight: item.isDifferent ? .semibold : .regular))
                                .foregroundColor(item.isDifferent ? .green : .primary)
                                .frame(maxWidth: .infinity, alignment: .leading)
                                .padding(.leading, 8)
                        }
                        .padding(.horizontal, 14)
                        .padding(.vertical, 8)
                        .background(item.isDifferent ? Color.orange.opacity(0.06) : Color.clear)
                        Divider()
                    }
                }
                .background(Color(nsColor: .windowBackgroundColor))
                .cornerRadius(8)
                .overlay(
                    RoundedRectangle(cornerRadius: 8)
                        .stroke(Color(nsColor: .separatorColor), lineWidth: 1)
                )
            }
            .padding(20)
        }
    }

    private func docInfoCard(title: String, doc: WordDocumentModel?, badgeColor: Color) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Image(systemName: "doc.text.fill")
                    .foregroundColor(badgeColor)
                Text(title)
                    .font(.system(size: 13, weight: .bold))
                    .lineLimit(1)
                Spacer()
                Text(doc?.metadata.fileFormat ?? "-")
                    .font(.caption2)
                    .foregroundColor(.secondary)
                    .padding(.horizontal, 6)
                    .padding(.vertical, 2)
                    .background(Capsule().fill(Color.secondary.opacity(0.12)))
            }

            Divider()

            HStack(spacing: 16) {
                VStack(alignment: .leading, spacing: 2) {
                    Text(LanguageManager.shared.text(.wordCount))
                        .font(.caption2)
                        .foregroundColor(.secondary)
                    Text("\(doc?.metadata.wordCount ?? 0)")
                        .font(.system(size: 16, weight: .bold))
                }

                VStack(alignment: .leading, spacing: 2) {
                    Text(LanguageManager.shared.text(.paragraphs))
                        .font(.caption2)
                        .foregroundColor(.secondary)
                    Text("\(doc?.metadata.paragraphCount ?? 0)")
                        .font(.system(size: 16, weight: .bold))
                }

                VStack(alignment: .leading, spacing: 2) {
                    Text(LanguageManager.shared.text(.fileSize))
                        .font(.caption2)
                        .foregroundColor(.secondary)
                    Text(doc?.metadata.fileSizeFormatted ?? "0 KB")
                        .font(.system(size: 14, weight: .medium))
                }
            }
        }
        .padding(14)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Color(nsColor: .controlBackgroundColor))
        .cornerRadius(8)
    }
}
