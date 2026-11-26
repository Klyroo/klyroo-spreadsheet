using System.Numerics;

namespace Klyroo.Spreadsheet;

/// <summary>An allowed value of a <see cref="SpreadsheetColumnKind.Select"/> column, as the importer and the component see it: the value is its text.</summary>
/// <param name="Value">The option's value as text (the enum member name for an enum column). It is what a value answer maps to.</param>
/// <param name="Label">The text shown to the user. Cells are matched against it as well as against the value.</param>
/// <param name="Aliases">Extra spellings that also match this option, for example a symbol or an abbreviation.</param>
/// <remarks>Matching ignores case, accents, repeated spaces, a trailing <c>*</c> and a trailing <c>(required)</c>, see <see cref="SpreadsheetImporter.NormalizeText"/>. You declare options with <see cref="SpreadsheetOption{TValue}"/>.</remarks>
public sealed record SpreadsheetOption(
    string Value,
    string Label,
    IReadOnlyList<string>? Aliases = null);

/// <summary>An allowed value of a select column, typed like the column.</summary>
/// <typeparam name="TValue">The value type of the column.</typeparam>
/// <param name="Value">The value written to the row property when this option matches.</param>
/// <param name="Label">The text shown to the user. Cells are matched against it as well as against the value.</param>
/// <param name="Aliases">Extra spellings that also match this option, for example a symbol or an abbreviation.</param>
/// <remarks>Matching ignores case, accents, repeated spaces, a trailing <c>*</c> and a trailing <c>(required)</c>, see <see cref="SpreadsheetImporter.NormalizeText"/>.</remarks>
public sealed record SpreadsheetOption<TValue>(
    TValue Value,
    string Label,
    IReadOnlyList<string>? Aliases = null);

/// <summary>The outcome of a <see cref="SpreadsheetParser{TValue}"/>: a value or an error message.</summary>
/// <typeparam name="TValue">The value type of the column.</typeparam>
public readonly struct SpreadsheetParseResult<TValue>
{
    private SpreadsheetParseResult(bool ok, TValue? value, string? error)
    {
        IsOk = ok;
        Value = value;
        Error = error;
    }

    /// <summary><see langword="true"/> when the text was read into <see cref="Value"/>.</summary>
    public bool IsOk { get; }
    /// <summary>The parsed value, meaningful when <see cref="IsOk"/> is <see langword="true"/>.</summary>
    public TValue? Value { get; }
    /// <summary>The message shown on the cell when <see cref="IsOk"/> is <see langword="false"/>.</summary>
    public string? Error { get; }

    /// <summary>The text was read as <paramref name="value"/>.</summary>
    /// <param name="value">The parsed value.</param>
    /// <returns>A successful result.</returns>
    public static SpreadsheetParseResult<TValue> Ok(TValue value) => new(true, value, null);

    /// <summary>The text cannot be read. The cell gets an error issue with this message.</summary>
    /// <param name="message">The issue text shown to the user.</param>
    /// <returns>A failed result.</returns>
    public static SpreadsheetParseResult<TValue> Fail(string message) => new(false, default, message);
}

/// <summary>Reads the text of a cell into a value. Replaces the built-in parsing of a column, see <see cref="SpreadsheetColumnBuilder{TRow, TValue}.Parser"/>.</summary>
/// <typeparam name="TValue">The value type of the column.</typeparam>
/// <param name="text">The trimmed text of a non-empty cell.</param>
/// <returns><see cref="SpreadsheetParseResult{TValue}.Ok"/> with the value, or <see cref="SpreadsheetParseResult{TValue}.Fail"/> with a message. An exception also marks the cell as an error, with the exception message.</returns>
public delegate SpreadsheetParseResult<TValue> SpreadsheetParser<TValue>(string text);

