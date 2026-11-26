using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Klyroo.Spreadsheet;

/// <summary>The data type a <see cref="SpreadsheetColumnSchema"/> parses its cells into.</summary>
public enum SpreadsheetColumnKind
{
    /// <summary>Free text. The trimmed cell text is the value.</summary>
    Text,
    /// <summary>A number parsed to <see cref="double"/>. Spaces, non-breaking spaces and apostrophes are removed first, and the decimal separator comes from <see cref="SpreadsheetColumnSchema.DecimalSeparator"/>.</summary>
    Number,
    /// <summary>A date parsed with <see cref="SpreadsheetColumnSchema.DateFormats"/> (or the default formats) and stored as an ISO 8601 string, <c>yyyy-MM-dd</c> or <c>yyyy-MM-ddTHH:mm</c> when the time is not midnight.</summary>
    Date,
    /// <summary>A yes/no value parsed to <see cref="bool"/>. Accepts yes, y, oui, o, true, vrai, x and 1 for true, and no, n, non, false, faux and 0 for false, ignoring case and accents.</summary>
    Boolean,
    /// <summary>A value chosen from <see cref="SpreadsheetColumnSchema.Options"/>. Cells that match no option become value questions governed by the column's unknown-value policy.</summary>
    Select,
}

/// <summary>What the importer does with a cell of a <see cref="SpreadsheetColumnKind.Select"/> column that matches none of the column's options.</summary>
public enum SpreadsheetUnknownValuePolicy
{
    /// <summary>Raise a <see cref="SpreadsheetValueQuestion"/> and block the import until it is answered. This is the default.</summary>
    Ask,
    /// <summary>Do not ask: every row with the unknown value gets an error issue (code <c>value.not-found</c>) and is not importable.</summary>
    Error,
    /// <summary>Answer automatically by leaving out every row that contains the unknown value (discard reason <see cref="SpreadsheetDiscardReason.Value"/>).</summary>
    SkipRows,
    /// <summary>Answer automatically by importing the row with an empty value for the cell.</summary>
    LeaveEmpty,
    /// <summary>Answer automatically by keeping the cell text as a new option. <see cref="SpreadsheetColumnSchema.OnCreateOption"/> is called once per new value on submit.</summary>
    Create,
}

/// <summary>How rows that share the same <see cref="SpreadsheetSchema.RowKeyField"/> value inside one file are handled.</summary>
public enum SpreadsheetDuplicatesPolicy
{
    /// <summary>Every row of the duplicated key gets a blocking issue (code <c>row.duplicate</c>), so none of them is importable until you edit or discard some.</summary>
    Error,
    /// <summary>Keep the first row of each key and leave out the others (discard reason <see cref="SpreadsheetDiscardReason.Duplicate"/>).</summary>
    KeepFirst,
    /// <summary>Keep the last row of each key and leave out the others (discard reason <see cref="SpreadsheetDiscardReason.Duplicate"/>).</summary>
    KeepLast,
}

/// <summary>What happens to an imported row whose key is already present in <see cref="SpreadsheetSchema.ExistingRecords"/>.</summary>
public enum SpreadsheetExistingAction
{
    /// <summary>Import the row in <see cref="SpreadsheetRowMode.Update"/> mode. This is the default.</summary>
    Update,
    /// <summary>Leave the row out (mode <see cref="SpreadsheetRowMode.Skip"/>, discard reason <see cref="SpreadsheetDiscardReason.Existing"/>).</summary>
    Skip,
    /// <summary>Mark the row with a blocking issue (code <c>row.exists</c>) so it is not importable.</summary>
    Error,
}

/// <summary>How a schema column relates to the loaded file. See <see cref="SpreadsheetFieldState.Status"/>.</summary>
public enum SpreadsheetColumnStatus
{
    /// <summary>The column is read from a file column, or is computed from another column through <see cref="SpreadsheetColumnSchema.From"/>.</summary>
    Matched,
    /// <summary>No file column matches, but the column has a default value (<see cref="SpreadsheetColumnSchema.HasDefault"/>).</summary>
    Default,
    /// <summary>No file column matches and the column is required without a default. The importer cannot submit until a column is assigned.</summary>
    Missing,
    /// <summary>No file column matches and the column is optional. Its cells are empty.</summary>
    Unmatched,
}

/// <summary>How serious a <see cref="SpreadsheetRowIssue"/> is.</summary>
public enum SpreadsheetIssueLevel
{
    /// <summary>Informational. Does not affect whether the row can be imported.</summary>
    Info,
    /// <summary>Worth a look, but the row stays importable.</summary>
    Warning,
    /// <summary>Blocks the row from being imported.</summary>
    Error,
}

/// <summary>The validation state of a <see cref="SpreadsheetRow"/>.</summary>
public enum SpreadsheetRowStatus
{
    /// <summary>No error or warning, and the row is not discarded.</summary>
    Valid,
    /// <summary>At least one warning and no error. The row is importable.</summary>
    Warning,
    /// <summary>At least one error. The row is not importable until it is edited or discarded.</summary>
    Blocking,
    /// <summary>The row is left out of the import. See <see cref="SpreadsheetRow.DiscardReason"/>.</summary>
    Discarded,
}

/// <summary>Why a <see cref="SpreadsheetRow"/> is left out of the import. When several reasons apply, the first in this order wins: limit, value, duplicate, existing, manual.</summary>
public enum SpreadsheetDiscardReason
{
    /// <summary>The user left the row out, through <see cref="SpreadsheetImporter.DiscardRows"/>.</summary>
    Manual,
    /// <summary>A value answer of <see cref="SpreadsheetValueAnswerAction.SkipRows"/> applies to the row.</summary>
    Value,
    /// <summary>The row is past <see cref="SpreadsheetSchema.MaxRows"/>.</summary>
    Limit,
    /// <summary>The row repeats a key and the policy keeps only the first or the last row.</summary>
    Duplicate,
    /// <summary>The key already exists and <see cref="SpreadsheetExistingAction.Skip"/> is set.</summary>
    Existing,
}

/// <summary>Whether an importable row creates a record or updates an existing one.</summary>
public enum SpreadsheetRowMode
{
    /// <summary>The key is new, or the schema has no key.</summary>
    Create,
    /// <summary>The key matches an entry of <see cref="SpreadsheetSchema.ExistingRecords"/>.</summary>
    Update,
    /// <summary>The key matches an existing record and the schema skips those rows.</summary>
    Skip,
}

/// <summary>The row subset shown by the review table. See <see cref="SpreadsheetImporter.SetReviewTab"/>.</summary>
public enum SpreadsheetReviewTab
{
    /// <summary>Every row, discarded ones included.</summary>
    All,
    /// <summary>Rows that would be submitted.</summary>
    Importable,
    /// <summary>Rows with at least one error that are not discarded.</summary>
    Invalid,
    /// <summary>Rows left out of the import, whatever the reason.</summary>
    Discarded,
}

/// <summary>Filters the review table by the severity of the issues a row has. See <see cref="SpreadsheetImporter.SetReviewLevel"/>.</summary>
public enum SpreadsheetReviewLevel
{
    /// <summary>No severity filter.</summary>
    All,
    /// <summary>Only rows with at least one error issue.</summary>
    Blocking,
    /// <summary>Only rows with at least one warning issue.</summary>
    Warnings,
}

/// <summary>The decision recorded in a <see cref="SpreadsheetValueAnswer"/> for an unknown select value.</summary>
public enum SpreadsheetValueAnswerAction
{
    /// <summary>Replace the value with one of the column's options.</summary>
    Map,
    /// <summary>Import the row with an empty cell.</summary>
    LeaveEmpty,
    /// <summary>Leave out every row that contains the value.</summary>
    SkipRows,
    /// <summary>Keep the value as a new option.</summary>
    Create,
}

/// <summary>Where <see cref="SpreadsheetImporter.SubmitAsync"/> is in its lifecycle.</summary>
public enum SpreadsheetSubmitStatus
{
    /// <summary>Nothing has been submitted since the file was loaded.</summary>
    Idle,
    /// <summary>A submit or retry is in progress.</summary>
    Running,
    /// <summary>The last submit finished. Rows may still have been rejected, see <see cref="SpreadsheetImporter.RejectedCount"/>.</summary>
    Done,
    /// <summary>The submit callback threw. <see cref="SpreadsheetImporter.SubmitError"/> holds the message. The rows accepted before the failure stay accepted, and <see cref="SpreadsheetImporter.SubmitAsync"/> resumes with the rest.</summary>
    Error,
    /// <summary>The submit was cancelled, through <see cref="SpreadsheetImporter.CancelSubmit"/> or the token given to <see cref="SpreadsheetImporter.SubmitAsync"/>. The rows accepted so far stay accepted, and <see cref="SpreadsheetImporter.SubmitAsync"/> resumes with the rest.</summary>
    Cancelled,
}

/// <summary>Where the async checks of <see cref="SpreadsheetImporter.ValidateAsync"/> are in their lifecycle.</summary>
public enum SpreadsheetValidationStatus
{
    /// <summary>No check has run since the file was loaded.</summary>
    Idle,
    /// <summary>The checks are running.</summary>
    Running,
    /// <summary>The last run finished.</summary>
    Done,
    /// <summary>A callback threw. <see cref="SpreadsheetImporter.ValidationError"/> holds the message.</summary>
    Error,
    /// <summary>The last run was cancelled before it finished.</summary>
    Cancelled,
}

/// <summary>An allowed value of a <see cref="SpreadsheetColumnKind.Select"/> column.</summary>
/// <param name="Value">The value stored in <see cref="SpreadsheetRow.Data"/> when this option matches.</param>
/// <param name="Label">The text shown to the user. Cells are matched against it as well as against the value.</param>
/// <param name="Aliases">Extra spellings that also match this option, for example a symbol or an abbreviation.</param>
/// <remarks>Matching ignores case, accents, repeated spaces, a trailing <c>*</c> and a trailing <c>(required)</c>, see <see cref="SpreadsheetImporter.NormalizeText"/>.</remarks>
public sealed record SpreadsheetOption(
    string Value,
    string Label,
    IReadOnlyList<string>? Aliases = null);

