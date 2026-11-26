using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Klyroo.Spreadsheet;

/// <summary>
/// Reads an Excel workbook in the Office Open XML format (<c>.xlsx</c>) into a <see cref="SpreadsheetWorkbook"/>, with the
/// base class library only. Every worksheet becomes a sheet. Shared, inline and formula strings are read as text, numbers
/// keep the invariant culture (the importer reads them whatever the column's decimal mark), booleans become <c>TRUE</c>
/// or <c>FALSE</c>, and numbers formatted as dates become <c>yyyy-MM-dd</c> (with <c>HH:mm</c> or <c>HH:mm:ss</c> when
/// they have a time). Formulas are not evaluated: the value Excel saved is used. The legacy binary format (<c>.xls</c>) is
/// not supported.
/// </summary>
public static class SpreadsheetXlsxReader
{
    /// <summary>The largest uncompressed size of one part of the file, 256 MB, beyond which the file is refused.</summary>
    public const long MaxPartLength = 256L * 1024 * 1024;

    private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    // Built-in number formats that display a date or a time (ECMA-376, 18.8.30).
    private static readonly HashSet<int> BuiltInDateFormats = [14, 15, 16, 17, 18, 19, 20, 21, 22, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 45, 46, 47, 50, 51, 52, 53, 54, 55, 56, 57, 58];

