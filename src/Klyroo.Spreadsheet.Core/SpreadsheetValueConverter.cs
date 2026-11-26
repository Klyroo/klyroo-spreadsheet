using System.Globalization;

namespace Klyroo.Spreadsheet;

// Maps the value types a column can be declared with to the value the parsing engine reads (the invariant text of a number, a bool, an ISO 8601 text or
// the text of a select option) and back. Everything here works on runtime Type values with explicit type tests and never builds a
// generic instantiation at runtime, so it stays NativeAOT friendly.
internal static class SpreadsheetValueConverter
{
    private static readonly string[] IsoFormats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mmzzz", "yyyy-MM-ddTHH:mm:sszzz"];

    internal static Type Underlying(Type valueType) => Nullable.GetUnderlyingType(valueType) ?? valueType;

    internal static bool IsNullable(Type valueType) => !valueType.IsValueType || Nullable.GetUnderlyingType(valueType) is not null;

    internal static bool IsIntegral(Type t) =>
        t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte) || t == typeof(sbyte)
        || t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort);

    internal static bool IsNumber(Type t) => IsIntegral(t) || t == typeof(double) || t == typeof(float) || t == typeof(decimal);

    internal static bool IsDate(Type t) => t == typeof(DateOnly) || t == typeof(DateTime) || t == typeof(DateTimeOffset);

    /// <summary>The kind a column of this (underlying) value type reads, or <see langword="null"/> when the type needs a parser.</summary>
    internal static SpreadsheetColumnKind? InferKind(Type underlying)
    {
        if (underlying == typeof(string)) return SpreadsheetColumnKind.Text;
        if (IsNumber(underlying)) return SpreadsheetColumnKind.Number;
        if (underlying == typeof(bool)) return SpreadsheetColumnKind.Boolean;
        if (IsDate(underlying)) return SpreadsheetColumnKind.Date;
        if (underlying.IsEnum) return SpreadsheetColumnKind.Select;
        return null;
    }

    /// <summary>Whether values of this type can be written as option text and read back (select options).</summary>
    internal static bool SupportsText(Type underlying) =>
        underlying == typeof(string) || underlying.IsEnum || IsNumber(underlying) || underlying == typeof(Guid) || underlying == typeof(bool);

    /// <summary>The text of a value as an option value: the first declared name for enums, invariant text for numbers.</summary>
    internal static string Format(object? value) => value switch
    {
        null => "",
        string s => s,
        Enum e => EnumName(e),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    // The first declared name of an enum value. Enum.ToString picks any of the names of a value that has several.
    private static string EnumName(Enum value)
    {
        var type = value.GetType();
        foreach (var name in Enum.GetNames(type))
        {
            if (Enum.Parse(type, name).Equals(value)) return name;
        }
        return value.ToString();
    }

    /// <summary>
    /// The text the review table shows for a typed value of a column, the same for a cell that was read, a typed default and a value from the column's own parser: invariant,
    /// whatever the culture of the server. A number is its invariant text, a date is <c>yyyy-MM-dd</c> or with the time (and the offset when it is not zero), a select value is the
    /// label of its option, and a flag is <c>True</c> or <c>False</c>.
    /// </summary>
    internal static string DisplayText(SpreadsheetColumnSchema column, object? value)
    {
        if (value is null) return "";
        if (column.Kind == SpreadsheetColumnKind.Select)
        {
            var text = Format(value);
            return column.Options?.FirstOrDefault(o => o.Value == text)?.Label ?? text;
        }
        return value switch
        {
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime dt => IsoDateTime(dt),
            DateTimeOffset o => o.Offset == TimeSpan.Zero
                ? IsoDateTime(o.DateTime)
                : IsoDateTime(o.DateTime, withTime: true) + o.ToString("zzz", CultureInfo.InvariantCulture),
            bool b => b ? "True" : "False",
            _ => Format(value),
        };
    }

    /// <summary>
    /// The text of a row key: the one <c>ExistingRecords</c> is looked up with and <c>LookupExistingRecordsAsync</c> receives. It never depends on the culture.
    /// A <see cref="DateOnly"/> is <c>yyyy-MM-dd</c>, a <see cref="DateTime"/> is <c>yyyy-MM-dd</c> at midnight and otherwise <c>yyyy-MM-ddTHH:mm</c> or <c>yyyy-MM-ddTHH:mm:ss</c>
    /// (the text the importer gave date keys before columns were typed), a <see cref="DateTimeOffset"/> is that text for its UTC instant, and a number has no trailing zeros
    /// (<c>12.50</c> is <c>12.5</c>). Every other value is its invariant text.
    /// </summary>
    internal static string KeyText(object? value) => value switch
    {
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime dt => IsoDateTime(dt),
        DateTimeOffset o => IsoDateTime(o.UtcDateTime),
        decimal m => (m / 1.000000000000000000000000000000000m).ToString(CultureInfo.InvariantCulture),
        _ => Format(value),
    };

    // yyyy-MM-dd at midnight, otherwise with the time to the minute, or to the second when it has seconds.
    internal static string IsoDateTime(DateTime dt, bool withTime = false) =>
        dt.TimeOfDay == TimeSpan.Zero && !withTime
            ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : dt.Second == 0 && dt.Ticks % TimeSpan.TicksPerSecond == 0
                ? dt.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)
                : dt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>Turns the value read by the engine into the column's value type. Fails with a message for a value the type cannot hold.</summary>
    internal static bool TryConvert(Type underlying, SpreadsheetColumnKind kind, object? engine, string cellText, out object? typed, out string? error)
    {
        typed = null;
        error = null;
        if (engine is null) return true;
        switch (kind)
        {
            case SpreadsheetColumnKind.Number when engine is string number:
                return TryNumber(underlying, number, cellText, out typed, out error);
            case SpreadsheetColumnKind.Date when engine is string iso:
                return TryDate(underlying, iso, cellText, out typed, out error);
            case SpreadsheetColumnKind.Select when engine is string text:
                if (TryFromText(underlying, text, out typed)) return true;
                error = $"\"{cellText}\" is not a known value";
                return false;
            default:
                typed = engine;
                return true;
        }
    }

    /// <summary>The value of an answered select cell (a mapped or created option) as the column's value type, or <see langword="null"/> when the text does not fit it. A created value that is not an option yet stays text.</summary>
    internal static object? Loose(Type underlying, object? engine) =>
        engine is string text && TryFromText(underlying, text, out var typed) ? typed : null;

    internal static bool TryFromText(Type underlying, string text, out object? value)
    {
        value = null;
        if (underlying == typeof(string))
        {
            value = text;
            return true;
        }
        if (underlying.IsEnum)
        {
            if (!Enum.TryParse(underlying, text, true, out var parsed) || !Enum.IsDefined(underlying, parsed)) return false;
            value = parsed;
            return true;
        }
        if (underlying == typeof(bool))
        {
            if (!bool.TryParse(text, out var b)) return false;
            value = b;
            return true;
        }
        if (underlying == typeof(Guid))
        {
            if (!Guid.TryParse(text, out var g)) return false;
            value = g;
            return true;
        }
        if (IsNumber(underlying))
        {
            return TryNumber(underlying, text, text, out value, out _);
        }
        return false;
    }

    // The text of a number as the engine reads it: an optional sign, digits with at most one '.', and an optional exponent, always with
    // '.' as the decimal mark. The value is read from this text into the property type, so no digit goes through a double on the way.
    private static bool TryNumber(Type t, string text, string cellText, out object? value, out string? error)
    {
        value = null;
        error = null;
        if (!Decompose(text, out var negative, out var digits, out var exp))
        {
            error = $"\"{cellText}\" is not a number";
            return false;
        }
        var zero = digits.Length == 0;
        if (zero) text = "0";
        if (t == typeof(double))
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d) && (d != 0 || zero))
            {
                value = d;
                return true;
            }
        }
        else if (t == typeof(float))
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && float.IsFinite(f) && (f != 0 || zero))
            {
                value = f;
                return true;
            }
        }
        else if (t == typeof(decimal))
        {
            // 29 digits is the most a decimal holds. A value past them is out of range, and one with more decimals than it keeps is refused instead of rounded.
            if (digits.Length + exp > 29)
            {
                error = $"\"{cellText}\" is out of range";
                return false;
            }
            if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var m)
                && Decompose(m.ToString(CultureInfo.InvariantCulture), out var mNegative, out var mDigits, out var mExp)
                && mDigits == digits && mExp == exp && (mNegative == negative || zero))
            {
                value = m;
                return true;
            }
            error = digits.Length + exp >= 29 ? $"\"{cellText}\" is out of range" : $"\"{cellText}\" has more decimals than this column can hold";
            return false;
        }
        else if (IsIntegral(t))
        {
            if (exp < 0)
            {
                error = $"\"{cellText}\" is not a whole number";
                return false;
            }
            // 20 digits is the most any of the integral types holds (ulong).
            if (digits.Length + exp <= 20)
            {
                var whole = zero ? "0" : (negative ? "-" : "") + digits + new string('0', (int)exp);
                if (TryInteger(t, whole, out value)) return true;
            }
        }
        error = $"\"{cellText}\" is out of range";
        return false;
    }

    private static bool TryInteger(Type t, string text, out object? value)
    {
        value = null;
        const NumberStyles style = NumberStyles.AllowLeadingSign;
        var culture = CultureInfo.InvariantCulture;
        if (t == typeof(int)) { if (int.TryParse(text, style, culture, out var v)) { value = v; return true; } }
        else if (t == typeof(long)) { if (long.TryParse(text, style, culture, out var v)) { value = v; return true; } }
        else if (t == typeof(short)) { if (short.TryParse(text, style, culture, out var v)) { value = v; return true; } }
        else if (t == typeof(byte)) { if (byte.TryParse(text, style, culture, out var v)) { value = v; return true; } }
        else if (t == typeof(sbyte)) { if (sbyte.TryParse(text, style, culture, out var v)) { value = v; return true; } }
        else if (t == typeof(uint)) { if (uint.TryParse(text, style, culture, out var v)) { value = v; return true; } }
        else if (t == typeof(ulong)) { if (ulong.TryParse(text, style, culture, out var v)) { value = v; return true; } }
        else if (t == typeof(ushort)) { if (ushort.TryParse(text, style, culture, out var v)) { value = v; return true; } }
        return false;
    }

    /// <summary>Whether the text is a plain number as the engine writes it (sign, digits, one '.', exponent).</summary>
    internal static bool IsNumberText(string text) => Decompose(text, out _, out _, out _);

    // Splits a number text into its sign, its significant digits (no leading or trailing zeros) and the power of ten that scales them:
    // 12.50 is digits "125" with exponent -1, and 1.2e3 is "12" with exponent 2. The exponent is capped so a hostile "1e999999999999999" cannot overflow.
    // False when the text is not a plain decimal number. A zero has no digits.
    private static bool Decompose(string text, out bool negative, out string digits, out long exp)
    {
        negative = false;
        digits = "";
        exp = 0;
        var s = text.AsSpan().Trim();
        var i = 0;
        if (i < s.Length && s[i] is '+' or '-')
        {
            negative = s[i] == '-';
            i++;
        }
        var start = i;
        var dot = -1;
        for (; i < s.Length; i++)
        {
            var c = s[i];
            if (c is >= '0' and <= '9') continue;
            if (c == '.' && dot < 0)
            {
                dot = i;
                continue;
            }
            break;
        }
        var end = i;
        var mantissaLength = end - start - (dot >= 0 ? 1 : 0);
        if (mantissaLength == 0) return false;
        long e = 0;
        if (i < s.Length)
        {
            if (s[i] is not ('e' or 'E')) return false;
            i++;
            var exponentNegative = false;
            if (i < s.Length && s[i] is '+' or '-')
            {
                exponentNegative = s[i] == '-';
                i++;
            }
            if (i >= s.Length) return false;
            for (; i < s.Length; i++)
            {
                if (s[i] is < '0' or > '9') return false;
                e = Math.Min(e * 10 + (s[i] - '0'), 1_000_000_000_000L);
            }
            if (exponentNegative) e = -e;
        }
        var scale = dot < 0 ? 0 : end - dot - 1;
        var mantissa = dot < 0 ? s[start..end].ToString() : string.Concat(s[start..dot], s[(dot + 1)..end]);
        var significant = mantissa.TrimStart('0');
        var trimmed = significant.TrimEnd('0');
        if (trimmed.Length == 0) return true;
        digits = trimmed;
        exp = e - scale + (significant.Length - trimmed.Length);
        return true;
    }

    private static bool TryDate(Type t, string iso, string cellText, out object? value, out string? error)
    {
        value = null;
        error = null;
        // A text without an offset is UTC; one with an offset is the instant it says. A DateOnly or DateTime keeps the written date and time.
        if (!DateTimeOffset.TryParseExact(iso, IsoFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant))
        {
            error = $"\"{cellText}\" is not a date this column reads";
            return false;
        }
        if (t == typeof(DateOnly)) value = DateOnly.FromDateTime(instant.DateTime);
        else if (t == typeof(DateTimeOffset)) value = instant;
        else value = instant.DateTime;
        return true;
    }
}