/// <summary>Declares one field of the imported data: how to find it in the file, how to parse it and how to validate it. Create columns with <see cref="SpreadsheetSchemaBuilder{TRow}.Column{TValue}"/>.</summary>
/// <remarks>This base type is what the importer and the component read: texts, options and settings, never a typed value. The typed column is <see cref="SpreadsheetColumn{TRow, TValue}"/>.</remarks>
public abstract class SpreadsheetColumnSchema
{
    internal SpreadsheetColumnSchema(string key, Type valueType)
    {
        Key = key;
        ValueType = valueType;
    }

    /// <summary>The field name. It is the key of <see cref="SpreadsheetRow.Cells"/> and it also matches a file header. It defaults to the name of the row property.</summary>
    public string Key { get; }
    /// <summary>The type of the row property this column fills.</summary>
    public Type ValueType { get; }
    /// <summary>The display name, also matched against file headers. Defaults to <see cref="EffectiveLabel"/>.</summary>
    public string? Label { get; internal set; }
    /// <summary>How cells are parsed. It follows the value type: text for <see cref="string"/>, number for numeric types, boolean for <see cref="bool"/>, date for <see cref="DateOnly"/>, <see cref="DateTime"/> and <see cref="DateTimeOffset"/>, select for enums and for any column with options.</summary>
    public SpreadsheetColumnKind Kind { get; internal set; }
    /// <summary>Additional header spellings that match this column, on top of <see cref="Key"/> and <see cref="EffectiveLabel"/>.</summary>
    public IReadOnlyList<string>? Headers { get; internal set; }
    /// <summary>Help text for your own UI and for the built-in component, which shows it under the column name.</summary>
    public string? Description { get; internal set; }
    /// <summary>A sample value written in the second row of <see cref="SpreadsheetImporter.ExportTemplateCsv"/>.</summary>
    public string? Example { get; internal set; }
    /// <summary>A grouping label for the built-in component, which groups the column mapping by it.</summary>
    public string? Group { get; internal set; }
    /// <summary>The <see cref="Key"/> of an earlier column whose cell text this column reuses instead of reading its own file column. A column with <see cref="From"/> never matches a header and is left out of the template.</summary>
    public string? From { get; internal set; }
    /// <summary>When <see langword="true"/>, an empty cell is a blocking issue (code <c>required</c>) and a file with no matching column cannot be submitted, unless the column has a default. A column whose property type is a non-nullable value type (<see cref="int"/>, <see cref="bool"/>, an enum) is always required, because an empty cell cannot be stored in it.</summary>
    public bool Required { get; internal set; }
    /// <summary>Whether the built-in component lets the user edit this column's cells in the review table. <see cref="SpreadsheetImporter.EditCell"/> ignores it.</summary>
    public bool Editable { get; internal set; } = true;
    /// <summary>Whether the column has a default for empty cells. A column with a default is never missing.</summary>
    public bool HasDefault { get; internal set; }
    /// <summary>The decimal mark of <see cref="SpreadsheetColumnKind.Number"/> cells, <c>.</c> or <c>,</c>. The other character is accepted as a thousands separator only between groups of three digits, so <c>12,50</c> is rejected when the mark is <c>.</c>.</summary>
    public char DecimalSeparator { get; internal set; } = '.';
    /// <summary>Exact <see cref="DateTime"/> format strings accepted for <see cref="SpreadsheetColumnKind.Date"/> cells. When empty, the defaults are <c>yyyy-MM-dd</c>, <c>yyyy-MM-dd HH:mm</c>, <c>yyyy-MM-dd HH:mm:ss</c>, <c>dd/MM/yyyy</c> and <c>dd/MM/yyyy HH:mm</c>.</summary>
    public IReadOnlyList<string>? DateFormats { get; internal set; }
    /// <summary>The allowed values of a <see cref="SpreadsheetColumnKind.Select"/> column, as text. For an enum column without explicit options, one per member.</summary>
    public IReadOnlyList<SpreadsheetOption>? Options { get; internal set; }
    /// <summary>What to do with select cells that match no option. Defaults to <see cref="SpreadsheetUnknownValuePolicy.Ask"/>.</summary>
    public SpreadsheetUnknownValuePolicy UnknownPolicy { get; internal set; } = SpreadsheetUnknownValuePolicy.Ask;
    /// <summary>Whether the column creates new options: it has an <see cref="SpreadsheetColumnBuilder{TRow, TValue}.OnCreateOption"/> or <see cref="SpreadsheetColumnBuilder{TRow, TValue}.OnCreateOptionAsync"/> callback, so the component offers a create choice (<see cref="SpreadsheetValueQuestion.CanCreate"/>).</summary>
    public bool CanCreateOption { get; internal set; }
    /// <summary>Validation rules run, in order, on every non-empty parsed value.</summary>
    public IReadOnlyList<SpreadsheetRule>? Rules { get; internal set; }