/// <summary>A validation rule attached to a column. A rule runs on the parsed value of a non-empty cell.</summary>
/// <param name="Code">A stable identifier copied to <see cref="SpreadsheetRowIssue.Code"/>, usable for filtering.</param>
/// <param name="Message">The text of the issue shown to the user when the rule fails.</param>
/// <param name="Validate">Receives the parsed value and returns <see langword="true"/> when it is acceptable.</param>
/// <param name="Level">The severity of the issue. Only <see cref="SpreadsheetIssueLevel.Error"/> blocks the row.</param>
public sealed record SpreadsheetRule(
    string Code,
    string Message,
    Func<object?, bool> Validate,
    SpreadsheetIssueLevel Level = SpreadsheetIssueLevel.Error)
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.Compiled);

    /// <summary>Creates a rule that accepts text shaped like an email address (something before and after one <c>@</c>, and a dot followed by at least two characters).</summary>
    /// <param name="message">The issue text.</param>
    /// <param name="level">The issue severity.</param>
    /// <returns>A rule with code <c>email</c>. It fails for any value that is not a string.</returns>
    public static SpreadsheetRule Email(string message = "Not an email address", SpreadsheetIssueLevel level = SpreadsheetIssueLevel.Error) =>
        new("email", message, v => v is string s && EmailRegex.IsMatch(s), level);

    /// <summary>The match timeout <see cref="Pattern"/> uses when you pass none: 100 milliseconds per cell.</summary>
    public static readonly TimeSpan DefaultPatternTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>Creates a rule that accepts text matching a regular expression. A cell that makes the match run past the timeout (catastrophic backtracking on a hostile value) does not hang the import: it gets a <c>pattern</c> issue saying the format could not be checked in time.</summary>
    /// <param name="pattern">The regular expression. It is not anchored unless you add <c>^</c> and <c>$</c>.</param>
    /// <param name="message">The issue text.</param>
    /// <param name="level">The issue severity. A timed-out check gets the same severity.</param>
    /// <param name="matchTimeout">The longest one match may take. Defaults to <see cref="DefaultPatternTimeout"/>. Use <see cref="Regex.InfiniteMatchTimeout"/> to remove the limit.</param>
    /// <returns>A rule with code <c>pattern</c>. It fails for any value that is not a string.</returns>
    /// <exception cref="ArgumentException"><paramref name="pattern"/> is not a valid regular expression.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="matchTimeout"/> is zero, negative or too large for <see cref="Regex"/>.</exception>
    public static SpreadsheetRule Pattern(string pattern, string message = "Unexpected format", SpreadsheetIssueLevel level = SpreadsheetIssueLevel.Error, TimeSpan? matchTimeout = null)
    {
        var rx = new Regex(pattern, RegexOptions.Compiled, matchTimeout ?? DefaultPatternTimeout);
        return new("pattern", message, v => v is string s && rx.IsMatch(s), level);
    }

    /// <summary>Creates a rule that accepts numbers greater than or equal to a minimum.</summary>
    /// <param name="min">The smallest accepted value.</param>
    /// <param name="message">The issue text. Defaults to a message naming the minimum.</param>
    /// <param name="level">The issue severity.</param>
    /// <returns>A rule with code <c>min</c>. It fails for any value that is not a <see cref="double"/>, so use it on a <see cref="SpreadsheetColumnKind.Number"/> column.</returns>
    public static SpreadsheetRule Min(double min, string? message = null, SpreadsheetIssueLevel level = SpreadsheetIssueLevel.Error) =>
        new("min", message ?? $"Value is below {min.ToString(CultureInfo.InvariantCulture)}",
            v => v is double d && d >= min, level);

    /// <summary>Creates a rule that accepts numbers less than or equal to a maximum.</summary>
    /// <param name="max">The largest accepted value.</param>
    /// <param name="message">The issue text. Defaults to a message naming the maximum.</param>
    /// <param name="level">The issue severity.</param>
    /// <returns>A rule with code <c>max</c>. It fails for any value that is not a <see cref="double"/>, so use it on a <see cref="SpreadsheetColumnKind.Number"/> column.</returns>
    public static SpreadsheetRule Max(double max, string? message = null, SpreadsheetIssueLevel level = SpreadsheetIssueLevel.Error) =>
        new("max", message ?? $"Value is above {max.ToString(CultureInfo.InvariantCulture)}",
            v => v is double d && d <= max, level);

    /// <summary>Creates a rule that accepts numbers inside an inclusive range.</summary>
    /// <param name="min">The smallest accepted value.</param>
    /// <param name="max">The largest accepted value.</param>
    /// <param name="message">The issue text. Defaults to a message naming the range.</param>
    /// <param name="level">The issue severity.</param>
    /// <returns>A rule with code <c>between</c>. It fails for any value that is not a <see cref="double"/>, so use it on a <see cref="SpreadsheetColumnKind.Number"/> column.</returns>
    public static SpreadsheetRule Between(double min, double max, string? message = null, SpreadsheetIssueLevel level = SpreadsheetIssueLevel.Error) =>
        new("between", message ?? $"Value is not between {min.ToString(CultureInfo.InvariantCulture)} and {max.ToString(CultureInfo.InvariantCulture)}",
            v => v is double d && d >= min && d <= max, level);
}

/// <summary>Declares one field of the imported data: how to find it in the file, how to parse it and how to validate it.</summary>
public sealed class SpreadsheetColumnSchema
{
    /// <summary>Creates a column definition. Set <see cref="Key"/> in the object initializer.</summary>
    public SpreadsheetColumnSchema()
    {
    }

    /// <summary>The field name. It is the key of <see cref="SpreadsheetRow.Data"/> and <see cref="SpreadsheetRow.Cells"/>, and it also matches a file header.</summary>
    public required string Key { get; init; }
    /// <summary>The display name, also matched against file headers. Defaults to <see cref="EffectiveLabel"/>.</summary>
    public string? Label { get; init; }
    /// <summary>How cells are parsed. Defaults to <see cref="SpreadsheetColumnKind.Text"/>.</summary>
    public SpreadsheetColumnKind Kind { get; init; } = SpreadsheetColumnKind.Text;
    /// <summary>Additional header spellings that match this column, on top of <see cref="Key"/> and <see cref="EffectiveLabel"/>.</summary>
    public IReadOnlyList<string>? Headers { get; init; }
    /// <summary>Help text for your own UI. The importer and the built-in component do not read it.</summary>
    public string? Description { get; init; }
    /// <summary>A sample value written in the second row of <see cref="SpreadsheetImporter.ExportTemplateCsv"/>.</summary>
    public string? Example { get; init; }
    /// <summary>A grouping label for your own UI. The importer and the built-in component do not read it.</summary>
    public string? Group { get; init; }
    /// <summary>The <see cref="Key"/> of an earlier column whose cell text this column reuses instead of reading its own file column. A column with <see cref="From"/> never matches a header and is left out of the template. The source column must come first in <see cref="SpreadsheetSchema.Columns"/>.</summary>
    public string? From { get; init; }
    /// <summary>When <see langword="true"/>, an empty cell is a blocking issue (code <c>required</c>) and a file with no matching column cannot be submitted, unless the column has a default.</summary>
    public bool Required { get; init; }
    /// <summary>Whether the built-in component lets the user edit this column's cells in the review table. <see cref="SpreadsheetImporter.EditCell"/> ignores it.</summary>
    public bool Editable { get; init; } = true;
    /// <summary>The value used for empty cells. It only applies when <see cref="HasDefault"/> is <see langword="true"/>.</summary>
    public object? DefaultValue { get; init; }
    /// <summary>Set to <see langword="true"/> to apply <see cref="DefaultValue"/>, which lets the default itself be <see langword="null"/>. A column with a default is never missing.</summary>
    public bool HasDefault { get; init; }
    /// <summary>The decimal mark of <see cref="SpreadsheetColumnKind.Number"/> cells, <c>.</c> or <c>,</c>. The other character is accepted as a thousands separator only between groups of three digits, so <c>12,50</c> is rejected when the mark is <c>.</c>.</summary>
    public char DecimalSeparator { get; init; } = '.';
    /// <summary>Exact <see cref="DateTime"/> format strings accepted for <see cref="SpreadsheetColumnKind.Date"/> cells. When empty, the defaults are <c>yyyy-MM-dd</c>, <c>yyyy-MM-dd HH:mm</c>, <c>yyyy-MM-dd HH:mm:ss</c>, <c>dd/MM/yyyy</c> and <c>dd/MM/yyyy HH:mm</c>.</summary>
    public IReadOnlyList<string>? DateFormats { get; init; }
    /// <summary>The allowed values of a <see cref="SpreadsheetColumnKind.Select"/> column.</summary>
    public IReadOnlyList<SpreadsheetOption>? Options { get; init; }
    /// <summary>What to do with select cells that match no option. Defaults to <see cref="SpreadsheetUnknownValuePolicy.Ask"/>.</summary>
    public SpreadsheetUnknownValuePolicy UnknownPolicy { get; init; } = SpreadsheetUnknownValuePolicy.Ask;
    /// <summary>Called once per distinct value answered with <see cref="SpreadsheetValueAnswerAction.Create"/>, when the import is submitted and before any row is sent. Use it to persist the new option. The rows then carry the returned option: its <see cref="SpreadsheetOption.Value"/> in <see cref="SpreadsheetRow.Data"/> and its label in the review, and later sends do not call it again for that value. Setting it also makes the component offer a create choice (<see cref="SpreadsheetValueQuestion.CanCreate"/>).</summary>
    public Func<string, SpreadsheetOption>? OnCreateOption { get; init; }
    /// <summary>The asynchronous form of <see cref="OnCreateOption"/>, for a callback that calls a server. It receives the cancellation token of the submit, so cancelling the submit cancels it. When both are set, this one is used.</summary>
    public Func<string, CancellationToken, Task<SpreadsheetOption>>? OnCreateOptionAsync { get; init; }
    /// <summary>Validation rules run, in order, on every non-empty parsed value.</summary>
    public IReadOnlyList<SpreadsheetRule>? Rules { get; init; }
    /// <summary>Replaces the built-in parsing of non-select columns. It receives the cell text and returns the value. An exception marks the cell as an error with the exception message.</summary>
    public Func<string, object?>? CustomParse { get; init; }

    /// <summary><see cref="Label"/> when set, otherwise <see cref="Key"/> turned into words, for example <c>secureCode</c> becomes <c>Secure code</c>.</summary>
    public string EffectiveLabel => Label ?? SpreadsheetImporter.HumanizeKey(Key);
}

/// <summary>Describes an import: its columns and the policies for limits, duplicates, existing records and submission. Pass it to <see cref="SpreadsheetImporter"/>.</summary>
public sealed class SpreadsheetSchema
{
    /// <summary>Creates a schema with no columns and the default policies.</summary>
    public SpreadsheetSchema()
    {
    }

