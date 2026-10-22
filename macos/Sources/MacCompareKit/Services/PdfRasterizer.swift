import Foundation
import CoreGraphics
import PDFKit

public final class PdfRasterizer: Sendable {
    public static let shared = PdfRasterizer()

    public init() {}

    /// Rasterize a specific page of a PDFDocument into a CGImage.
    public func rasterizePage(
        from document: PDFDocument,
        at pageIndex: Int,
        scale: CGFloat = 1.5
    ) -> CGImage? {
        guard pageIndex >= 0 && pageIndex < document.pageCount,
              let page = document.page(at: pageIndex) else {
            return nil
        }

        let mediaBox = page.bounds(for: .mediaBox)
        let width = Int(mediaBox.width * scale)
        let height = Int(mediaBox.height * scale)

        guard width > 0 && height > 0 else { return nil }

        let colorSpace = CGColorSpaceCreateDeviceRGB()
        let bitmapInfo = CGImageAlphaInfo.premultipliedLast.rawValue

        guard let context = CGContext(
            data: nil,
            width: width,
            height: height,
            bitsPerComponent: 8,
            bytesPerRow: width * 4,
            space: colorSpace,
            bitmapInfo: bitmapInfo
        ) else {
            return nil
        }

        // Draw opaque white background
        context.setFillColor(CGColor(red: 1, green: 1, blue: 1, alpha: 1))
        context.fill(CGRect(x: 0, y: 0, width: width, height: height))

        // Scale and draw PDF page
        context.saveGState()
        context.scaleBy(x: scale, y: scale)
        page.draw(with: .mediaBox, to: context)
        context.restoreGState()

        return context.makeImage()
    }
}
