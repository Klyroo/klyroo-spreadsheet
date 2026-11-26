# Guides

Each guide solves one task and assumes you read [Getting started](getting-started.md). Every sample compiles against this version. Samples use `using Klyroo.Spreadsheet;` and implicit usings.

- [Parse typed columns and validate them](#parse-typed-columns-and-validate-them)
- [Cope with banners, renamed headers and wrong sheets](#cope-with-banners-renamed-headers-and-wrong-sheets)
- [Resolve unknown select values](#resolve-unknown-select-values)
- [Handle duplicates and existing records](#handle-duplicates-and-existing-records)
- [Validate across fields](#validate-across-fields)
- [Submit in batches and retry rejected rows](#submit-in-batches-and-retry-rejected-rows)
- [Read an Excel file](#read-an-excel-file)
- [Let users paste rows in Blazor](#let-users-paste-rows-in-blazor)
- [Import large files](#import-large-files)
- [Export a template and the invalid rows](#export-a-template-and-the-invalid-rows)
- [Test an import](#test-an-import)

## Parse typed columns and validate them

Goal: turn cells into typed values and report the cells that do not fit.

```csharp
var schema = new SpreadsheetSchema
{
    MaxRows = 10_000,
    Columns =
    [
        new SpreadsheetColumnSchema
        {
            Key = "sku",
            Label = "SKU",
            Required = true,
            Rules =
            [
                SpreadsheetRule.Pattern("^[A-Z]{2}-[0-9]{3}$", "Use the form AB-123"),
                new SpreadsheetRule("not-reserved", "ZZ- codes are reserved",
                    v => v is string s && !s.StartsWith("ZZ-", StringComparison.Ordinal)),
            ],
        },
        new SpreadsheetColumnSchema { Key = "available", Kind = SpreadsheetColumnKind.Boolean, HasDefault = true, DefaultValue = true },
        new SpreadsheetColumnSchema { Key = "launch", Kind = SpreadsheetColumnKind.Date, DateFormats = ["dd/MM/yyyy"] },
        new SpreadsheetColumnSchema
        {
            Key = "weightKg",
            Label = "Weight (kg)",
            Kind = SpreadsheetColumnKind.Number,
            Rules = [SpreadsheetRule.Max(30, "Too heavy to ship", SpreadsheetIssueLevel.Warning)],
        },
    ],
};

var importer = new SpreadsheetImporter(schema);
importer.LoadText("SKU,Available,Launch,Weight (kg)\nAB-123,oui,01/03/2026,12\nZZ-001,,15/03/2026,45\nab-12,no,2026-03-20,3", "catalog.csv");

Console.WriteLine(importer.AllRows[0].Data["launch"]);    // 2026-03-01
Console.WriteLine(importer.AllRows[1].Data["available"]); // True
```

What happens for each row of that file:

- Row 2 is valid. `oui` is a true value, and the date is stored as the ISO string `2026-03-01`.
- Row 3 is blocked by the `not-reserved` rule. Its empty `Available` cell takes the default, and its weight raises a warning (code `max`) that does not block the row.
- Row 4 is blocked twice: `ab-12` fails the pattern, and `2026-03-20` is not in the only format this column accepts, so you get a `date` error.

`Pattern` matches with a timeout (100 ms by default, the `matchTimeout` argument changes it), so a pasted value built to trigger catastrophic backtracking cannot freeze the import: that cell gets a `pattern` issue saying the format could not be checked in time.

Three details are easy to miss. A rule only runs on a non-empty value that parsed. The `Min`, `Max` and `Between` rules only accept a `double`, so use them on `Number` columns. And `DefaultValue` is applied only when `HasDefault` is `true`, which lets a default be `null` on purpose.

The built-in boolean values are `yes`, `y`, `oui`, `o`, `true`, `vrai`, `x`, `1` and `no`, `n`, `non`, `false`, `faux`, `0`. For anything else, set `CustomParse` on the column: it receives the cell text, returns the value, and an exception becomes an error on the cell with the exception message.

## Cope with banners, renamed headers and wrong sheets

Goal: accept exports whose header is not on the first row or is spelled differently.

By default the importer scans the first 20 rows of each sheet and picks the row that matches the most columns, so a title above the header is ignored. List alternative spellings in `Headers`:

```csharp
var schema = new SpreadsheetSchema
{
    Columns =
    [
        new SpreadsheetColumnSchema { Key = "email", Headers = ["E-mail", "Courriel"], Required = true },
        new SpreadsheetColumnSchema
        {
            Key = "age",
            Kind = SpreadsheetColumnKind.Number,
            Required = true,
            Rules = [SpreadsheetRule.Between(0, 120)],
        },
    ],
};

var importer = new SpreadsheetImporter(schema);
importer.LoadText("Export 2026;;\nE-mail;Years\nada@example.com;31\nalan@example.com;45", "people.csv");

Console.WriteLine(importer.HeaderRowNumber);       // 2
var missing = importer.MissingFields[0];           // the "age" column
Console.WriteLine(missing.Suggestion?.HeaderText); // Years

if (missing.Suggestion is { } suggestion)
{
    importer.AssignColumn(missing.Column.Key, suggestion.HeaderIndex);
}
Console.WriteLine(importer.CanSubmit);             // True
```

`Years` matches no name, so `age` is missing. Because `age` is a number column with a rule, the importer proposes the unused `Years` column: at least 80% of up to 30 sampled values parse and pass. The suggestion is never applied for you. Show it to the user, or apply it as above when you trust it.

When detection is wrong or you know the layout, force it. `ForcedHeaderRow` is the 1-based row number in the file:

```csharp
var schema = new SpreadsheetSchema
{
    ForcedSheet = "Prices",
    ForcedHeaderRow = 4,
    Columns = [new SpreadsheetColumnSchema { Key = "sku", Required = true }],
};
```

For workbooks with several sheets, `importer.AmbiguousDetection` is `true` when more than one sheet matched equally well and the first was taken. Let the user pick with `SetSheet(name)`, and use `SetHeaderRow(index)` to move the header.

## Resolve unknown select values

Goal: decide what to do when a file says `Gold` and your list only has `pro`.

A `Select` column compares each cell, ignoring case and accents, to the option value, the option label and its aliases. A cell with no match becomes a `SpreadsheetValueQuestion`, and by default the import cannot be submitted until every question is answered:

```csharp
var createdTiers = new List<string>();
var schema = new SpreadsheetSchema
{
    Columns =
    [
        new SpreadsheetColumnSchema { Key = "name", Required = true },
        new SpreadsheetColumnSchema
        {
            Key = "tier",
            Kind = SpreadsheetColumnKind.Select,
            Options = [new("std", "Standard", ["basic"]), new("pro", "Professional")],
            OnCreateOption = raw =>
            {
                createdTiers.Add(raw);
                return new SpreadsheetOption(raw, raw);
            },
        },
    ],
};

var importer = new SpreadsheetImporter(schema);
importer.LoadText("Name,Tier\nAcme,Basic\nBeta,Gold\nGamma,Platinum\nDelta,Legacy", "accounts.csv");

foreach (var question in importer.OpenQuestions)
{
    Console.WriteLine($"{question.RawValue}: {question.RowIndexes.Count} row(s)");
}

importer.AnswerValue("tier", "Gold", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Map, "pro", "Professional"));
importer.AnswerValue("tier", "Platinum", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Create));
importer.AnswerValue("tier", "Legacy", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.SkipRows));

Console.WriteLine(importer.CanSubmit);             // True
await importer.SubmitAsync();
Console.WriteLine(string.Join(",", createdTiers)); // Platinum
```

`Basic` matched the alias, so only three questions were asked. One answer applies to every row that has the value. The four actions are `Map` (use an existing option), `LeaveEmpty`, `SkipRows` (leave those rows out) and `Create` (keep the text as a new value). `OnCreateOption` runs once per created value when you submit, before any row is sent, so it is the place to insert the new option in your database. The rows then carry the option it returns (its `Value` in `row.Data`, its label in the review), and a retry does not create it again.

To skip the questions, set `UnknownPolicy` on the column. `LeaveEmpty`, `SkipRows` and `Create` answer for the user. `Error` does not ask and blocks each row with the unknown value:

```csharp
var country = new SpreadsheetColumnSchema
{
    Key = "country",
    Kind = SpreadsheetColumnKind.Select,
    Options = [new("FR", "France"), new("DE", "Germany")],
    UnknownPolicy = SpreadsheetUnknownValuePolicy.LeaveEmpty,
};
```

## Handle duplicates and existing records

Goal: avoid importing the same key twice, and update records you already store.

Name the key column with `RowKeyField`. Two independent checks use it. `Duplicates` decides what happens when the file repeats a key: `Error` (the default) blocks every row of the key, `KeepFirst` and `KeepLast` leave the others out. `ExistingRecords` holds the keys you already have, and `ExistingAction` decides what to do with a row that matches one:

```csharp
var stored = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase)
{
    ["A-1"] = new Dictionary<string, object?> { ["sku"] = "A-1", ["stock"] = 4.0 },
};

var schema = new SpreadsheetSchema
{
    RowKeyField = "sku",
    Duplicates = SpreadsheetDuplicatesPolicy.KeepLast,
    ExistingRecords = stored,
    ExistingAction = SpreadsheetExistingAction.Update,
    Columns =
    [
        new SpreadsheetColumnSchema { Key = "sku", Label = "SKU", Required = true },
        new SpreadsheetColumnSchema { Key = "stock", Kind = SpreadsheetColumnKind.Number, Required = true },
    ],
};

var importer = new SpreadsheetImporter(schema);
importer.LoadText("SKU,Stock\nA-1,10\nB-2,5\nB-2,7", "stock.csv");

foreach (var row in importer.ImportableRows)
{
    Console.WriteLine($"{row.Data["sku"]} {row.Mode} {row.Data["stock"]}");
}
// A-1 Update 10
// B-2 Create 7
```

Each importable row carries a `Mode` of `Create` or `Update`, so your `OnSubmit` can branch on it. The two checks compare keys differently: duplicates inside the file ignore case and surrounding spaces, but the lookup in `ExistingRecords` uses the dictionary's own comparer on the exact cell text. Build the dictionary with `StringComparer.OrdinalIgnoreCase` as above if your keys are case-insensitive. The values inside `ExistingRecords` are not read by the importer, only the keys.

## Validate across fields

Goal: reject a row because two cells disagree, which no single column rule can express.

`ValidateRow` runs after the column rules and receives the parsed values by column key. Return the issues to add:

```csharp
var schema = new SpreadsheetSchema
{
    Columns =
    [
        new SpreadsheetColumnSchema { Key = "start", Kind = SpreadsheetColumnKind.Date, Required = true },
        new SpreadsheetColumnSchema { Key = "end", Kind = SpreadsheetColumnKind.Date, Required = true },
    ],
    ValidateRow = row =>
    {
        if (row["start"] is string start && row["end"] is string end && string.CompareOrdinal(end, start) < 0)
        {
            return [new SpreadsheetRowIssue("end", "range", SpreadsheetIssueLevel.Error, "End is before start")];
        }
        return [];
    },
};
```

Dates arrive as ISO 8601 strings (`yyyy-MM-dd`, or `yyyy-MM-ddTHH:mm` with a time), so comparing them as strings orders them correctly. Set `Field` to a column key to show the issue on that cell, or `null` to attach it to the whole row.

## Submit in batches and retry rejected rows

Goal: save rows in chunks and send again the rows the server refused.

`SubmitAsync` calls `OnSubmit` with batches of `BatchSize` importable rows (all rows at once when it is zero). The callback returns the refused rows by `Index`:

```csharp
var lockedAccounts = new HashSet<string> { "bob@example.com" };
var schema = new SpreadsheetSchema
{
    BatchSize = 100,
    Columns = [new SpreadsheetColumnSchema { Key = "email", Required = true, Rules = [SpreadsheetRule.Email()] }],
    OnSubmit = async rows =>
    {
        var rejected = new List<SpreadsheetRejectedRow>();
        foreach (var row in rows)
        {
            if (lockedAccounts.Contains((string)row.Data["email"]!))
            {
                rejected.Add(new SpreadsheetRejectedRow(row.Index, "Account is locked", "email"));
            }
        }
        await Task.Delay(1); // call your API here
        return rejected;
    },
};

var importer = new SpreadsheetImporter(schema);
importer.LoadText("Email\nada@example.com\nbob@example.com", "users.csv");

await importer.SubmitAsync();
Console.WriteLine($"{importer.ImportedCount} imported, {importer.RejectedCount} rejected"); // 1 imported, 1 rejected
Console.WriteLine(importer.InvalidRows[0].Issues[0].Code);                                   // server

lockedAccounts.Clear(); // the account was unlocked in the meantime
await importer.RetryRejectedAsync();
Console.WriteLine($"{importer.ImportedCount} imported, {importer.RejectedCount} rejected"); // 2 imported, 0 rejected
```

A refused row comes back as an error issue with code `server` on that row, so it appears under the Invalid tab of the review. `RetryRejectedAsync` sends those rows again as they are, and it reports only what that retry sent: `ImportedCount` is `1` again, not `2`. Keep your own running total if you need one.

Editing a cell of a refused row clears its rejection, which removes the row from the retry set, so `RetryRejectedAsync` no longer sends it. The edited row is importable again, but nothing tracks that it was never saved. To resend corrected rows, save them from your own code, or load a file that holds only those rows.

If `OnSubmit` throws, the importer catches it, sets `SubmitStatus` to `Error` and puts the message in `SubmitError`. Batches already sent are not undone, and nothing records which they were, so make the callback idempotent, or send everything in one batch inside a transaction of your own.

## Read an Excel file

Goal: import `.xlsx` files.

`LoadFileAsync` reads an `.xlsx` workbook with `SpreadsheetXlsxReader`, which uses the base class library only and works under NativeAOT. The component calls it for a chosen or dropped file; from your own code, pass any stream:

```csharp
await using var stream = File.OpenRead("prices.xlsx");
await importer.LoadFileAsync(stream, "prices.xlsx");
```

Every worksheet becomes a sheet, and the importer picks the one whose header row matches the most columns; the component offers a sheet selector when there are several. Shared and inline strings are read as text, booleans as `TRUE` or `FALSE`, and cells formatted as dates as `yyyy-MM-dd` (with the time when there is one), which the default date formats of a `Date` column read. Numbers keep the invariant form `1234.5`, and a `Number` column reads that form whatever its `DecimalSeparator`. Formulas are not evaluated: the reader uses the value Excel saved with the file. Legacy `.xls` workbooks (Excel 97 to 2003, BIFF8) are read the same way, by `SpreadsheetXlsReader`, with the same value conventions; error cells give their text (`#DIV/0!`), and hidden sheets are read like the visible ones. The reader also uses no dependency and no reflection, and it treats the file as untrusted: a damaged or hostile file throws an `InvalidDataException` instead of looping or allocating without bound. Both readers refuse a workbook of more than 2,000,000 cells, counting the empty cells left of each value, or of more than 32 million characters of text. Password-protected workbooks and the Excel 5.0/95 format (BIFF5) are refused with an `InvalidDataException` that tells users to save as `.xlsx` or `.csv`. `LoadFileAsync` chooses by content as well as by extension: a file that starts with the OLE2 signature is read as `.xls` whatever its name, a zip archive named `.xls` is read as `.xlsx`, and a text export named `.xls` (tab-separated or CSV text, which many applications produce under that name) is read like a `.csv` file. An HTML or XML export named `.xls` (a web page table, the Excel 2003 XML format) is refused, since its rows are markup.

For another format, build a `SpreadsheetWorkbook` yourself and call `LoadWorkbook`. `RowNumbers` holds the row number of each entry of `Rows` in the original sheet, which is what the review shows to users and what `ForcedHeaderRow` refers to.

## Let users paste rows in Blazor

Goal: accept rows copied from Excel or Google Sheets instead of a file.

`Paste` is `LoadText` with a default name of `clipboard.tsv`. Cells copied from a spreadsheet arrive tab-separated, which the separator detection recognizes. With `Wizard="false"` the component renders only the current step, with no step bar and no buttons, so your page decides when to move on:

```razor
@using Klyroo.Spreadsheet

<textarea @bind="_pasted" @bind:event="oninput" rows="6" placeholder="Paste rows from Excel or Sheets"></textarea>
<button type="button" @onclick="Load">Load</button>

<KlySpreadsheetImport Importer="Importer" Wizard="false" />

@code {
    private string _pasted = "";

    [Parameter, EditorRequired] public SpreadsheetImporter Importer { get; set; } = default!;

    private void Load() => Importer.Paste(_pasted);
}
```

Without the wizard, call `Importer.NextStep()`, `PreviousStep()` and `SubmitAsync()` from your own buttons, and read `Importer.GetSteps()` to draw your own step bar.

## Import large files

Goal: let users review a file of tens of thousands of rows without a frozen page.

The importer reads the file once, then updates only the rows a change concerns. Editing a cell re-validates that row, plus the rows that share its old or new key when you change the key, so an edit in a 50,000-row file takes milliseconds. The lists it builds (`ImportableRows`, `InvalidRows`, `DiscardedRows`, `VisibleReviewRows`) are cached until a row or a filter changes, so reading them again costs nothing.

The review is paged. Build your own table on `ReviewPageRows` and move with `SetReviewPage` and `SetReviewPageSize`:

```csharp
var importer = new SpreadsheetImporter(schema);
importer.LoadText(csv, "big.csv");

importer.SetReviewPageSize(100);
importer.SetReviewPage(2);
foreach (var row in importer.ReviewPageRows)   // rows 201 to 300 of the current filter
{
    Console.WriteLine(row.RowNumber);
}
Console.WriteLine($"{importer.ReviewPage + 1} of {importer.ReviewPageCount}");
```

Changing a filter (`SetReviewTab`, `SetReviewLevel`, `SetReviewIssueFilter`, `SetReviewSearch`) goes back to the first page, and a page number is kept inside the pages that exist.

`KlySpreadsheetImport` does this for you. Its review table renders one page (`PageSize`, 50 by default, with a page size list in the pager) and the pager is made of native buttons, so it works with the keyboard. The checkbox in the table header selects the rows of the current page.

Cap the import with `SpreadsheetSchema.MaxRows`, or with the `MaxRows` parameter of the component, which is 10,000 by default and applies when the schema sets none (`MaxRows="@null"` removes it). Rows past the limit are left out with the discard reason `Limit`, and the component tells the user how many:

```razor
<KlySpreadsheetImport Importer="_importer" MaxRows="50000" PageSize="100" />
```

Headless, set `importer.DefaultMaxRows` for the same effect, and read `EffectiveMaxRows` and `LimitedRowCount` to tell your own users.

## Export a template and the invalid rows

Goal: give users a file to fill in, and give them back the rows they must fix.

```csharp
File.WriteAllText("template.csv", importer.ExportTemplateCsv());
File.WriteAllText("rejected.csv", importer.ExportInvalidRowsCsv());
```

`ExportTemplateCsv` writes a header row and an example row (`Example` of each column) for every column that is not computed with `From`. `ExportInvalidRowsCsv` lists the rows that are invalid and not discarded, with an `Issues` column. Both start with a byte order mark so Excel opens them as UTF-8. The invalid rows export has one column per schema column, in schema order, headed by the file header it was read from, so the user can fix the cells and import the file again.

## Test an import

Goal: test your schema and your page without a browser.

The engine is plain C#, so test it with xUnit alone. The component is tested with [bUnit](https://bunit.dev): register the services, render the component with your importer, load text, and click the buttons the component exposes through `data-slot` attributes. The tests below compile with `xunit.v3` and `bunit`:

```csharp
using Bunit;
using Klyroo.Spreadsheet;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MyApp.Tests;

public sealed class PriceImportTests
{
    private static SpreadsheetImporter CreateImporter() => new(new SpreadsheetSchema
    {
        Columns =
        [
            new SpreadsheetColumnSchema { Key = "sku", Label = "SKU", Required = true },
            new SpreadsheetColumnSchema
            {
                Key = "price",
                Kind = SpreadsheetColumnKind.Number,
                Required = true,
                Rules = [SpreadsheetRule.Min(0)],
            },
        ],
    });

    [Fact]
    public void Negative_prices_block_the_row()
    {
        var importer = CreateImporter();

        importer.LoadText("SKU,Price\nA-1,10\nA-2,-5", "prices.csv");

        Assert.Single(importer.ImportableRows);
        var invalid = Assert.Single(importer.InvalidRows);
        Assert.Contains(invalid.Issues, i => i.Field == "price" && i.Code == "min");
    }

    [Fact]
    public void A_missing_required_column_blocks_submission_until_it_is_assigned()
    {
        var importer = CreateImporter();

        importer.LoadText("SKU,Amount\nA-1,10\nA-2,12", "prices.csv");

        Assert.False(importer.CanSubmit);
        Assert.Equal("Amount", importer.MissingFields[0].Suggestion?.HeaderText);

        importer.AssignColumn("price", importer.MissingFields[0].Suggestion!.HeaderIndex);

        Assert.True(importer.CanSubmit);
    }
}

public sealed class PriceImportComponentTests : BunitContext
{
    public PriceImportComponentTests()
    {
        Services.AddKlyrooSpreadsheet();
    }

    [Fact]
    public void Continue_moves_from_the_file_step_to_review_when_nothing_needs_mapping()
    {
        var importer = new SpreadsheetImporter(new SpreadsheetSchema
        {
            Columns = [new SpreadsheetColumnSchema { Key = "sku", Required = true }],
        });
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        importer.LoadText("SKU\nA-1", "prices.csv");
        cut.Render();
        cut.Find("[data-slot=spreadsheet-nav-continue]").Click();

        Assert.Equal("review", importer.CurrentStepKey);
        Assert.Contains("A-1", cut.Markup);
    }
}
```

The suggestion in the second test needs at least two non-empty sample cells in the candidate column, which is why the file has two data rows. To test `OnSubmit` without a database, give the schema a callback that records the rows it receives and assert on them after `await importer.SubmitAsync()`. The repository's own [tests](../tests/Klyroo.Spreadsheet.Tests) cover more cases, including a 5,000-row import.
