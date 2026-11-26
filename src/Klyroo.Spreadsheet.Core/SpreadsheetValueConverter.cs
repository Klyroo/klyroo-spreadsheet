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
        string s => s,
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
    internal static bool TryNumber(Type t, string text, string cellText, out object? value, out string? error)
    {
        if (!Decompose(text, out var parts))
        {
            value = null;
            error = $"\"{cellText}\" is not a number";
            return false;
        }
        return TryNumber(t, text, parts, cellText, out value, out error);
    }

    // Reads the text into the property type from its decomposition, so a cell is scanned once whatever the type.
    internal static bool TryNumber(Type t, ReadOnlySpan<char> text, in NumberParts parts, string cellText, out object? value, out string? error)
    {
        value = null;
        error = null;
        var zero = parts.IsZero;
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
            if (parts.DigitCount + parts.Exp > 29)
            {
                error = $"\"{cellText}\" is out of range";
                return false;
            }
            if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var m) && IsExact(m, text, parts))
            {
                value = m;
                return true;
            }
            error = parts.DigitCount + parts.Exp >= 29 ? $"\"{cellText}\" is out of range" : $"\"{cellText}\" has more decimals than this column can hold";
            return false;
        }
        else if (IsIntegral(t))
        {
            if (parts.Exp < 0)
            {
                error = $"\"{cellText}\" is not a whole number";
                return false;
            }
            // 20 digits is the most any of the integral types holds (ulong).
            if (parts.DigitCount + parts.Exp <= 20 && TryMagnitude(text, parts, out var magnitude) && TryInteger(t, zero ? false : parts.Negative, magnitude, out value)) return true;
        }
        error = $"\"{cellText}\" is out of range";
        return false;
    }

    // Whether the decimal read from the text holds exactly the number the text writes. A decimal keeps any integer below 10^28 and a scale of up to 28,
    // so a text inside those bounds is exact and needs no check; a longer one is read back from the decimal's own text and compared digit for digit.
    private static bool IsExact(decimal m, ReadOnlySpan<char> text, in NumberParts parts)
    {
        if (parts.DigitCount <= 28 && parts.Exp >= -28 && parts.DigitCount + Math.Max(parts.Exp, 0) <= 28) return true;
        Span<char> buffer = stackalloc char[64];
        if (!m.TryFormat(buffer, out var written, default, CultureInfo.InvariantCulture)) return false;
        var back = buffer[..written];
        return Decompose(back, out var mParts)
            && mParts.DigitCount == parts.DigitCount && mParts.Exp == parts.Exp && (mParts.Negative == parts.Negative || parts.IsZero)
            && Digits(back, mParts) == Digits(text, parts);
    }

    // The significant digits of a decomposed text as a number, times the power of ten of its exponent: only for an exponent that is not negative.
    private static bool TryMagnitude(ReadOnlySpan<char> text, in NumberParts parts, out ulong magnitude)
    {
        magnitude = 0;
        if (parts.IsZero) return true;
        var s = text.Trim();
        for (var i = parts.FirstSignificant; i <= parts.LastSignificant; i++)
        {
            if (i == parts.Dot) continue;
            var digit = (ulong)(s[i] - '0');
            if (magnitude > (ulong.MaxValue - digit) / 10) return false;
            magnitude = magnitude * 10 + digit;
        }
        for (var e = 0L; e < parts.Exp; e++)
        {
            if (magnitude > ulong.MaxValue / 10) return false;
            magnitude *= 10;
        }
        return true;
    }

    private static bool TryInteger(Type t, bool negative, ulong magnitude, out object? value)
    {
        value = null;
        unchecked
        {
            if (t == typeof(int)) { if (negative ? magnitude <= 1UL << 31 : magnitude <= int.MaxValue) { value = (int)(negative ? 0UL - magnitude : magnitude); return true; } }
            else if (t == typeof(long)) { if (negative ? magnitude <= 1UL << 63 : magnitude <= long.MaxValue) { value = (long)(negative ? 0UL - magnitude : magnitude); return true; } }
            else if (t == typeof(short)) { if (negative ? magnitude <= 1UL << 15 : magnitude <= (ulong)short.MaxValue) { value = (short)(negative ? 0UL - magnitude : magnitude); return true; } }
            else if (t == typeof(sbyte)) { if (negative ? magnitude <= 1UL << 7 : magnitude <= (ulong)sbyte.MaxValue) { value = (sbyte)(negative ? 0UL - magnitude : magnitude); return true; } }
            else if (!negative)
            {
                if (t == typeof(byte)) { if (magnitude <= byte.MaxValue) { value = (byte)magnitude; return true; } }
                else if (t == typeof(uint)) { if (magnitude <= uint.MaxValue) { value = (uint)magnitude; return true; } }
                else if (t == typeof(ulong)) { value = magnitude; return true; }
                else if (t == typeof(ushort)) { if (magnitude <= ushort.MaxValue) { value = (ushort)magnitude; return true; } }
            }
        }
        return false;
    }

    /// <summary>Whether the text is a plain number as the engine writes it (sign, digits, one '.', exponent).</summary>
    internal static bool IsNumberText(ReadOnlySpan<char> text) => Decompose(text, out _);

    // The significant digits of a decomposed text: no leading or trailing zeros and no decimal mark.
    private static string Digits(ReadOnlySpan<char> text, in NumberParts parts)
    {
        if (parts.IsZero) return "";
        var s = text.Trim();
        if (parts.Dot < parts.FirstSignificant || parts.Dot > parts.LastSignificant) return s[parts.FirstSignificant..(parts.LastSignificant + 1)].ToString();
        return string.Concat(s[parts.FirstSignificant..parts.Dot], s[(parts.Dot + 1)..(parts.LastSignificant + 1)]);
    }

    // Splits a number text into its sign, how many significant digits it has (no leading or trailing zeros) and the power of ten that scales them:
    // 12.50 is 3 digits "125" with exponent -1, and 1.2e3 is "12" with exponent 2. The exponent is capped so a hostile "1e999999999999999" cannot overflow.
    // False when the text is not a plain decimal number. A zero has no digits. Nothing is allocated: the positions of the first and the last
    // significant digit are kept so that the digits can be read back when they are needed.
    internal static bool Decompose(ReadOnlySpan<char> text, out NumberParts parts)
    {
        parts = default;
        var s = text.Trim();
        var i = 0;
        var negative = false;
        if (i < s.Length && s[i] is '+' or '-')
        {
            negative = s[i] == '-';
            i++;
        }
        var dot = -1;
        var digitsBeforeDot = 0;
        var total = 0;
        int firstPosition = -1, lastPosition = -1, firstIndex = -1, lastIndex = -1;
        for (; i < s.Length; i++)
        {
            var c = s[i];
            if (c is >= '0' and <= '9')
            {
                if (c != '0')
                {
                    if (firstIndex < 0)
                    {
                        firstIndex = total;
                        firstPosition = i;
                    }
                    lastIndex = total;
                    lastPosition = i;
                }
                total++;
                continue;
            }
            if (c == '.' && dot < 0)
            {
                dot = i;
                digitsBeforeDot = total;
                continue;
            }
            break;
        }
        if (total == 0) return false;
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
        if (firstIndex < 0)
        {
            parts = new NumberParts(negative, 0, 0, 0, 0, dot);
            return true;
        }
        var scale = dot < 0 ? 0 : total - digitsBeforeDot;
        var trailingZeros = total - 1 - lastIndex;
        parts = new NumberParts(negative, lastIndex - firstIndex + 1, e - scale + trailingZeros, firstPosition, lastPosition, dot);
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

/// <summary>A number text split by <see cref="SpreadsheetValueConverter.Decompose"/>: the digits stay in the text, the struct holds where they are.</summary>
/// <param name="Negative">Whether the text starts with a minus sign.</param>
/// <param name="DigitCount">The number of significant digits: no leading or trailing zeros. Zero for the number zero.</param>
/// <param name="Exp">The power of ten that scales the significant digits taken as a whole number.</param>
/// <param name="FirstSignificant">The index of the first significant digit in the trimmed text.</param>
/// <param name="LastSignificant">The index of the last significant digit in the trimmed text.</param>
/// <param name="Dot">The index of the decimal mark in the trimmed text, or -1 when there is none.</param>
internal readonly record struct NumberParts(bool Negative, int DigitCount, long Exp, int FirstSignificant, int LastSignificant, int Dot)
{
    public bool IsZero => DigitCount == 0;
}
