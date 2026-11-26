using System.Diagnostics.CodeAnalysis;

namespace Klyroo.Spreadsheet;

/// <summary>
/// The headless import engine for a row type. It is a <see cref="SpreadsheetImporter"/> whose schema is typed, so the schema callbacks
/// receive <typeparamref name="TRow"/> instances, and which can hand you the rows it holds as <see cref="SpreadsheetRow{TRow}"/>.
/// </summary>
/// <typeparam name="TRow">The row class of the schema.</typeparam>
/// <remarks>An instance holds the state of one import session and is not thread-safe. Create one per user and per import.</remarks>
public sealed class SpreadsheetImporter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TRow> : SpreadsheetImporter where TRow : class
{
    /// <summary>Creates an importer for a schema, with no file loaded.</summary>
    /// <param name="schema">The columns and policies of the import, from <see cref="SpreadsheetSchema.For{TRow}(Action{SpreadsheetSchemaBuilder{TRow}})"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> is <see langword="null"/>.</exception>
    public SpreadsheetImporter(SpreadsheetSchema<TRow> schema) : base(schema ?? throw new ArgumentNullException(nameof(schema)))
    {
        Schema = schema;
    }

    /// <summary>The schema this importer was created with.</summary>
    public new SpreadsheetSchema<TRow> Schema { get; }

    /// <summary>Gives a row of this importer the typed form: the same row with its parsed values as a <typeparamref name="TRow"/>.</summary>
    /// <param name="row">A row of <see cref="SpreadsheetImporter.AllRows"/>, <see cref="SpreadsheetImporter.ImportableRows"/> or one of the other row lists.</param>
    /// <returns>The typed row. Each call builds a new <typeparamref name="TRow"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The factory or a property setter of the row class threw for this row. The importer already reports that as an error issue with the code <c>row.failed</c> when it reads the file, so the row is not importable; the message names the row and the inner exception is the cause.</exception>
    public SpreadsheetRow<TRow> ToTyped(SpreadsheetRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return Schema.ToTyped(row);
    }

    /// <summary>The rows that would be sent on submit, typed. It builds one <typeparamref name="TRow"/> per row on each call. It never throws for a row the host refused: such a row is not importable.</summary>
    public IReadOnlyList<SpreadsheetRow<TRow>> ImportableTypedRows => ImportableRows.Select(Schema.ToTyped).ToList();
}
