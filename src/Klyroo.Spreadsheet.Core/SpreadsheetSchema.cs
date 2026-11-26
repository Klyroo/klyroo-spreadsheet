using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace Klyroo.Spreadsheet;

/// <summary>The keys a host reported as already stored, in the form the importer reads them.</summary>
internal sealed class ExistingKeys(Func<string, bool> contains, int count)
{
    public int Count { get; } = count;
    public bool Contains(string key) => contains(key);
}

/// <summary>Describes an import: its columns and the policies for limits, duplicates, existing records and submission. Create one with <see cref="For{TRow}(Action{SpreadsheetSchemaBuilder{TRow}})"/> and pass it to <see cref="SpreadsheetImporter{TRow}"/>.</summary>
/// <remarks>This base type is what the importer and the component read: columns and policies, never a typed row. The typed schema is <see cref="SpreadsheetSchema{TRow}"/>, which also holds the callbacks that receive rows.</remarks>
public abstract class SpreadsheetSchema
{
    internal SpreadsheetSchema(
        string key,
        IReadOnlyList<string> accept,
        int? maxRows,
        string? forcedSheet,
        int? forcedHeaderRow,
        IReadOnlyList<SpreadsheetColumnSchema> columns,
        string? rowKeyField,
        SpreadsheetDuplicatesPolicy duplicates,
        SpreadsheetExistingAction existingAction,
        int ruleTimeoutLimit,
        int batchSize,
        int validateBatchSize,
        int lookupBatchSize)
    {
        Key = key;
        Accept = accept;
        MaxRows = maxRows;
        ForcedSheet = forcedSheet;
        ForcedHeaderRow = forcedHeaderRow;
        Columns = columns;
        RowKeyField = rowKeyField;
        Duplicates = duplicates;
        ExistingAction = existingAction;
        RuleTimeoutLimit = ruleTimeoutLimit;
        BatchSize = batchSize;
        ValidateBatchSize = validateBatchSize;
        LookupBatchSize = lookupBatchSize;
    }

    /// <summary>A name for the import. The component uses it to name the downloads and a saved mapping profile is tied to it.</summary>
    public string Key { get; }
    /// <summary>The file extensions the built-in component lists in its drop zone. They are display text: the importer enforces nothing, and <see cref="SpreadsheetImporter.LoadFileAsync"/> reads <c>.xlsx</c> and <c>.xls</c> workbooks and delimited text whatever this list says.</summary>
    public IReadOnlyList<string> Accept { get; }
    /// <summary>The maximum number of data rows to import. Later rows are left out with <see cref="SpreadsheetDiscardReason.Limit"/>. No limit when <see langword="null"/>.</summary>
    public int? MaxRows { get; }
    /// <summary>The name of the sheet to read, which skips sheet detection. <see langword="null"/>, empty or <c>auto</c> detects the sheet; an unknown name falls back to the first sheet.</summary>
    public string? ForcedSheet { get; }
    /// <summary>The 1-based file row number of the header row, which skips header detection. A number that matches no row selects the first row.</summary>
    public int? ForcedHeaderRow { get; }
    /// <summary>The columns to import, in display order. A column with <see cref="SpreadsheetColumnSchema.From"/> comes after its source.</summary>
    public IReadOnlyList<SpreadsheetColumnSchema> Columns { get; }
    /// <summary>The <see cref="SpreadsheetColumnSchema.Key"/> of the column that identifies a row. It enables the duplicate and existing-record checks.</summary>
    public string? RowKeyField { get; }
    /// <summary>How rows that repeat a key within the file are handled. Keys are compared trimmed and case-insensitively. Needs <see cref="RowKeyField"/>.</summary>
    public SpreadsheetDuplicatesPolicy Duplicates { get; }
    /// <summary>What to do with rows whose key is in the existing records.</summary>
    public SpreadsheetExistingAction ExistingAction { get; }
    /// <summary>How many match timeouts one rule may cause in a file before the importer stops evaluating it (a circuit breaker, so a hostile file cannot cost one <see cref="SpreadsheetRule.DefaultPatternTimeout"/> per cell). The rule is then listed once in <see cref="SpreadsheetImporter.DisabledRules"/> and the component shows a notice. Zero or less never stops a rule.</summary>
    public int RuleTimeoutLimit { get; }
    /// <summary>The number of rows passed to the submit callback per call. Zero or less sends all rows in one call.</summary>
    public int BatchSize { get; }
    /// <summary>The number of rows passed to the batched row check per call. Zero or less sends all rows in one call.</summary>
    public int ValidateBatchSize { get; }
    /// <summary>The number of keys passed to the existing-record lookup per call. Zero or less sends all keys in one call.</summary>
    public int LookupBatchSize { get; }

