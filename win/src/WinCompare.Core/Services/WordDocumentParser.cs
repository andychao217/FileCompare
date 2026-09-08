using System.IO.Compression;
using System.Xml.Linq;

namespace WinCompare.Core.Services;

public sealed class WordDocument
{
    public string FilePath { get; set; } = string.Empty;
    public List<WordParagraph> Paragraphs { get; set; } = new();
    public List<WordTable> Tables { get; set; } = new();
}

public sealed class WordParagraph
{
    public int Index { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Style { get; set; } = "Normal";
    public bool IsHeading => Style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase);
}

public sealed class WordTable
{
    public int Index { get; set; }
    public List<List<string>> Rows { get; set; } = new();
}

/// <summary>
/// High-performance zero-dependency OpenXML (.docx) document parser using System.IO.Compression & Linq to XML.
/// </summary>
public static class WordDocumentParser
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public static WordDocument Parse(string filePath)
    {
        var doc = new WordDocument { FilePath = filePath };

        if (!File.Exists(filePath))
        {
            return doc;
        }

        using var zip = ZipFile.OpenRead(filePath);
        var entry = zip.GetEntry("word/document.xml");
        if (entry == null)
        {
            return doc;
        }

        using var stream = entry.Open();
        var xDoc = XDocument.Load(stream);

        // 1. Parse paragraphs
        int pIndex = 0;
        foreach (var p in xDoc.Descendants(W + "p"))
        {
            // Skip paragraphs that are inside tables to avoid duplicate text
            if (p.Ancestors(W + "tc").Any())
            {
                continue;
            }

            string text = string.Concat(p.Descendants(W + "t").Select(t => t.Value));
            if (string.IsNullOrWhiteSpace(text)) continue;

            string style = p.Element(W + "pPr")?.Element(W + "pStyle")?.Attribute(W + "val")?.Value ?? "Normal";

            doc.Paragraphs.Add(new WordParagraph
            {
                Index = ++pIndex,
                Text = text,
                Style = style
            });
        }

        // 2. Parse tables
        int tIndex = 0;
        foreach (var tbl in xDoc.Descendants(W + "tbl"))
        {
            var table = new WordTable { Index = ++tIndex };

            foreach (var row in tbl.Elements(W + "tr"))
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
            }
        }

        return doc;
    }
}
