using System.Text.RegularExpressions;

namespace WinCompare.Core.Models;

public enum WordMediaType
{
    Image,
    Shape,
    Video,
    Audio,
    Attachment
}

public sealed class WordMediaItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public WordMediaType MediaType { get; set; } = WordMediaType.Image;
    public string FileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string HashSHA256 { get; set; } = string.Empty;
    public string? LocalCachePath { get; set; }
    public double? WidthPoints { get; set; }
    public double? HeightPoints { get; set; }
    public string? RelationshipId { get; set; }
    public string? ShapeType { get; set; }
    public string? FillColorHex { get; set; }
    public string? StrokeColorHex { get; set; }

    public string FormattedSize
    {
        get
        {
            if (MediaType == WordMediaType.Shape)
            {
                if (WidthPoints.HasValue && HeightPoints.HasValue)
                    return $"{(int)WidthPoints.Value}×{(int)HeightPoints.Value} pt";
                return "Vector Shape";
            }
            if (FileSize <= 0) return "";
            if (FileSize < 1024) return $"{FileSize} B";
            if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:F1} KB";
            return $"{FileSize / (1024.0 * 1024.0):F1} MB";
        }
    }
}

public sealed class WordTable
{
    public int Index { get; set; }
    public List<List<string>> Rows { get; set; } = new();
    public int ColumnCount => Rows.Count > 0 ? Rows.Max(r => r.Count) : 0;
    public int RowCount => Rows.Count;
}

public sealed class WordParagraph
{
    public int Index { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Style { get; set; } = "Normal";
    public List<WordMediaItem> MediaItems { get; set; } = new();
    public WordTable? Table { get; set; }

    private bool _isTableBlock;
    public bool IsTableBlock
    {
        get => Table != null || _isTableBlock;
        set => _isTableBlock = value;
    }
    public bool HasMedia => MediaItems.Count > 0;

    public bool IsHeading =>
        Style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) ||
        Style.StartsWith("标题", StringComparison.OrdinalIgnoreCase) ||
        Style.Equals("Title", StringComparison.OrdinalIgnoreCase) ||
        Style.Equals("Subtitle", StringComparison.OrdinalIgnoreCase) ||
        (Style.Length == 1 && char.IsDigit(Style[0]) && Style[0] >= '1' && Style[0] <= '6') ||
        (Style.Length == 2 && (Style.StartsWith("h", StringComparison.OrdinalIgnoreCase) || Style.StartsWith("H")) && char.IsDigit(Style[1])) ||
        IsTextHeadingPattern(Text);

    public static bool IsTextHeadingPattern(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        string trimmed = text.Trim();
        if (trimmed.Length > 60) return false;
        if (trimmed.StartsWith("# ") || trimmed.StartsWith("## ") || trimmed.StartsWith("### ")) return true;
        if (Regex.IsMatch(trimmed, @"^(第[0-9一二三四五六七八九十百]+[章节部分篇条]|(一|二|三|四|五|六|七|八|九|十)[、.．]|\d+(\.\d+)*[、.．\s]|【[^】]+】)"))
            return true;
        return false;
    }
}

public sealed class WordDocumentMetadata
{
    public int WordCount { get; set; }
    public int CharacterCount { get; set; }
    public int ParagraphCount { get; set; }
    public long FileSize { get; set; }
    public string FileSizeFormatted
    {
        get
        {
            if (FileSize < 1024) return $"{FileSize} B";
            if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:F1} KB";
            return $"{FileSize / (1024.0 * 1024.0):F1} MB";
        }
    }
}

public sealed class WordDocument
{
    public string FilePath { get; set; } = string.Empty;
    public List<WordParagraph> Paragraphs { get; set; } = new();
    public List<WordTable> Tables { get; set; } = new();

    public string FileName => string.IsNullOrEmpty(FilePath) ? "" : System.IO.Path.GetFileName(FilePath);

    public WordDocumentMetadata Metadata
    {
        get
        {
            long size = 0;
            try
            {
                if (!string.IsNullOrEmpty(FilePath) && System.IO.File.Exists(FilePath))
                {
                    size = new System.IO.FileInfo(FilePath).Length;
                }
            }
            catch { }

            int words = 0;
            int chars = 0;
            foreach (var p in Paragraphs)
            {
                chars += p.Text.Length;
                words += CountWords(p.Text);
            }

            return new WordDocumentMetadata
            {
                WordCount = words,
                CharacterCount = chars,
                ParagraphCount = Paragraphs.Count,
                FileSize = size
            };
        }
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        int count = 0;
        bool inWord = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
            }
            else if (c >= 0x4e00 && c <= 0x9fa5)
            {
                count++;
                inWord = false;
            }
            else
            {
                if (!inWord)
                {
                    count++;
                    inWord = true;
                }
            }
        }
        return count;
    }
}