    internal abstract IReadOnlyList<SpreadsheetRowIssue>? CheckRow(IReadOnlyDictionary<string, object?> data);
    internal abstract bool HasSubmit { get; }
    internal abstract Task<IReadOnlyList<SpreadsheetRejectedRow>?> SubmitBatchAsync(IReadOnlyList<SpreadsheetRow> batch, CancellationToken cancellationToken);
    internal abstract bool HasValidateRowsAsync { get; }
    internal abstract Task<IReadOnlyList<SpreadsheetIndexedIssue>?> ValidateRowsBatchAsync(IReadOnlyList<SpreadsheetRow> batch, CancellationToken cancellationToken);
    internal abstract bool HasLookupExisting { get; }
    internal abstract Task<ExistingKeys?> LookupExistingAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken);
    internal abstract bool IsStoredKey(string key);

    /// <summary>Creates a schema for a row type. Declare each column with <see cref="SpreadsheetSchemaBuilder{TRow}.Column{TValue}"/>.</summary>
    /// <typeparam name="TRow">The row class. The importer creates one instance per row with its public parameterless constructor and sets the column properties on it.</typeparam>
    /// <param name="configure">Declares the columns and policies.</param>
    /// <returns>The immutable schema, safe to share between importers.</returns>
    /// <exception cref="ArgumentException">A column is declared twice, a <see cref="SpreadsheetColumnSchema.From"/> source is not declared before its column, or a column type has no reader and no parser.</exception>
    public static SpreadsheetSchema<TRow> For<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TRow>(Action<SpreadsheetSchemaBuilder<TRow>> configure)
        where TRow : class, new()
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new SpreadsheetSchemaBuilder<TRow>(static () => new TRow());
        configure(builder);
        return builder.Build();
    }

    /// <summary>Creates a schema for a row type that has no public parameterless constructor, for example a type with <see langword="required"/> members.</summary>
    /// <typeparam name="TRow">The row class.</typeparam>
    /// <param name="factory">Creates an empty row. The importer then sets the column properties on it.</param>
    /// <param name="configure">Declares the columns and policies.</param>
    /// <returns>The immutable schema, safe to share between importers.</returns>
    /// <exception cref="ArgumentException">A column is declared twice, a <see cref="SpreadsheetColumnSchema.From"/> source is not declared before its column, or a column type has no reader and no parser.</exception>
    public static SpreadsheetSchema<TRow> For<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TRow>(Func<TRow> factory, Action<SpreadsheetSchemaBuilder<TRow>> configure)
        where TRow : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new SpreadsheetSchemaBuilder<TRow>(factory);
        configure(builder);
        return builder.Build();
    }
}

/// <summary>A schema bound to a row type: the columns map to its properties, and the callbacks receive its rows.</summary>
/// <typeparam name="TRow">The row class.</typeparam>
public sealed class SpreadsheetSchema<TRow> : SpreadsheetSchema where TRow : class
{
    private readonly Func<TRow> _factory;
    private readonly SpreadsheetColumn<TRow>[] _typedColumns;

