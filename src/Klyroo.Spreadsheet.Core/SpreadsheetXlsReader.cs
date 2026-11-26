namespace Klyroo.Spreadsheet;

/// <summary>
/// Reads a legacy Excel workbook (<c>.xls</c>: BIFF8 records inside an OLE2 compound file, Excel 97 to 2003) into a
/// <see cref="SpreadsheetWorkbook"/>, with the base class library only. It gives the same values as
/// <see cref="SpreadsheetXlsxReader"/>: every worksheet becomes a sheet (hidden ones too), strings are text, numbers keep
/// the invariant culture, booleans become <c>TRUE</c> or <c>FALSE</c>, error cells give their text (<c>#DIV/0!</c>), and
/// numbers formatted as dates become <c>yyyy-MM-dd</c> (with <c>HH:mm</c> or <c>HH:mm:ss</c> when they have a time).
/// Formulas are not evaluated: the value Excel saved is used. Password-protected workbooks and the Excel 5.0/95 format
/// (BIFF5) are refused. The content is treated as untrusted: malformed or hostile files throw
/// <see cref="InvalidDataException"/> instead of looping or allocating without bound.
/// </summary>
public static class SpreadsheetXlsReader
{
    /// <summary>The largest file, and the largest decoded stream, 256 MB, beyond which the file is refused.</summary>
    public const long MaxFileLength = 256L * 1024 * 1024;

    /// <summary>Reads a workbook.</summary>
    /// <param name="stream">The file content, read from its current position to its end.</param>
    /// <param name="fileName">The file name, kept as <see cref="SpreadsheetWorkbook.Name"/>.</param>
    /// <returns>The workbook, with <see cref="SpreadsheetWorkbook.InvariantNumbers"/> set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The content is not an <c>.xls</c> workbook, is encrypted, is in the BIFF5 format, is malformed or is larger than <see cref="MaxFileLength"/>.</exception>
    public static SpreadsheetWorkbook Read(Stream stream, string fileName)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var file = CompoundFile.Open(ReadAll(stream));
        var workbook = file.FindStream("Workbook");
        if (workbook is null)
        {
            if (file.FindStream("Book") is not null)
            {
                throw new InvalidDataException("This .xls file is in the Excel 5.0/95 format (BIFF5), which is not supported. Save it as .xlsx or .csv.");
            }
            if (file.FindStream("EncryptedPackage") is not null)
            {
                throw new InvalidDataException("The workbook is password-protected (encrypted). Remove the password, or save it as .xlsx or .csv.");
            }
            throw new InvalidDataException("The file is not an .xls workbook: it has no Workbook stream.");
        }
        return new BiffWorkbookReader(file.ReadStream(workbook, MaxFileLength)).Read(fileName);
    }

    private static byte[] ReadAll(Stream stream)
    {
        if (stream.CanSeek)
        {
            var remaining = stream.Length - stream.Position;
            if (remaining > MaxFileLength) throw new InvalidDataException("The .xls file is too large.");
            var bytes = new byte[remaining];
            stream.ReadExactly(bytes);
            return bytes;
        }
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = stream.Read(chunk)) > 0)
        {
            if (buffer.Length + read > MaxFileLength) throw new InvalidDataException("The .xls file is too large.");
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }
}
