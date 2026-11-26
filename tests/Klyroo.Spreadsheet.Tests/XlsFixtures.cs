using System.Text;

namespace Klyroo.Spreadsheet.Tests;

// Test-only writers for the legacy .xls format: BIFF8 records (MS-XLS) and the compound file container (MS-CFB). They
// build workbooks of any shape for the reader tests, so they can also build malformed ones.

internal sealed class Bytes
{
    private readonly List<byte> _bytes = [];
    public int Length => _bytes.Count;
    public Bytes U8(int value) { _bytes.Add((byte)value); return this; }
    public Bytes U16(int value) { _bytes.Add((byte)value); _bytes.Add((byte)(value >> 8)); return this; }
    public Bytes U32(uint value) { U16((int)(value & 0xFFFF)); U16((int)(value >> 16)); return this; }
    public Bytes Dbl(double value) { _bytes.AddRange(BitConverter.GetBytes(value)); return this; }
    public Bytes Raw(IEnumerable<byte> value) { _bytes.AddRange(value); return this; }
    public Bytes Zeros(int count) { _bytes.AddRange(new byte[count]); return this; }
    public byte[] ToArray() => [.. _bytes];
}

internal static class Biff
{
    public static byte[] Rec(int id, byte[]? payload = null) =>
        new Bytes().U16(id).U16(payload?.Length ?? 0).Raw(payload ?? []).ToArray();

    public static byte[] Cat(params byte[][] parts) => [.. parts.SelectMany(p => p)];

    public static byte[] Bof(int type, int version = 0x0600) =>
        Rec(0x0809, new Bytes().U16(version).U16(type).U16(0x0DBB).U16(0x07CC).U32(0).U32(6).ToArray());

    public static byte[] Eof() => Rec(0x000A);
    public static byte[] DateMode(bool date1904) => Rec(0x0022, new Bytes().U16(date1904 ? 1 : 0).ToArray());
    public static byte[] FilePass() => Rec(0x002F, new Bytes().U16(0).U16(1).U16(1).Zeros(48).ToArray());
    public static byte[] Xf(int formatId) => Rec(0x00E0, new Bytes().U16(0).U16(formatId).Zeros(16).ToArray());
    public static byte[] Format(int id, string code) => Rec(0x041E, new Bytes().U16(id).Raw(XlString(code)).ToArray());

    // XLUnicodeString: character count, flags (bit 0: UTF-16), characters. Compressed when every character fits one byte.
    public static byte[] XlString(string text, bool forceUnicode = false)
    {
        var wide = forceUnicode || text.Any(c => c > 0xFF);
        var bytes = new Bytes().U16(text.Length).U8(wide ? 1 : 0);
        foreach (var c in text) { if (wide) bytes.U16(c); else bytes.U8(c); }
        return bytes.ToArray();
    }

    public static byte[] BoundSheet(long offset, int state, int type, string name)
    {
        var wide = name.Any(c => c > 0xFF);
        var bytes = new Bytes().U32((uint)offset).U8(state).U8(type).U8(name.Length).U8(wide ? 1 : 0);
        foreach (var c in name) { if (wide) bytes.U16(c); else bytes.U8(c); }
        return Rec(0x0085, bytes.ToArray());
    }

    public static byte[] Number(int row, int col, int xf, double value) =>
        Rec(0x0203, new Bytes().U16(row).U16(col).U16(xf).Dbl(value).ToArray());

    public static byte[] Rk(int row, int col, int xf, uint rk) =>
        Rec(0x027E, new Bytes().U16(row).U16(col).U16(xf).U32(rk).ToArray());

    public static uint RkInt(int value, bool times100 = false) => (uint)(value << 2) | 2u | (times100 ? 1u : 0u);

    public static uint RkDouble(double value, bool times100 = false) =>
        ((uint)(BitConverter.DoubleToUInt64Bits(value) >> 32) & 0xFFFFFFFC) | (times100 ? 1u : 0u);

    public static byte[] MulRk(int row, int firstCol, params (int Xf, uint Rk)[] cells)
    {
        var bytes = new Bytes().U16(row).U16(firstCol);
        foreach (var (xf, rk) in cells) bytes.U16(xf).U32(rk);
        return Rec(0x00BD, bytes.U16(firstCol + cells.Length - 1).ToArray());
    }

    public static byte[] LabelSst(int row, int col, int xf, int index) =>
        Rec(0x00FD, new Bytes().U16(row).U16(col).U16(xf).U32((uint)index).ToArray());