    /// <summary>A name for the import for your own use. The importer does not read it.</summary>
    public string Key { get; init; } = "import";
    /// <summary>The file extensions the built-in component lists in its drop zone. They are display text: the importer enforces nothing, and <see cref="SpreadsheetImporter.LoadFileAsync"/> reads <c>.xlsx</c> and <c>.xls</c> workbooks and delimited text whatever this list says.</summary>
    public IReadOnlyList<string> Accept { get; init; } = [".xlsx", ".xls", ".csv", ".tsv"];
    /// <summary>The maximum number of data rows to import. Later rows are left out with <see cref="SpreadsheetDiscardReason.Limit"/>. No limit when <see langword="null"/>.</summary>
    public int? MaxRows { get; init; }
    /// <summary>The name of the sheet to read, which skips sheet detection. <see langword="null"/>, empty or <c>auto</c> detects the sheet; an unknown name falls back to the first sheet.</summary>
    public string? ForcedSheet { get; init; }
    /// <summary>The 1-based file row number of the header row, which skips header detection. A number that matches no row selects the first row.</summary>
    public int? ForcedHeaderRow { get; init; }
    /// <summary>The columns to import, in display order. A column with <see cref="SpreadsheetColumnSchema.From"/> must come after its source.</summary>
    public IReadOnlyList<SpreadsheetColumnSchema> Columns { get; init; } = [];
    /// <summary>The <see cref="SpreadsheetColumnSchema.Key"/> of the column that identifies a row. It enables the duplicate and existing-record checks.</summary>
    public string? RowKeyField { get; init; }
    /// <summary>How rows that repeat a key within the file are handled. Keys are compared trimmed and case-insensitively. Needs <see cref="RowKeyField"/>.</summary>
    public SpreadsheetDuplicatesPolicy Duplicates { get; init; } = SpreadsheetDuplicatesPolicy.Error;
    /// <summary>Records that already exist, keyed by the row key value. A row matches when its key text equals a dictionary key under the dictionary's own comparer, with no trimming or case folding applied to the row, so build the dictionary with <see cref="StringComparer.OrdinalIgnoreCase"/> for case-insensitive keys. Needs <see cref="RowKeyField"/>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? ExistingRecords { get; init; }
    /// <summary>What to do with rows whose key is in <see cref="ExistingRecords"/>.</summary>
    public SpreadsheetExistingAction ExistingAction { get; init; } = SpreadsheetExistingAction.Update;
    /// <summary>A cross-field check run on every row after the column rules. It receives the parsed values by column key and returns extra issues, which are added to the row.</summary>
    public Func<IReadOnlyDictionary<string, object?>, IReadOnlyList<SpreadsheetRowIssue>>? ValidateRow { get; init; }
    /// <summary>The number of rows passed to <see cref="OnSubmit"/> per call. Zero or less sends all rows in one call.</summary>
    public int BatchSize { get; init; }
    /// <summary>Receives each batch of importable rows and returns the rows the server refused, or <see langword="null"/> or an empty list when all were accepted. If it throws, the submit stops with <see cref="SpreadsheetSubmitStatus.Error"/>.</summary>
    public Func<IReadOnlyList<SpreadsheetRow>, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>? OnSubmit { get; init; }
    /// <summary>The cancellable form of <see cref="OnSubmit"/>: it also receives a <see cref="CancellationToken"/> that is cancelled by <see cref="SpreadsheetImporter.CancelSubmit"/> (the Cancel button of the component) or by the token passed to <see cref="SpreadsheetImporter.SubmitAsync"/>. A batch that throws <see cref="OperationCanceledException"/> counts as not accepted, so make the call safe to repeat. When both are set, this one is used.</summary>
    public Func<IReadOnlyList<SpreadsheetRow>, CancellationToken, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>? OnSubmitAsync { get; init; }
    /// <summary>A batched check that needs the host, for example "do these SKUs exist". It receives rows that are not discarded and have no error, <see cref="ValidateBatchSize"/> at a time, and returns the issues it found with the index of their row, or <see langword="null"/> or an empty list for none. See <see cref="SpreadsheetImporter.ValidateAsync"/> for when it runs.</summary>
    public Func<IReadOnlyList<SpreadsheetRow>, CancellationToken, Task<IReadOnlyList<SpreadsheetIndexedIssue>?>>? ValidateRowsAsync { get; init; }
    /// <summary>The number of rows passed to <see cref="ValidateRowsAsync"/> per call. Zero or less sends all rows in one call. Defaults to 500.</summary>
    public int ValidateBatchSize { get; init; } = 500;
    /// <summary>Looks up existing records by key on demand, instead of loading them all into <see cref="ExistingRecords"/>. It receives the distinct row key values of the file, <see cref="LookupBatchSize"/> at a time, and returns the records found by key (keys it does not return are new). A row matches when its key text equals a returned key under the dictionary's own comparer, like <see cref="ExistingRecords"/>. Needs <see cref="RowKeyField"/>. See <see cref="SpreadsheetImporter.ValidateAsync"/> for when it runs.</summary>
    public Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>>? LookupExistingRecordsAsync { get; init; }
    /// <summary>The number of keys passed to <see cref="LookupExistingRecordsAsync"/> per call. Zero or less sends all keys in one call. Defaults to 500.</summary>
    public int LookupBatchSize { get; init; } = 500;
}

/// <summary>An issue found by <see cref="SpreadsheetSchema.ValidateRowsAsync"/>, with the row it belongs to.</summary>
/// <param name="RowIndex">The <see cref="SpreadsheetRow.Index"/> of the row.</param>
/// <param name="Issue">The issue added to that row. Only <see cref="SpreadsheetIssueLevel.Error"/> blocks the row.</param>
public sealed record SpreadsheetIndexedIssue(int RowIndex, SpreadsheetRowIssue Issue);

/// <summary>A row that <see cref="SpreadsheetSchema.OnSubmit"/> refused. It comes back as an error issue with code <c>server</c> on that row.</summary>
/// <param name="RowIndex">The <see cref="SpreadsheetRow.Index"/> of the refused row.</param>
/// <param name="Message">The reason, shown to the user.</param>
/// <param name="Field">The column key the reason relates to, or <see langword="null"/> for the whole row.</param>
public sealed record SpreadsheetRejectedRow(int RowIndex, string Message, string? Field = null);

/// <summary>One sheet of a workbook, as rows of cell text.</summary>
/// <param name="Name">The sheet name.</param>
/// <param name="Rows">The non-empty rows, each a list of trimmed cell text.</param>
/// <param name="RowNumbers">The 1-based row number in the source file of each entry of <paramref name="Rows"/>, so the same index in both lists describes one row.</param>
public sealed record SpreadsheetSheet(
    string Name,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    IReadOnlyList<int> RowNumbers);

/// <summary>A parsed file made of one or more sheets. Build one yourself to import a format the library does not parse, then pass it to <see cref="SpreadsheetImporter.LoadWorkbook"/>.</summary>
/// <param name="Name">The file name, shown by the component.</param>
/// <param name="Sheets">The sheets of the file.</param>
public sealed record SpreadsheetWorkbook(
    string Name,
    IReadOnlyList<SpreadsheetSheet> Sheets)
{
    /// <summary>
    /// <see langword="true"/> when number cells hold invariant-culture text (<c>1234.5</c>), as in a workbook read by
    /// <see cref="SpreadsheetXlsxReader"/>. Number columns then read that form whatever their
    /// <see cref="SpreadsheetColumnSchema.DecimalSeparator"/>, and still accept text in the column's own convention.
    /// </summary>
    public bool InvariantNumbers { get; init; }
}

/// <summary>A file column offered for a missing required column because its sample values look right. The importer never applies it on its own.</summary>
/// <param name="HeaderIndex">The index to pass to <see cref="SpreadsheetImporter.AssignColumn"/>.</param>
/// <param name="HeaderText">The header text of the suggested file column.</param>
/// <param name="Samples">Up to three distinct sample values from that column.</param>
public sealed record SpreadsheetColumnSuggestion(
    int HeaderIndex,
    string HeaderText,
    IReadOnlyList<string> Samples);

/// <summary>The current mapping of one schema column to the loaded file.</summary>
/// <param name="Column">The schema column.</param>
/// <param name="Status">Whether the column is matched, defaulted, missing or unmatched.</param>
/// <param name="HeaderIndex">The index of the file column read for this field, or <see langword="null"/> when none is.</param>
/// <param name="HeaderText">The header text of that file column.</param>
/// <param name="AssignedBy"><c>file</c> when the header matched by name, <c>user</c> after <see cref="SpreadsheetImporter.AssignColumn"/>, otherwise <see langword="null"/>.</param>
/// <param name="Samples">Up to three distinct non-empty values of the mapped file column.</param>
/// <param name="Suggestion">A suggested file column for a missing field, or <see langword="null"/>.</param>
public sealed record SpreadsheetFieldState(
    SpreadsheetColumnSchema Column,
    SpreadsheetColumnStatus Status,
    int? HeaderIndex,
    string? HeaderText,
    string? AssignedBy,
    IReadOnlyList<string> Samples,
    SpreadsheetColumnSuggestion? Suggestion);

/// <summary>The decision for one unknown select value, given by the user or by the column's policy.</summary>
/// <param name="Action">What to do with the value.</param>
/// <param name="MappedValue">For <see cref="SpreadsheetValueAnswerAction.Map"/>, the <see cref="SpreadsheetOption.Value"/> to use.</param>
/// <param name="MappedLabel">For <see cref="SpreadsheetValueAnswerAction.Map"/>, the label to display. Defaults to the value.</param>
public sealed record SpreadsheetValueAnswer(
    SpreadsheetValueAnswerAction Action,
    string? MappedValue = null,
    string? MappedLabel = null);

/// <summary>An unknown select value found in the file, with the answer given so far. One question exists per column and normalized value.</summary>
/// <param name="Id">The column key and the normalized value joined by <c>::</c>.</param>
/// <param name="FieldKey">The key of the select column.</param>
/// <param name="FieldLabel">The display name of the select column.</param>
/// <param name="RawValue">The value as it first appeared in the file.</param>
/// <param name="NormalizedKey">The value after <see cref="SpreadsheetImporter.NormalizeText"/>.</param>
/// <param name="RowIndexes">The <see cref="SpreadsheetRow.Index"/> of every row that contains the value.</param>
/// <param name="Choices">The options the value can be mapped to.</param>
/// <param name="Answer">The current answer, or <see langword="null"/> while the question is open.</param>
/// <param name="AnsweredBy"><c>user</c> or <c>policy</c> when answered, otherwise <see langword="null"/>.</param>
/// <param name="CanCreate">Whether the column defines <see cref="SpreadsheetColumnSchema.OnCreateOption"/>, so creating the value is allowed.</param>
public sealed record SpreadsheetValueQuestion(
    string Id,
    string FieldKey,
    string FieldLabel,
    string RawValue,
    string NormalizedKey,
    IReadOnlyList<int> RowIndexes,
    IReadOnlyList<SpreadsheetOption> Choices,
    SpreadsheetValueAnswer? Answer,
    string? AnsweredBy,
    bool CanCreate)
{
    /// <summary><see langword="true"/> while no answer exists. Open questions block submission.</summary>
    public bool IsOpen => Answer is null;
}