    /// <summary><see cref="Label"/> when set, otherwise <see cref="Key"/> turned into words, for example <c>SecureCode</c> becomes <c>Secure code</c>.</summary>
    public string EffectiveLabel => Label ?? SpreadsheetImporter.HumanizeKey(Key);

    internal object? DefaultBoxed { get; set; }
    internal abstract bool HasParser { get; }
    internal abstract bool TryParseCustom(string text, out object? value, out string? error);
    internal abstract bool TryConvert(object? engineValue, string cellText, out object? typed, out string? error);
    internal abstract object? ConvertLoose(object? engineValue);
    internal abstract Task<SpreadsheetOption?> CreateOptionAsync(string rawValue, CancellationToken cancellationToken);
    internal abstract void Assign(object row, object? boxedValue);
}

/// <summary>A column of a row type, with the value type erased. It is the type the schema stores.</summary>
/// <typeparam name="TRow">The row type the column belongs to.</typeparam>
public abstract class SpreadsheetColumn<TRow> : SpreadsheetColumnSchema where TRow : class
{
    internal SpreadsheetColumn(string key, Type valueType) : base(key, valueType)
    {
    }
}

/// <summary>A column bound to one property of a row type. The kind of cell it reads follows <typeparamref name="TValue"/>.</summary>
/// <typeparam name="TRow">The row type the column belongs to.</typeparam>
/// <typeparam name="TValue">The type of the row property.</typeparam>
public sealed class SpreadsheetColumn<TRow, TValue> : SpreadsheetColumn<TRow> where TRow : class
{
    private static readonly Type Under = SpreadsheetValueConverter.Underlying(typeof(TValue));

    private readonly Action<TRow, TValue> _setter;
    private readonly SpreadsheetParser<TValue>? _parser;
    private readonly Func<string, SpreadsheetOption<TValue?>>? _createOption;
    private readonly Func<string, CancellationToken, Task<SpreadsheetOption<TValue?>>>? _createOptionAsync;

    internal SpreadsheetColumn(
        string key,
        Action<TRow, TValue> setter,
        SpreadsheetParser<TValue>? parser,
        Func<string, SpreadsheetOption<TValue?>>? createOption,
        Func<string, CancellationToken, Task<SpreadsheetOption<TValue?>>>? createOptionAsync)
        : base(key, typeof(TValue))
    {
        _setter = setter;
        _parser = parser;
        _createOption = createOption;
        _createOptionAsync = createOptionAsync;
    }

    /// <summary>The value used for empty cells when <see cref="SpreadsheetColumnSchema.HasDefault"/> is <see langword="true"/>.</summary>
    public TValue? DefaultValue { get; internal set; }

    internal override bool HasParser => _parser is not null;

    internal override bool TryParseCustom(string text, out object? value, out string? error)
    {
        try
        {
            var result = _parser!(text);
            value = result.IsOk ? result.Value : null;
            error = result.IsOk ? null : result.Error ?? "Invalid value";
            return result.IsOk;
        }
        catch (Exception ex)
        {
            value = null;
            error = ex.Message;
            return false;
        }
    }

    internal override bool TryConvert(object? engineValue, string cellText, out object? typed, out string? error) =>
        SpreadsheetValueConverter.TryConvert(Under, Kind, engineValue, cellText, out typed, out error);

    internal override object? ConvertLoose(object? engineValue) => SpreadsheetValueConverter.Loose(Under, engineValue);