    /// <summary>Reads a workbook.</summary>
    /// <param name="stream">The file content. It must be seekable; copy a browser or network stream to a <see cref="MemoryStream"/> first.</param>
    /// <param name="fileName">The file name, kept as <see cref="SpreadsheetWorkbook.Name"/>.</param>
    /// <returns>The workbook, with <see cref="SpreadsheetWorkbook.InvariantNumbers"/> set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The content is not an <c>.xlsx</c> workbook, or a part is larger than <see cref="MaxPartLength"/>.</exception>
    public static SpreadsheetWorkbook Read(Stream stream, string fileName)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException("The file is not an .xlsx workbook.", ex);
        }

        using (zip)
        {
            var workbook = Entry(zip, "xl/workbook.xml") ?? throw new InvalidDataException("The file is not an .xlsx workbook: xl/workbook.xml is missing.");
            var (sheetRefs, date1904) = ReadWorkbook(workbook);
            var targets = ReadRelationships(Entry(zip, "xl/_rels/workbook.xml.rels"));
            var sharedStrings = ReadSharedStrings(Entry(zip, "xl/sharedStrings.xml"));
            var dateStyles = ReadDateStyles(Entry(zip, "xl/styles.xml"));

            var sheets = new List<SpreadsheetSheet>();
            foreach (var (name, relId) in sheetRefs)
            {
                if (!targets.TryGetValue(relId, out var target)) continue;
                var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
                var part = Entry(zip, path);
                if (part is null || !path.Contains("worksheets/", StringComparison.OrdinalIgnoreCase)) continue;
                sheets.Add(ReadSheet(part, name, sharedStrings, dateStyles, date1904));
            }
            if (sheets.Count == 0) throw new InvalidDataException("The workbook has no worksheet.");
            return new SpreadsheetWorkbook(fileName, sheets) { InvariantNumbers = true };
        }
    }

    private static ZipArchiveEntry? Entry(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path) ?? zip.Entries.FirstOrDefault(e => string.Equals(e.FullName, path, StringComparison.OrdinalIgnoreCase));
        if (entry is not null && entry.Length > MaxPartLength)
        {
            throw new InvalidDataException($"The part {path} of the workbook is too large.");
        }
        return entry;
    }

    private static XmlReader Open(ZipArchiveEntry entry) =>
        XmlReader.Create(entry.Open(), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = false,
        });

    private static (List<(string Name, string RelId)> Sheets, bool Date1904) ReadWorkbook(ZipArchiveEntry entry)
    {
        var sheets = new List<(string, string)>();
        var date1904 = false;
        using var xml = Open(entry);
        while (xml.Read())
        {
            if (xml.NodeType != XmlNodeType.Element) continue;
            if (xml.LocalName == "workbookPr")
            {
                var value = xml.GetAttribute("date1904");
                date1904 = value is "1" or "true";
            }
            else if (xml.LocalName == "sheet")
            {
                var name = xml.GetAttribute("name") ?? $"Sheet {sheets.Count + 1}";
                var relId = xml.GetAttribute("id", RelationshipNamespace) ?? "";
                sheets.Add((name, relId));
            }
        }
        return (sheets, date1904);
    }

    private static Dictionary<string, string> ReadRelationships(ZipArchiveEntry? entry)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (entry is null) return map;
        using var xml = Open(entry);
        while (xml.Read())
        {
            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Relationship"
                && xml.GetAttribute("Id") is { } id && xml.GetAttribute("Target") is { } target)
            {
                map[id] = target;
            }
        }
        return map;
    }

    private static List<string> ReadSharedStrings(ZipArchiveEntry? entry)
    {
        var strings = new List<string>();
        if (entry is null) return strings;
        using var xml = Open(entry);
        StringBuilder? current = null;
        var phoneticDepth = 0;
        while (xml.Read())
        {
            if (xml.NodeType == XmlNodeType.Element)
            {
                switch (xml.LocalName)
                {
                    case "si":
                        current = new StringBuilder();
                        if (xml.IsEmptyElement)
                        {
                            strings.Add("");
                            current = null;
                        }
                        break;
                    case "rPh":
                        if (!xml.IsEmptyElement) phoneticDepth++;
                        break;
                    case "t" when current is not null && phoneticDepth == 0 && !xml.IsEmptyElement:
                        current.Append(xml.ReadElementContentAsString());
                        // ReadElementContentAsString leaves the reader on the next node, which may be the </si>.
                        if (xml.NodeType == XmlNodeType.EndElement && xml.LocalName == "si")
                        {
                            strings.Add(current.ToString());
                            current = null;
                        }
                        break;
                }
            }
            else if (xml.NodeType == XmlNodeType.EndElement)
            {
                if (xml.LocalName == "rPh") phoneticDepth--;
                else if (xml.LocalName == "si" && current is not null)
                {
                    strings.Add(current.ToString());
                    current = null;
                }
            }
        }
        return strings;
    }

    // The indexes of cellXfs entries whose number format displays a date or a time.
    private static HashSet<int> ReadDateStyles(ZipArchiveEntry? entry)
    {
        var result = new HashSet<int>();
        if (entry is null) return result;
        var customDateFormats = new HashSet<int>();
        using var xml = Open(entry);
        var inCellXfs = false;
        var xfIndex = 0;
        while (xml.Read())
        {
            if (xml.NodeType == XmlNodeType.Element)
            {
                if (xml.LocalName == "numFmt"
                    && int.TryParse(xml.GetAttribute("numFmtId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                    && IsDateFormatCode(xml.GetAttribute("formatCode")))
                {
                    customDateFormats.Add(id);
                }
                else if (xml.LocalName == "cellXfs")
                {
                    inCellXfs = !xml.IsEmptyElement;
                    xfIndex = 0;
                }
                else if (inCellXfs && xml.LocalName == "xf")
                {
                    if (int.TryParse(xml.GetAttribute("numFmtId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fmt)
                        && (BuiltInDateFormats.Contains(fmt) || customDateFormats.Contains(fmt)))
                    {
                        result.Add(xfIndex);
                    }
                    xfIndex++;
                }
            }
            else if (xml.NodeType == XmlNodeType.EndElement && xml.LocalName == "cellXfs")
            {
                inCellXfs = false;
            }
        }
        return result;
    }

    // A format code displays a date or time when, outside quoted text, brackets and escaped characters, it uses one of
    // the date or time letters. "[h]:mm" (elapsed time) and "General" are not dates.
    internal static bool IsDateFormatCode(string? code)
    {
        if (string.IsNullOrEmpty(code)) return false;
        var section = code.Split(';')[0];
        var plain = new StringBuilder(section.Length);
        for (var i = 0; i < section.Length; i++)
        {
            var c = section[i];
            if (c == '"')
            {
                var end = section.IndexOf('"', i + 1);
                i = end < 0 ? section.Length : end;
            }
            else if (c == '[')
            {
                var end = section.IndexOf(']', i + 1);
                i = end < 0 ? section.Length : end;
            }
            else if (c is '\\' or '_' or '*')
            {
                i++;
            }
            else
            {
                plain.Append(char.ToLowerInvariant(c));
            }
        }
        var text = plain.ToString();
        if (text.Contains("general", StringComparison.Ordinal)) return false;
        return text.IndexOfAny(['d', 'm', 'y', 'h', 's']) >= 0;
    }

    private static SpreadsheetSheet ReadSheet(ZipArchiveEntry entry, string name, List<string> sharedStrings, HashSet<int> dateStyles, bool date1904)
    {
        var rows = new List<IReadOnlyList<string>>();
        var rowNumbers = new List<int>();
        using var xml = Open(entry);

        List<string>? cells = null;
        var rowNumber = 0;
        var nextColumn = 0;
        string? cellType = null;
        var cellStyle = -1;
        var cellColumn = 0;
        string? cellValue = null;
        var inCell = false;

        void EndRow()
        {
            if (cells is null) return;
            while (cells.Count > 0 && cells[^1].Length == 0) cells.RemoveAt(cells.Count - 1);
            if (cells.Count > 0)
            {
                rows.Add(cells);
                rowNumbers.Add(rowNumber);
            }
            cells = null;
        }

        void EndCell()
        {
            if (cells is null) return;
            var text = CellText(cellType, cellStyle, cellValue, sharedStrings, dateStyles, date1904).Trim();
            while (cells.Count <= cellColumn) cells.Add("");
            cells[cellColumn] = text;
            nextColumn = cellColumn + 1;
            inCell = false;
        }

        while (xml.Read())
        {
            if (xml.NodeType == XmlNodeType.Element)
            {
                switch (xml.LocalName)
                {
                    case "row":
                        EndRow();
                        rowNumber = int.TryParse(xml.GetAttribute("r"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ? r : rowNumber + 1;
                        nextColumn = 0;
                        cells = [];
                        if (xml.IsEmptyElement) EndRow();
                        break;
                    case "c" when cells is not null:
                        cellType = xml.GetAttribute("t");
                        cellStyle = int.TryParse(xml.GetAttribute("s"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : -1;
                        cellColumn = ColumnIndex(xml.GetAttribute("r")) ?? nextColumn;
                        cellValue = null;
                        inCell = true;
                        if (xml.IsEmptyElement) EndCell();
                        break;
                    case "v" when inCell && !xml.IsEmptyElement:
                        cellValue = xml.ReadElementContentAsString();
                        if (xml.NodeType == XmlNodeType.EndElement && xml.LocalName == "c") EndCell();
                        break;
                    case "t" when inCell && cellType == "inlineStr" && !xml.IsEmptyElement:
                        cellValue = (cellValue ?? "") + xml.ReadElementContentAsString();
                        if (xml.NodeType == XmlNodeType.EndElement && xml.LocalName == "c") EndCell();
                        break;
                }
            }
            else if (xml.NodeType == XmlNodeType.EndElement)
            {
                if (xml.LocalName == "c" && inCell) EndCell();
                else if (xml.LocalName == "row") EndRow();
            }
        }
        EndRow();
        return new SpreadsheetSheet(name, rows, rowNumbers);
    }

    private static string CellText(string? type, int style, string? raw, List<string> sharedStrings, HashSet<int> dateStyles, bool date1904)
    {
        if (raw is null) return "";
        switch (type)
        {
            case "s":
                return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index >= 0 && index < sharedStrings.Count
                    ? sharedStrings[index]
                    : "";
            case "b":
                return raw.Trim() == "1" ? "TRUE" : "FALSE";
            case "inlineStr":
            case "str":
            case "e":
                return raw;
        }

        if (style >= 0 && dateStyles.Contains(style)
            && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial))
        {
            return FormatDate(serial, date1904) ?? raw;
        }
        return raw;
    }

    private static string? FormatDate(double serial, bool date1904)
    {
        if (date1904) serial += 1462;
        if (serial < 0 || serial > 2958465) return null;
        var date = DateTime.FromOADate(serial);
        // Round to the second: Excel stores times as fractions of a day.
        date = new DateTime(date.Ticks + TimeSpan.TicksPerSecond / 2 - (date.Ticks + TimeSpan.TicksPerSecond / 2) % TimeSpan.TicksPerSecond, DateTimeKind.Unspecified);
        if (date.TimeOfDay == TimeSpan.Zero) return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return date.Second == 0
            ? date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    // "BC12" -> 54 (0-based). Null when the reference has no column letters.
    private static int? ColumnIndex(string? reference)
    {
        if (string.IsNullOrEmpty(reference)) return null;
        var index = 0;
        var letters = 0;
        foreach (var c in reference)
        {
            if (c is >= 'A' and <= 'Z') index = index * 26 + (c - 'A' + 1);
            else if (c is >= 'a' and <= 'z') index = index * 26 + (c - 'a' + 1);
            else break;
            letters++;
        }
        return letters == 0 ? null : index - 1;
    }
}
