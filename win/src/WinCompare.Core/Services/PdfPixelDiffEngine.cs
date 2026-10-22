using System;
using System.Collections.Generic;
using WinCompare.Core.Models;

namespace WinCompare.Core.Services;

/// <summary>
/// High-performance visual pixel difference engine for rasterized PDF pages on Windows.
/// Detects changes across text layout, images, tables, annotations, and graphics without relying on OCR or font extraction.
/// </summary>
public static class PdfPixelDiffEngine
{
    public static PdfVisualDiffResult ComparePixelBuffers(
        byte[] leftBytes,
        byte[] rightBytes,
        int width,
        int height,
        int pageIndex,
        double tolerance = 0.08)
    {
        var result = new PdfVisualDiffResult { PageIndex = pageIndex };
        if (width <= 0 || height <= 0 || leftBytes.Length < width * height * 4 || rightBytes.Length < width * height * 4)
        {
            return result;
        }

        int bytesPerPixel = 4;
        int bytesPerRow = width * bytesPerPixel;
        int byteTolerance = (int)(tolerance * 255.0 * 3.0);
        int diffPixelCount = 0;

        int tileSize = 32;
        int gridCols = (width + tileSize - 1) / tileSize;
        int gridRows = (height + tileSize - 1) / tileSize;
        bool[] tileHasDiff = new bool[gridCols * gridRows];

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * bytesPerRow;
            int tileY = y / tileSize;

            for (int x = 0; x < width; x++)
            {
                int offset = rowOffset + x * bytesPerPixel;
                int rDiff = Math.Abs((int)leftBytes[offset] - (int)rightBytes[offset]);
                int gDiff = Math.Abs((int)leftBytes[offset + 1] - (int)rightBytes[offset + 1]);
                int bDiff = Math.Abs((int)leftBytes[offset + 2] - (int)rightBytes[offset + 2]);

                if ((rDiff + gDiff + bDiff) > byteTolerance)
                {
                    diffPixelCount++;
                    int tileX = x / tileSize;
                    tileHasDiff[tileY * gridCols + tileX] = true;
                }
            }
        }

        result.DiffPixelCount = diffPixelCount;
        result.DiffPercentage = (double)diffPixelCount / (width * height) * 100.0;

        // BFS 8-neighbor connected component aggregation
        bool[] visited = new bool[gridCols * gridRows];
        var rawBoxes = new List<PdfBoundingBox>();

        for (int ty = 0; ty < gridRows; ty++)
        {
            for (int tx = 0; tx < gridCols; tx++)
            {
                int idx = ty * gridCols + tx;
                if (!tileHasDiff[idx] || visited[idx]) continue;

                int minTileX = tx, maxTileX = tx;
                int minTileY = ty, maxTileY = ty;

                var queue = new Queue<(int X, int Y)>();
                queue.Enqueue((tx, ty));
                visited[idx] = true;

                while (queue.Count > 0)
                {
                    var (currX, currY) = queue.Dequeue();
                    minTileX = Math.Min(minTileX, currX);
                    maxTileX = Math.Max(maxTileX, currX);
                    minTileY = Math.Min(minTileY, currY);
                    maxTileY = Math.Max(maxTileY, currY);

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = currX + dx;
                            int ny = currY + dy;
                            if (nx >= 0 && nx < gridCols && ny >= 0 && ny < gridRows)
                            {
                                int nIdx = ny * gridCols + nx;
                                if (tileHasDiff[nIdx] && !visited[nIdx])
                                {
                                    visited[nIdx] = true;
                                    queue.Enqueue((nx, ny));
                                }
                            }
                        }
                    }
                }

                double rx = minTileX * tileSize;
                double ry = minTileY * tileSize;
                double rw = Math.Min((maxTileX - minTileX + 1) * tileSize, width - minTileX * tileSize);
                double rh = Math.Min((maxTileY - minTileY + 1) * tileSize, height - minTileY * tileSize);
                rawBoxes.Add(new PdfBoundingBox { X = rx, Y = ry, Width = rw, Height = rh });
            }
        }

        // Merge neighboring regions within 24px threshold
        var boundingBoxes = new List<PdfBoundingBox>();
        foreach (var box in rawBoxes)
        {
            bool merged = false;
            for (int i = 0; i < boundingBoxes.Count; i++)
            {
                var b = boundingBoxes[i];
                if (IntersectsWithPadding(b, box, 24))
                {
                    boundingBoxes[i] = UnionBoxes(b, box);
                    merged = true;
                    break;
                }
            }
            if (!merged)
            {
                boundingBoxes.Add(box);
            }
        }

        result.BoundingBoxes = boundingBoxes;
        return result;
    }

    private static bool IntersectsWithPadding(PdfBoundingBox a, PdfBoundingBox b, double pad)
    {
        return !(b.X > a.X + a.Width + pad ||
                 b.X + b.Width < a.X - pad ||
                 b.Y > a.Y + a.Height + pad ||
                 b.Y + b.Height < a.Y - pad);
    }

    private static PdfBoundingBox UnionBoxes(PdfBoundingBox a, PdfBoundingBox b)
    {
        double x = Math.Min(a.X, b.X);
        double y = Math.Min(a.Y, b.Y);
        double r = Math.Max(a.X + a.Width, b.X + b.Width);
        double bottom = Math.Max(a.Y + a.Height, b.Y + b.Height);
        return new PdfBoundingBox { X = x, Y = y, Width = r - x, Height = bottom - y };
    }
}