    internal SpreadsheetSchema(SpreadsheetSchemaSettings<TRow> s, SpreadsheetColumn<TRow>[] columns)
        : base(s.Key, s.Accept, s.MaxRows, s.ForcedSheet, s.ForcedHeaderRow, columns, s.RowKeyField, s.Duplicates, s.ExistingAction,
            s.RuleTimeoutLimit, s.BatchSize, s.ValidateBatchSize, s.LookupBatchSize)
    {
        _factory = s.Factory;
        _typedColumns = columns;
        ExistingRecords = s.ExistingRecords;
        ValidateRow = s.ValidateRow;
        OnSubmit = s.OnSubmit;
        OnSubmitAsync = s.OnSubmitAsync;
        ValidateRowsAsync = s.ValidateRowsAsync;
        LookupExistingRecordsAsync = s.LookupExistingRecordsAsync;
    }

    /// <summary>Records that already exist, keyed by the row key text (invariant: a <see cref="DateOnly"/> is <c>yyyy-MM-dd</c>, a <see cref="DateTime"/> <c>yyyy-MM-ddTHH:mm</c> or with seconds, a <see cref="DateTimeOffset"/> the same for its UTC instant, a <see cref="decimal"/> without trailing zeros). A row matches when its key text equals a dictionary key under the dictionary's own comparer, with no trimming or case folding applied to the row, so build the dictionary with <see cref="StringComparer.OrdinalIgnoreCase"/> for case-insensitive keys. Needs a row key.</summary>
    public IReadOnlyDictionary<string, TRow>? ExistingRecords { get; }
    /// <summary>A cross-field check run on every row after the column rules. It receives the parsed row (a cell that is empty or could not be read leaves its property at the default) and returns extra issues, which are added to the row.</summary>
    public Func<TRow, IReadOnlyList<SpreadsheetRowIssue>>? ValidateRow { get; }
    /// <summary>Receives each batch of importable rows and returns the rows the server refused, or <see langword="null"/> or an empty list when all were accepted. If it throws, the submit stops with <see cref="SpreadsheetSubmitStatus.Error"/>.</summary>
    public Func<IReadOnlyList<SpreadsheetRow<TRow>>, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>? OnSubmit { get; }
    /// <summary>The cancellable form of <see cref="OnSubmit"/>: it also receives a <see cref="CancellationToken"/> that is cancelled by <see cref="SpreadsheetImporter.CancelSubmit"/> (the Cancel button of the component) or by the token passed to <see cref="SpreadsheetImporter.SubmitAsync"/>. A batch that throws <see cref="OperationCanceledException"/> counts as not accepted, so make the call safe to repeat. When both are set, this one is used.</summary>
    public Func<IReadOnlyList<SpreadsheetRow<TRow>>, CancellationToken, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>? OnSubmitAsync { get; }
    /// <summary>A batched check that needs the host, for example "do these SKUs exist". It receives rows that are not discarded and have no error, <see cref="SpreadsheetSchema.ValidateBatchSize"/> at a time, and returns the issues it found with the index of their row, or <see langword="null"/> or an empty list for none. See <see cref="SpreadsheetImporter.ValidateAsync"/> for when it runs.</summary>
    public Func<IReadOnlyList<SpreadsheetRow<TRow>>, CancellationToken, Task<IReadOnlyList<SpreadsheetIndexedIssue>?>>? ValidateRowsAsync { get; }
    /// <summary>Looks up existing records by key on demand, instead of loading them all into <see cref="ExistingRecords"/>. It receives the distinct row key texts of the file, <see cref="SpreadsheetSchema.LookupBatchSize"/> at a time, and returns the records found by key (keys it does not return are new). A row matches when its key text equals a returned key under the dictionary's own comparer, like <see cref="ExistingRecords"/>. Needs a row key. See <see cref="SpreadsheetImporter.ValidateAsync"/> for when it runs.</summary>
    public Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyDictionary<string, TRow>>>? LookupExistingRecordsAsync { get; }

    /// <summary>Creates a row and fills its properties from the values the importer read.</summary>
    internal TRow CreateValue(IReadOnlyDictionary<string, object?> data)
    {
        var row = _factory();
        foreach (var column in _typedColumns)
        {
            if (data.TryGetValue(column.Key, out var value)) column.Assign(row, value);
        }
        return row;
    }

