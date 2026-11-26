using System.Globalization;
using System.Text;

namespace Klyroo.Spreadsheet;

// Date detection and formatting shared by the .xlsx and .xls readers, so both give the same text for the same cell.
internal static class SpreadsheetDateFormats
{
    // Built-in number formats that display a date or a time (ECMA-376, 18.8.30). 46 is "[h]:mm:ss", an elapsed time, so it is not one.
    private static readonly HashSet<int> BuiltInDateFormats = [14, 15, 16, 17, 18, 19, 20, 21, 22, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 45, 47, 50, 51, 52, 53, 54, 55, 56, 57, 58];

    internal static bool IsBuiltInDate(int formatId) => BuiltInDateFormats.Contains(formatId);

    // A format code displays a date or time when, outside quoted text, brackets and escaped characters, it uses one of
    // the date or time letters. "General" is not a date, and neither is an elapsed time ("[h]:mm", "[mm]:ss"): it counts hours,
    // minutes or seconds past 24 h or 60, so its cell is a duration, read as its number of days.
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
                if (end > i + 1 && IsElapsed(section.AsSpan(i + 1, end - i - 1))) return false;
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

    // [h], [mm], [ss]...: one letter, repeated, in brackets.
    private static bool IsElapsed(ReadOnlySpan<char> inner)
    {
        var first = char.ToLowerInvariant(inner[0]);
        if (first is not ('h' or 'm' or 's')) return false;
        foreach (var c in inner)
        {
            if (char.ToLowerInvariant(c) != first) return false;
        }
        return true;
    }

    // An Excel serial number as yyyy-MM-dd, with HH:mm or HH:mm:ss when it has a time. Null when out of range.
    internal static string? FormatSerial(double serial, bool date1904)
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
}
