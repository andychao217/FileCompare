using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WinCompare.Core.Models;

namespace WinCompare.Core.Services;

/// <summary>
/// High-performance zero-dependency PDF document parser and text stream extractor for Windows.
/// Supports FlateDecode decompression, ToUnicode CMap font mapping, and multi-page text extraction.
/// </summary>
public static class PdfDocumentParser
{
    public static PdfDocument Parse(string filePath)
    {
        var doc = new PdfDocument
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath)
        };

        if (!File.Exists(filePath))
        {
            return doc;
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            doc.Metadata.FileSizeFormatted = FormatByteCount(fileInfo.Length);

            byte[] data = File.ReadAllBytes(filePath);

            // 1. Parse all ToUnicode CMaps in the document
            var cmap = ParseAllCMaps(data);

            // 2. Extract pages by /Type /Page
            var pages = new List<PdfPage>();
            var pageChunks = SplitByPageObjects(data);

            if (pageChunks.Count > 0)
            {
                for (int i = 0; i < pageChunks.Count; i++)
                {
                    string pageText = ExtractPageText(pageChunks[i], cmap);
                    var lines = ExtractLinesFromText(pageText);

                    pages.Add(new PdfPage
                    {
                        PageIndex = i,
                        Width = 595,
                        Height = 842,
                        Text = pageText,
                        Lines = lines
                    });
                }
            }
            else
            {
                // Fallback: parse entire document as single stream
                string allText = ExtractPageText(data, cmap);
                var lines = ExtractLinesFromText(allText);
                pages.Add(new PdfPage
                {
                    PageIndex = 0,
                    Width = 595,
                    Height = 842,
                    Text = allText,
                    Lines = lines
                });
            }

            doc.Pages = pages;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PdfDocumentParser] Error parsing {filePath}: {ex.Message}");
        }

        return doc;
    }

    private static Dictionary<int, string> ParseAllCMaps(byte[] data)
    {
        var result = new Dictionary<int, string>();
        var streams = ExtractStreams(data);

        foreach (var streamBytes in streams)
        {
            byte[]? decompressed = DecompressStream(streamBytes);
            if (decompressed == null) continue;

            string text = Encoding.Latin1.GetString(decompressed);
            if (text.Contains("beginbfchar") || text.Contains("beginbfrange"))
            {
                ParseCMapContent(text, result);
            }
        }

        return result;
    }

    private static void ParseCMapContent(string cmapText, Dictionary<int, string> map)
    {
        // Parse beginbfchar: <srcHex> <dstHex>
        var bfcharMatch = Regex.Match(cmapText, @"(\d+)\s+beginbfchar([\s\S]*?)endbfchar");
        while (bfcharMatch.Success)
        {
            string body = bfcharMatch.Groups[2].Value;
            var pairs = Regex.Matches(body, @"<([0-9a-fA-F]+)>\s+<([0-9a-fA-F]+)>");
            foreach (Match p in pairs)
            {
                if (int.TryParse(p.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int src))
                {
                    string dstHex = p.Groups[2].Value;
                    string dstStr = DecodeUtf16Hex(dstHex);
                    map.TryAdd(src, dstStr);
                }
            }
            bfcharMatch = bfcharMatch.NextMatch();
        }

        // Parse beginbfrange: <startHex> <endHex> <dstStartHex>
        var bfrangeMatch = Regex.Match(cmapText, @"(\d+)\s+beginbfrange([\s\S]*?)endbfrange");
        while (bfrangeMatch.Success)
        {
            string body = bfrangeMatch.Groups[2].Value;
            var ranges = Regex.Matches(body, @"<([0-9a-fA-F]+)>\s+<([0-9a-fA-F]+)>\s+<([0-9a-fA-F]+)>");
            foreach (Match r in ranges)
            {
                if (int.TryParse(r.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int start) &&
                    int.TryParse(r.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int end) &&
                    int.TryParse(r.Groups[3].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int dstStart))
                {
                    for (int cid = start; cid <= end; cid++)
                    {
                        map.TryAdd(cid, ((char)(dstStart + (cid - start))).ToString());
                    }
                }
            }
            bfrangeMatch = bfrangeMatch.NextMatch();
        }
    }

    private static string DecodeUtf16Hex(string hex)
    {
        var sb = new StringBuilder();
        for (int i = 0; i + 4 <= hex.Length; i += 4)
        {
            if (int.TryParse(hex.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int ch))
            {
                sb.Append((char)ch);
            }
        }
        return sb.ToString();
    }

    private readonly record struct TextFragment(double X, double Y, string Text);

    private static List<byte[]> SplitByPageObjects(byte[] data)
    {
        var chunks = new List<byte[]>();
        string dataLatin1 = Encoding.Latin1.GetString(data);
        var matches = Regex.Matches(dataLatin1, @"/Type\s*/Page(?![a-zA-Z])");

        if (matches.Count == 0) return chunks;

        for (int i = 0; i < matches.Count; i++)
        {
            int start = matches[i].Index;
            int end = (i + 1 < matches.Count) ? matches[i + 1].Index : data.Length;
            byte[] chunk = new byte[end - start];
            Array.Copy(data, start, chunk, 0, chunk.Length);
            chunks.Add(chunk);
        }

        return chunks;
    }

    private static string ExtractPageText(byte[] chunk, Dictionary<int, string> globalCMap)
    {
        var streams = ExtractStreams(chunk);
        var pageCMap = new Dictionary<int, string>(globalCMap);

        // Prioritize local CMap definitions inside this page chunk
        foreach (var s in streams)
        {
            byte[]? decompressed = DecompressStream(s);
            if (decompressed == null) continue;
            string txt = Encoding.Latin1.GetString(decompressed);
            if (txt.Contains("beginbfchar") || txt.Contains("beginbfrange"))
            {
                ParseCMapContent(txt, pageCMap);
            }
        }

        var fragments = new List<TextFragment>();

        foreach (var s in streams)
        {
            byte[]? decompressed = DecompressStream(s);
            byte[] target = decompressed ?? s;
            string streamText = Encoding.Latin1.GetString(target);

            // Find BT ... ET blocks
            int btIdx = 0;
            while ((btIdx = streamText.IndexOf("BT", btIdx, StringComparison.Ordinal)) != -1)
            {
                int etIdx = streamText.IndexOf("ET", btIdx, StringComparison.Ordinal);
                if (etIdx == -1) break;

                string block = streamText.Substring(btIdx, etIdx - btIdx);
                ExtractBlockFragments(block, pageCMap, fragments);
                btIdx = etIdx + 2;
            }
        }

        if (fragments.Count == 0) return string.Empty;

        // Group fragments by Y baseline threshold (5.0), sorted from top to bottom
        var sorted = fragments.OrderByDescending(f => f.Y).ThenBy(f => f.X).ToList();
        var lineGroups = new List<List<TextFragment>>();

        foreach (var frag in sorted)
        {
            if (lineGroups.Count > 0 && Math.Abs(lineGroups[^1][0].Y - frag.Y) <= 5.0)
            {
                lineGroups[^1].Add(frag);
            }
            else
            {
                lineGroups.Add(new List<TextFragment> { frag });
            }
        }

        var resultLines = new List<string>();
        foreach (var lg in lineGroups)
        {
            var orderedLine = lg.OrderBy(f => f.X).ToList();
            var lineSb = new StringBuilder();
            foreach (var f in orderedLine)
            {
                lineSb.Append(f.Text);
            }
            string lineStr = lineSb.ToString().Trim();
            if (!string.IsNullOrEmpty(lineStr))
            {
                resultLines.Add(lineStr);
            }
        }

        return string.Join("\n", resultLines).Trim();
    }

    private static void ExtractBlockFragments(string block, Dictionary<int, string> cmap, List<TextFragment> fragments)
    {
        double curX = 0.0;
        double curY = 0.0;
        double scaleX = 1.0;
        double scaleY = 1.0;

        var tokenMatches = Regex.Matches(block,
            @"(-?\d+\.?\d*)\s+(-?\d+\.?\d*)\s+(-?\d+\.?\d*)\s+(-?\d+\.?\d*)\s+(-?\d+\.?\d*)\s+(-?\d+\.?\d*)\s+Tm|(-?\d+\.?\d*)\s+(-?\d+\.?\d*)\s+T[Dd]|<([0-9a-fA-F]+)>\s*Tj|\[([\s\S]*?)\]\s*TJ|\(([\s\S]*?)\)\s*Tj");

        foreach (Match m in tokenMatches)
        {
            if (m.Groups[6].Success)
            {
                if (double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double a) &&
                    double.TryParse(m.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) &&
                    double.TryParse(m.Groups[5].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double e) &&
                    double.TryParse(m.Groups[6].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double f))
                {
                    scaleX = a;
                    scaleY = d;
                    curX = e;
                    curY = f;
                }
            }
            else if (m.Groups[7].Success && m.Groups[8].Success)
            {
                if (double.TryParse(m.Groups[7].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double tx) &&
                    double.TryParse(m.Groups[8].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double ty))
                {
                    curX += tx * scaleX;
                    curY += ty * scaleY;
                }
            }
            else if (m.Groups[9].Success)
            {
                string hex = m.Groups[9].Value;
                string decoded = DecodeHexWithCMap(hex, cmap);
                if (!string.IsNullOrWhiteSpace(decoded))
                {
                    fragments.Add(new TextFragment(curX, curY, decoded));
                }
            }
            else if (m.Groups[10].Success)
            {
                string arrayBody = m.Groups[10].Value;
                var subMatches = Regex.Matches(arrayBody, @"<([0-9a-fA-F]+)>|\(([\s\S]*?)\)");
                var arraySb = new StringBuilder();
                foreach (Match sub in subMatches)
                {
                    if (sub.Groups[1].Success)
                    {
                        arraySb.Append(DecodeHexWithCMap(sub.Groups[1].Value, cmap));
                    }
                    else if (sub.Groups[2].Success)
                    {
                        arraySb.Append(sub.Groups[2].Value);
                    }
                }
                string text = arraySb.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    fragments.Add(new TextFragment(curX, curY, text));
                }
            }
            else if (m.Groups[11].Success)
            {
                string text = m.Groups[11].Value;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    fragments.Add(new TextFragment(curX, curY, text));
                }
            }
        }
    }

    private static string DecodeHexWithCMap(string hex, Dictionary<int, string> cmap)
    {
        var sb = new StringBuilder();
        for (int i = 0; i + 4 <= hex.Length; i += 4)
        {
            if (int.TryParse(hex.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int cid))
            {
                if (cmap.TryGetValue(cid, out var mapped))
                {
                    sb.Append(mapped);
                }
            }
        }
        return sb.ToString();
    }

    private static List<PdfTextLine> ExtractLinesFromText(string text)
    {
        var lines = new List<PdfTextLine>();
        if (string.IsNullOrWhiteSpace(text)) return lines;

        string[] raw = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < raw.Length; i++)
        {
            string t = raw[i].Trim();
            if (!string.IsNullOrEmpty(t))
            {
                lines.Add(new PdfTextLine
                {
                    LineNumber = i + 1,
                    Text = t
                });
            }
        }
        return lines;
    }

    private static List<byte[]> ExtractStreams(byte[] data)
    {
        var list = new List<byte[]>();
        byte[] streamPattern = Encoding.ASCII.GetBytes("stream");
        byte[] endstreamPattern = Encoding.ASCII.GetBytes("endstream");

        int pos = 0;
        while (pos < data.Length)
        {
            int streamStart = IndexOfBytes(data, streamPattern, pos);
            if (streamStart == -1) break;

            int dataStart = streamStart + streamPattern.Length;
            if (dataStart < data.Length && data[dataStart] == '\r') dataStart++;
            if (dataStart < data.Length && data[dataStart] == '\n') dataStart++;

            int endstreamIdx = IndexOfBytes(data, endstreamPattern, dataStart);
            if (endstreamIdx == -1) break;

            int dataEnd = endstreamIdx;
            while (dataEnd > dataStart && (data[dataEnd - 1] == '\r' || data[dataEnd - 1] == '\n'))
            {
                dataEnd--;
            }

            if (dataEnd > dataStart)
            {
                byte[] streamBytes = new byte[dataEnd - dataStart];
                Array.Copy(data, dataStart, streamBytes, 0, streamBytes.Length);
                list.Add(streamBytes);
            }

            pos = endstreamIdx + endstreamPattern.Length;
        }

        return list;
    }

    private static byte[]? DecompressStream(byte[] compressed)
    {
        try
        {
            using var ms = new MemoryStream(compressed);
            using var zlib = new ZLibStream(ms, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            zlib.CopyTo(outMs);
            return outMs.ToArray();
        }
        catch
        {
            try
            {
                using var ms = new MemoryStream(compressed);
                using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                deflate.CopyTo(outMs);
                return outMs.ToArray();
            }
            catch
            {
                return null;
            }
        }
    }

    private static List<int> FindAllByteOccurrences(byte[] src, byte[] pattern)
    {
        var list = new List<int>();
        int pos = 0;
        while ((pos = IndexOfBytes(src, pattern, pos)) != -1)
        {
            list.Add(pos);
            pos += pattern.Length;
        }
        return list;
    }

    private static int IndexOfBytes(byte[] src, byte[] pattern, int startIndex)
    {
        if (pattern.Length == 0 || startIndex < 0 || startIndex + pattern.Length > src.Length)
            return -1;

        for (int i = startIndex; i <= src.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (src[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }

    private static string FormatByteCount(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double len = bytes;
        while (len >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.#} {suffixes[order]}";
    }
}
