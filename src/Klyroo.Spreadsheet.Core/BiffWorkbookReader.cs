using System.Buffers.Binary;
using System.Globalization;

namespace Klyroo.Spreadsheet;

// Reads the Workbook stream of a BIFF8 file (MS-XLS). Every offset and length read from the file is checked against the
// stream, sheets are parsed once each in file order (a sheet that starts inside the previous one is refused, so the
// work is bounded by the stream size), and the text kept in cells is capped.
internal sealed class BiffWorkbookReader(byte[] data)
{
    private const ushort Bof = 0x0809, Eof = 0x000A, Continue = 0x003C, BoundSheet = 0x0085, Sst = 0x00FC, FilePass = 0x002F;
    private const ushort DateMode = 0x0022, Format = 0x041E, Xf = 0x00E0;
    private const ushort LabelSst = 0x00FD, Label = 0x0204, RString = 0x00D6, Number = 0x0203, Rk = 0x027E, MulRk = 0x00BD;
    private const ushort BoolErr = 0x0205, Formula = 0x0006, StringRecord = 0x0207;
    private const int MaxRow = 65535, MaxColumn = 255, MaxSheets = 4096;
    private const long MaxTextLength = 128L * 1024 * 1024;

    private readonly List<string> _sharedStrings = [];
    private readonly Dictionary<int, string> _formats = [];
    private readonly List<int> _xfFormats = [];
    private readonly List<(string Name, long Offset)> _sheets = [];
    private bool[] _dateXf = [];
    private bool _date1904;
    private long _budget = MaxTextLength;

    internal SpreadsheetWorkbook Read(string fileName)
    {
        var globalsEnd = ReadGlobals();
        _dateXf = new bool[_xfFormats.Count];
        for (var i = 0; i < _dateXf.Length; i++)
        {
            var id = _xfFormats[i];
            _dateXf[i] = _formats.TryGetValue(id, out var code) ? SpreadsheetDateFormats.IsDateFormatCode(code) : SpreadsheetDateFormats.IsBuiltInDate(id);
        }

        // Parse the sheets in file order, each once, and return them in workbook order.
        var parsed = new SpreadsheetSheet?[_sheets.Count];
        var end = (long)globalsEnd;
        foreach (var index in Enumerable.Range(0, _sheets.Count).OrderBy(i => _sheets[i].Offset))
        {
            var (name, offset) = _sheets[index];
            if (offset < end) throw new InvalidDataException("The .xls file is corrupt: worksheets overlap.");
            var (sheet, next) = ReadSheet(name, (int)offset);
            parsed[index] = sheet;
            end = next;
        }
        var sheets = parsed.OfType<SpreadsheetSheet>().ToList();
        if (sheets.Count == 0) throw new InvalidDataException("The workbook has no worksheet.");
        return new SpreadsheetWorkbook(fileName, sheets) { InvariantNumbers = true };
    }

    // Reads the workbook globals substream and returns the position after its EOF.
    private int ReadGlobals()
    {
        var pos = ReadRecord(0, out var id, out var dp, out var len);
        if (id != Bof) throw new InvalidDataException("The file is not an .xls workbook: the Workbook stream does not start with a BOF record.");
        CheckBof(dp, len, 0x0005);
        var depth = 1;
        while (true)
        {
            var next = ReadRecord(pos, out id, out dp, out len);
            if (id == Bof)
            {
                depth++;
            }
            else if (id == Eof)
            {
                if (--depth == 0) return next;
            }
            else if (depth == 1)
            {
                switch (id)
                {
                    case FilePass:
                        throw new InvalidDataException("The workbook is password-protected (encrypted). Remove the password, or save it as .xlsx or .csv.");
                    case DateMode when len >= 2:
                        _date1904 = U16(dp) == 1;
                        break;
                    case Format when len >= 5:
                    {
                        var cursor = new BiffCursor(data);
                        cursor.Add(dp, len);
                        var formatId = cursor.ReadUInt16();
                        var cch = cursor.ReadUInt16();
                        var flags = cursor.ReadByte();
                        _formats[formatId] = cursor.ReadChars(cch, (flags & 1) != 0);
                        break;
                    }
                    case Xf when len >= 4:
                        _xfFormats.Add(U16(dp + 2));
                        break;
                    case BoundSheet:
                        ReadBoundSheet(dp, len);
                        break;
                    case Sst:
                    {
                        var cursor = Collect(dp, len, ref next);
                        ReadSharedStrings(cursor);
                        break;
                    }
                }
            }
            pos = next;
        }
    }