    internal override async Task<SpreadsheetOption?> CreateOptionAsync(string rawValue, CancellationToken cancellationToken)
    {
        SpreadsheetOption<TValue?>? option;
        if (_createOptionAsync is not null) option = await _createOptionAsync(rawValue, cancellationToken).ConfigureAwait(false);
        else if (_createOption is not null) option = _createOption(rawValue);
        else return null;
        return option is null ? null : new SpreadsheetOption(SpreadsheetValueConverter.Format(option.Value), option.Label, option.Aliases);
    }

    // A value the column cannot hold (a null for a non-nullable property, a pending created option that is not a TValue yet) leaves the property at its default.
    internal override void Assign(object row, object? boxedValue)
    {
        if (boxedValue is TValue typed) _setter((TRow)row, typed);
    }
}

/// <summary>Configures one column. Methods return the builder so calls chain: <c>c =&gt; c.Required().Headers("mail")</c>. Settings that only make sense for some value types are extension methods on this type, so they do not compile on the others.</summary>
/// <typeparam name="TRow">The row type the column belongs to.</typeparam>
/// <typeparam name="TValue">The type of the row property.</typeparam>
public sealed class SpreadsheetColumnBuilder<TRow, TValue> where TRow : class
{
    internal string? KeyOverride;
    internal string? LabelText;
    internal IReadOnlyList<string>? HeaderTexts;
    internal string? DescriptionText;
    internal string? ExampleText;
    internal string? GroupText;
    internal string? FromKey;
    internal bool IsRequired;
    internal bool IsEditable = true;
    internal bool HasDefaultValue;
    internal TValue? DefaultValue;
    internal char Separator = '.';
    internal IReadOnlyList<string>? Formats;
    internal IReadOnlyList<SpreadsheetOption<TValue?>>? OptionList;
    internal SpreadsheetUnknownValuePolicy Policy = SpreadsheetUnknownValuePolicy.Ask;
    internal readonly List<SpreadsheetRule> RuleList = [];
    internal SpreadsheetParser<TValue>? ParserDelegate;
    internal Func<string, SpreadsheetOption<TValue?>>? CreateOptionDelegate;
    internal Func<string, CancellationToken, Task<SpreadsheetOption<TValue?>>>? CreateOptionAsyncDelegate;

    internal SpreadsheetColumnBuilder()
    {
    }

    /// <summary>Overrides the key, which is the property name by default. The key is what a saved mapping profile stores, so set it to keep a profile valid when you rename the property.</summary>
    /// <param name="key">The new key.</param>
    /// <returns>This builder.</returns>
    public SpreadsheetColumnBuilder<TRow, TValue> Key(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        KeyOverride = key;
        return this;
    }

    /// <summary>Sets the display name, also matched against file headers.</summary>
    /// <param name="label">The label.</param>
    /// <returns>This builder.</returns>
    public SpreadsheetColumnBuilder<TRow, TValue> Label(string label) { LabelText = label; return this; }

    /// <summary>Adds header spellings that match this column, on top of the key and the label.</summary>
    /// <param name="headers">The extra headers.</param>
    /// <returns>This builder.</returns>
    public SpreadsheetColumnBuilder<TRow, TValue> Headers(params string[] headers) { HeaderTexts = headers; return this; }

    /// <summary>Sets help text shown under the column name.</summary>
    /// <param name="description">The text.</param>
    /// <returns>This builder.</returns>
    public SpreadsheetColumnBuilder<TRow, TValue> Description(string description) { DescriptionText = description; return this; }

    /// <summary>Sets the sample value written in the second row of the CSV template.</summary>
    /// <param name="example">The sample text.</param>
    /// <returns>This builder.</returns>
    public SpreadsheetColumnBuilder<TRow, TValue> Example(string example) { ExampleText = example; return this; }

    /// <summary>Sets the grouping label the component uses in the column mapping.</summary>
    /// <param name="group">The group label.</param>
    /// <returns>This builder.</returns>
    public SpreadsheetColumnBuilder<TRow, TValue> Group(string group) { GroupText = group; return this; }

