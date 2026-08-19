import SwiftUI

public struct MinimapView: View {
    public let lines: [DiffLine]

    public init(lines: [DiffLine]) {
        self.lines = lines
    }

    public var body: some View {
        GeometryReader { geometry in
            ZStack(alignment: .top) {
                // Background
                RoundedRectangle(cornerRadius: 6)
                    .fill(Color(nsColor: .windowBackgroundColor).opacity(0.4))

                // Diff Blocks
                VStack(spacing: 1.5) {
                    ForEach(Array(lines.prefix(120).enumerated()), id: \.offset) { _, line in
                        RoundedRectangle(cornerRadius: 1)
                            .fill(diffColor(for: line.changeType))
                            .frame(height: 2)
                    }
                }
                .padding(.horizontal, 4)
                .padding(.vertical, 8)

                // Visible Viewport Indicator
                RoundedRectangle(cornerRadius: 4)
                    .stroke(Color.white.opacity(0.3), lineWidth: 1.5)
                    .background(RoundedRectangle(cornerRadius: 4).fill(Color.white.opacity(0.08)))
                    .frame(height: max(geometry.size.height * 0.25, 40))
                    .padding(.horizontal, 2)
                    .offset(y: geometry.size.height * 0.15)
            }
        }
        .frame(width: 54)
    }

    private func diffColor(for change: ChangeType) -> Color {
        switch change {
        case .unchanged:
            return Color.gray.opacity(0.2)
        case .added:
            return Color.green.opacity(0.8)
        case .deleted:
            return Color.red.opacity(0.8)
        case .modified:
            return Color.orange.opacity(0.8)
        }
    }
}