/// <summary>A problem or note about a row or one of its cells.</summary>
/// <param name="Field">The column key the issue relates to, or <see langword="null"/> for the whole row.</param>
/// <param name="Code">A stable identifier. Built-in codes are <c>required</c>, <c>number</c>, <c>boolean</c>, <c>date</c>, <c>text</c>, <c>value.unknown</c>, <c>value.not-found</c>, <c>value.skipped</c>, <c>row.duplicate</c>, <c>row.exists</c> and <c>server</c>, plus the code of each failed <see cref="SpreadsheetRule"/>.</param>
/// <param name="Level">The severity.</param>
/// <param name="Message">The text shown to the user.</param>
public sealed record SpreadsheetRowIssue(
    string? Field,
    string Code,
    SpreadsheetIssueLevel Level,
    string Message);

/// <summary>How one cell of a row is read and displayed.</summary>
/// <param name="Text">The text being parsed: the user's edit when there is one, otherwise the file text.</param>
/// <param name="OriginalText">The text in the file.</param>
/// <param name="DisplayText">The text to show: the option label for selects, the parsed value for other kinds, or the default.</param>
/// <param name="Edited">Whether the user edited the cell.</param>
/// <param name="Defaulted">Whether the column default replaced an empty cell.</param>
/// <param name="Created">Whether the value is a new option answered with <see cref="SpreadsheetValueAnswerAction.Create"/>.</param>
/// <param name="Issue">The first issue found on this cell, or <see langword="null"/>.</param>
public sealed record SpreadsheetCellView(
    string Text,
    string OriginalText,
    string DisplayText,
    bool Edited,
    bool Defaulted,
    bool Created,
    SpreadsheetRowIssue? Issue);

/// <summary>One data row after parsing and validation, as the importer currently sees it.</summary>
/// <param name="Index">The 0-based position among the data rows (the rows after the header). Edit, discard and rejection methods use it.</param>
/// <param name="RowNumber">The 1-based row number in the file, for messages to the user.</param>
/// <param name="Data">The parsed values by column key: <see cref="string"/> for text, <see cref="double"/> for numbers, <see cref="bool"/> for booleans, an ISO 8601 <see cref="string"/> for dates and the option value for selects. Empty cells are <see langword="null"/>.</param>
/// <param name="Cells">The display state of each cell by column key.</param>
/// <param name="Issues">Every issue found on the row.</param>
/// <param name="Status">The overall validation state.</param>
/// <param name="DiscardReason">Why the row is left out, or <see langword="null"/> when it is not.</param>
/// <param name="Mode">Whether the row creates a record or updates an existing one.</param>
/// <param name="Edited">Whether the user edited any cell of the row.</param>
/// <param name="Importable">Whether the row is sent on submit: not discarded and without an error.</param>
public sealed record SpreadsheetRow(
    int Index,
    int RowNumber,
    IReadOnlyDictionary<string, object?> Data,
    IReadOnlyDictionary<string, SpreadsheetCellView> Cells,
    IReadOnlyList<SpreadsheetRowIssue> Issues,
    SpreadsheetRowStatus Status,
    SpreadsheetDiscardReason? DiscardReason,
    SpreadsheetRowMode Mode,
    bool Edited,
    bool Importable);

/// <summary>The state of one step of the import wizard.</summary>
/// <param name="Key">One of <c>file</c>, <c>columns</c>, <c>values</c>, <c>review</c> or <c>submit</c>.</param>
/// <param name="Label">The step title.</param>
/// <param name="Hint">A short description of the step.</param>
/// <param name="State"><c>current</c>, <c>done</c>, <c>skipped</c> or <c>upcoming</c>.</param>
/// <param name="Position">The 1-based position in the wizard.</param>
/// <param name="Ready">Whether the step has nothing left to resolve, so the wizard can move on from it.</param>
/// <param name="Skipped">Whether the step needs no action and is passed over: <c>columns</c> without missing fields, <c>values</c> without questions.</param>
/// <param name="Blockers">The number of items to resolve before the step is ready.</param>
public sealed record SpreadsheetStepStatus(
    string Key,
    string Label,
    string Hint,
    string State,
    int Position,
    bool Ready,
    bool Skipped,
    int Blockers);

/// <summary>
/// The headless import engine. It loads delimited text or a prepared workbook, finds the sheet and header row, maps file columns
/// to the columns of a <see cref="SpreadsheetSchema"/>, parses and validates every row, tracks the user's corrections, and sends
/// the importable rows to <see cref="SpreadsheetSchema.OnSubmit"/> in batches.
/// </summary>
/// <remarks>
/// An instance holds the state of one import session and is not thread-safe. Create one per user and per import.
/// Every method that changes state raises <see cref="Changed"/> synchronously on the calling thread.
/// </remarks>
public sealed partial class SpreadsheetImporter
{
    private static readonly Regex DotDecimalNumber = new(@"^[+-]?(?:[1-9]\d{0,2}(?:,\d{3})+|\d*)(?:\.\d*)?(?:[eE][+-]?\d+)?$", RegexOptions.CultureInvariant);
    private static readonly Regex InvariantNumber = new(@"^[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?$", RegexOptions.CultureInvariant);
    private static readonly Regex CommaDecimalNumber = new(@"^[+-]?(?:[1-9]\d{0,2}(?:\.\d{3})+|\d*)(?:,\d*)?(?:[eE][+-]?\d+)?$", RegexOptions.CultureInvariant);

    /// <summary>How many leading rows of a sheet are scanned to find the header row.</summary>
    public const int HeaderScanRows = 20;
    /// <summary>The share of sampled cells that must parse and pass the error-level rules before a file column is suggested for a missing column.</summary>
    public const double SuggestionPassRate = 0.8;
    /// <summary>The maximum number of non-empty cells sampled from a file column when looking for a suggestion.</summary>
    public const int SuggestionSampleSize = 30;

    private static readonly HashSet<string> DefaultTrueTexts = new(StringComparer.OrdinalIgnoreCase)
    {
        "yes", "y", "oui", "o", "true", "vrai", "x", "1"
    };

    private static readonly HashSet<string> DefaultFalseTexts = new(StringComparer.OrdinalIgnoreCase)
    {
        "no", "n", "non", "false", "faux", "0"
    };

    private static readonly string[] DefaultDateFormats =
    [
        "yyyy-MM-dd", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "dd/MM/yyyy", "dd/MM/yyyy HH:mm"
    ];

    private readonly Dictionary<string, int?> _manualColumns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SpreadsheetValueAnswer> _valueAnswers = new(StringComparer.Ordinal);
    private readonly Dictionary<(int RowIndex, string FieldKey), string> _edits = new();
    private readonly HashSet<int> _manualDiscards = [];
    private readonly Dictionary<int, List<SpreadsheetRowIssue>> _serverRejections = [];
    private readonly HashSet<string> _visitedSteps = ["file"];
    private readonly HashSet<int> _selectedRows = [];

    private SpreadsheetWorkbook? _workbook;
    private string? _selectedSheetName;
    private int _headerRowIndex;
    private IReadOnlyList<SpreadsheetFieldState> _fields = [];
    private IReadOnlyList<SpreadsheetValueQuestion> _questions = [];

    /// <summary>Creates an importer for a schema, with no file loaded.</summary>
    /// <param name="schema">The columns and policies of the import.</param>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> is <see langword="null"/>.</exception>
    public SpreadsheetImporter(SpreadsheetSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        Schema = schema;
        Recompute();
    }

    /// <summary>Raised after every state change. The built-in component subscribes to it to re-render. Handlers run synchronously on the thread that made the change.</summary>
    public event Action? Changed;

    /// <summary>The schema this importer was created with.</summary>
    public SpreadsheetSchema Schema { get; }
    /// <summary>Whether a workbook with at least one sheet is loaded.</summary>
    public bool FileLoaded => _workbook is not null && CurrentSheet is not null;
    /// <summary>The name of the loaded file, or <see langword="null"/>.</summary>
    public string? FileName => _workbook?.Name;
    /// <summary>The sheets of the loaded file. Empty when nothing is loaded.</summary>
    public IReadOnlyList<SpreadsheetSheet> Sheets => _workbook?.Sheets ?? [];
    /// <summary>The sheet being imported: the selected one, otherwise the first.</summary>
    public SpreadsheetSheet? CurrentSheet =>
        _workbook?.Sheets.FirstOrDefault(s => s.Name == _selectedSheetName) ?? _workbook?.Sheets.FirstOrDefault();

    /// <summary>The index, within <see cref="SpreadsheetSheet.Rows"/>, of the row used as header.</summary>
    public int HeaderRowIndex => _headerRowIndex;
    /// <summary>The file row number of the header row, for messages to the user. 1 when no file is loaded.</summary>
    public int HeaderRowNumber =>
        CurrentSheet is { } s && _headerRowIndex >= 0 && _headerRowIndex < s.RowNumbers.Count
            ? s.RowNumbers[_headerRowIndex]
            : 1;

    /// <summary><see langword="true"/> when several sheets matched the schema equally well and the first one was picked. Offer the user a sheet choice in that case.</summary>
    public bool AmbiguousDetection { get; private set; }
    /// <summary>The cell text of the header row.</summary>
    public IReadOnlyList<string> Headers { get; private set; } = [];
    /// <summary>The non-empty headers that no schema column uses. They are not imported.</summary>
    public IReadOnlyList<string> UnusedHeaders { get; private set; } = [];
    /// <summary>The mapping state of every schema column, in schema order.</summary>
    public IReadOnlyList<SpreadsheetFieldState> Fields => _fields;
    /// <summary>Every unknown select value found in the file, answered or not.</summary>
    public IReadOnlyList<SpreadsheetValueQuestion> Questions => _questions;
    /// <summary><see langword="true"/> when a file is loaded, no field is missing, no question is open and at least one row is importable.</summary>
    public bool CanSubmit =>
        FileLoaded && MissingFields.Count == 0 && OpenQuestions.Count == 0 && ImportableRows.Count > 0;

