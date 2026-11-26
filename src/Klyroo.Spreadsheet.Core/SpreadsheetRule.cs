using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Klyroo.Spreadsheet;

/// <summary>A validation rule attached to a column. A rule runs on the parsed value of a non-empty cell. Create one with the factory methods of this type or with <see cref="SpreadsheetRule{TValue}"/>.</summary>
/// <param name="Code">A stable identifier copied to <see cref="SpreadsheetRowIssue.Code"/>, usable for filtering.</param>
/// <param name="Message">The text of the issue shown to the user when the rule fails.</param>
/// <param name="Level">The severity of the issue. Only <see cref="SpreadsheetIssueLevel.Error"/> blocks the row.</param>
/// <param name="MessageArgs">Set by the built-in rules when <paramref name="Message"/> is their default text: the values it contains (the minimum of <see cref="Min{TValue}"/>, for example). It is copied to <see cref="SpreadsheetRowIssue.Args"/>, which tells the component to show the message of the user's locale instead of the English default. <see langword="null"/> for a rule with your own message, which is shown as it is.</param>
public abstract record SpreadsheetRule(
    string Code,
    string Message,
    SpreadsheetIssueLevel Level = SpreadsheetIssueLevel.Error,
    IReadOnlyList<string>? MessageArgs = null)
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.Compiled);

    /// <summary>The match timeout <see cref="Pattern"/> uses when you pass none: 100 milliseconds per cell.</summary>
    public static readonly TimeSpan DefaultPatternTimeout = TimeSpan.FromMilliseconds(100);

    // Runs the rule on the typed value the column produced. A rule on another value type than the column's never accepts.
    internal abstract bool Check(object? value);

    /// <summary>Creates a rule that accepts text shaped like an email address (something before and after one <c>@</c>, and a dot followed by at least two characters).</summary>
    /// <param name="message">The issue text. When <see langword="null"/>, the component shows the built-in message of the user's locale.</param>
    /// <param name="level">The issue severity.</param>
    /// <returns>A rule with code <c>email</c>, for a <see cref="string"/> column. It fails for a value that is not a string.</returns>
    public static SpreadsheetRule<string?> Email(string? message = null, SpreadsheetIssueLevel level = SpreadsheetIssueLevel.Error) =>
        new("email", message ?? "Not an email address", s => s is not null && EmailRegex.IsMatch(s), level, message is null ? [] : null);

    /// <summary>Creates a rule that accepts text matching a regular expression. A cell that makes the match run past the timeout (catastrophic backtracking on a hostile value) does not hang the import: it gets a <c>pattern</c> issue saying the format could not be checked in time.</summary>
    /// <param name="pattern">The regular expression. It is not anchored unless you add <c>^</c> and <c>$</c>.</param>
    /// <param name="message">The issue text. When <see langword="null"/>, the component shows the built-in message of the user's locale.</param>
    /// <param name="level">The issue severity. A timed-out check gets the same severity.</param>
    /// <param name="matchTimeout">The longest one match may take. Defaults to <see cref="DefaultPatternTimeout"/>. Use <see cref="Regex.InfiniteMatchTimeout"/> to remove the limit.</param>
    /// <returns>A rule with code <c>pattern</c>, for a <see cref="string"/> column.</returns>
    /// <exception cref="ArgumentException"><paramref name="pattern"/> is not a valid regular expression.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="matchTimeout"/> is zero, negative or too large for <see cref="Regex"/>.</exception>
    public static SpreadsheetRule<string?> Pattern(string pattern, string? message = null, SpreadsheetIssueLevel level = SpreadsheetIssueLevel.Error, TimeSpan? matchTimeout = null)
    {
        var rx = new Regex(pattern, RegexOptions.Compiled, matchTimeout ?? DefaultPatternTimeout);
        return new("pattern", message ?? "Unexpected format", s => s is not null && rx.IsMatch(s), level, message is null ? [] : null);
    }

    /// <summary>Creates a rule that accepts numbers greater than or equal to a minimum.</summary>
    /// <typeparam name="TValue">The number type of the column. For a nullable column, pass the underlying type.</typeparam>
    /// <param name="min">The smallest accepted value.</param>
    /// <param name="message">The issue text. Defaults to a message naming the minimum.</param>
    /// <param name="level">The issue severity.</param>
    /// <returns>A rule with code <c>min</c>.</returns>
    public static SpreadsheetRule<TValue> Min<TValue>(TValue min, string? message = null, SpreadsheetIssueLevel level = SpreadsheetIssueLevel.Error)
        where TValue : INumber<TValue> =>
        new("min", message ?? $"Value is below {Text(min)}", v => v >= min, level, message is null ? [Text(min)] : null);

    /// <summary>Creates a rule that accepts numbers less than or equal to a maximum.</summary>
    /// <typeparam name="TValue">The number type of the column. For a nullable column, pass the underlying type.</typeparam>
    /// <param name="max">The largest accepted value.</param>
    /// <param name="message">The issue text. Defaults to a message naming the maximum.</param>
    /// <param name="level">The issue severity.</param>
    /// <returns>A rule with code <c>max</c>.</returns>
    public static SpreadsheetRule<TValue> Max<TValue>(TValue max, string? message = null, SpreadsheetIssueLevel level = SpreadsheetIssueLevel.Error)
        where TValue : INumber<TValue> =>
        new("max", message ?? $"Value is above {Text(max)}", v => v <= max, level, message is null ? [Text(max)] : null);

    /// <summary>Creates a rule that accepts numbers inside an inclusive range.</summary>
    /// <typeparam name="TValue">The number type of the column. For a nullable column, pass the underlying type.</typeparam>
    /// <param name="min">The smallest accepted value.</param>
    /// <param name="max">The largest accepted value.</param>
    /// <param name="message">The issue text. Defaults to a message naming the range.</param>
    /// <param name="level">The issue severity.</param>
    /// <returns>A rule with code <c>between</c>.</returns>
    public static SpreadsheetRule<TValue> Between<TValue>(TValue min, TValue max, string? message = null, SpreadsheetIssueLevel level = SpreadsheetIssueLevel.Error)
        where TValue : INumber<TValue> =>
        new("between", message ?? $"Value is not between {Text(min)} and {Text(max)}",
            v => v >= min && v <= max, level,
            message is null ? [Text(min), Text(max)] : null);

    private static string Text<TValue>(TValue value) where TValue : INumber<TValue> => value.ToString(null, CultureInfo.InvariantCulture);
}

/// <summary>A validation rule on the parsed value of a column.</summary>
/// <typeparam name="TValue">The value type of the column the rule is added to. For a nullable column, the underlying type: a rule only runs on a cell that holds a value.</typeparam>
/// <param name="Code">A stable identifier copied to <see cref="SpreadsheetRowIssue.Code"/>, usable for filtering.</param>
/// <param name="Message">The text of the issue shown to the user when the rule fails.</param>
/// <param name="Validate">Receives the parsed value and returns <see langword="true"/> when it is acceptable.</param>
/// <param name="Level">The severity of the issue. Only <see cref="SpreadsheetIssueLevel.Error"/> blocks the row.</param>
/// <param name="MessageArgs">The values the default message contains, for the built-in rules. <see langword="null"/> for a rule with your own message, which is shown as it is.</param>
public sealed record SpreadsheetRule<TValue>(
    string Code,
    string Message,
    Func<TValue, bool> Validate,
    SpreadsheetIssueLevel Level = SpreadsheetIssueLevel.Error,
    IReadOnlyList<string>? MessageArgs = null) : SpreadsheetRule(Code, Message, Level, MessageArgs)
{
    internal override bool Check(object? value) => value is TValue typed && Validate(typed);
}
