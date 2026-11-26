using System.Buffers.Binary;

namespace Klyroo.Spreadsheet;

// A reader for the Compound File Binary format (MS-CFB), the OLE2 container of legacy Office files. The input is user
// content, so every length and sector index read from it is checked against the bytes actually present, and every
// chain walk is bounded by the number of sectors in the file: a cycle or an oversized chain throws instead of looping
// or allocating. A decoded stream is never larger than the file that holds it.
internal sealed class CompoundFile
{
    internal static ReadOnlySpan<byte> Signature => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    private const uint EndOfChain = 0xFFFFFFFE;
    private const uint FreeSector = 0xFFFFFFFF;
    private const int HeaderSize = 512;
    private const int MiniSectorSize = 64;
    private const int DirectoryEntrySize = 128;
    private const int MiniStreamCutoff = 4096;

    private readonly byte[] _data;
    private readonly int _sectorSize;
    private readonly int _sectorCount;
    private readonly uint[] _fat;
    private readonly uint _firstMiniFatSector;
    private readonly bool _version4;
    private uint[]? _miniFat;
    private byte[]? _miniStream;

    /// <summary>The directory entries, in directory order. Entry 0 is the root.</summary>
    internal List<CompoundEntry> Entries { get; } = [];

    private CompoundFile(byte[] data, int sectorSize, int sectorCount, uint[] fat, uint firstMiniFatSector, bool version4)
    {
        _data = data;
        _sectorSize = sectorSize;
        _sectorCount = sectorCount;
        _fat = fat;
        _firstMiniFatSector = firstMiniFatSector;
        _version4 = version4;
    }

    internal static bool HasSignature(ReadOnlySpan<byte> bytes) => bytes.Length >= Signature.Length && bytes[..Signature.Length].SequenceEqual(Signature);