    public static byte[] Label(int row, int col, int xf, string text) =>
        Rec(0x0204, new Bytes().U16(row).U16(col).U16(xf).Raw(XlString(text)).ToArray());

    public static byte[] RString(int row, int col, int xf, string text) =>
        Rec(0x00D6, new Bytes().U16(row).U16(col).U16(xf).Raw(XlString(text)).U16(0).ToArray());

    public static byte[] BoolErr(int row, int col, int xf, int value, bool isError) =>
        Rec(0x0205, new Bytes().U16(row).U16(col).U16(xf).U8(value).U8(isError ? 1 : 0).ToArray());

    public static byte[] Blank(int row, int col, int xf) => Rec(0x0201, new Bytes().U16(row).U16(col).U16(xf).ToArray());

    public static byte[] MulBlank(int row, int firstCol, int count) =>
        Rec(0x00BE, new Bytes().U16(row).U16(firstCol).Raw(Enumerable.Range(0, count).SelectMany(_ => new byte[] { 0, 0 })).U16(firstCol + count - 1).ToArray());

    public static byte[] Formula(int row, int col, int xf, byte[] result) =>
        Rec(0x0006, new Bytes().U16(row).U16(col).U16(xf).Raw(result).U16(0).U32(0).U16(0).ToArray());

    public static byte[] FormulaNumber(double value) => new Bytes().Dbl(value).ToArray();
    public static byte[] FormulaString() => [0, 0, 0, 0, 0, 0, 0xFF, 0xFF];
    public static byte[] FormulaBool(bool value) => [1, 0, (byte)(value ? 1 : 0), 0, 0, 0, 0xFF, 0xFF];
    public static byte[] FormulaError(int code) => [2, 0, (byte)code, 0, 0, 0, 0xFF, 0xFF];
    public static byte[] FormulaEmpty() => [3, 0, 0, 0, 0, 0, 0xFF, 0xFF];

    // A shared formula record, which sits between a FORMULA record and its STRING record.
    public static byte[] SharedFormula() => Rec(0x04BC, new Bytes().Zeros(12).ToArray());

    public static byte[] StringRecord(string text) => Rec(0x0207, XlString(text));

    public static byte[] Unknown(int length) => Rec(0x0FFF, new byte[length]);
}

// A record that continues in CONTINUE records. Break() ends the current one.
internal sealed class ChunkedRecord(int id)
{
    private readonly List<Bytes> _chunks = [new Bytes()];
    public Bytes Current => _chunks[^1];
    public void Break() => _chunks.Add(new Bytes());

    public byte[] Build() =>
        Biff.Cat([.. _chunks.Select((chunk, i) => Biff.Rec(i == 0 ? id : 0x003C, chunk.ToArray()))]);
}

// One shared string. Parts are the character runs between record boundaries: the first part follows the header, each
// next part starts a CONTINUE record with its own option byte.
internal sealed record SstEntry(IReadOnlyList<(string Text, bool Unicode)> Parts, int Runs = 0, byte[]? Phonetic = null, int BreakInTail = -1, bool BreakBefore = false)
{
    public static SstEntry Plain(string text) => new([(text, text.Any(c => c > 0xFF))]);
}

internal static class SstWriter
{
    public static byte[] Build(params SstEntry[] entries)
    {
        var record = new ChunkedRecord(0x00FC);
        record.Current.U32((uint)entries.Length).U32((uint)entries.Length);
        foreach (var entry in entries)
        {
            if (entry.BreakBefore) record.Break();
            var flags = (entry.Parts[0].Unicode ? 1 : 0) | (entry.Phonetic is not null ? 4 : 0) | (entry.Runs > 0 ? 8 : 0);
            record.Current.U16(entry.Parts.Sum(p => p.Text.Length)).U8(flags);
            if (entry.Runs > 0) record.Current.U16(entry.Runs);
            if (entry.Phonetic is not null) record.Current.U32((uint)entry.Phonetic.Length);
            for (var i = 0; i < entry.Parts.Count; i++)
            {
                var (text, unicode) = entry.Parts[i];
                if (i > 0)
                {
                    record.Break();
                    record.Current.U8(unicode ? 1 : 0);
                }
                foreach (var c in text) { if (unicode) record.Current.U16(c); else record.Current.U8(c); }
            }
            var tail = new Bytes().Raw(Enumerable.Range(0, entry.Runs * 4).Select(i => (byte)(i + 1))).Raw(entry.Phonetic ?? []).ToArray();
            if (entry.BreakInTail >= 0)
            {
                record.Current.Raw(tail.Take(entry.BreakInTail));
                record.Break();
                record.Current.Raw(tail.Skip(entry.BreakInTail));
            }
            else
            {
                record.Current.Raw(tail);
            }
        }
        return record.Build();
    }
}

