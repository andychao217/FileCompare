using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;
using WinCompare.Core.Models;

namespace WinCompare.Core.Services;

/// <summary>
/// High-performance zero-dependency OpenXML (.docx) document parser using System.IO.Compression & Linq to XML.
/// Fully extracts sequential paragraphs, tables, embedded images, and vector shapes with relationship resolution.
/// </summary>
public static class WordDocumentParser
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private static string GetMediaCacheFolder()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string cacheDir = Path.Combine(appData, "WinCompare", "MediaCache");
        Directory.CreateDirectory(cacheDir);
        return cacheDir;
    }

    public static WordDocument Parse(string filePath)
    {
        var doc = new WordDocument { FilePath = filePath };

        if (!File.Exists(filePath))
        {
            return doc;
        }

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
            var entry = zip.GetEntry("word/document.xml");
            if (entry == null)
            {
                return doc;
            }

            // 1. Extract and cache all media entries (word/media/*)
            var mediaCacheMap = new Dictionary<string, (string Hash, string LocalPath, long Size, string FileName, string Ext)>(StringComparer.OrdinalIgnoreCase);
            string cacheFolder = GetMediaCacheFolder();

            foreach (var zipEntry in zip.Entries)
            {
                if (zipEntry.FullName.StartsWith("word/media/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(zipEntry.Name))
                {
                    try
                    {
                        using var entryStream = zipEntry.Open();
                        using var ms = new MemoryStream();
                        entryStream.CopyTo(ms);
                        byte[] bytes = ms.ToArray();

                        byte[] hashBytes = SHA256.HashData(bytes);
                        string hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                        string ext = Path.GetExtension(zipEntry.Name).TrimStart('.').ToLowerInvariant();
                        string fileName = zipEntry.Name;
                        string localPath = Path.Combine(cacheFolder, $"{hash}.{ext}");

                        if (!File.Exists(localPath))
                        {
                            File.WriteAllBytes(localPath, bytes);
                        }

                        var info = (hash, localPath, (long)bytes.Length, fileName, ext);
                        mediaCacheMap[zipEntry.FullName] = info;
                        mediaCacheMap["media/" + zipEntry.Name] = info;
                        mediaCacheMap[zipEntry.Name] = info;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to cache media {zipEntry.FullName}: {ex.Message}");
                    }
                }
            }

            // 2. Extract relationships (word/_rels/document.xml.rels)
            var relsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var relsEntry = zip.GetEntry("word/_rels/document.xml.rels");
            if (relsEntry != null)
            {
                try
                {
                    using var relsStream = relsEntry.Open();
                    var xRels = XDocument.Load(relsStream);
                    foreach (var rel in xRels.Descendants().Where(e => e.Name.LocalName == "Relationship"))
                    {
                        string? id = rel.Attribute("Id")?.Value;
                        string? target = rel.Attribute("Target")?.Value;
                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(target))
                        {
                            string cleanTarget = target.Replace("../", "").TrimStart('/');
                            relsMap[id] = cleanTarget;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to parse document.xml.rels: {ex.Message}");
                }
            }

            // 3. Parse sequential body blocks (paragraphs, tables, drawings)
            using var stream = entry.Open();
            var xDoc = XDocument.Load(stream);

            var body = xDoc.Descendants(W + "body").FirstOrDefault();
            if (body == null)
            {
                return doc;
            }

            // Collect top-level blocks in document order
            var contentBlocks = new List<XElement>();
            void CollectBlocks(XElement parent)
            {
                foreach (var child in parent.Elements())
                {
                    if (child.Name == W + "tbl")
                    {
                        contentBlocks.Add(child);
                    }
                    else if (child.Name == W + "p")
                    {
                        contentBlocks.Add(child);
                    }
                    else if (child.Name.LocalName is "sdt" or "sdtContent")
                    {
                        CollectBlocks(child);
                    }
                }
            }

            CollectBlocks(body);

            int pIndex = 0;
            int tIndex = 0;

            foreach (var elem in contentBlocks)
            {
                if (elem.Name == W + "tbl")
                {
                    // Table block
                    var table = new WordTable { Index = ++tIndex };
                    foreach (var row in elem.Elements(W + "tr"))
                    {
                        var rowCells = new List<string>();
                        foreach (var cell in row.Elements(W + "tc"))
                        {
                            string cellText = string.Concat(cell.Descendants(W + "t").Select(t => t.Value));
                            rowCells.Add(cellText.Trim());
                        }
                        table.Rows.Add(rowCells);
                    }

                    if (table.Rows.Count > 0)
                    {
                        doc.Tables.Add(table);
                        string tableSummary = $"Table ({table.Rows.Count} \u00D7 {table.ColumnCount})";
                        doc.Paragraphs.Add(new WordParagraph
                        {
                            Index = ++pIndex,
                            Text = tableSummary,
                            Style = "Table",
                            Table = table,
                            IsTableBlock = true
                        });
                    }
                }
                else if (elem.Name == W + "p")
                {
                    // Paragraph block
                    string text = string.Concat(elem.Descendants().Where(e => e.Name == W + "t" || e.Name == W + "tab")
                        .Select(e => e.Name == W + "tab" ? "\t" : e.Value)).Trim();

                    string style = elem.Element(W + "pPr")?.Element(W + "pStyle")?.Attribute(W + "val")?.Value ?? "Normal";

                    var mediaItems = new List<WordMediaItem>();

                    // A. DrawingML: <w:drawing>
                    foreach (var draw in elem.Descendants().Where(e => e.Name.LocalName == "drawing"))
                    {
                        double? widthPt = null;
                        double? heightPt = null;
                        var extentElem = draw.Descendants().FirstOrDefault(e => e.Name.LocalName == "extent");
                        if (extentElem != null)
                        {
                            if (double.TryParse(extentElem.Attribute("cx")?.Value, out double cx))
                                widthPt = cx / 12700.0;
                            if (double.TryParse(extentElem.Attribute("cy")?.Value, out double cy))
                                heightPt = cy / 12700.0;
                        }

                        // Blip images (a:blip r:embed="...")
                        foreach (var blip in draw.Descendants().Where(e => e.Name.LocalName == "blip"))
                        {
                            string? embedId = blip.Attributes().FirstOrDefault(a => a.Name.LocalName is "embed" or "link" or "id")?.Value;
                            if (!string.IsNullOrEmpty(embedId) && relsMap.TryGetValue(embedId, out string? target))
                            {
                                if (mediaCacheMap.TryGetValue(target, out var cached) ||
                                    mediaCacheMap.TryGetValue("media/" + Path.GetFileName(target), out cached) ||
                                    mediaCacheMap.TryGetValue(Path.GetFileName(target), out cached))
                                {
                                    mediaItems.Add(new WordMediaItem
                                    {
                                        MediaType = WordMediaType.Image,
                                        FileName = cached.FileName,
                                        FileExtension = cached.Ext,
                                        FileSize = cached.Size,
                                        HashSHA256 = cached.Hash,
                                        LocalCachePath = cached.LocalPath,
                                        WidthPoints = widthPt,
                                        HeightPoints = heightPt,
                                        RelationshipId = embedId
                                    });
                                }
                            }
                        }

                        // Modern vector shapes: <wps:wsp>
                        foreach (var wsp in draw.Descendants().Where(e => e.Name.LocalName == "wsp"))
                        {
                            string shapeType = wsp.Descendants().FirstOrDefault(e => e.Name.LocalName == "prstGeom")?.Attribute("prst")?.Value ?? "rect";
                            string fillColor = "#5B9BD5";
                            var srgbClr = wsp.Descendants().FirstOrDefault(e => e.Name.LocalName == "srgbClr")?.Attribute("val")?.Value;
                            if (!string.IsNullOrEmpty(srgbClr))
                            {
                                fillColor = "#" + srgbClr;
                            }
                            string? strokeColor = null;
                            var lnClr = wsp.Descendants().FirstOrDefault(e => e.Name.LocalName == "ln")?.Descendants().FirstOrDefault(e => e.Name.LocalName == "srgbClr")?.Attribute("val")?.Value;
                            if (!string.IsNullOrEmpty(lnClr))
                            {
                                strokeColor = "#" + lnClr;
                            }

                            mediaItems.Add(new WordMediaItem
                            {
                                MediaType = WordMediaType.Shape,
                                FileName = $"shape_{shapeType}.vector",
                                FileExtension = "shape",
                                WidthPoints = widthPt ?? 80,
                                HeightPoints = heightPt ?? 28,
                                ShapeType = shapeType,
                                FillColorHex = fillColor,
                                StrokeColorHex = strokeColor,
                                HashSHA256 = $"{shapeType}-{fillColor}-{strokeColor}-{widthPt}-{heightPt}"
                            });
                        }
                    }

                    // B. Legacy VML: <w:pict>
                    foreach (var pict in elem.Descendants().Where(e => e.Name.LocalName == "pict"))
                    {
                        foreach (var imgData in pict.Descendants().Where(e => e.Name.LocalName == "imagedata"))
                        {
                            string? relId = imgData.Attributes().FirstOrDefault(a => a.Name.LocalName is "id" or "relid" or "href")?.Value;
                            if (!string.IsNullOrEmpty(relId) && relsMap.TryGetValue(relId, out string? target))
                            {
                                if (mediaCacheMap.TryGetValue(target, out var cached) ||
                                    mediaCacheMap.TryGetValue("media/" + Path.GetFileName(target), out cached) ||
                                    mediaCacheMap.TryGetValue(Path.GetFileName(target), out cached))
                                {
                                    mediaItems.Add(new WordMediaItem
                                    {
                                        MediaType = WordMediaType.Image,
                                        FileName = cached.FileName,
                                        FileExtension = cached.Ext,
                                        FileSize = cached.Size,
                                        HashSHA256 = cached.Hash,
                                        LocalCachePath = cached.LocalPath,
                                        RelationshipId = relId
                                    });
                                }
                            }
                        }
                    }

                    // Keep paragraph if it has text OR has media items!
                    if (!string.IsNullOrWhiteSpace(text) || mediaItems.Count > 0)
                    {
                        doc.Paragraphs.Add(new WordParagraph
                        {
                            Index = ++pIndex,
                            Text = text,
                            Style = style,
                            MediaItems = mediaItems
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WordDocumentParser.Parse error: {ex.Message}");
        }

        return doc;
    }
}