    // Review state
    /// <summary>The row subset selected for <see cref="VisibleReviewRows"/>.</summary>
    public SpreadsheetReviewTab ReviewTab { get; private set; } = SpreadsheetReviewTab.All;
    /// <summary>The severity filter applied to <see cref="VisibleReviewRows"/>.</summary>
    public SpreadsheetReviewLevel ReviewLevel { get; private set; } = SpreadsheetReviewLevel.All;
    /// <summary>The issue filter applied to <see cref="VisibleReviewRows"/>, as <c>fieldKey</c>, <c>fieldKey|code</c>, or <c>|code</c> for issues on the whole row (such as <c>row.duplicate</c>). Empty for none.</summary>
    public string ReviewIssueFilter { get; private set; } = "";
    /// <summary>The text searched in the cells of <see cref="VisibleReviewRows"/>. Empty for none.</summary>
    public string ReviewSearch { get; private set; } = "";
    /// <summary>The <see cref="SpreadsheetRow.Index"/> of the row open in the inspector, or <see langword="null"/>.</summary>
    public int? InspectedRowIndex { get; private set; }
    /// <summary>The indexes of the rows selected in the review table.</summary>
    public IReadOnlySet<int> SelectedRowIndexes => _selectedRows;

    /// <summary>The row open in the inspector, or <see langword="null"/>.</summary>
    public SpreadsheetRow? InspectedRow =>
        InspectedRowIndex is { } idx && idx >= 0 && idx < _rowArr.Length ? _rowArr[idx] : null;

    private bool InvariantNumbers => _workbook?.InvariantNumbers == true;

    // Submit state
    /// <summary>The state of the current or last submit.</summary>
    public SpreadsheetSubmitStatus SubmitStatus { get; private set; } = SpreadsheetSubmitStatus.Idle;
    /// <summary>How many rows of the current or last submit have been sent so far.</summary>
    public int SubmitDone { get; private set; }
    /// <summary>How many rows the current or last submit sends.</summary>
    public int SubmitTotal { get; private set; }
    /// <summary>How many rows the host accepted so far, across the submit, its retries and its resumptions: <c>AcceptedRowIndexes.Count</c>. A retry adds the rows it got accepted and never counts a row twice.</summary>
    public int ImportedCount => _accepted.Count;
    /// <summary>The number of rows the server refused in the last submit and that have not been fixed or retried.</summary>
    public int RejectedCount => _serverRejections.Count;
    /// <summary>The message of the exception thrown by the submit callback, or <see langword="null"/>.</summary>
    public string? SubmitError { get; private set; }

    // Wizard steps state
    /// <summary>The key of the wizard step in progress: <c>file</c>, <c>columns</c>, <c>values</c>, <c>review</c> or <c>submit</c>.</summary>
    public string CurrentStepKey { get; private set; } = "file";

