import Foundation
import CoreGraphics
import AppKit

public struct PixelDiffOutput: Sendable {
    public let result: PdfVisualDiffResult
    public let diffMaskImage: CGImage?

    public init(result: PdfVisualDiffResult, diffMaskImage: CGImage?) {
        self.result = result
        self.diffMaskImage = diffMaskImage
    }
}

public final class PdfPixelDiffEngine: Sendable {
    public static let shared = PdfPixelDiffEngine()

    public init() {}

    /// Compare two rasterized page images pixel by pixel.
    public func compareImages(
        left: CGImage,
        right: CGImage,
        pageIndex: Int,
        tolerance: Double = 0.08
    ) -> PixelDiffOutput {
        let width = min(left.width, right.width)
        let height = min(left.height, right.height)

        guard width > 0 && height > 0 else {
            return PixelDiffOutput(result: PdfVisualDiffResult(pageIndex: pageIndex), diffMaskImage: nil)
        }

        let totalPixels = width * height
        let bytesPerPixel = 4
        let bytesPerRow = width * bytesPerPixel

        var leftBytes = [UInt8](repeating: 0, count: totalPixels * bytesPerPixel)
        var rightBytes = [UInt8](repeating: 0, count: totalPixels * bytesPerPixel)
        var maskBytes = [UInt8](repeating: 0, count: totalPixels * bytesPerPixel)

        let colorSpace = CGColorSpaceCreateDeviceRGB()
        let bitmapInfo = CGImageAlphaInfo.premultipliedLast.rawValue

        // Extract left image buffer
        guard let leftContext = CGContext(
            data: &leftBytes,
            width: width,
            height: height,
            bitsPerComponent: 8,
            bytesPerRow: bytesPerRow,
            space: colorSpace,
            bitmapInfo: bitmapInfo
        ) else {
            return PixelDiffOutput(result: PdfVisualDiffResult(pageIndex: pageIndex), diffMaskImage: nil)
        }
        leftContext.draw(left, in: CGRect(x: 0, y: 0, width: width, height: height))

        // Extract right image buffer
        guard let rightContext = CGContext(
            data: &rightBytes,
            width: width,
            height: height,
            bitsPerComponent: 8,
            bytesPerRow: bytesPerRow,
            space: colorSpace,
            bitmapInfo: bitmapInfo
        ) else {
            return PixelDiffOutput(result: PdfVisualDiffResult(pageIndex: pageIndex), diffMaskImage: nil)
        }
        rightContext.draw(right, in: CGRect(x: 0, y: 0, width: width, height: height))

        let byteTolerance = Int(tolerance * 255.0 * 3.0)
        var diffPixelCount = 0

        // Grid clustering for bounding boxes (32x32 tiles)
        let tileSize = 32
        let gridCols = (width + tileSize - 1) / tileSize
        let gridRows = (height + tileSize - 1) / tileSize
        var tileHasDiff = [Bool](repeating: false, count: gridCols * gridRows)

        for y in 0..<height {
            let rowOffset = y * bytesPerRow
            let tileY = y / tileSize

            for x in 0..<width {
                let offset = rowOffset + x * bytesPerPixel
                let rDiff = abs(Int(leftBytes[offset]) - Int(rightBytes[offset]))
                let gDiff = abs(Int(leftBytes[offset + 1]) - Int(rightBytes[offset + 1]))
                let bDiff = abs(Int(leftBytes[offset + 2]) - Int(rightBytes[offset + 2]))

                if (rDiff + gDiff + bDiff) > byteTolerance {
                    diffPixelCount += 1

                    // Highlight pixel in mask (Semi-transparent red: R=239, G=68, B=68, A=160)
                    maskBytes[offset] = 239
                    maskBytes[offset + 1] = 68
                    maskBytes[offset + 2] = 68
                    maskBytes[offset + 3] = 160

                    let tileX = x / tileSize
                    tileHasDiff[tileY * gridCols + tileX] = true
                } else {
                    // Transparent
                    maskBytes[offset] = 0
                    maskBytes[offset + 1] = 0
                    maskBytes[offset + 2] = 0
                    maskBytes[offset + 3] = 0
                }
            }
        }

        // Generate clustered bounding boxes from marked tiles (Connected Component BFS)
        var visited = [Bool](repeating: false, count: gridCols * gridRows)
        var rawBoxes: [CGRect] = []

        for ty in 0..<gridRows {
            for tx in 0..<gridCols {
                let idx = ty * gridCols + tx
                guard tileHasDiff[idx], !visited[idx] else { continue }

                var minTileX = tx
                var maxTileX = tx
                var minTileY = ty
                var maxTileY = ty

                var queue: [(Int, Int)] = [(tx, ty)]
                visited[idx] = true

                while !queue.isEmpty {
                    let (currX, currY) = queue.removeFirst()
                    minTileX = min(minTileX, currX)
                    maxTileX = max(maxTileX, currX)
                    minTileY = min(minTileY, currY)
                    maxTileY = max(maxTileY, currY)

                    // 8-direction neighbor search
                    for dy in -1...1 {
                        for dx in -1...1 {
                            guard dx != 0 || dy != 0 else { continue }
                            let nx = currX + dx
                            let ny = currY + dy
                            if nx >= 0 && nx < gridCols && ny >= 0 && ny < gridRows {
                                let nIdx = ny * gridCols + nx
                                if tileHasDiff[nIdx] && !visited[nIdx] {
                                    visited[nIdx] = true
                                    queue.append((nx, ny))
                                }
                            }
                        }
                    }
                }

                let x = CGFloat(minTileX * tileSize)
                let y = CGFloat(minTileY * tileSize)
                let w = CGFloat(min((maxTileX - minTileX + 1) * tileSize, width - minTileX * tileSize))
                let h = CGFloat(min((maxTileY - minTileY + 1) * tileSize, height - minTileY * tileSize))
                rawBoxes.append(CGRect(x: x, y: y, width: w, height: h))
            }
        }

        // Merge neighboring boxes within 20px threshold to form clean semantic regions
        var boundingBoxes: [CGRect] = []
        for box in rawBoxes {
            var merged = false
            for i in 0..<boundingBoxes.count {
                if boundingBoxes[i].insetBy(dx: -20, dy: -20).intersects(box) {
                    boundingBoxes[i] = boundingBoxes[i].union(box)
                    merged = true
                    break
                }
            }
            if !merged {
                boundingBoxes.append(box)
            }
        }

        // Create Mask CGImage
        var maskImage: CGImage?
        if diffPixelCount > 0 {
            if let maskContext = CGContext(
                data: &maskBytes,
                width: width,
                height: height,
                bitsPerComponent: 8,
                bytesPerRow: bytesPerRow,
                space: colorSpace,
                bitmapInfo: bitmapInfo
            ) {
                maskImage = maskContext.makeImage()
            }
        }

        let result = PdfVisualDiffResult(
            pageIndex: pageIndex,
            diffBoundingBoxes: boundingBoxes,
            diffPixelCount: diffPixelCount,
            totalPixelCount: totalPixels
        )

        return PixelDiffOutput(result: result, diffMaskImage: maskImage)
    }
}