    /// <summary>Makes the column reuse the cell text of an earlier column instead of reading its own file column. Such a column never matches a header and is left out of the template.</summary>
    /// <param name="source">A column declared earlier in the same schema.</param>
    /// <returns>This builder.</returns>
    public SpreadsheetColumnBuilder<TRow, TValue> From(SpreadsheetColumnSchema source)
    {
        ArgumentNullException.ThrowIfNull(source);
        FromKey = source.Key;
        return this;
    }

    /// <summary>Makes an empty cell a blocking issue and a missing file column a blocker, unless the column has a default.</summary>
    /// <param name="required">Pass <see langword="false"/> to undo it.</param>
    /// <returns>This builder.</returns>
    public SpreadsheetColumnBuilder<TRow, TValue> Required(bool required = true) { IsRequired = required; return this; }

    /// <summary>Sets whether the component lets the user edit this column's cells in the review table.</summary>
    /// <param name="editable">Whether the cells are editable.</param>
    /// <returns>This builder.</returns>
    public SpreadsheetColumnBuilder<TRow, TValue> Editable(bool editable) { IsEditable = editable; return this; }

    /// <summary>Sets the value used for empty cells. A column with a default is never missing. For a nullable column the default may itself be <see langword="null"/>.</summary>
    /// <param name="value">The default value.</param>
    /// <returns>This builder.</returns>
    public SpreadsheetColumnBuilder<TRow, TValue> Default(TValue value)
    {
        DefaultValue = value;
        HasDefaultValue = true;
        return this;
    }

    /// <summary>Adds a validation rule, run on every non-empty parsed value in the order the rules were added.</summary>
    /// <param name="rule">The rule, typed like the column.</param>
    /// <returns>This builder.</returns>
    public SpreadsheetColumnBuilder<TRow, TValue> Rule(SpreadsheetRule<TValue?> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        RuleList.Add(rule);
        return this;
    }

    /// <summary>Adds a validation rule written as a predicate.</summary>
    /// <param name="code">A stable identifier copied to <see cref="SpreadsheetRowIssue.Code"/>.</param>
    /// <param name="message">The issue text shown when the predicate returns <see langword="false"/>.</param>
    /// <param name="validate">Receives the parsed value and returns whether it is acceptable.</param>
    /// <param name="level">The severity. Only an error blocks the row.</param>
    /// <returns>This builder.</returns>
    public SpreadsheetColumnBuilder<TRow, TValue> Rule(string code, string message, Func<TValue, bool> validate, SpreadsheetIssueLevel level = SpreadsheetIssueLevel.Error) =>
        Rule(new SpreadsheetRule<TValue?>(code, message, v => v is not null && validate(v), level));

    /// <summary>Replaces the built-in parsing of a non-select column with your own.</summary>
    /// <param name="parser">Reads the cell text into a value or fails with a message.</param>
    /// <returns>This builder.</returns>
    public SpreadsheetColumnBuilder<TRow, TValue> Parser(SpreadsheetParser<TValue> parser)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ParserDelegate = parser;
        return this;
    }

    /// <summary>Makes the column a select with these allowed values. Cells that match none become value questions governed by <see cref="UnknownPolicy"/>.</summary>
    /// <param name="options">The allowed values. Their values must be a <see cref="string"/>, an enum, a number, a <see cref="bool"/> or a <see cref="Guid"/>.</param>
    /// <returns>This builder.</returns>
    public SpreadsheetColumnBuilder<TRow, TValue> Options(IEnumerable<SpreadsheetOption<TValue?>> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        OptionList = options.ToArray();
        return this;
    }

    /// <summary>Sets what happens to select cells that match no option.</summary>
    /// <param name="policy">The policy.</param>
    /// <returns>This builder.</returns>
    public SpreadsheetColumnBuilder<TRow, TValue> UnknownPolicy(SpreadsheetUnknownValuePolicy policy) { Policy = policy; return this; }

    /// <summary>Called once per distinct value answered with <see cref="SpreadsheetValueAnswerAction.Create"/>, when the import is submitted and before any row is sent. Use it to persist the new option. The rows then carry the returned option's value and later sends do not call it again for that value. Setting it makes the component offer a create choice.</summary>
    /// <param name="create">Receives the text of the new value and returns the option.</param>
    /// <returns>This builder.</returns>
    public SpreadsheetColumnBuilder<TRow, TValue> OnCreateOption(Func<string, SpreadsheetOption<TValue?>> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        CreateOptionDelegate = create;
        return this;
    }

    /// <summary>The asynchronous form of <see cref="OnCreateOption"/>, for a callback that calls a server. It receives the cancellation token of the submit. When both are set, this one is used.</summary>
    /// <param name="create">Receives the text of the new value and the token, and returns the option.</param>
    /// <returns>This builder.</returns>
    public SpreadsheetColumnBuilder<TRow, TValue> OnCreateOptionAsync(Func<string, CancellationToken, Task<SpreadsheetOption<TValue?>>> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        CreateOptionAsyncDelegate = create;
        return this;
    }
}