    // A row the host's setters or factory refuse already carries a row.failed issue and is not importable; asking for its typed value fails with the cause.
    internal SpreadsheetRow<TRow> ToTyped(SpreadsheetRow row)
    {
        if (row is SpreadsheetRow<TRow> typed) return typed;
        try
        {
            return new SpreadsheetRow<TRow>(row, CreateValue(row.Data));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"The typed value of row {row.RowNumber} cannot be built: {ex.Message}", ex);
        }
    }

    private IReadOnlyList<SpreadsheetRow<TRow>> ToTyped(IReadOnlyList<SpreadsheetRow> rows)
    {
        var typed = new SpreadsheetRow<TRow>[rows.Count];
        for (var i = 0; i < typed.Length; i++) typed[i] = ToTyped(rows[i]);
        return typed;
    }

    // Builds the row the host will receive, so a factory or a setter that throws is found while the file is read, then runs ValidateRow. An exception
    // of the host becomes an error issue on the row (code row.failed) instead of failing the load.
    internal override IReadOnlyList<SpreadsheetRowIssue>? CheckRow(IReadOnlyDictionary<string, object?> data)
    {
        try
        {
            var value = CreateValue(data);
            return ValidateRow is { } validate ? validate(value) : null;
        }
        catch (Exception ex)
        {
            return [new SpreadsheetRowIssue(null, "row.failed", SpreadsheetIssueLevel.Error, ex.Message)];
        }
    }

    internal override bool HasSubmit => OnSubmitAsync is not null || OnSubmit is not null;

    internal override Task<IReadOnlyList<SpreadsheetRejectedRow>?> SubmitBatchAsync(IReadOnlyList<SpreadsheetRow> batch, CancellationToken cancellationToken)
    {
        if (OnSubmitAsync is { } submitAsync) return submitAsync(ToTyped(batch), cancellationToken);
        if (OnSubmit is { } submit) return submit(ToTyped(batch));
        return Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>(null);
    }

    internal override bool HasValidateRowsAsync => ValidateRowsAsync is not null;

    internal override Task<IReadOnlyList<SpreadsheetIndexedIssue>?> ValidateRowsBatchAsync(IReadOnlyList<SpreadsheetRow> batch, CancellationToken cancellationToken) =>
        ValidateRowsAsync is { } validate ? validate(ToTyped(batch), cancellationToken) : Task.FromResult<IReadOnlyList<SpreadsheetIndexedIssue>?>(null);

    internal override bool HasLookupExisting => LookupExistingRecordsAsync is not null;

    internal override async Task<ExistingKeys?> LookupExistingAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        if (LookupExistingRecordsAsync is not { } lookup) return null;
        var found = await lookup(keys, cancellationToken).ConfigureAwait(false);
        return found is null ? null : new ExistingKeys(found.ContainsKey, found.Count);
    }

    internal override bool IsStoredKey(string key) => ExistingRecords?.ContainsKey(key) == true;
}

/// <summary>Everything a schema is built from, collected by <see cref="SpreadsheetSchemaBuilder{TRow}"/>.</summary>
internal sealed class SpreadsheetSchemaSettings<TRow> where TRow : class
{
    public required Func<TRow> Factory { get; init; }
    public required string Key { get; init; }
    public required IReadOnlyList<string> Accept { get; init; }
    public int? MaxRows { get; init; }
    public string? ForcedSheet { get; init; }
    public int? ForcedHeaderRow { get; init; }
    public string? RowKeyField { get; init; }
    public SpreadsheetDuplicatesPolicy Duplicates { get; init; }
    public SpreadsheetExistingAction ExistingAction { get; init; }
    public int RuleTimeoutLimit { get; init; }
    public int BatchSize { get; init; }
    public int ValidateBatchSize { get; init; }
    public int LookupBatchSize { get; init; }
    public IReadOnlyDictionary<string, TRow>? ExistingRecords { get; init; }
    public Func<TRow, IReadOnlyList<SpreadsheetRowIssue>>? ValidateRow { get; init; }
    public Func<IReadOnlyList<SpreadsheetRow<TRow>>, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>? OnSubmit { get; init; }
    public Func<IReadOnlyList<SpreadsheetRow<TRow>>, CancellationToken, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>? OnSubmitAsync { get; init; }
    public Func<IReadOnlyList<SpreadsheetRow<TRow>>, CancellationToken, Task<IReadOnlyList<SpreadsheetIndexedIssue>?>>? ValidateRowsAsync { get; init; }
    public Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyDictionary<string, TRow>>>? LookupExistingRecordsAsync { get; init; }
}

