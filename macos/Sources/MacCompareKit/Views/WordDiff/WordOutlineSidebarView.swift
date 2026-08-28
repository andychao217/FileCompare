import SwiftUI

public struct WordOutlineSidebarView: View {
    @Bindable public var viewModel: WordDiffViewModel
    public var onDismiss: (() -> Void)? = nil

    public init(viewModel: WordDiffViewModel, onDismiss: (() -> Void)? = nil) {
        self.viewModel = viewModel
        self.onDismiss = onDismiss
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            // Header
            HStack {
                Image(systemName: "list.bullet.indent")
                    .foregroundColor(.accentColor)
                    .font(.system(size: 12))
                Text(LanguageManager.shared.text(.documentOutline))
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundColor(.primary)
                Spacer()
                Text("\(headingsCount)")
                    .font(.caption2)
                    .foregroundColor(.secondary)
                    .padding(.horizontal, 6)
                    .padding(.vertical, 2)
                    .background(Capsule().fill(Color.secondary.opacity(0.15)))

                if let onDismiss {
                    Button(action: onDismiss) {
                        Image(systemName: "xmark")
                            .font(.system(size: 10, weight: .bold))
                            .foregroundColor(.secondary)
                    }
                    .buttonStyle(.plain)
                    .padding(.leading, 4)
                }
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 8)
            .background(Color(nsColor: .controlBackgroundColor))

            Divider()

            // Headings List
            if combinedHeadings.isEmpty {
                VStack(spacing: 12) {
                    Spacer()
                    Image(systemName: "list.bullet.rectangle.portrait")
                        .font(.system(size: 32))
                        .foregroundColor(.secondary.opacity(0.4))
                    Text(LanguageManager.shared.text(.noHeadingsDetected))
                        .font(.system(size: 12, weight: .medium))
                        .foregroundColor(.secondary)
                    Text("Word 标题样式（H1~H6）将自动在此提取为章节大纲")
                        .font(.system(size: 10))
                        .foregroundColor(.secondary.opacity(0.7))
                        .multilineTextAlignment(.center)
                        .padding(.horizontal, 16)
                    Spacer()
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: 2) {
                        ForEach(combinedHeadings, id: \.headingIndex) { item in
                            let isSelected = viewModel.selectedBlockIndex == item.blockIndex
                            Button {
                                viewModel.selectedBlockIndex = item.blockIndex
                            } label: {
                                HStack(spacing: 6) {
                                    // Indentation based on level
                                    Spacer().frame(width: CGFloat(max(0, (item.level - 1) * 12)))

                                    Circle()
                                        .fill(statusColor(for: item.changeType))
                                        .frame(width: 6, height: 6)

                                    Text(item.text)
                                        .font(.system(size: 11, weight: item.level == 1 ? .bold : (item.level == 2 ? .semibold : .regular)))
                                        .lineLimit(1)
                                        .foregroundColor(isSelected ? .accentColor : .primary)

                                    Spacer()
                                }
                                .padding(.horizontal, 8)
                                .padding(.vertical, 5)
                                .background(
                                    RoundedRectangle(cornerRadius: 4)
                                        .fill(isSelected ? Color.accentColor.opacity(0.12) : Color.clear)
                                )
                            }
                            .buttonStyle(.plain)
                        }
                    }
                    .padding(6)
                }
            }
        }
        .frame(width: 220)
        .background(Color(nsColor: .windowBackgroundColor))
    }

    private struct OutlineItem: Identifiable {
        var id: String { "\(headingIndex)-\(blockIndex)" }
        let headingIndex: Int
        let blockIndex: Int
        let level: Int
        let text: String
        let changeType: ChangeType
    }

    private var headingsCount: Int {
        combinedHeadings.count
    }

    private var combinedHeadings: [OutlineItem] {
        var items: [OutlineItem] = []
        for block in viewModel.diffResult.blocks {
            if let p = block.leftParagraph, p.isHeading {
                items.append(OutlineItem(
                    headingIndex: p.index,
                    blockIndex: block.blockIndex,
                    level: p.headingLevel ?? 1,
                    text: p.text,
                    changeType: block.changeType
                ))
            } else if let p = block.rightParagraph, p.isHeading {
                items.append(OutlineItem(
                    headingIndex: p.index,
                    blockIndex: block.blockIndex,
                    level: p.headingLevel ?? 1,
                    text: p.text,
                    changeType: block.changeType
                ))
            }
        }
        return items
    }

    private func statusColor(for type: ChangeType) -> Color {
        switch type {
        case .added: return .green
        case .deleted: return .red
        case .modified: return .orange
        case .unchanged: return .secondary.opacity(0.5)
        }
    }
}