    private void ReadBoundSheet(int dp, int len)
    {
        if (len < 8) throw new InvalidDataException("The .xls file is corrupt: a BOUNDSHEET record is too short.");
        var offset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(dp));
        var sheetType = data[dp + 5];
        var cch = data[dp + 6];
        var wide = (data[dp + 7] & 1) != 0;
        if (8 + cch * (wide ? 2 : 1) > len) throw new InvalidDataException("The .xls file is corrupt: a sheet name is longer than its record.");
        // Chart sheets, macro sheets and VBA modules hold no cells. Hidden sheets are kept, as the .xlsx reader does.
        if (sheetType != 0) return;
        if (_sheets.Count >= MaxSheets) throw new InvalidDataException("The .xls file has too many sheets.");
        if (offset > data.Length - 4) throw new InvalidDataException("The .xls file is corrupt: a sheet starts outside the Workbook stream.");
        var cursor = new BiffCursor(data);
        cursor.Add(dp + 8, len - 8);
        var name = cursor.ReadChars(cch, wide);
        _sheets.Add((name.Length == 0 ? $"Sheet {_sheets.Count + 1}" : name, offset));
    }

    private void ReadSharedStrings(BiffCursor cursor)
    {
        cursor.ReadUInt32();
        var unique = cursor.ReadUInt32();
        // The declared count is not trusted: strings are read while data remains.
        for (uint i = 0; i < unique && !cursor.AtEnd; i++)
        {
            var cch = cursor.ReadUInt16();
            var flags = cursor.ReadByte();
            var runs = (flags & 8) != 0 ? cursor.ReadUInt16() : 0;
            var extension = (flags & 4) != 0 ? cursor.ReadUInt32() : 0;
            var text = cursor.ReadChars(cch, (flags & 1) != 0);
            cursor.Skip(4L * runs);
            cursor.Skip(extension);
            Spend(text.Length);
            _sharedStrings.Add(text);
        }
    }

    // Reads one worksheet substream and returns the sheet and the position after its EOF.
    private (SpreadsheetSheet Sheet, int End) ReadSheet(string name, int offset)
    {
        var pos = ReadRecord(offset, out var id, out var dp, out var len);
        if (id != Bof) throw new InvalidDataException("The .xls file is corrupt: a worksheet does not start with a BOF record.");
        CheckBof(dp, len, 0x0010);

        var rows = new Dictionary<int, List<string>>();
        var depth = 1;
        (int Row, int Column)? pendingString = null;
        while (true)
        {
            var next = ReadRecord(pos, out id, out dp, out len);
            if (id == Bof)
            {
                depth++;
            }
            else if (id == Eof)
            {
                if (--depth == 0)
                {
                    pos = next;
                    break;
                }
            }
            else if (depth == 1)
            {
                switch (id)
                {
                    case LabelSst when len >= 10:
                    {
                        var index = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(dp + 6));
                        Put(rows, U16(dp), U16(dp + 2), index < _sharedStrings.Count ? _sharedStrings[(int)index] : "");
                        break;
                    }
                    case Label or RString when len >= 9:
                    {
                        var cursor = Collect(dp, len, ref next);
                        cursor.Skip(6);
                        var cch = cursor.ReadUInt16();
                        var flags = cursor.ReadByte();
                        Put(rows, U16(dp), U16(dp + 2), cursor.ReadChars(cch, (flags & 1) != 0));
                        break;
                    }
                    case Number when len >= 14:
                        Put(rows, U16(dp), U16(dp + 2), NumberText(BinaryPrimitives.ReadDoubleLittleEndian(data.AsSpan(dp + 6)), U16(dp + 4)));
                        break;
                    case Rk when len >= 10:
                        Put(rows, U16(dp), U16(dp + 2), NumberText(DecodeRk(BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(dp + 6))), U16(dp + 4)));
                        break;
                    case MulRk when len >= 6:
                    {
                        var count = (len - 6) / 6;
                        var row = U16(dp);
                        var first = U16(dp + 2);
                        for (var i = 0; i < count; i++)
                        {
                            var at = dp + 4 + 6 * i;
                            Put(rows, row, first + i, NumberText(DecodeRk(BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at + 2))), U16(at)));
                        }
                        break;
                    }
                    case BoolErr when len >= 8:
                        Put(rows, U16(dp), U16(dp + 2), BoolErrText(data[dp + 6], data[dp + 7]));
                        break;
                    case Formula when len >= 14:
                    {
                        var row = U16(dp);
                        var column = U16(dp + 2);
                        pendingString = null;
                        if (U16(dp + 12) == 0xFFFF)
                        {
                            // The result is not a number: byte 0 says what it is (0 string, 1 boolean, 2 error, 3 empty).
                            switch (data[dp + 6])
                            {
                                case 0:
                                    pendingString = (row, column);
                                    break;
                                case 1:
                                    Put(rows, row, column, BoolErrText(data[dp + 8], 0));
                                    break;
                                case 2:
                                    Put(rows, row, column, BoolErrText(data[dp + 8], 1));
                                    break;
                            }
                        }
                        else
                        {
                            Put(rows, row, column, NumberText(BinaryPrimitives.ReadDoubleLittleEndian(data.AsSpan(dp + 6)), U16(dp + 4)));
                        }
                        break;
                    }
                    case StringRecord when pendingString is { } cell:
                    {
                        var cursor = Collect(dp, len, ref next);
                        var cch = cursor.ReadUInt16();
                        var flags = cursor.ReadByte();
                        Put(rows, cell.Row, cell.Column, cursor.ReadChars(cch, (flags & 1) != 0));
                        pendingString = null;
                        break;
                    }
                }
            }
            pos = next;
        }

        var lines = new List<IReadOnlyList<string>>();
        var numbers = new List<int>();
        foreach (var row in rows.Keys.Order())
        {
            var cells = rows[row];
            while (cells.Count > 0 && cells[^1].Length == 0) cells.RemoveAt(cells.Count - 1);
            if (cells.Count == 0) continue;
            lines.Add(cells);
            numbers.Add(row + 1);
        }
        return (new SpreadsheetSheet(name, lines, numbers), pos);
    }

    private void Put(Dictionary<int, List<string>> rows, int row, int column, string text)
    {
        if (row > MaxRow || column > MaxColumn) throw new InvalidDataException("The .xls file is corrupt: a cell lies outside the 65536 x 256 grid of the format.");
        Spend(text.Length);
        if (!rows.TryGetValue(row, out var cells)) rows[row] = cells = [];
        while (cells.Count <= column) cells.Add("");
        cells[column] = text.Trim();
    }

    private void Spend(int length)
    {
        _budget -= length;
        if (_budget < 0) throw new InvalidDataException("The workbook holds too much text.");
    }

    // The raw value of a number cell as text, the way the .xlsx reader gives it: a date when the cell's format is one.
    private string NumberText(double value, int xf)
    {
        // Excel cannot save these; a damaged file may hold them. #NUM! is what Excel shows for such a value.
        if (!double.IsFinite(value)) return "#NUM!";
        if (xf < _dateXf.Length && _dateXf[xf] && SpreadsheetDateFormats.FormatSerial(value, _date1904) is { } date) return date;
        if (value == 0) return "0";
        // Whole numbers print in full (an identifier of 16 digits must not become 1.234E+15).
        if (value == Math.Floor(value) && Math.Abs(value) < 9e18) return ((long)value).ToString(CultureInfo.InvariantCulture);
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    // RK: bit 0 means "divide by 100", bit 1 means the other 30 bits are an integer, otherwise they are the high bits of an IEEE double.
    private static double DecodeRk(uint rk)
    {
        var value = (rk & 2) != 0
            ? (int)rk >> 2
            : BitConverter.UInt64BitsToDouble((ulong)(rk & 0xFFFFFFFC) << 32);
        return (rk & 1) != 0 ? value / 100 : value;
    }

    private static string BoolErrText(byte value, byte isError)
    {
        if (isError == 0) return value == 0 ? "FALSE" : "TRUE";
        return value switch
        {
            0x00 => "#NULL!",
            0x07 => "#DIV/0!",
            0x0F => "#VALUE!",
            0x17 => "#REF!",
            0x1D => "#NAME?",
            0x24 => "#NUM!",
            0x2A => "#N/A",
            _ => "#VALUE!",
        };
    }

    private static void CheckBofVersion(int version)
    {
        if (version == 0x0600) return;
        throw version == 0x0500
            ? new InvalidDataException("This .xls file is in the Excel 5.0/95 format (BIFF5), which is not supported. Save it as .xlsx or .csv.")
            : new InvalidDataException($"This .xls file uses an unsupported BIFF version (0x{version:X4}). Save it as .xlsx or .csv.");
    }

    private void CheckBof(int dp, int len, int expectedType)
    {
        if (len < 4) throw new InvalidDataException("The .xls file is corrupt: a BOF record is too short.");
        CheckBofVersion(U16(dp));
        if (U16(dp + 2) != expectedType) throw new InvalidDataException("The .xls file is corrupt: unexpected substream type.");
    }

    // Reads a record header at pos and returns the position of the next record. The record must fit in the stream.
    private int ReadRecord(int pos, out ushort id, out int dataPos, out int length)
    {
        if (pos < 0 || pos > data.Length - 4) throw new InvalidDataException("The .xls file is truncated: the Workbook stream ends inside a substream.");
        id = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(pos));
        length = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(pos + 2));
        dataPos = pos + 4;
        if (length > data.Length - dataPos) throw new InvalidDataException("The .xls file is truncated: a record runs past the end of the Workbook stream.");
        return dataPos + length;
    }

    // The content of a record and of the CONTINUE records that follow it. next is moved past them.
    private BiffCursor Collect(int dp, int len, ref int next)
    {
        var cursor = new BiffCursor(data);
        cursor.Add(dp, len);
        while (next <= data.Length - 4 && BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(next)) == Continue)
        {
            next = ReadRecord(next, out _, out var continuedAt, out var continuedLength);
            cursor.Add(continuedAt, continuedLength);
        }
        return cursor;
    }

    private int U16(int at) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(at));
}
