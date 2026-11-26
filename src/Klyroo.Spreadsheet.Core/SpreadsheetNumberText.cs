namespace Klyroo.Spreadsheet;

// The shape checks the importer runs on the text of a number cell before it reads the value: sign, digits, an optional decimal mark and an optional
// exponent, with thousands separators only between groups of three digits. They are scans over the text, with the same answers as the regular
// expressions they replace (a digit is any Unicode decimal digit, and a final line feed is allowed, like the end of a pattern) but with no
// regular expression to build, run or keep, so a cell costs one pass and no allocation.
internal static class SpreadsheetNumberText
{
    /// <summary>A number written with '.' as the decimal mark and no thousands separator: <c>12</c>, <c>-3.5</c>, <c>.5</c>, <c>1e5</c>. At least one digit in the mantissa.</summary>
    internal static bool IsPlain(ReadOnlySpan<char> s)
    {
        var i = SkipSign(s, 0);
        var integer = SkipDigits(s, ref i);
        if (i < s.Length && s[i] == '.')
        {
            i++;
            var fraction = SkipDigits(s, ref i);
            if (integer == 0 && fraction == 0) return false;
        }
        else if (integer == 0)
        {
            return false;
        }
        return SkipExponent(s, ref i) && AtEnd(s, i);
    }

    /// <summary>A number written with the given decimal mark, where the other character may group the integer part in threes: <c>1,234.5</c> for the mark '.', <c>1.234,5</c> for ','. The mantissa may be empty (the caller reads the value and refuses it).</summary>
    internal static bool IsGrouped(ReadOnlySpan<char> s, char thousands, char decimalMark)
    {
        var i = SkipSign(s, 0);
        var start = i;
        var run = SkipDigits(s, ref i);
        if (i < s.Length && s[i] == thousands)
        {
            if (run is < 1 or > 3 || s[start] is < '1' or > '9') return false;
            while (i < s.Length && s[i] == thousands)
            {
                if (i + 3 >= s.Length || !char.IsDigit(s[i + 1]) || !char.IsDigit(s[i + 2]) || !char.IsDigit(s[i + 3])) return false;
                i += 4;
            }
        }
        if (i < s.Length && s[i] == decimalMark)
        {
            i++;
            SkipDigits(s, ref i);
        }
        return SkipExponent(s, ref i) && AtEnd(s, i);
    }

    private static int SkipSign(ReadOnlySpan<char> s, int i) => i < s.Length && s[i] is '+' or '-' ? i + 1 : i;

    private static int SkipDigits(ReadOnlySpan<char> s, ref int i)
    {
        var start = i;
        while (i < s.Length && char.IsDigit(s[i])) i++;
        return i - start;
    }

    // An optional exponent. False when an 'e' starts one that has no digit, which no shape accepts.
    private static bool SkipExponent(ReadOnlySpan<char> s, ref int i)
    {
        if (i >= s.Length || s[i] is not ('e' or 'E')) return true;
        i = SkipSign(s, i + 1);
        return SkipDigits(s, ref i) > 0;
    }

    // The end of the text, or a line feed that ends it, as the end anchor of a regular expression allows.
    private static bool AtEnd(ReadOnlySpan<char> s, int i) => i == s.Length || (i == s.Length - 1 && s[i] == '\n');
}