/// <summary>Settings that exist only for some column value types.</summary>
public static class SpreadsheetColumnBuilderExtensions
{
    /// <summary>Sets the decimal mark of a number column, <c>.</c> or <c>,</c>. The other character is accepted as a thousands separator only between groups of three digits.</summary>
    /// <typeparam name="TRow">The row type.</typeparam>
    /// <typeparam name="TValue">The number type of the column.</typeparam>
    /// <param name="builder">The column builder.</param>
    /// <param name="separator">The decimal mark.</param>
    /// <returns>The builder.</returns>
    public static SpreadsheetColumnBuilder<TRow, TValue> DecimalSeparator<TRow, TValue>(this SpreadsheetColumnBuilder<TRow, TValue> builder, char separator)
        where TRow : class where TValue : struct, INumber<TValue>
    {
        builder.Separator = separator;
        return builder;
    }

    /// <summary>Sets the decimal mark of a nullable number column, <c>.</c> or <c>,</c>.</summary>
    /// <typeparam name="TRow">The row type.</typeparam>
    /// <typeparam name="TValue">The underlying number type of the column.</typeparam>
    /// <param name="builder">The column builder.</param>
    /// <param name="separator">The decimal mark.</param>
    /// <returns>The builder.</returns>
    public static SpreadsheetColumnBuilder<TRow, TValue?> DecimalSeparator<TRow, TValue>(this SpreadsheetColumnBuilder<TRow, TValue?> builder, char separator)
        where TRow : class where TValue : struct, INumber<TValue>
    {
        builder.Separator = separator;
        return builder;
    }

    /// <summary>Sets the exact formats a <see cref="DateOnly"/> column reads, instead of the defaults.</summary>
    /// <typeparam name="TRow">The row type.</typeparam>
    /// <param name="builder">The column builder.</param>
    /// <param name="formats">Exact <see cref="DateTime"/> format strings.</param>
    /// <returns>The builder.</returns>
    public static SpreadsheetColumnBuilder<TRow, DateOnly> DateFormats<TRow>(this SpreadsheetColumnBuilder<TRow, DateOnly> builder, params string[] formats) where TRow : class
    {
        builder.Formats = formats;
        return builder;
    }

    /// <summary>Sets the exact formats a nullable <see cref="DateOnly"/> column reads, instead of the defaults.</summary>
    /// <typeparam name="TRow">The row type.</typeparam>
    /// <param name="builder">The column builder.</param>
    /// <param name="formats">Exact <see cref="DateTime"/> format strings.</param>
    /// <returns>The builder.</returns>
    public static SpreadsheetColumnBuilder<TRow, DateOnly?> DateFormats<TRow>(this SpreadsheetColumnBuilder<TRow, DateOnly?> builder, params string[] formats) where TRow : class
    {
        builder.Formats = formats;
        return builder;
    }