    internal static CompoundFile Open(byte[] data)
    {
        if (data.Length < HeaderSize || !HasSignature(data))
        {
            throw new InvalidDataException("The file is not an .xls workbook: the OLE2 header is missing.");
        }
        if (BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(28)) != 0xFFFE)
        {
            throw new InvalidDataException("The file is not an .xls workbook: unsupported byte order.");
        }
        var major = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(26));
        var sectorShift = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(30));
        var miniShift = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(32));
        if (!(major == 3 && sectorShift == 9) && !(major == 4 && sectorShift == 12) || miniShift != 6)
        {
            throw new InvalidDataException("The file is not a valid .xls workbook: unsupported sector size.");
        }
        var sectorSize = 1 << sectorShift;
        if (data.Length < sectorSize)
        {
            throw new InvalidDataException("The .xls file is truncated.");
        }
        // The last sector may be cut short by some writers: it is read zero padded.
        var sectorCount = (int)(((long)data.Length - sectorSize + sectorSize - 1) / sectorSize);
        var cutoff = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(56));
        if (cutoff != MiniStreamCutoff)
        {
            throw new InvalidDataException("The file is not a valid .xls workbook: unsupported mini stream cutoff.");
        }
        var firstDirectory = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(48));
        var firstMiniFat = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(60));
        var firstDifat = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(68));
        var difatCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(72));
        if (difatCount > sectorCount)
        {
            throw new InvalidDataException("The .xls file is corrupt: the DIFAT chain is longer than the file.");
        }

        // Locate the FAT sectors: 109 entries in the header, then the DIFAT chain.
        var fatSectors = new List<uint>();
        void AddFatSector(uint id)
        {
            if (id == FreeSector || id == EndOfChain) return;
            if (id >= sectorCount) throw new InvalidDataException("The .xls file is corrupt: a FAT sector lies outside the file.");
            // More FAT sectors than sectors in the file would be a lie, and would size the table past the file.
            if (fatSectors.Count >= sectorCount) throw new InvalidDataException("The .xls file is corrupt: too many FAT sectors.");
            fatSectors.Add(id);
        }
        for (var i = 0; i < 109; i++) AddFatSector(BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(76 + 4 * i)));
        var next = firstDifat;
        var perDifat = sectorSize / 4 - 1;
        for (uint n = 0; n < difatCount && next != EndOfChain && next != FreeSector; n++)
        {
            if (next >= sectorCount) throw new InvalidDataException("The .xls file is corrupt: a DIFAT sector lies outside the file.");
            var offset = SectorOffset(next, sectorSize);
            for (var i = 0; i < perDifat; i++) AddFatSector(ReadU32(data, offset + 4L * i));
            next = ReadU32(data, offset + 4L * perDifat);
        }
        if (fatSectors.Count == 0) throw new InvalidDataException("The .xls file is corrupt: it has no FAT.");

        var perFat = sectorSize / 4;
        var fat = new uint[fatSectors.Count * perFat];
        for (var f = 0; f < fatSectors.Count; f++)
        {
            var offset = SectorOffset(fatSectors[f], sectorSize);
            for (var i = 0; i < perFat; i++) fat[f * perFat + i] = ReadU32(data, offset + 4L * i);
        }

        var file = new CompoundFile(data, sectorSize, sectorCount, fat, firstMiniFat, major == 4);
        file.ReadDirectory(firstDirectory);
        return file;
    }

    /// <summary>
    /// Finds a stream of the root storage by name, ignoring case. Only the root's own tree is walked: a stream of the same name
    /// inside a sub-storage (an embedded workbook in an <c>MBD...</c> storage) is not the file's.
    /// </summary>
    internal CompoundEntry? FindStream(string name)
    {
        var seen = new bool[Entries.Count];
        var pending = new Stack<uint>();
        pending.Push(Entries[0].Child);
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            // A damaged tree may point outside the directory or back at an entry already seen.
            if (id >= Entries.Count || seen[id]) continue;
            seen[id] = true;
            var entry = Entries[(int)id];
            if (entry.Type == 2 && string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)) return entry;
            pending.Push(entry.Left);
            pending.Push(entry.Right);
        }
        return null;
    }

    /// <summary>Reads a whole stream, at most <paramref name="maxLength"/> bytes.</summary>
    internal byte[] ReadStream(CompoundEntry entry, long maxLength)
    {
        if (entry.Size > maxLength) throw new InvalidDataException($"The stream {entry.Name} of the .xls file is too large.");
        if (entry.Size == 0) return [];
        return entry.Size < MiniStreamCutoff ? ReadMiniChain(entry.Start, entry.Size) : ReadChain(entry.Start, entry.Size);
    }

    private void ReadDirectory(uint first)
    {
        var bytes = ReadChain(first, -1);
        var count = bytes.Length / DirectoryEntrySize;
        if (count == 0) throw new InvalidDataException("The .xls file is corrupt: the directory is empty.");
        for (var i = 0; i < count; i++)
        {
            var o = i * DirectoryEntrySize;
            var nameBytes = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(o + 64));
            var name = "";
            if (nameBytes is >= 2 and <= 64)
            {
                var chars = new char[nameBytes / 2 - 1];
                for (var c = 0; c < chars.Length; c++) chars[c] = (char)BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(o + 2 * c));
                name = new string(chars);
            }
            var size = _version4
                ? BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(o + 120))
                : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(o + 120));
            Entries.Add(new CompoundEntry(name, bytes[o + 66], BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(o + 116)), size > long.MaxValue ? long.MaxValue : (long)size,
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(o + 68)), BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(o + 72)), BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(o + 76))));
        }
        if (Entries[0].Type != 5) throw new InvalidDataException("The .xls file is corrupt: the directory has no root entry.");
    }

    // Reads a chain of regular sectors. With a size, the chain must hold at least that many bytes and the result is cut to
    // it; with -1 the whole chain is returned.
    private byte[] ReadChain(uint start, long size)
    {
        var sectors = new List<uint>();
        var id = start;
        while (id != EndOfChain)
        {
            // A chain cannot be longer than the file: more sectors than that means a cycle.
            if (sectors.Count >= _sectorCount) throw new InvalidDataException("The .xls file is corrupt: a sector chain loops or is longer than the file.");
            if (id >= _sectorCount || id >= _fat.Length) throw new InvalidDataException("The .xls file is corrupt: a sector chain leaves the file.");
            sectors.Add(id);
            id = _fat[id];
            if (id == FreeSector) throw new InvalidDataException("The .xls file is corrupt: a sector chain hits an unallocated or reserved sector.");
        }
        var capacity = (long)sectors.Count * _sectorSize;
        if (size > capacity) throw new InvalidDataException("The .xls file is truncated: a stream is longer than its sector chain.");
        var result = new byte[size >= 0 ? size : capacity];
        for (var i = 0; i < sectors.Count; i++)
        {
            var written = (long)i * _sectorSize;
            var take = (int)Math.Min(_sectorSize, result.Length - written);
            if (take <= 0) break;
            var offset = SectorOffset(sectors[i], _sectorSize);
            var available = (int)Math.Min(take, _data.Length - offset);
            if (available > 0) Buffer.BlockCopy(_data, (int)offset, result, (int)written, available);
        }
        return result;
    }

    private byte[] ReadMiniChain(uint start, long size)
    {
        if (_miniStream is null)
        {
            var root = Entries[0];
            _miniStream = root.Size == 0 ? [] : ReadChain(root.Start, root.Size);
            _miniFat = [];
            if (_firstMiniFatSector != EndOfChain && _firstMiniFatSector != FreeSector)
            {
                var raw = ReadChain(_firstMiniFatSector, -1);
                _miniFat = new uint[raw.Length / 4];
                for (var i = 0; i < _miniFat.Length; i++) _miniFat[i] = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4 * i));
            }
        }
        var miniFat = _miniFat!;
        var miniCount = (_miniStream.Length + MiniSectorSize - 1) / MiniSectorSize;
        var result = new byte[size];
        var id = start;
        var index = 0;
        while (id != EndOfChain)
        {
            if (index >= miniCount) throw new InvalidDataException("The .xls file is corrupt: a mini sector chain loops or is longer than the mini stream.");
            if (id >= miniCount || id >= miniFat.Length) throw new InvalidDataException("The .xls file is corrupt: a mini sector chain leaves the mini stream.");
            var written = (long)index * MiniSectorSize;
            if (written < size)
            {
                var take = (int)Math.Min(MiniSectorSize, size - written);
                var source = (long)id * MiniSectorSize;
                var available = (int)Math.Min(take, _miniStream.Length - source);
                if (available > 0) Buffer.BlockCopy(_miniStream, (int)source, result, (int)written, available);
            }
            index++;
            id = miniFat[id];
            if (id == FreeSector) throw new InvalidDataException("The .xls file is corrupt: a mini sector chain hits an unallocated or reserved sector.");
        }
        if ((long)index * MiniSectorSize < size) throw new InvalidDataException("The .xls file is truncated: a stream is longer than its mini sector chain.");
        return result;
    }

    private static long SectorOffset(uint id, int sectorSize) => ((long)id + 1) * sectorSize;

    // Reads past the end of a zero padded last sector as zero.
    private static uint ReadU32(byte[] data, long offset) =>
        offset + 4 <= data.Length ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)offset)) : EndOfChain;
}

/// <summary>A directory entry of a compound file: 1 storage, 2 stream, 5 root.</summary>
// Left, Right and Child are directory indexes: the siblings of an entry form a red-black tree under their storage's Child.
// A plain class, not a record: nothing compares, prints or deconstructs the entries of a directory.
internal sealed class CompoundEntry(string name, byte type, uint start, long size, uint left, uint right, uint child)
{
    public string Name { get; } = name;
    public byte Type { get; } = type;
    public uint Start { get; } = start;
    public long Size { get; } = size;
    public uint Left { get; } = left;
    public uint Right { get; } = right;
    public uint Child { get; } = child;
}
