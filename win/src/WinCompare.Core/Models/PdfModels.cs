namespace WinCompare.Core.Models;

public enum PdfDiffChangeType
{
    Unchanged,
    Modified,
    Inserted,
    Deleted
}

public sealed class PdfTextLine
{
    public int LineNumber { get; set; }
    public string Text { get; set; } = string.Empty;
}

public sealed class PdfPage
{
    public int PageIndex { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Text { get; set; } = string.Empty;
    public List<PdfTextLine> Lines { get; set; } = new();
}

public sealed class PdfMetadata
{
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Subject { get; set; }
    public string? Creator { get; set; }
    public string? Producer { get; set; }
    public DateTime? CreationDate { get; set; }
    public string FileSizeFormatted { get; set; } = string.Empty;
}

public sealed class PdfDocument
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    private int? _pageCount;
    public int PageCount
    {
        get => _pageCount ?? Pages.Count;
        set => _pageCount = value;
    }
    public PdfMetadata Metadata { get; set; } = new();
    public List<PdfPage> Pages { get; set; } = new();
}

public sealed class PdfDiffToken
{
    public string Text { get; set; } = string.Empty;
    public PdfDiffChangeType ChangeType { get; set; } = PdfDiffChangeType.Unchanged;
}

public sealed class PdfPageDiffBlock
{
    public int? LeftPageIndex { get; set; }
    public int? RightPageIndex { get; set; }
    public int? PageIndex => LeftPageIndex ?? RightPageIndex;
    public PdfDiffChangeType ChangeType { get; set; } = PdfDiffChangeType.Unchanged;
    public string? LeftText { get; set; }
    public string? RightText { get; set; }
    public List<PdfDiffToken> Tokens { get; set; } = new();
}

public sealed class PdfDiffResult
{
    public List<PdfPageDiffBlock> Blocks { get; set; } = new();
    public int TotalDiffCount { get; set; }
}

public sealed class PdfBoundingBox
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public sealed class PdfVisualDiffResult
{
    public int PageIndex { get; set; }
    public List<PdfBoundingBox> BoundingBoxes { get; set; } = new();
    public int DiffPixelCount { get; set; }
    public double DiffPercentage { get; set; }
}

public sealed class PdfPageReportItem
{
    public int PageIndex { get; set; }
    public PdfPageDiffBlock? TextBlock { get; set; }
    public PdfVisualDiffResult? VisualDiff { get; set; }
    public string? LeftImageBase64 { get; set; }
    public string? RightImageBase64 { get; set; }
    public uint RenderWidth { get; set; }
    public uint RenderHeight { get; set; }
    public double PageWidth { get; set; } = 595;
    public double PageHeight { get; set; } = 842;
}