    /// <summary>Sets the exact formats a <see cref="DateTime"/> column reads, instead of the defaults.</summary>
    /// <typeparam name="TRow">The row type.</typeparam>
    /// <param name="builder">The column builder.</param>
    /// <param name="formats">Exact <see cref="DateTime"/> format strings.</param>
    /// <returns>The builder.</returns>
    public static SpreadsheetColumnBuilder<TRow, DateTime> DateFormats<TRow>(this SpreadsheetColumnBuilder<TRow, DateTime> builder, params string[] formats) where TRow : class
    {
        builder.Formats = formats;
        return builder;
    }

    /// <summary>Sets the exact formats a nullable <see cref="DateTime"/> column reads, instead of the defaults.</summary>
    /// <typeparam name="TRow">The row type.</typeparam>
    /// <param name="builder">The column builder.</param>
    /// <param name="formats">Exact <see cref="DateTime"/> format strings.</param>
    /// <returns>The builder.</returns>
    public static SpreadsheetColumnBuilder<TRow, DateTime?> DateFormats<TRow>(this SpreadsheetColumnBuilder<TRow, DateTime?> builder, params string[] formats) where TRow : class
    {
        builder.Formats = formats;
        return builder;
    }

    /// <summary>Sets the exact formats a <see cref="DateTimeOffset"/> column reads, instead of the defaults. Cells carry no offset: they are read as UTC.</summary>
    /// <typeparam name="TRow">The row type.</typeparam>
    /// <param name="builder">The column builder.</param>
    /// <param name="formats">Exact <see cref="DateTime"/> format strings.</param>
    /// <returns>The builder.</returns>
    public static SpreadsheetColumnBuilder<TRow, DateTimeOffset> DateFormats<TRow>(this SpreadsheetColumnBuilder<TRow, DateTimeOffset> builder, params string[] formats) where TRow : class
    {
        builder.Formats = formats;
        return builder;
    }

    /// <summary>Sets the exact formats a nullable <see cref="DateTimeOffset"/> column reads, instead of the defaults. Cells carry no offset: they are read as UTC.</summary>
    /// <typeparam name="TRow">The row type.</typeparam>
    /// <param name="builder">The column builder.</param>
    /// <param name="formats">Exact <see cref="DateTime"/> format strings.</param>
    /// <returns>The builder.</returns>
    public static SpreadsheetColumnBuilder<TRow, DateTimeOffset?> DateFormats<TRow>(this SpreadsheetColumnBuilder<TRow, DateTimeOffset?> builder, params string[] formats) where TRow : class
    {
        builder.Formats = formats;
        return builder;
    }

    /// <summary>Adds a rule to a nullable column. The rule is typed like the underlying value because it only runs on a cell that holds one.</summary>
    /// <typeparam name="TRow">The row type.</typeparam>
    /// <typeparam name="TValue">The underlying value type of the column.</typeparam>
    /// <param name="builder">The column builder.</param>
    /// <param name="rule">The rule.</param>
    /// <returns>The builder.</returns>
    public static SpreadsheetColumnBuilder<TRow, TValue?> Rule<TRow, TValue>(this SpreadsheetColumnBuilder<TRow, TValue?> builder, SpreadsheetRule<TValue> rule)
        where TRow : class where TValue : struct
    {
        ArgumentNullException.ThrowIfNull(rule);
        builder.RuleList.Add(rule);
        return builder;
    }

    /// <summary>Makes a nullable column a select with these allowed values.</summary>
    /// <typeparam name="TRow">The row type.</typeparam>
    /// <typeparam name="TValue">The underlying value type of the column.</typeparam>
    /// <param name="builder">The column builder.</param>
    /// <param name="options">The allowed values.</param>
    /// <returns>The builder.</returns>
    public static SpreadsheetColumnBuilder<TRow, TValue?> Options<TRow, TValue>(this SpreadsheetColumnBuilder<TRow, TValue?> builder, IEnumerable<SpreadsheetOption<TValue>> options)
        where TRow : class where TValue : struct
    {
        ArgumentNullException.ThrowIfNull(options);
        builder.OptionList = options.Select(o => new SpreadsheetOption<TValue?>(o.Value, o.Label, o.Aliases)).ToArray();
        return builder;
    }
}