/// <summary>Declares the columns and policies of a <see cref="SpreadsheetSchema{TRow}"/>. Columns are declared in display order.</summary>
/// <typeparam name="TRow">The row class.</typeparam>
public sealed class SpreadsheetSchemaBuilder<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TRow> where TRow : class
{
    private readonly Func<TRow> _factory;
    private readonly List<SpreadsheetColumn<TRow>> _columns = [];
    private string? _rowKey;

    internal SpreadsheetSchemaBuilder(Func<TRow> factory) => _factory = factory;

    /// <summary>A name for the import. The component uses it to name the downloads and a saved mapping profile is tied to it. Defaults to <c>import</c>.</summary>
    public string Key { get; set; } = "import";
    /// <summary>The file extensions the built-in component lists in its drop zone. They are display text: the importer enforces nothing.</summary>
    public IReadOnlyList<string> Accept { get; set; } = [".xlsx", ".xls", ".csv", ".tsv"];
    /// <summary>The maximum number of data rows to import. Later rows are left out with <see cref="SpreadsheetDiscardReason.Limit"/>. No limit when <see langword="null"/>.</summary>
    public int? MaxRows { get; set; }
    /// <summary>The name of the sheet to read, which skips sheet detection.</summary>
    public string? ForcedSheet { get; set; }
    /// <summary>The 1-based file row number of the header row, which skips header detection.</summary>
    public int? ForcedHeaderRow { get; set; }
    /// <summary>How rows that repeat a key within the file are handled. Needs <see cref="RowKey"/>. Defaults to <see cref="SpreadsheetDuplicatesPolicy.Error"/>.</summary>
    public SpreadsheetDuplicatesPolicy Duplicates { get; set; } = SpreadsheetDuplicatesPolicy.Error;
    /// <summary>What to do with rows whose key is in <see cref="ExistingRecords"/>. Defaults to <see cref="SpreadsheetExistingAction.Update"/>.</summary>
    public SpreadsheetExistingAction ExistingAction { get; set; } = SpreadsheetExistingAction.Update;
    /// <summary>How many match timeouts one rule may cause in a file before the importer stops evaluating it. Defaults to 3. Zero or less never stops a rule.</summary>
    public int RuleTimeoutLimit { get; set; } = 3;
    /// <summary>The number of rows passed to <see cref="OnSubmit"/> per call. Zero or less sends all rows in one call.</summary>
    public int BatchSize { get; set; }
    /// <summary>The number of rows passed to <see cref="ValidateRowsAsync"/> per call. Zero or less sends all rows in one call. Defaults to 500.</summary>
    public int ValidateBatchSize { get; set; } = 500;
    /// <summary>The number of keys passed to <see cref="LookupExistingRecordsAsync"/> per call. Zero or less sends all keys in one call. Defaults to 500.</summary>
    public int LookupBatchSize { get; set; } = 500;
    /// <summary>Records that already exist, keyed by the row key text. Needs <see cref="RowKey"/>. See <see cref="SpreadsheetSchema{TRow}.ExistingRecords"/>.</summary>
    public IReadOnlyDictionary<string, TRow>? ExistingRecords { get; set; }
    /// <summary>A cross-field check run on every row after the column rules. See <see cref="SpreadsheetSchema{TRow}.ValidateRow"/>.</summary>
    public Func<TRow, IReadOnlyList<SpreadsheetRowIssue>>? ValidateRow { get; set; }
    /// <summary>Receives each batch of importable rows. See <see cref="SpreadsheetSchema{TRow}.OnSubmit"/>.</summary>
    public Func<IReadOnlyList<SpreadsheetRow<TRow>>, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>? OnSubmit { get; set; }
    /// <summary>The cancellable form of <see cref="OnSubmit"/>. See <see cref="SpreadsheetSchema{TRow}.OnSubmitAsync"/>.</summary>
    public Func<IReadOnlyList<SpreadsheetRow<TRow>>, CancellationToken, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>? OnSubmitAsync { get; set; }
    /// <summary>A batched check that needs the host. See <see cref="SpreadsheetSchema{TRow}.ValidateRowsAsync"/>.</summary>
    public Func<IReadOnlyList<SpreadsheetRow<TRow>>, CancellationToken, Task<IReadOnlyList<SpreadsheetIndexedIssue>?>>? ValidateRowsAsync { get; set; }
    /// <summary>Looks up existing records by key on demand. See <see cref="SpreadsheetSchema{TRow}.LookupExistingRecordsAsync"/>.</summary>
    public Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyDictionary<string, TRow>>>? LookupExistingRecordsAsync { get; set; }

