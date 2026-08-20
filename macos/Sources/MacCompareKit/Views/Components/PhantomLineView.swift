import SwiftUI

/// Diagonal hatching pattern for phantom (aligned empty placeholder) lines.
public struct PhantomHatchShape: Shape {
    public func path(in rect: CGRect) -> Path {
        var path = Path()
        let step: CGFloat = 8.0
        for x in stride(from: -rect.height, to: rect.width + rect.height, by: step) {
            path.move(to: CGPoint(x: x, y: 0))
            path.addLine(to: CGPoint(x: x + rect.height, y: rect.height))
        }
        return path
    }
}

public struct PhantomLineView: View {
    public init() {}

    public var body: some View {
        ZStack {
            Color(nsColor: .separatorColor).opacity(0.12)
            PhantomHatchShape()
                .stroke(Color.secondary.opacity(0.2), lineWidth: 1.2)
                .clipped()
        }
        .frame(height: 20)
    }
}
