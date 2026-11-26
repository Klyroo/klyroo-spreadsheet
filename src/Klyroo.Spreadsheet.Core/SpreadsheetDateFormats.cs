using System.Globalization;
using System.Text;

namespace Klyroo.Spreadsheet;

// Date detection and formatting shared by the .xlsx and .xls readers, so both give the same text for the same cell.
internal static class SpreadsheetDateFormats
{
    // Built-in number formats that display a date or a time (ECMA-376, 18.8.30).
    private static readonly HashSet<int> BuiltInDateFormats = [14, 15, 16, 17, 18, 19, 20, 21, 22, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 45, 46, 47, 50, 51, 52, 53, 54, 55, 56, 57, 58];

    internal static bool IsBuiltInDate(int formatId) => BuiltInDateFormats.Contains(formatId);

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
