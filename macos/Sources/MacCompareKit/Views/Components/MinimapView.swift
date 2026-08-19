import SwiftUI

public struct MinimapView: View {
    public let lines: [DiffLine]
    public let onSelectLine: ((Int) -> Void)?
    @State private var viewportRatio: CGFloat = 0.0
    @State private var isDragging: Bool = false

    public init(lines: [DiffLine], onSelectLine: ((Int) -> Void)? = nil) {
        self.lines = lines
        self.onSelectLine = onSelectLine
    }

    public var body: some View {
        GeometryReader { geometry in
            let totalHeight = geometry.size.height
            let totalLines = max(lines.count, 1)
            let viewportHeight = max(totalHeight * min(25.0 / CGFloat(totalLines), 0.35), 36)

            ZStack(alignment: .top) {
                // Background Track
                RoundedRectangle(cornerRadius: 6)
                    .fill(Color(nsColor: .windowBackgroundColor).opacity(0.45))

                // Canvas rendering for high-performance and full-scale diff representation
                Canvas { context, size in
                    guard totalLines > 0 else { return }
                    let step = size.height / CGFloat(totalLines)

                    for (index, line) in lines.enumerated() {
                        let y = CGFloat(index) * step
                        let rect = CGRect(x: 4, y: y, width: size.width - 8, height: max(step, 1.5))

                        switch line.changeType {
                        case .unchanged:
                            context.fill(Path(rect), with: .color(Color.gray.opacity(0.18)))
                        case .added:
                            context.fill(Path(rect), with: .color(Color.green.opacity(0.85)))
                        case .deleted:
                            context.fill(Path(rect), with: .color(Color.red.opacity(0.85)))
                        case .modified:
                            context.fill(Path(rect), with: .color(Color.orange.opacity(0.85)))
                        }
                    }
                }
                .padding(.vertical, 4)

                // Interactive Viewport Thumb Indicator
                RoundedRectangle(cornerRadius: 4)
                    .stroke(isDragging ? Color.accentColor : Color.white.opacity(0.4), lineWidth: 1.5)
                    .background(
                        RoundedRectangle(cornerRadius: 4)
                            .fill(isDragging ? Color.accentColor.opacity(0.2) : Color.white.opacity(0.12))
                    )
                    .frame(height: viewportHeight)
                    .padding(.horizontal, 2)
                    .offset(y: viewportRatio * max(totalHeight - viewportHeight, 0))
            }
            .contentShape(Rectangle())
            // Click / Tap gesture to jump directly
            .gesture(
                DragGesture(minimumDistance: 0)
                    .onChanged { value in
                        isDragging = true
                        let clampedY = max(0, min(value.location.y, totalHeight))
                        let ratio = totalHeight > 0 ? clampedY / totalHeight : 0
                        self.viewportRatio = max(0, min(ratio, 1.0))

                        let targetLine = Int(ratio * CGFloat(lines.count))
                        let clampedLine = max(0, min(targetLine, lines.count - 1))
                        onSelectLine?(clampedLine)
                    }
                    .onEnded { _ in
                        isDragging = false
                    }
            )
        }
        .frame(width: 58)
    }
}
