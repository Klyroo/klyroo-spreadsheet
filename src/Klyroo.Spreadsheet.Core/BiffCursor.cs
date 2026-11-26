namespace Klyroo.Spreadsheet;

// Reads the logical content of a BIFF8 record followed by its CONTINUE records. Fixed-size fields and skipped data cross a
// record boundary as if the bytes were contiguous. Characters of a string are different: a string cut by a boundary
// restarts in the next record with its own option byte, whose low bit says whether that part is compressed (one byte per
// character, Latin-1) or UTF-16, so ReadChars handles it. Every read is checked against the bytes present.
internal sealed class BiffCursor(byte[] data)
{
    private readonly List<(int Start, int Length)> _segments = [];
    private int _segment;
    private int _offset;

    internal void Add(int start, int length) => _segments.Add((start, length));

    // True when no byte is left in any segment.
    internal bool AtEnd
    {
        get
        {
            while (_segment < _segments.Count && _offset >= _segments[_segment].Length)
            {
                _segment++;
                _offset = 0;
            }
            return _segment >= _segments.Count;
        }
    }

    internal byte ReadByte()
    {
        if (AtEnd) throw Truncated();
        var (start, _) = _segments[_segment];
        return data[start + _offset++];
    }

    internal int ReadUInt16() => ReadByte() | ReadByte() << 8;

    internal uint ReadUInt32() => (uint)ReadUInt16() | (uint)ReadUInt16() << 16;

    internal void Skip(long count)
    {
        while (count > 0)
        {
            if (AtEnd) throw Truncated();
            var take = Math.Min(count, _segments[_segment].Length - _offset);
            _offset += (int)take;
            count -= take;
        }
    }

    // Reads count characters. unicode is the high-byte flag of the string header, and is replaced by the option byte of a
    // continuation when the characters run past the end of a record.
    internal string ReadChars(int count, bool unicode)
    {
        var chars = new char[count];
        var i = 0;
        var needOption = false;
        while (i < count)
        {
            if (_segment >= _segments.Count) throw Truncated();
            var (start, length) = _segments[_segment];
            if (_offset >= length)
            {
                _segment++;
                _offset = 0;
                needOption = true;
                continue;
            }
            if (needOption)
            {
                unicode = (data[start + _offset++] & 1) != 0;
                needOption = false;
                continue;
            }
            if (unicode)
            {
                if (length - _offset < 2) throw new InvalidDataException("The .xls file is corrupt: a character is split across two records.");
                chars[i++] = (char)(data[start + _offset] | data[start + _offset + 1] << 8);
                _offset += 2;
            }
            else
            {
                chars[i++] = (char)data[start + _offset++];
            }
        }
        return new string(chars);
    }

    private static InvalidDataException Truncated() => new("The .xls file is corrupt: a record is shorter than its content.");
}
