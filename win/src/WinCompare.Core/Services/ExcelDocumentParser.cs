using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using WinCompare.Core.Models;

namespace WinCompare.Core.Services;

/// <summary>
/// High-performance zero-dependency OpenXML (.xlsx) & CSV spreadsheet parser
/// using System.IO.Compression and System.Xml.Linq.
/// </summary>
public static class ExcelDocumentParser
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static async Task<ExcelWorkbookModel> ParseWorkbookAsync(string filePath)
    {
        return await Task.Run(() => ParseWorkbook(filePath));
    }

    public static ExcelWorkbookModel ParseWorkbook(string filePath)
    {
        var model = new ExcelWorkbookModel
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath)
        };

        if (!File.Exists(filePath)) return model;

        try
        {
            var fi = new FileInfo(filePath);
            model.FileSizeFormatted = FormatByteSize(fi.Length);

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".xlsx" || ext == ".xlsm")
            {
                ParseXlsx(filePath, model);
            }
            else if (ext == ".csv" || ext == ".tsv")
            {
                char delimiter = ext == ".tsv" ? '\t' : ',';
                ParseDelimited(filePath, model, delimiter);
            }
            else
            {
                // Try XLSX first, fallback to CSV
                try
                {
                    ParseXlsx(filePath, model);
                }
                catch
                {
                    ParseDelimited(filePath, model, ',');
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ExcelDocumentParser failed for {filePath}: {ex.Message}");
        }

        return model;
    }

    private static void ParseXlsx(string filePath, ExcelWorkbookModel model)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Read);

        // 1. Read shared strings
        var sharedStrings = new List<string>();
        var sstEntry = zip.GetEntry("xl/sharedStrings.xml");
        if (sstEntry != null)
        {
            using var sstStream = sstEntry.Open();
            var doc = XDocument.Load(sstStream);
            foreach (var si in doc.Descendants(S + "si"))
            {
                var sb = new StringBuilder();
                foreach (var t in si.Descendants(S + "t"))
                {
                    sb.Append(t.Value);
                }
                sharedStrings.Add(sb.ToString());
            }
        }

        // 2. Read workbook relationships (xl/_rels/workbook.xml.rels)
        var relsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var relsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
        if (relsEntry != null)
        {
            using var relsStream = relsEntry.Open();
            var doc = XDocument.Load(relsStream);
            foreach (var rel in doc.Descendants())
            {
                if (rel.Name.LocalName == "Relationship")
                {
                    string? id = rel.Attribute("Id")?.Value;
                    string? target = rel.Attribute("Target")?.Value;
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(target))
                    {
                        // Clean relative target: e.g. "worksheets/sheet1.xml" -> "xl/worksheets/sheet1.xml"
                        string cleanTarget = target.TrimStart('/');
                        if (!cleanTarget.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
                        {
                            cleanTarget = "xl/" + cleanTarget;
                        }
                        relsMap[id] = cleanTarget;
                    }
                }
            }
        }

        // 3. Read workbook.xml for sheet definitions
        var sheetDefs = new List<(string Name, string EntryPath)>();
        var wbEntry = zip.GetEntry("xl/workbook.xml");
        if (wbEntry != null)
        {
            using var wbStream = wbEntry.Open();
            var doc = XDocument.Load(wbStream);
            foreach (var s in doc.Descendants(S + "sheet"))
            {
                string name = s.Attribute("name")?.Value ?? "Sheet";
                string? rId = s.Attribute(R + "id")?.Value ?? s.Attribute("id")?.Value;
                if (!string.IsNullOrEmpty(rId) && relsMap.TryGetValue(rId, out string? entryPath))
                {
                    sheetDefs.Add((name, entryPath));
                }
            }
        }

        // Fallback: If no sheet definitions found, scan worksheets directory
        if (sheetDefs.Count == 0)
        {
            var sheetEntries = zip.Entries
                .Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.FullName)
                .ToList();

            for (int i = 0; i < sheetEntries.Count; i++)
            {
                sheetDefs.Add(($"Sheet{i + 1}", sheetEntries[i].FullName));
            }
        }

        // 3.5 Read styles.xml for date and number format detection
        var dateStyles = new HashSet<int>();
        var stylesEntry = zip.GetEntry("xl/styles.xml");
        if (stylesEntry != null)
        {
            try
            {
                using var stylesStream = stylesEntry.Open();
                var stylesDoc = XDocument.Load(stylesStream);
                var customNumFmts = new Dictionary<int, string>();
                foreach (var numFmt in stylesDoc.Descendants(S + "numFmt"))
                {
                    if (int.TryParse(numFmt.Attribute("numFmtId")?.Value, out int id))
                    {
                        customNumFmts[id] = numFmt.Attribute("formatCode")?.Value ?? string.Empty;
                    }
                }

                bool IsDateFormat(int numFmtId)
                {
                    int[] standardDateIds = { 14, 15, 16, 17, 18, 19, 20, 21, 22, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 45, 46, 47, 50, 51, 52, 53, 54, 55, 56, 57, 58 };
                    if (standardDateIds.Contains(numFmtId)) return true;
                    if (customNumFmts.TryGetValue(numFmtId, out var code))
                    {
                        string lower = code.ToLowerInvariant();
                        if (lower.Contains("yy") || lower.Contains("m/d") || lower.Contains("d/m") || lower.Contains("yyyy") || lower.Contains("hh:"))
                            return true;
                    }
                    return false;
                }

                var cellXfs = stylesDoc.Descendants(S + "cellXfs").FirstOrDefault();
                if (cellXfs != null)
                {
                    int xfIdx = 0;
                    foreach (var xf in cellXfs.Elements(S + "xf"))
                    {
                        if (int.TryParse(xf.Attribute("numFmtId")?.Value, out int numFmtId))
                        {
                            if (IsDateFormat(numFmtId))
                            {
                                dateStyles.Add(xfIdx);
                            }
                        }
                        xfIdx++;
                    }
                }
            }
            catch
            {
                // Ignore styles parse errors and fallback to raw values
            }
        }

        // 4. Parse each worksheet
        for (int sheetIdx = 0; sheetIdx < sheetDefs.Count; sheetIdx++)
        {
            var (sheetName, entryPath) = sheetDefs[sheetIdx];
            var sheetEntry = zip.GetEntry(entryPath);
            if (sheetEntry == null) continue;

            var sheetModel = new ExcelSheetModel
            {
                Id = $"sheet_{sheetIdx}",
                Name = sheetName
            };

            using var sheetStream = sheetEntry.Open();
            var doc = XDocument.Load(sheetStream);

            int maxCols = 0;
            var rows = new List<ExcelRowData>();

            int autoRowIdx = 1;
            foreach (var rElem in doc.Descendants(S + "row"))
            {
                string? rAttr = rElem.Attribute("r")?.Value;
                int rowNumber = int.TryParse(rAttr, out int parsedRow) ? parsedRow : autoRowIdx;
                autoRowIdx = rowNumber + 1;

                var rowData = new ExcelRowData { RowIndex = rowNumber };

                foreach (var cElem in rElem.Elements(S + "c"))
                {
                    string cellRef = cElem.Attribute("r")?.Value ?? string.Empty;
                    int colIdx = GetColumnIndex(cellRef);
                    string colLetter = GetColumnLetter(cellRef, colIdx);

                    string typeAttr = cElem.Attribute("t")?.Value ?? string.Empty;
                    string rawVal = string.Empty;
                    var cellType = ExcelCellType.String;

                    int styleIdx = -1;
                    if (int.TryParse(cElem.Attribute("s")?.Value, out int parsedStyle))
                    {
                        styleIdx = parsedStyle;
                    }

                    if (typeAttr == "s") // Shared String
                    {
                        string? v = cElem.Element(S + "v")?.Value;
                        if (int.TryParse(v, out int sIdx) && sIdx >= 0 && sIdx < sharedStrings.Count)
                        {
                            rawVal = sharedStrings[sIdx];
                        }
                        cellType = ExcelCellType.String;
                    }
                    else if (typeAttr == "b") // Boolean
                    {
                        string? v = cElem.Element(S + "v")?.Value;
                        rawVal = (v == "1") ? "TRUE" : "FALSE";
                        cellType = ExcelCellType.Boolean;
                    }
                    else if (typeAttr == "inlineStr") // Inline String
                    {
                        rawVal = cElem.Element(S + "is")?.Element(S + "t")?.Value ?? string.Empty;
                        cellType = ExcelCellType.String;
                    }
                    else
                    {
                        rawVal = cElem.Element(S + "v")?.Value ?? string.Empty;
                        if (string.IsNullOrEmpty(rawVal))
                        {
                            cellType = ExcelCellType.Blank;
                        }
                        else if (double.TryParse(rawVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double numVal))
                        {
                            if (dateStyles.Contains(styleIdx) && numVal >= 1.0 && numVal <= 2958465.0)
                            {
                                cellType = ExcelCellType.Date;
                                try
                                {
                                    // Excel base epoch is 1899-12-30
                                    var dt = new DateTime(1899, 12, 30).AddDays(numVal);
                                    rawVal = dt.ToString("yyyy/M/d");
                                }
                                catch
                                {
                                    cellType = ExcelCellType.Number;
                                }
                            }
                            else
                            {
                                cellType = ExcelCellType.Number;
                            }
                        }
                        else
                        {
                            cellType = ExcelCellType.String;
                        }
                    }

                    string? formula = cElem.Element(S + "f")?.Value;

                    rowData.Cells.Add(new ExcelCellData
                    {
                        ColumnIndex = colIdx,
                        ColumnLetter = colLetter,
                        RawValue = rawVal,
                        FormattedValue = rawVal,
                        Formula = formula,
                        CellType = cellType.ToString().ToLowerInvariant(),
                        Type = cellType
                    });

                    if (colIdx + 1 > maxCols)
                    {
                        maxCols = colIdx + 1;
                    }
                }

                rowData.Cells.Sort((a, b) => a.ColumnIndex.CompareTo(b.ColumnIndex));
                rows.Add(rowData);
            }

            sheetModel.Rows = rows;
            sheetModel.MaxColumns = maxCols;
            sheetModel.MaxRows = rows.Count;
            model.Sheets.Add(sheetModel);
        }
    }

    private static void ParseDelimited(string filePath, ExcelWorkbookModel model, char delimiter)
    {
        var lines = File.ReadAllLines(filePath);
        var sheetModel = new ExcelSheetModel
        {
            Id = "sheet_0",
            Name = Path.GetFileNameWithoutExtension(filePath)
        };

        int maxCols = 0;
        for (int rIdx = 0; rIdx < lines.Length; rIdx++)
        {
            string line = lines[rIdx];
            var parts = ParseCsvLine(line, delimiter);
            var rowData = new ExcelRowData { RowIndex = rIdx + 1 };

            for (int cIdx = 0; cIdx < parts.Count; cIdx++)
            {
                string val = parts[cIdx];
                rowData.Cells.Add(new ExcelCellData
                {
                    ColumnIndex = cIdx,
                    ColumnLetter = IndexToColumnLetter(cIdx),
                    RawValue = val,
                    FormattedValue = val,
                    CellType = "string",
                    Type = ExcelCellType.String
                });
            }

            if (parts.Count > maxCols)
            {
                maxCols = parts.Count;
            }

            sheetModel.Rows.Add(rowData);
        }

        sheetModel.MaxColumns = maxCols;
        sheetModel.MaxRows = lines.Length;
        model.Sheets.Add(sheetModel);
    }

    private static List<string> ParseCsvLine(string line, char delimiter)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }

    public static int GetColumnIndex(string cellRef)
    {
        if (string.IsNullOrEmpty(cellRef)) return 0;
        int sum = 0;
        foreach (char c in cellRef)
        {
            if (char.IsLetter(c))
            {
                sum = sum * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
            }
            else
            {
                break;
            }
        }
        return Math.Max(0, sum - 1);
    }

    public static string GetColumnLetter(string cellRef, int fallbackIdx)
    {
        if (string.IsNullOrEmpty(cellRef)) return IndexToColumnLetter(fallbackIdx);
        var sb = new StringBuilder();
        foreach (char c in cellRef)
        {
            if (char.IsLetter(c))
            {
                sb.Append(char.ToUpperInvariant(c));
            }
            else
            {
                break;
            }
        }
        return sb.Length > 0 ? sb.ToString() : IndexToColumnLetter(fallbackIdx);
    }

    public static string IndexToColumnLetter(int colIndex)
    {
        int dividend = colIndex + 1;
        string columnName = string.Empty;

        while (dividend > 0)
        {
            int modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar(65 + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static string FormatByteSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