internal sealed record XlsSheet(string Name, byte[] Body, int State = 0, int Type = 0);

internal sealed class XlsBook
{
    public bool Date1904 { get; set; }
    public int Version { get; set; } = 0x0600;
    public bool Encrypted { get; set; }
    public List<(int Id, string Code)> Formats { get; } = [];
    // The number format id of each XF record, in file order. Index 0 and 1 are for General cells and dates.
    public List<int> XfFormats { get; } = [0, 14];
    public byte[] Sst { get; set; } = [];
    public List<XlsSheet> Sheets { get; } = [];

    public byte[] BuildBiff()
    {
        var prefix = new List<byte[]> { Biff.Bof(0x0005, Version) };
        if (Encrypted) prefix.Add(Biff.FilePass());
        prefix.Add(Biff.DateMode(Date1904));
        prefix.AddRange(Formats.Select(f => Biff.Format(f.Id, f.Code)));
        prefix.AddRange(XfFormats.Select(Biff.Xf));
        var tail = Biff.Cat(Sst, Biff.Eof());
        var head = Biff.Cat([.. prefix]);
        var boundSheetsLength = Sheets.Sum(s => 4 + 8 + s.Name.Length * (s.Name.Any(c => c > 0xFF) ? 2 : 1));
        var offset = (long)head.Length + boundSheetsLength + tail.Length;
        var boundSheets = new List<byte[]>();
        var bodies = new List<byte[]>();
        foreach (var sheet in Sheets)
        {
            boundSheets.Add(Biff.BoundSheet(offset, sheet.State, sheet.Type, sheet.Name));
            var body = Biff.Cat(Biff.Bof(sheet.Type == 0 ? 0x0010 : 0x0020, Version), sheet.Body, Biff.Eof());
            bodies.Add(body);
            offset += body.Length;
        }
        return Biff.Cat(head, Biff.Cat([.. boundSheets]), tail, Biff.Cat([.. bodies]));
    }

    public byte[] BuildFile(int sectorSize = 512, string streamName = "Workbook") =>
        Cfb.Build([("\u0005SummaryInformation", new byte[200]), (streamName, BuildBiff())], sectorSize);
}

internal static class Cfb
{
    private const uint EndOfChain = 0xFFFFFFFE, Free = 0xFFFFFFFF, FatSector = 0xFFFFFFFD, DifSector = 0xFFFFFFFC;