    /// <summary>Parses delimited text as a one-sheet workbook and loads it, replacing any previous file and discarding the user's corrections.</summary>
    /// <param name="text">The file content. The separator (tab, semicolon or comma) is detected from the first row, quoted fields and a leading byte order mark are handled, and blank rows are skipped.</param>
    /// <param name="fileName">The name to show. Its extension is not used to choose a parser.</param>
    public void LoadText(string text, string fileName = "import.csv")
    {
        var sheetName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(sheetName)) sheetName = "Sheet 1";
        var sheet = ParseDelimitedSheet(text, sheetName);
        LoadWorkbook(new SpreadsheetWorkbook(fileName, [sheet]));
    }

    /// <summary>Loads text pasted from a spreadsheet. It behaves exactly like <see cref="LoadText"/> with a different default name.</summary>
    /// <param name="text">The pasted content.</param>
    /// <param name="fileName">The name to show.</param>
    public void Paste(string text, string fileName = "clipboard.tsv") =>
        LoadText(text, fileName);

    /// <summary>
    /// Reads a file and loads it: an <c>.xlsx</c> workbook through <see cref="SpreadsheetXlsxReader"/>, a legacy <c>.xls</c>
    /// workbook through <see cref="SpreadsheetXlsReader"/>, anything else as delimited text decoded as UTF-8 (with or without
    /// a byte order mark), or as Latin-1 when the bytes are not valid UTF-8. The content decides for the legacy format: a
    /// file that starts with the OLE2 signature is read as <c>.xls</c> whatever its extension, a zip archive named
    /// <c>.xls</c> is read as <c>.xlsx</c>, and a text export named <c>.xls</c> (tab-separated or CSV, as many applications
    /// produce) is read as delimited text. An HTML or XML export named <c>.xls</c> is refused.
    /// </summary>
    /// <param name="content">The file content. It is copied to memory, so a browser or network stream can be passed as it is.</param>
    /// <param name="fileName">The file name. Its extension selects the reader, except for a legacy workbook.</param>
    /// <param name="maxBytes">The largest file accepted, in bytes (10 MB by default).</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>A task that completes when the file is loaded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The file is larger than <paramref name="maxBytes"/>, is an <c>.xls</c> file that is neither a readable workbook nor text (damaged, encrypted, Excel 5.0/95, or an HTML or XML export), or is not a valid <c>.xlsx</c> workbook.</exception>
    public async Task LoadFileAsync(Stream content, string fileName, long maxBytes = 10 * 1024 * 1024, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                throw new InvalidDataException($"The file is larger than {maxBytes / (1024 * 1024)} MB.");
            }
            buffer.Write(chunk, 0, read);
        }
        buffer.Position = 0;

        var head = buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 8));
        if (CompoundFile.HasSignature(head))
        {
            LoadWorkbook(SpreadsheetXlsReader.Read(buffer, fileName));
            return;
        }
        if (extension == ".xls")
        {
            // A zip archive named .xls is an .xlsx. Many applications also name their text exports .xls: tab-separated or CSV text is read
            // like a .csv file, while an HTML or XML export (a web page table, the Excel 2003 XML format) is refused, since its rows are markup.
            if (head.Length >= 4 && head[0] == 'P' && head[1] == 'K' && head[2] == 3 && head[3] == 4)
            {
                LoadWorkbook(SpreadsheetXlsxReader.Read(buffer, fileName));
                return;
            }
            var exported = DecodeText(buffer.ToArray());
            if (exported.AsSpan().TrimStart(['\uFEFF', ' ', '\t', '\r', '\n']).StartsWith("<"))
            {
                throw new InvalidDataException("The .xls file is an HTML or XML export, not a workbook. Save it as .xlsx or .csv.");
            }
            if (!IsPlainText(exported))
            {
                throw new InvalidDataException("The file is not a valid .xls workbook. Save it as .xlsx or .csv.");
            }
            LoadText(exported, fileName);
            return;
        }
        if (extension == ".xlsx")
        {
            LoadWorkbook(SpreadsheetXlsxReader.Read(buffer, fileName));
            return;
        }
        LoadText(DecodeText(buffer.ToArray()), fileName);
    }

    // Text has no control characters other than tab, line breaks and form feed: anything else is a damaged or unknown binary file.
    private static bool IsPlainText(string text)
    {
        foreach (var c in text)
        {
            if (char.IsControl(c) && c is not ('\t' or '\r' or '\n' or '\f')) return false;
        }
        return true;
    }

    private static string DecodeText(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    /// <summary>Loads a prepared workbook, replacing any previous file and discarding the user's column choices, answers, edits and discards. Detects the sheet and header row unless the schema forces them.</summary>
    /// <param name="workbook">The workbook, for example built from an Excel reader of your choice.</param>
    /// <exception cref="ArgumentNullException"><paramref name="workbook"/> is <see langword="null"/>.</exception>
    public void LoadWorkbook(SpreadsheetWorkbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        _workbook = workbook;
        _manualColumns.Clear();
        _valueAnswers.Clear();
        _edits.Clear();
        _manualDiscards.Clear();
        _selectedRows.Clear();
        InspectedRowIndex = null;
        ResetSubmitState();
        _lookedUpKeys.Clear();
        _lookedUpRecords.Clear();
        ReviewPage = 0;
        DetectLayout();
        Recompute();
        Changed?.Invoke();
    }

    /// <summary>Unloads the file, clears every user decision and returns the wizard to the <c>file</c> step.</summary>
    public void ClearFile()
    {
        _workbook = null;
        _selectedSheetName = null;
        _headerRowIndex = 0;
        _manualColumns.Clear();
        _valueAnswers.Clear();
        _edits.Clear();
        _manualDiscards.Clear();
        _selectedRows.Clear();
        InspectedRowIndex = null;
        CurrentStepKey = "file";
        _visitedSteps.Clear();
        _visitedSteps.Add("file");
        ResetSubmitState();
        _lookedUpKeys.Clear();
        _lookedUpRecords.Clear();
        ReviewPage = 0;
        Recompute();
        Changed?.Invoke();
    }

    /// <summary>Switches to another sheet of the loaded file, re-detects its header row and clears column choices and discards. Does nothing for an unknown name.</summary>
    /// <param name="sheetName">The name of the sheet.</param>
    public void SetSheet(string sheetName)
    {
        if (_workbook is null || _workbook.Sheets.All(s => s.Name != sheetName)) return;
        _selectedSheetName = sheetName;
        _manualColumns.Clear();
        _manualDiscards.Clear();
        ResetSubmitState();
        _headerRowIndex = BestHeaderRowForSheet(CurrentSheet!).RowIndex;
        ReviewPage = 0;
        Recompute();
        Changed?.Invoke();
    }

    /// <summary>Chooses a different header row of the current sheet and clears column choices and discards.</summary>
    /// <param name="rowIndex">The index within <see cref="SpreadsheetSheet.Rows"/>, clamped to the sheet.</param>
    public void SetHeaderRow(int rowIndex)
    {
        if (CurrentSheet is null) return;
        _headerRowIndex = Math.Clamp(rowIndex, 0, Math.Max(0, CurrentSheet.Rows.Count - 1));
        _manualColumns.Clear();
        _manualDiscards.Clear();
        ResetSubmitState();
        ReviewPage = 0;
        Recompute();
        Changed?.Invoke();
    }

    /// <summary>Maps a file column to a schema column. If another column was manually mapped to the same file column, that mapping is removed.</summary>
    /// <param name="fieldKey">The key of the schema column.</param>
    /// <param name="headerIndex">The index into <see cref="Headers"/>.</param>
    public void AssignColumn(string fieldKey, int headerIndex)
    {
        foreach (var k in _manualColumns.Where(kv => kv.Value == headerIndex).Select(kv => kv.Key).ToList())
        {
            _manualColumns.Remove(k);
        }
        _manualColumns[fieldKey] = headerIndex;
        Recompute();
        Changed?.Invoke();
    }

    /// <summary>Declares that a schema column is not in the file, overriding automatic matching. A required column then becomes missing.</summary>
    /// <param name="fieldKey">The key of the schema column.</param>
    public void IgnoreColumn(string fieldKey)
    {
        _manualColumns[fieldKey] = null;
        Recompute();
        Changed?.Invoke();
    }

    /// <summary>Removes a manual choice made by <see cref="AssignColumn"/> or <see cref="IgnoreColumn"/> so the column is matched by header again.</summary>
    /// <param name="fieldKey">The key of the schema column.</param>
    public void ResetColumn(string fieldKey)
    {
        _manualColumns.Remove(fieldKey);
        Recompute();
        Changed?.Invoke();
    }

    /// <summary>Answers an unknown select value for every row that contains it. A user answer overrides the column's policy.</summary>
    /// <param name="fieldKey">The key of the select column.</param>
    /// <param name="rawValue">The value as it appears in <see cref="SpreadsheetValueQuestion.RawValue"/>. Matching ignores case and accents.</param>
    /// <param name="answer">The decision.</param>
    public void AnswerValue(string fieldKey, string rawValue, SpreadsheetValueAnswer answer)
    {
        var key = $"{fieldKey}::{NormalizeText(rawValue)}";
        _valueAnswers[key] = answer;
        ReparseAnswered(key);
        Changed?.Invoke();
    }

    /// <summary>Removes a user answer so the value falls back to the column's policy.</summary>
    /// <param name="fieldKey">The key of the select column.</param>
    /// <param name="rawValue">The value the answer was given for.</param>
    public void ClearAnswer(string fieldKey, string rawValue)
    {
        var key = $"{fieldKey}::{NormalizeText(rawValue)}";
        _valueAnswers.Remove(key);
        ReparseAnswered(key);
        Changed?.Invoke();
    }

    /// <summary>Replaces the text of one cell and revalidates that row (and, when the edit changes the row key, the other rows that share the old or the new key). The edit applies in the importer only and never changes the source file. It also clears a server rejection of that row.</summary>
    /// <param name="rowIndex">The <see cref="SpreadsheetRow.Index"/> of the row.</param>
    /// <param name="fieldKey">The key of the column.</param>
    /// <param name="text">The new cell text, trimmed before use.</param>
    public void EditCell(int rowIndex, string fieldKey, string text)
    {
        _edits[(rowIndex, fieldKey)] = text.Trim();
        _serverRejections.Remove(rowIndex);
        ReparseRows([rowIndex], alwaysRebuildQuestions: false);
        Changed?.Invoke();
    }

    /// <summary>Removes an edit made by <see cref="EditCell"/> and restores the file text.</summary>
    /// <param name="rowIndex">The <see cref="SpreadsheetRow.Index"/> of the row.</param>
    /// <param name="fieldKey">The key of the column.</param>
    public void RevertCell(int rowIndex, string fieldKey)
    {
        _edits.Remove((rowIndex, fieldKey));
        ReparseRows([rowIndex], alwaysRebuildQuestions: false);
        Changed?.Invoke();
    }

    /// <summary>Leaves rows out of the import (reason <see cref="SpreadsheetDiscardReason.Manual"/>) and clears the selection.</summary>
    /// <param name="rowIndexes">The <see cref="SpreadsheetRow.Index"/> values to discard.</param>
    public void DiscardRows(IEnumerable<int> rowIndexes)
    {
        var changed = rowIndexes.ToList();
        foreach (var idx in changed) _manualDiscards.Add(idx);
        _selectedRows.Clear();
        RefinalizeRows(changed);
        Changed?.Invoke();
    }

    /// <summary>Brings back rows discarded with <see cref="DiscardRows"/> and clears the selection. Rows left out for another reason stay out.</summary>
    /// <param name="rowIndexes">The <see cref="SpreadsheetRow.Index"/> values to restore.</param>
    public void RestoreRows(IEnumerable<int> rowIndexes)
    {
        var changed = rowIndexes.ToList();
        foreach (var idx in changed) _manualDiscards.Remove(idx);
        _selectedRows.Clear();
        _asyncDirty = true;
        RefinalizeRows(changed);
        Changed?.Invoke();
    }

    /// <summary>Selects the review subset and clears the selection. Choosing Importable or Discarded also resets the severity filter.</summary>
    /// <param name="tab">The subset to show.</param>
    public void SetReviewTab(SpreadsheetReviewTab tab)
    {
        ReviewTab = tab;
        _selectedRows.Clear();
        if (tab is SpreadsheetReviewTab.Importable or SpreadsheetReviewTab.Discarded)
        {
            ReviewLevel = SpreadsheetReviewLevel.All;
        }
        ReviewPage = 0;
        BumpView();
        Changed?.Invoke();
    }

    /// <summary>Filters the review rows by issue severity. A filter other than All switches the Importable and Discarded tabs back to All.</summary>
    /// <param name="level">The severity filter.</param>
    public void SetReviewLevel(SpreadsheetReviewLevel level)
    {
        ReviewLevel = level;
        if (level != SpreadsheetReviewLevel.All && ReviewTab is SpreadsheetReviewTab.Importable or SpreadsheetReviewTab.Discarded)
        {
            ReviewTab = SpreadsheetReviewTab.All;
        }
        ReviewPage = 0;
        BumpView();
        Changed?.Invoke();
    }

    /// <summary>Filters the review rows to those with an issue on one column, optionally with one code. A non-empty filter switches the Importable and Discarded tabs back to All.</summary>
    /// <param name="filter"><c>fieldKey</c> for any issue on that column, <c>fieldKey|code</c> for one code, or an empty string for none.</param>
    public void SetReviewIssueFilter(string filter)
    {
        ReviewIssueFilter = filter;
        if (!string.IsNullOrEmpty(filter) && ReviewTab is SpreadsheetReviewTab.Importable or SpreadsheetReviewTab.Discarded)
        {
            ReviewTab = SpreadsheetReviewTab.All;
        }
        ReviewPage = 0;
        BumpView();
        Changed?.Invoke();
    }

    /// <summary>Filters the review rows to those with a cell containing the text, ignoring case.</summary>
    /// <param name="search">The text to find, or an empty string for none.</param>
    public void SetReviewSearch(string search)
    {
        ReviewSearch = search;
        ReviewPage = 0;
        BumpView();
        Changed?.Invoke();
    }

    /// <summary>How many rows <see cref="DefaultReviewPageSize"/> and <see cref="ReviewPageRows"/> use by default.</summary>
    public const int DefaultReviewPageSize = 50;

    private int _reviewPage;

    /// <summary>The number of rows <see cref="ReviewPageRows"/> returns per page. See <see cref="SetReviewPageSize"/>.</summary>
    public int ReviewPageSize { get; private set; } = DefaultReviewPageSize;

    /// <summary>The 0-based page <see cref="ReviewPageRows"/> returns, kept inside the pages that exist. Changing a review filter goes back to the first page.</summary>
    public int ReviewPage
    {
        get => Math.Clamp(_reviewPage, 0, Math.Max(0, ReviewPageCount - 1));
        private set => _reviewPage = Math.Max(0, value);
    }

    /// <summary>The number of pages of <see cref="VisibleReviewRows"/> at <see cref="ReviewPageSize"/>. At least 1, so an empty table still has a first page.</summary>
    public int ReviewPageCount => Math.Max(1, (VisibleReviewRows.Count + ReviewPageSize - 1) / ReviewPageSize);

    /// <summary>The rows of the current page of <see cref="VisibleReviewRows"/>. A table renders this list, so a file of tens of thousands of rows renders one page of rows only.</summary>
    public IReadOnlyList<SpreadsheetRow> ReviewPageRows
    {
        get
        {
            var visible = VisibleReviewRows;
            var start = ReviewPage * ReviewPageSize;
            if (start >= visible.Count) return [];
            var length = Math.Min(ReviewPageSize, visible.Count - start);
            return visible is SpreadsheetRow[] array ? new ArraySegment<SpreadsheetRow>(array, start, length) : visible.Skip(start).Take(length).ToArray();
        }
    }

    /// <summary>Shows another page of the review rows. The number is clamped to the pages that exist.</summary>
    /// <param name="page">The 0-based page.</param>
    public void SetReviewPage(int page)
    {
        var clamped = Math.Clamp(page, 0, ReviewPageCount - 1);
        if (clamped == ReviewPage) return;
        _reviewPage = clamped;
        Changed?.Invoke();
    }

    /// <summary>Changes the number of rows per page and goes back to the first page.</summary>
    /// <param name="pageSize">The rows per page, at least 1.</param>
    public void SetReviewPageSize(int pageSize)
    {
        pageSize = Math.Max(1, pageSize);
        if (pageSize == ReviewPageSize) return;
        ReviewPageSize = pageSize;
        _reviewPage = 0;
        Changed?.Invoke();
    }

    /// <summary>Adds a row to or removes it from the selection.</summary>
    /// <param name="rowIndex">The <see cref="SpreadsheetRow.Index"/> of the row.</param>
    /// <param name="selected"><see langword="true"/> to select, <see langword="false"/> to deselect.</param>
    public void ToggleRowSelection(int rowIndex, bool selected)
    {
        if (selected) _selectedRows.Add(rowIndex);
        else _selectedRows.Remove(rowIndex);
        Changed?.Invoke();
    }

    /// <summary>Empties the selection.</summary>
    public void ClearSelection()
    {
        _selectedRows.Clear();
        Changed?.Invoke();
    }

    /// <summary>Opens a row in the inspector, or closes the inspector.</summary>
    /// <param name="rowIndex">The <see cref="SpreadsheetRow.Index"/> to inspect, or <see langword="null"/> to close.</param>
    public void InspectRow(int? rowIndex)
    {
        InspectedRowIndex = rowIndex;
        Changed?.Invoke();
    }

    /// <summary>Moves the inspector to the next non-discarded row that has a warning or an error, wrapping around. Does nothing when there is none.</summary>
    public void InspectNextIssueRow()
    {
        var withIssues = _rowArr.Where(r => r.DiscardReason is null && r.Issues.Any(i => i.Level != SpreadsheetIssueLevel.Info)).ToList();
        if (withIssues.Count == 0) return;
        var curIdx = withIssues.FindIndex(r => r.Index == InspectedRowIndex);
        InspectedRowIndex = withIssues[(curIdx + 1) % withIssues.Count].Index;
        Changed?.Invoke();
    }

    /// <summary>Moves the inspector to the previous non-discarded row that has a warning or an error, wrapping around. Does nothing when there is none.</summary>
    public void InspectPreviousIssueRow()
    {
        var withIssues = _rowArr.Where(r => r.DiscardReason is null && r.Issues.Any(i => i.Level != SpreadsheetIssueLevel.Info)).ToList();
        if (withIssues.Count == 0) return;
        var curIdx = withIssues.FindIndex(r => r.Index == InspectedRowIndex);
        InspectedRowIndex = withIssues[(curIdx - 1 + withIssues.Count) % withIssues.Count].Index;
        Changed?.Invoke();
    }

    /// <summary>The rows to show in a review table: the selected tab, narrowed by the severity filter, the issue filter and the search text. The list is cached until a row or a filter changes, so reading it again is free.</summary>
    public IReadOnlyList<SpreadsheetRow> VisibleReviewRows
    {
        get
        {
            if (_visibleCache.List is not null && _visibleCache.Rows == _rowsVersion && _visibleCache.View == _viewVersion)
            {
                return _visibleCache.List;
            }

            IEnumerable<SpreadsheetRow> query = ReviewTab switch
            {
                SpreadsheetReviewTab.Importable => ImportableRows,
                SpreadsheetReviewTab.Invalid => InvalidRows,
                SpreadsheetReviewTab.Discarded => DiscardedRows,
                _ => _rowArr,
            };

            if (ReviewTab != SpreadsheetReviewTab.Discarded)
            {
                if (ReviewLevel == SpreadsheetReviewLevel.Blocking)
                {
                    query = query.Where(r => r.Issues.Any(i => i.Level == SpreadsheetIssueLevel.Error));
                }
                else if (ReviewLevel == SpreadsheetReviewLevel.Warnings)
                {
                    query = query.Where(r => r.Issues.Any(i => i.Level == SpreadsheetIssueLevel.Warning));
                }
            }

            if (!string.IsNullOrEmpty(ReviewIssueFilter))
            {
                var parts = ReviewIssueFilter.Split('|', 2);
                var fKey = parts[0];
                var code = parts.Length > 1 ? parts[1] : "*";
                query = query.Where(r => r.Issues.Any(i =>
                    (fKey.Length == 0 ? i.Field is null : i.Field == fKey) && (code == "*" || i.Code == code)));
            }

            if (!string.IsNullOrWhiteSpace(ReviewSearch))
            {
                var term = ReviewSearch.Trim();
                query = query.Where(r => r.Cells.Values.Any(c =>
                    c.DisplayText.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || c.Text.Contains(term, StringComparison.OrdinalIgnoreCase)));
            }

            var list = query.ToArray();
            _visibleCache = (_rowsVersion, _viewVersion, list);
            return list;
        }
    }

    // Wizard step management
    /// <summary>Returns the five wizard steps (file, columns, values, review, submit) with their current state.</summary>
    /// <returns>The steps in order.</returns>
    public IReadOnlyList<SpreadsheetStepStatus> GetSteps()
    {
        var defs = new (string Key, string Label, string Hint, bool WhenNeeded, Func<bool> Needed, Func<bool> Ready, Func<int> Blockers)[]
        {
            ("file", "File", "The Excel or CSV file", false, () => true, () => FileLoaded, () => FileLoaded ? 0 : 1),
            ("columns", "Columns", "File columns → fields", true, () => MissingFields.Count > 0, () => MissingFields.Count == 0, () => MissingFields.Count),
            ("values", "Values", "Unknown values", true, () => _questions.Count > 0, () => OpenQuestions.Count == 0, () => OpenQuestions.Count),
            ("review", "Review", "Valid rows and issues", false, () => true, () => ImportableRows.Count > 0, () => ImportableRows.Count == 0 ? 1 : 0),
            ("submit", "Import", "Summary and import", false, () => true, () => SubmitStatus == SpreadsheetSubmitStatus.Done, () => 0),
        };

        var curPos = Array.FindIndex(defs, d => d.Key == CurrentStepKey);
        if (curPos < 0) curPos = 0;

        var list = new List<SpreadsheetStepStatus>(defs.Length);
        for (var i = 0; i < defs.Length; i++)
        {
            var d = defs[i];
            var skipped = d.WhenNeeded && !_visitedSteps.Contains(d.Key) && !d.Needed();
            var state = i == curPos ? "current"
                : skipped ? "skipped"
                : i < curPos ? "done"
                : "upcoming";
            list.Add(new SpreadsheetStepStatus(d.Key, d.Label, d.Hint, state, i + 1, d.Ready(), skipped, d.Blockers()));
        }
        return list;
    }

    /// <summary>Whether the current step is ready and is not the last one.</summary>
    public bool CanNextStep
    {
        get
        {
            var steps = GetSteps();
            var cur = steps.FirstOrDefault(s => s.Key == CurrentStepKey);
            return cur is { Ready: true } && cur.Key != "submit";
        }
    }

    /// <summary>Whether there is an earlier step and no submit is running.</summary>
    public bool CanPreviousStep
    {
        get
        {
            var steps = GetSteps();
            var curIdx = steps.ToList().FindIndex(s => s.Key == CurrentStepKey);
            return curIdx > 0 && SubmitStatus != SpreadsheetSubmitStatus.Running;
        }
    }

    /// <summary>Moves to the next step that is not skipped.</summary>
    /// <returns><see langword="true"/> when the step changed.</returns>
    public bool NextStep()
    {
        if (!CanNextStep) return false;
        var steps = GetSteps();
        var curIdx = steps.ToList().FindIndex(s => s.Key == CurrentStepKey);
        for (var i = curIdx + 1; i < steps.Count; i++)
        {
            if (!steps[i].Skipped)
            {
                CurrentStepKey = steps[i].Key;
                _visitedSteps.Add(CurrentStepKey);
                Changed?.Invoke();
                return true;
            }
        }
        return false;
    }

    /// <summary>Moves to the previous step that is not skipped.</summary>
    /// <returns><see langword="true"/> when the step changed.</returns>
    public bool PreviousStep()
    {
        if (!CanPreviousStep) return false;
        var steps = GetSteps();
        var curIdx = steps.ToList().FindIndex(s => s.Key == CurrentStepKey);
        for (var i = curIdx - 1; i >= 0; i--)
        {
            if (!steps[i].Skipped)
            {
                CurrentStepKey = steps[i].Key;
                Changed?.Invoke();
                return true;
            }
        }
        return false;
    }

    /// <summary>Jumps back to an earlier step, including a step that was skipped because it had nothing to resolve, so users can still re-map a column. Forward jumps are ignored.</summary>
    /// <param name="key">The step key, see <see cref="SpreadsheetStepStatus.Key"/>.</param>
    public void GoToStep(string key)
    {
        var steps = GetSteps();
        var curIdx = steps.ToList().FindIndex(s => s.Key == CurrentStepKey);
        var targetIdx = steps.ToList().FindIndex(s => s.Key == key);
        if (targetIdx >= 0 && targetIdx <= curIdx)
        {
            CurrentStepKey = key;
            _visitedSteps.Add(key);
            Changed?.Invoke();
        }
    }

    /// <summary>Builds a CSV template for users to fill in: a header row and an example row for every column without <see cref="SpreadsheetColumnSchema.From"/>.</summary>
    /// <returns>Comma-separated text starting with a byte order mark. Headers are the first of <see cref="SpreadsheetColumnSchema.Headers"/>, or the label.</returns>
    public string ExportTemplateCsv()
    {
        var cols = Schema.Columns.Where(c => string.IsNullOrEmpty(c.From)).ToList();
        var sb = new StringBuilder();
        sb.Append('\uFEFF');
        sb.AppendLine(string.Join(',', cols.Select(c => $"\"{c.Headers?.FirstOrDefault() ?? c.EffectiveLabel}\"")));
        sb.AppendLine(string.Join(',', cols.Select(c => $"\"{c.Example ?? ""}\"")));
        return sb.ToString();
    }

    /// <summary>Builds a CSV of the rows with errors so users can fix them in their own tool and import the file again.</summary>
    /// <returns>Comma-separated text starting with a byte order mark. There is one column per schema column that is not computed with <see cref="SpreadsheetColumnSchema.From"/>, in schema order, headed by the file header it was read from (or the first of <see cref="SpreadsheetColumnSchema.Headers"/>, or the label, when it was not mapped), then an <c>Issues</c> column. Cells hold the text being parsed, user edits included.</returns>
    public string ExportInvalidRowsCsv()
    {
        static string Quote(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

        var fields = _fields.Where(f => string.IsNullOrEmpty(f.Column.From)).ToList();
        var sb = new StringBuilder();
        sb.Append('\uFEFF');
        var headers = fields
            .Select(f => f.HeaderText ?? f.Column.Headers?.FirstOrDefault() ?? f.Column.EffectiveLabel)
            .Append("Issues");
        sb.AppendLine(string.Join(',', headers.Select(Quote)));
        foreach (var row in InvalidRows)
        {
            var cells = fields.Select(f => row.Cells.TryGetValue(f.Column.Key, out var cv) ? cv.Text : "");
            var issuesText = string.Join("; ", row.Issues.Select(i => i.Field is not null ? $"{i.Field}: {i.Message}" : i.Message));
            sb.AppendLine(string.Join(',', cells.Append(issuesText).Select(Quote)));
        }
        return sb.ToString();
    }

    private void DetectLayout()
    {
        if (_workbook is null || _workbook.Sheets.Count == 0)
        {
            _selectedSheetName = null;
            _headerRowIndex = 0;
            AmbiguousDetection = false;
            return;
        }

        if (!string.IsNullOrEmpty(Schema.ForcedSheet) && !string.Equals(Schema.ForcedSheet, "auto", StringComparison.OrdinalIgnoreCase))
        {
            var forced = _workbook.Sheets.FirstOrDefault(s => s.Name == Schema.ForcedSheet) ?? _workbook.Sheets[0];
            _selectedSheetName = forced.Name;
            _headerRowIndex = Schema.ForcedHeaderRow is { } r
                ? Math.Max(0, forced.RowNumbers.ToList().IndexOf(r))
                : BestHeaderRowForSheet(forced).RowIndex;
            AmbiguousDetection = false;
            return;
        }

        var scores = _workbook.Sheets
            .Select(s => (Sheet: s, Best: BestHeaderRowForSheet(s)))
            .ToList();
        var maxMatches = scores.Max(x => x.Best.MatchCount);
        var winners = scores.Where(x => x.Best.MatchCount == maxMatches).ToList();
        AmbiguousDetection = maxMatches > 0 && winners.Count > 1;

        var chosen = winners[0];
        _selectedSheetName = chosen.Sheet.Name;
        _headerRowIndex = Schema.ForcedHeaderRow is { } fr
            ? Math.Max(0, chosen.Sheet.RowNumbers.ToList().IndexOf(fr))
            : chosen.Best.RowIndex;
    }

    private (int RowIndex, int MatchCount) BestHeaderRowForSheet(SpreadsheetSheet sheet)
    {
        var scanLimit = Math.Min(HeaderScanRows, sheet.Rows.Count);
        var bestRow = 0;
        var bestMatches = -1;
        var colFields = Schema.Columns.Where(c => string.IsNullOrEmpty(c.From)).ToList();

        for (var r = 0; r < scanLimit; r++)
        {
            var rowCells = sheet.Rows[r];
            var used = new HashSet<int>();
            var matches = 0;
            foreach (var col in colFields)
            {
                for (var c = 0; c < rowCells.Count; c++)
                {
                    if (used.Contains(c) || string.IsNullOrWhiteSpace(rowCells[c])) continue;
                    if (IsHeaderMatch(col, rowCells[c]))
                    {
                        used.Add(c);
                        matches++;
                        break;
                    }
                }
            }
            if (matches > bestMatches)
            {
                bestMatches = matches;
                bestRow = r;
            }
        }
        return (bestRow, Math.Max(0, bestMatches));
    }

    // An answer only changes the rows that hold the value. Without a question for it (an Error policy raises none) the answer
    // still applies to cells with that text, so the whole file is read again.
    private void ReparseAnswered(string questionId)
    {
        var question = _questions.FirstOrDefault(q => q.Id == questionId);
        if (question is null)
        {
            Recompute();
            return;
        }
        ReparseRows(question.RowIndexes.ToList(), alwaysRebuildQuestions: true);
    }

    private static SpreadsheetValueAnswer? PolicyToAnswer(SpreadsheetUnknownValuePolicy policy) => policy switch
    {
        SpreadsheetUnknownValuePolicy.LeaveEmpty => new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.LeaveEmpty),
        SpreadsheetUnknownValuePolicy.SkipRows => new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.SkipRows),
        SpreadsheetUnknownValuePolicy.Create => new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Create),
        _ => null,
    };

    // A rule that makes a regular expression time out (RegexMatchTimeoutException) fails instead of aborting the import.
    private static bool RunRule(SpreadsheetRule rule, object? value, out bool timedOut)
    {
        try
        {
            timedOut = false;
            return rule.Validate(value);
        }
        catch (RegexMatchTimeoutException)
        {
            timedOut = true;
            return false;
        }
    }

    private static bool CanSuggestColumn(SpreadsheetColumnSchema col) =>
        col.Kind != SpreadsheetColumnKind.Text || col.CustomParse is not null || col.Rules is { Count: > 0 };

    private bool PassesColumnCheck(SpreadsheetColumnSchema col, string sample, bool invariantNumbers)
    {
        if (col.Kind == SpreadsheetColumnKind.Select)
        {
            return MatchSelectOption(col, sample) is not null;
        }
        if (!TryParseScalar(col, sample, invariantNumbers, out var val, out _)) return false;
        if (col.Rules is { Count: > 0 })
        {
            return col.Rules.All(r => r.Level != SpreadsheetIssueLevel.Error || RunRule(r, val, out _));
        }
        return true;
    }

    private static bool TryParseScalar(SpreadsheetColumnSchema col, string text, bool invariantNumbers, out object? value, out string? error)
    {
        if (col.CustomParse is not null)
        {
            try
            {
                value = col.CustomParse(text);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                value = null;
                error = ex.Message;
                return false;
            }
        }

        switch (col.Kind)
        {
            case SpreadsheetColumnKind.Number:
                var cleaned = text.Replace(" ", "", StringComparison.Ordinal)
                    .Replace("\u00A0", "", StringComparison.Ordinal)
                    .Replace("'", "", StringComparison.Ordinal);
                // A thousands separator is accepted only between groups of three digits, so "12,50" with a
                // '.' decimal mark is rejected instead of being read as 1250.
                if (invariantNumbers && InvariantNumber.IsMatch(cleaned)
                    && double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantValue))
                {
                    value = invariantValue;
                    error = null;
                    return true;
                }
                var commaDecimal = col.DecimalSeparator == ',';
                var wellFormed = (commaDecimal ? CommaDecimalNumber : DotDecimalNumber).IsMatch(cleaned);
                cleaned = commaDecimal
                    ? cleaned.Replace(".", "", StringComparison.Ordinal).Replace(',', '.')
                    : cleaned.Replace(",", "", StringComparison.Ordinal);
                if (wellFormed && double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                {
                    value = d;
                    error = null;
                    return true;
                }
                value = null;
                error = $"\"{text}\" is not a number";
                return false;

            case SpreadsheetColumnKind.Boolean:
                var norm = NormalizeText(text);
                if (DefaultTrueTexts.Contains(norm))
                {
                    value = true;
                    error = null;
                    return true;
                }
                if (DefaultFalseTexts.Contains(norm))
                {
                    value = false;
                    error = null;
                    return true;
                }
                value = null;
                error = $"\"{text}\" is not yes or no";
                return false;

            case SpreadsheetColumnKind.Date:
                var formats = col.DateFormats is { Count: > 0 } ? col.DateFormats.ToArray() : DefaultDateFormats;
                if (DateTime.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    value = dt.TimeOfDay == TimeSpan.Zero
                        ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : dt.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
                    error = null;
                    return true;
                }
                value = null;
                error = $"\"{text}\" is not a date this column reads";
                return false;

            default:
                value = text.Trim();
                error = null;
                return true;
        }
    }

    private static IReadOnlyList<string> CollectDistinctSamples(IReadOnlyList<IReadOnlyList<string>> rows, int colIdx, int max)
    {
        var list = new List<string>(max);
        foreach (var row in rows)
        {
            if (colIdx >= row.Count) continue;
            var v = row[colIdx];
            if (string.IsNullOrWhiteSpace(v) || list.Contains(v, StringComparer.Ordinal)) continue;
            list.Add(v);
            if (list.Count >= max) break;
        }
        return list;
    }

    private static List<string> CollectNonEmptyCells(IReadOnlyList<IReadOnlyList<string>> rows, int colIdx, int max)
    {
        var list = new List<string>(max);
        foreach (var row in rows)
        {
            if (colIdx >= row.Count) continue;
            var v = row[colIdx];
            if (string.IsNullOrWhiteSpace(v)) continue;
            list.Add(v);
            if (list.Count >= max) break;
        }
        return list;
    }

    private static bool IsHeaderMatch(SpreadsheetColumnSchema col, string headerText)
    {
        var norm = NormalizeText(headerText);
        if (string.IsNullOrEmpty(norm)) return false;
        if (NormalizeText(col.Key) == norm || NormalizeText(col.EffectiveLabel) == norm) return true;
        if (col.Headers is not null)
        {
            return col.Headers.Any(h => NormalizeText(h) == norm);
        }
        return false;
    }

    /// <summary>
    /// Normalizes a header or option text for comparison: removes a trailing <c>(required)</c> and trailing <c>*</c>, strips accents,
    /// collapses whitespace and lowercases.
    /// </summary>
    /// <param name="input">The text to normalize.</param>
    /// <returns>The normalized text, or an empty string for null or blank input.</returns>
    public static string NormalizeText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var text = input.Trim();
        if (text.EndsWith("(required)", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^10].TrimEnd();
        }
        text = text.TrimEnd('*', ' ');

        var formD = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        var prevSpace = false;
        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(ch))
            {
                if (!prevSpace && sb.Length > 0)
                {
                    sb.Append(' ');
                    prevSpace = true;
                }
            }
            else
            {
                sb.Append(char.ToLowerInvariant(ch));
                prevSpace = false;
            }
        }
        return sb.ToString().Trim();
    }

    /// <summary>Turns a camelCase, PascalCase, snake_case or kebab-case key into words, for example <c>secureCode</c> into <c>Secure code</c>.</summary>
    /// <param name="key">The key to convert.</param>
    /// <returns>The words, or an empty string for blank input.</returns>
    public static string HumanizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "";
        var sb = new StringBuilder();
        for (var i = 0; i < key.Length; i++)
        {
            var ch = key[i];
            if (i == 0)
            {
                sb.Append(char.ToUpperInvariant(ch));
            }
            else if (char.IsUpper(ch))
            {
                sb.Append(' ').Append(char.ToLowerInvariant(ch));
            }
            else if (ch is '_' or '-')
            {
                sb.Append(' ');
            }
            else
            {
                sb.Append(ch);
            }
        }
        return sb.ToString();
    }

    /// <summary>Parses CSV, TSV or semicolon-separated text into a sheet. Handles quoted fields with doubled quotes and line breaks, and strips a leading byte order mark.</summary>
    /// <param name="content">The text to parse. Null or empty gives an empty sheet.</param>
    /// <param name="sheetName">The name of the resulting sheet.</param>
    /// <returns>A sheet of the non-empty rows with trimmed cells. <see cref="SpreadsheetSheet.RowNumbers"/> counts blank records too.</returns>
    public static SpreadsheetSheet ParseDelimitedSheet(string? content, string sheetName = "Sheet 1")
    {
        if (string.IsNullOrEmpty(content))
        {
            return new SpreadsheetSheet(sheetName, [], []);
        }

        var text = content[0] == '\uFEFF' ? content[1..] : content;
        var sep = DetectSeparator(text);

        var rows = new List<IReadOnlyList<string>>();
        var rowNumbers = new List<int>();
        var current = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var fileRowNum = 1;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }
            }
            else if (ch == '"' && field.Length == 0)
            {
                inQuotes = true;
            }
            else if (ch == sep)
            {
                current.Add(field.ToString().Trim());
                field.Clear();
            }
            else if (ch is '\r' or '\n')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                current.Add(field.ToString().Trim());
                field.Clear();
                if (current.Any(c => c.Length > 0))
                {
                    rows.Add(current);
                    rowNumbers.Add(fileRowNum);
                }
                fileRowNum++;
                current = [];
            }
            else
            {
                field.Append(ch);
            }
        }

        if (field.Length > 0 || current.Count > 0)
        {
            current.Add(field.ToString().Trim());
            if (current.Any(c => c.Length > 0))
            {
                rows.Add(current);
                rowNumbers.Add(fileRowNum);
            }
        }

        return new SpreadsheetSheet(sheetName, rows, rowNumbers);
    }

    private static char DetectSeparator(string text)
    {
        // Order of precedence on tie: tab, semicolon, comma (spec section 2.2)
        ReadOnlySpan<char> candidates = ['\t', ';', ','];
        Span<int> counts = stackalloc int[3];
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '"') inQuotes = !inQuotes;
            else if (!inQuotes)
            {
                if (ch is '\r' or '\n') break;
                for (var c = 0; c < candidates.Length; c++)
                {
                    if (ch == candidates[c]) counts[c]++;
                }
            }
        }

        var best = ',';
        var bestCount = 0;
        for (var c = 0; c < candidates.Length; c++)
        {
            if (counts[c] > bestCount)
            {
                bestCount = counts[c];
                best = candidates[c];
            }
        }
        return best;
    }
}