    /// <summary>Declares a column bound to a property of the row. Its kind follows the property type: text for <see cref="string"/>, number for numeric types, boolean for <see cref="bool"/>, date for <see cref="DateOnly"/>, <see cref="DateTime"/> and <see cref="DateTimeOffset"/>, select for an enum. A nullable type reads the same way and stores <see langword="null"/> for an empty cell. Other types need <see cref="SpreadsheetColumnBuilder{TRow, TValue}.Parser"/>.</summary>
    /// <typeparam name="TValue">The type of the property.</typeparam>
    /// <param name="member">The property, as <c>r =&gt; r.Email</c>. The expression is read, never compiled.</param>
    /// <param name="configure">Sets the label, headers, rules and the other options of the column.</param>
    /// <returns>The column, to pass to <see cref="RowKey"/> or <see cref="SpreadsheetColumnBuilder{TRow, TValue}.From"/>.</returns>
    /// <exception cref="ArgumentException">The expression is not a plain property access, the property has no public setter, the key is already used, or the type needs a parser.</exception>
    public SpreadsheetColumn<TRow, TValue> Column<TValue>(Expression<Func<TRow, TValue>> member, Action<SpreadsheetColumnBuilder<TRow, TValue>>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (member.Body is not MemberExpression { Member: PropertyInfo property, Expression: ParameterExpression } || property.SetMethod is not { IsPublic: true } setMethod)
        {
            throw new ArgumentException("A column is declared with a property access of the row, like r => r.Email, and the property needs a public setter.", nameof(member));
        }

        var builder = new SpreadsheetColumnBuilder<TRow, TValue>();
        configure?.Invoke(builder);

        var key = builder.KeyOverride ?? property.Name;
        if (_columns.Any(c => c.Key == key))
        {
            throw new ArgumentException($"The schema already has a column with the key \"{key}\".", nameof(member));
        }
        if (builder.FromKey is { } from && _columns.All(c => c.Key != from))
        {
            throw new ArgumentException($"Column \"{key}\" reads from \"{from}\", which must be declared before it.", nameof(member));
        }

        var valueType = typeof(TValue);
        var underlying = SpreadsheetValueConverter.Underlying(valueType);
        var inferred = SpreadsheetValueConverter.InferKind(underlying);
        if (inferred is null && builder.ParserDelegate is null)
        {
            throw new ArgumentException($"Column \"{key}\" has the type {valueType.Name}, which has no built-in reading. Use string, a number, bool, DateOnly, DateTime, DateTimeOffset or an enum, or give the column a Parser.", nameof(member));
        }

        var typedOptions = builder.OptionList;
        if (typedOptions is null && underlying.IsEnum)
        {
            typedOptions = EnumOptions<TValue>(underlying);
        }
        if (typedOptions is not null && !SpreadsheetValueConverter.SupportsText(underlying))
        {
            throw new ArgumentException($"Column \"{key}\" has options but its type {valueType.Name} cannot be written as option text. Use string, an enum, a number, bool or Guid.", nameof(member));
        }

        var setter = setMethod.CreateDelegate<Action<TRow, TValue>>();
        var column = new SpreadsheetColumn<TRow, TValue>(key, setter, builder.ParserDelegate, builder.CreateOptionDelegate, builder.CreateOptionAsyncDelegate)
        {
            Label = builder.LabelText,
            Kind = typedOptions is not null ? SpreadsheetColumnKind.Select : inferred ?? SpreadsheetColumnKind.Text,
            Headers = builder.HeaderTexts,
            Description = builder.DescriptionText,
            Example = builder.ExampleText,
            Group = builder.GroupText,
            From = builder.FromKey,
            Required = builder.IsRequired || (!SpreadsheetValueConverter.IsNullable(valueType) && !builder.HasDefaultValue),
            Editable = builder.IsEditable,
            HasDefault = builder.HasDefaultValue,
            DefaultValue = builder.DefaultValue,
            DefaultBoxed = builder.HasDefaultValue ? builder.DefaultValue : null,
            DecimalSeparator = builder.Separator,
            DateFormats = builder.Formats,
            Options = typedOptions?.Select(o => new SpreadsheetOption(SpreadsheetValueConverter.Format(o.Value), o.Label, o.Aliases)).ToArray(),
            UnknownPolicy = builder.Policy,
            CanCreateOption = builder.CreateOptionDelegate is not null || builder.CreateOptionAsyncDelegate is not null,
            Rules = builder.RuleList.Count > 0 ? builder.RuleList.ToArray() : null,
        };
        _columns.Add(column);
        return column;
    }