    public static byte[] Build(IReadOnlyList<(string Name, byte[] Data)> streams, int sectorSize = 512)
    {
        var perFat = sectorSize / 4;
        var mini = new List<byte>();
        var miniFat = new List<uint>();
        var miniStarts = new Dictionary<int, uint>();
        for (var i = 0; i < streams.Count; i++)
        {
            var data = streams[i].Data;
            if (data.Length == 0 || data.Length >= 4096) continue;
            var count = (data.Length + 63) / 64;
            miniStarts[i] = (uint)miniFat.Count;
            mini.AddRange(data);
            mini.AddRange(new byte[count * 64 - data.Length]);
            for (var s = 0; s < count; s++) miniFat.Add(s == count - 1 ? EndOfChain : (uint)(miniFat.Count + 1));
        }

        int Sectors(long length) => (int)((length + sectorSize - 1) / sectorSize);
        var next = 0;
        var chains = new List<(int Start, int Count)>();
        int Allocate(int count) { var start = next; next += count; chains.Add((start, count)); return start; }

        var miniSectors = Sectors(mini.Count);
        var miniStart = miniSectors > 0 ? Allocate(miniSectors) : -1;
        var miniFatSectors = Sectors(miniFat.Count * 4L);
        var miniFatStart = miniFatSectors > 0 ? Allocate(miniFatSectors) : -1;
        var directorySectors = Sectors((streams.Count + 1) * 128L);
        var directoryStart = Allocate(directorySectors);
        var bigStarts = new Dictionary<int, int>();
        for (var i = 0; i < streams.Count; i++)
        {
            if (miniStarts.ContainsKey(i) || streams[i].Data.Length == 0) continue;
            bigStarts[i] = Allocate(Sectors(streams[i].Data.Length));
        }

        var fatSectors = 1;
        var difatSectors = 0;
        while (fatSectors * perFat < next + fatSectors + difatSectors)
        {
            fatSectors++;
            difatSectors = fatSectors > 109 ? (fatSectors - 109 + perFat - 2) / (perFat - 1) : 0;
        }
        var fatStart = next;
        var difatStart = fatStart + fatSectors;
        var total = difatStart + difatSectors;

        var fat = Enumerable.Repeat(Free, fatSectors * perFat).ToArray();
        foreach (var (start, count) in chains)
        {
            for (var s = 0; s < count; s++) fat[start + s] = s == count - 1 ? EndOfChain : (uint)(start + s + 1);
        }
        for (var s = 0; s < fatSectors; s++) fat[fatStart + s] = FatSector;
        for (var s = 0; s < difatSectors; s++) fat[difatStart + s] = DifSector;

        var file = new byte[(total + 1) * sectorSize];
        void Put32(int offset, uint value) => BitConverter.GetBytes(value).CopyTo(file, offset);
        void Put16(int offset, int value) => BitConverter.GetBytes((ushort)value).CopyTo(file, offset);
        int At(int sector) => (sector + 1) * sectorSize;

        // Header
        new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }.CopyTo(file, 0);
        Put16(24, 0x003E);
        Put16(26, sectorSize == 512 ? 3 : 4);
        Put16(28, 0xFFFE);
        Put16(30, sectorSize == 512 ? 9 : 12);
        Put16(32, 6);
        if (sectorSize != 512) Put32(40, (uint)directorySectors);
        Put32(44, (uint)fatSectors);
        Put32(48, (uint)directoryStart);
        Put32(56, 4096);
        Put32(60, miniFatStart < 0 ? EndOfChain : (uint)miniFatStart);
        Put32(64, (uint)miniFatSectors);
        Put32(68, difatSectors > 0 ? (uint)difatStart : EndOfChain);
        Put32(72, (uint)difatSectors);
        for (var i = 0; i < 109; i++) Put32(76 + 4 * i, i < fatSectors ? (uint)(fatStart + i) : Free);
        for (var d = 0; d < difatSectors; d++)
        {
            var offset = At(difatStart + d);
            for (var i = 0; i < perFat - 1; i++)
            {
                var index = 109 + d * (perFat - 1) + i;
                Put32(offset + 4 * i, index < fatSectors ? (uint)(fatStart + index) : Free);
            }
            Put32(offset + 4 * (perFat - 1), d == difatSectors - 1 ? EndOfChain : (uint)(difatStart + d + 1));
        }

        // FAT, mini stream, mini FAT
        for (var i = 0; i < fat.Length; i++) Put32(At(fatStart) + 4 * i, fat[i]);
        mini.CopyTo(file, miniSectors > 0 ? At(miniStart) : 0);
        for (var i = 0; i < miniFat.Count; i++) Put32(At(miniFatStart) + 4 * i, miniFat[i]);

        // Directory: the root, then the streams as right siblings of each other.
        void Entry(int index, string name, int type, uint left, uint right, uint child, uint start, long size)
        {
            var offset = At(directoryStart) + index * 128;
            for (var c = 0; c < name.Length; c++) Put16(offset + 2 * c, name[c]);
            Put16(offset + 64, (name.Length + 1) * 2);
            file[offset + 66] = (byte)type;
            file[offset + 67] = 1;
            Put32(offset + 68, left);
            Put32(offset + 72, right);
            Put32(offset + 76, child);
            Put32(offset + 116, start);
            Put32(offset + 120, (uint)size);
        }
        Entry(0, "Root Entry", 5, Free, Free, streams.Count > 0 ? 1u : Free, miniStart < 0 ? EndOfChain : (uint)miniStart, mini.Count);
        for (var i = 0; i < streams.Count; i++)
        {
            var start = miniStarts.TryGetValue(i, out var m) ? m : bigStarts.TryGetValue(i, out var b) ? (uint)b : EndOfChain;
            Entry(i + 1, streams[i].Name, 2, Free, i + 1 < streams.Count ? (uint)(i + 2) : Free, Free, start, streams[i].Data.Length);
        }
        for (var i = streams.Count + 1; i < directorySectors * sectorSize / 128; i++)
        {
            Put32(At(directoryStart) + i * 128 + 68, Free);
            Put32(At(directoryStart) + i * 128 + 72, Free);
            Put32(At(directoryStart) + i * 128 + 76, Free);
        }

        // Large streams
        foreach (var (i, start) in bigStarts) streams[i].Data.CopyTo(file, At(start));
        return file;
    }
}
