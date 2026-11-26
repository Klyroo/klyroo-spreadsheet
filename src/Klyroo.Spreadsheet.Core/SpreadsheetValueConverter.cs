using System.Globalization;

namespace Klyroo.Spreadsheet;

// Maps the value types a column can be declared with to the value the parsing engine reads (a double, a bool, an ISO 8601 text or
// the text of a select option) and back. Everything here works on runtime Type values with explicit type tests and never builds a
// generic instantiation at runtime, so it stays NativeAOT friendly.
internal static class SpreadsheetValueConverter
{
    private static readonly string[] IsoFormats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"];

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

    /// <summary>The text of a value as an option value: names for enums, invariant text for numbers.</summary>
    internal static string Format(object? value) => value switch
    {
        null => "",
        string s => s,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    /// <summary>Turns the value read by the engine into the column's value type. Fails with a message for a value the type cannot hold.</summary>
    internal static bool TryConvert(Type underlying, SpreadsheetColumnKind kind, object? engine, string cellText, out object? typed, out string? error)
    {
        typed = null;
        error = null;
        if (engine is null) return true;
        switch (kind)
        {
            case SpreadsheetColumnKind.Number when engine is double d:
                return TryNumber(underlying, d, cellText, out typed, out error);
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
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return false;
            return TryNumber(underlying, d, text, out value, out _);
        }
        return false;
    }

    private static bool TryNumber(Type t, double d, string cellText, out object? value, out string? error)
    {
        value = null;
        error = null;
        if (t == typeof(double))
        {
            value = d;
            return true;
        }
        if (t == typeof(float))
        {
            var f = (float)d;
            if (!float.IsInfinity(f))
            {
                value = f;
                return true;
            }
        }
        else if (t == typeof(decimal))
        {
            if (Math.Abs(d) <= 7.9228162514264337593543950335E28)
            {
                value = (decimal)d;
                return true;
            }
        }
        else if (IsIntegral(t))
        {
            if (d != Math.Truncate(d))
            {
                error = $"\"{cellText}\" is not a whole number";
                return false;
            }
            if (TryInteger(t, d, out value)) return true;
        }
        error = $"\"{cellText}\" is out of range";
        return false;
    }

    private static bool TryInteger(Type t, double d, out object? value)
    {
        value = null;
        if (t == typeof(int)) { if (d is >= int.MinValue and <= int.MaxValue) { value = (int)d; return true; } }
        else if (t == typeof(long)) { if (d is >= -9.2233720368547758E18 and < 9.2233720368547758E18) { value = (long)d; return true; } }
        else if (t == typeof(short)) { if (d is >= short.MinValue and <= short.MaxValue) { value = (short)d; return true; } }
        else if (t == typeof(byte)) { if (d is >= byte.MinValue and <= byte.MaxValue) { value = (byte)d; return true; } }
        else if (t == typeof(sbyte)) { if (d is >= sbyte.MinValue and <= sbyte.MaxValue) { value = (sbyte)d; return true; } }
        else if (t == typeof(uint)) { if (d is >= uint.MinValue and <= uint.MaxValue) { value = (uint)d; return true; } }
        else if (t == typeof(ulong)) { if (d is >= 0 and < 1.8446744073709552E19) { value = (ulong)d; return true; } }
        else if (t == typeof(ushort)) { if (d is >= ushort.MinValue and <= ushort.MaxValue) { value = (ushort)d; return true; } }
        return false;
    }

    private static bool TryDate(Type t, string iso, string cellText, out object? value, out string? error)
    {
        value = null;
        error = null;
        if (!DateTime.TryParseExact(iso, IsoFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            error = $"\"{cellText}\" is not a date this column reads";
            return false;
        }
        if (t == typeof(DateOnly)) value = DateOnly.FromDateTime(dt);
        else if (t == typeof(DateTimeOffset)) value = new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), TimeSpan.Zero);
        else value = dt;
        return true;
    }
}