    /// <summary>Sets the column that identifies a row, which enables the duplicate and existing-record checks.</summary>
    /// <param name="column">A column declared on this builder.</param>
    /// <exception cref="ArgumentException">The column is not part of this schema.</exception>
    public void RowKey(SpreadsheetColumnSchema column)
    {
        ArgumentNullException.ThrowIfNull(column);
        if (!_columns.Contains(column)) throw new ArgumentException("The row key must be a column declared on this schema.", nameof(column));
        _rowKey = column.Key;
    }

    internal SpreadsheetSchema<TRow> Build() => new(
        new SpreadsheetSchemaSettings<TRow>
        {
            Factory = _factory,
            Key = Key,
            Accept = Accept,
            MaxRows = MaxRows,
            ForcedSheet = ForcedSheet,
            ForcedHeaderRow = ForcedHeaderRow,
            RowKeyField = _rowKey,
            Duplicates = Duplicates,
            ExistingAction = ExistingAction,
            RuleTimeoutLimit = RuleTimeoutLimit,
            BatchSize = BatchSize,
            ValidateBatchSize = ValidateBatchSize,
            LookupBatchSize = LookupBatchSize,
            ExistingRecords = ExistingRecords,
            ValidateRow = ValidateRow,
            OnSubmit = OnSubmit,
            OnSubmitAsync = OnSubmitAsync,
            ValidateRowsAsync = ValidateRowsAsync,
            LookupExistingRecordsAsync = LookupExistingRecordsAsync,
        },
        [.. _columns]);

    // One option per member of an enum column that has no options of its own: the member name is the value, its name in words the label.
    private static IReadOnlyList<SpreadsheetOption<TItem?>> EnumOptions<TItem>(Type enumType)
    {
        var names = Enum.GetNames(enumType);
        var options = new List<SpreadsheetOption<TItem?>>(names.Length);
        foreach (var name in names)
        {
            options.Add(new SpreadsheetOption<TItem?>((TItem)Enum.Parse(enumType, name), SpreadsheetImporter.HumanizeKey(name)));
        }
        return options;
    }
}
