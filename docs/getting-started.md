# Getting started

In this tutorial you import a small price list: first from a console app with `Klyroo.Spreadsheet.Core`, then through the Blazor wizard. It takes about ten minutes.

Check the [prerequisites](../README.md#prerequisites) first: the .NET 10 SDK, and for the components a Blazor app with Klyroo.UI set up.

## 1. Install

Pick the package for what you build. The Blazor package includes the Core package, so you never install both.

| You build | Package |
| :--- | :--- |
| A console app, API, worker, or your own UI | `Klyroo.Spreadsheet.Core` |
| A Blazor Web App or a .NET MAUI Blazor Hybrid app | `Klyroo.Spreadsheet` |

```bash
dotnet add package Klyroo.Spreadsheet.Core --prerelease
```

## 2. Describe the data you expect

A schema lists the columns your application wants, and it is typed by the class you want one row to become. Write that class first, then pick its properties with `SpreadsheetSchema.For<TRow>`. Each `Column` takes a member expression, so the property name is the column key, and the kind of cell follows the property type: text for `string`, a number for `int`, `decimal` and the other numeric types, yes or no for `bool`, a date for `DateOnly`, `DateTime` and `DateTimeOffset`, and a choice for an enum. The expression is read, never compiled, so it works with NativeAOT.

This schema expects a SKU, a price written with a decimal comma, and a currency from a fixed list:

```csharp
using Klyroo.Spreadsheet;

public enum Currency { Eur, Usd }

public sealed class PriceRow
{
    public string? Sku { get; set; }
    public decimal Price { get; set; }
    public Currency Currency { get; set; }
}

var schema = SpreadsheetSchema.For<PriceRow>(s =>
{
    s.Column(r => r.Sku, c => c.Label("SKU").Required());
    s.Column(r => r.Price, c => c.Label("Unit price").DecimalSeparator(',').Rule(SpreadsheetRule.Min(0m)));
    s.Column(r => r.Currency, c => c.Options(
    [
        new SpreadsheetOption<Currency>(Currency.Eur, "Euro", ["€"]),
        new SpreadsheetOption<Currency>(Currency.Usd, "US dollar", ["$"]),
    ]));
});
```

The row class needs a public parameterless constructor and public setters; for a type without one, pass a factory: `SpreadsheetSchema.For(() => new PriceRow(...), s => ...)`. A property that cannot hold an empty cell (`decimal`, `bool`, an enum) makes its column required, so `Price` and `Currency` above are required without saying so; use `decimal?` for an optional price. An enum column offers one option per distinct value without `Options` (a member that repeats another's value, such as `Medium = Normal`, is accepted as an alias of the first declared one); `Options` replaces them to add labels and aliases such as the `€` symbol.

A column without a `Label` gets one from its key (`UnitPrice` becomes `Unit price`). File headers are matched against the key, the label and the optional `Headers` aliases.

## 3. Load a file

Create a `SpreadsheetImporter<PriceRow>` for the schema and give it the text of a file. `LoadText` detects the separator (tab, semicolon or comma), the header row and the columns:

```csharp
var importer = new SpreadsheetImporter<PriceRow>(schema);
importer.LoadText("SKU;Unit price;Currency\nA-1;12,50;€\nA-2;-3;USD", "prices.csv");

Console.WriteLine(importer.FileLoaded);               // True
Console.WriteLine(importer.ImportableRows.Count);     // 1
Console.WriteLine(importer.InvalidRows.Count);        // 1
```

The second row has a negative price, so the `Min(0m)` rule gives it an error and it is not importable. Inspect why:

```csharp
foreach (var row in importer.InvalidRows)
{
    foreach (var issue in row.Issues)
    {
        Console.WriteLine($"row {row.RowNumber}: {issue.Field}: {issue.Message}");
    }
}
// row 3: Price: Value is below 0
```

The importer never changes your file. It holds what the user decided (column choices, cell edits, discarded rows) and recomputes the rows after each change.

## 4. Send the rows to your code

`OnSubmit` receives the importable rows as `SpreadsheetRow<PriceRow>`, which carries the parsed row in `Value`, and returns the rows your server refused, or `null` when it accepted all of them. Set it on the builder of step 2:

```csharp
var schema = SpreadsheetSchema.For<PriceRow>(s =>
{
    // columns as above
    s.OnSubmit = rows =>
    {
        foreach (var row in rows)
        {
            Console.WriteLine($"{row.Value.Sku}: {row.Value.Price} {row.Value.Currency}");
        }
        return Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>(null);
    };
});
```

Then submit:

```csharp
await importer.SubmitAsync();                // OnSubmit prints: A-1: 12.5 Eur
Console.WriteLine(importer.ImportedCount);   // 1
```

`row.Value` is a `PriceRow` with the price as a `decimal` and the currency as a `Currency`. A cell that is empty without a default, or that could not be read, leaves its property at the default of its type, but the rows `OnSubmit` receives are importable, so every required value is there. The complete program of steps 2 to 4 is the sample in the [README](../README.md#quick-start).

## 5. Add the Blazor wizard

Install the UI package instead of the Core one in your Blazor project:

```bash
dotnet add package Klyroo.Spreadsheet --prerelease
```

Register the services. In a Blazor Web App that uses WebAssembly or Auto, call it in both the server and the client project:

```csharp
using Klyroo.Spreadsheet;

builder.Services.AddKlyrooSpreadsheet();
```

Import the namespace in `_Imports.razor`:

```razor
@using Klyroo.Spreadsheet
```

Load the stylesheets in `App.razor` or `wwwroot/index.html`, after the two stylesheets of [Klyroo.UI](https://github.com/klyroo/klyroo-ui):

```html
<link rel="stylesheet" href="_content/Klyroo.UI/klyroo.preflight.css" />
<link rel="stylesheet" href="_content/Klyroo.UI/klyroo.css" />
<link rel="stylesheet" href="_content/Klyroo.Spreadsheet/klyroo-spreadsheet.css" />
```

## 6. Build the page

`KlySpreadsheetImport` renders the five steps of the wizard (file, columns, values, review, import) for an importer you give it. The component takes the non-generic `SpreadsheetImporter`, because it reads and edits cells as text and never touches your row class, so a `SpreadsheetImporter<PriceRow>` is passed as it is. Its first step lets users choose or drop a file (CSV, TSV, text, `.xlsx` or `.xls`, 10 MB at most by default, see `MaxFileSize`) or paste rows:

```razor
@page "/prices/import"
@using Klyroo.Spreadsheet

<KlySpreadsheetImport Importer="_importer" Title="Import prices" />

@code {
    private SpreadsheetImporter<PriceRow> _importer = default!;

    protected override void OnInitialized()
    {
        _importer = new SpreadsheetImporter<PriceRow>(SpreadsheetSchema.For<PriceRow>(s =>
        {
            s.Column(r => r.Sku, c => c.Label("SKU").Required());
            s.Column(r => r.Price);
            s.OnSubmit = SaveAsync;
        }));
    }

    private Task<IReadOnlyList<SpreadsheetRejectedRow>?> SaveAsync(IReadOnlyList<SpreadsheetRow<PriceRow>> rows)
    {
        // Persist rows.Select(r => r.Value) here. Return the rows the server refused, or null.
        return Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>(null);
    }
}
```

The page needs an interactive render mode, set on the page or for the whole app. After the file loads, the wizard shows a Continue button. It skips the columns step when every required column matched, and the values step when no select value is unknown, so a clean file goes straight to the review.

The component subscribes to the importer's `Changed` event, so a file your own code loads with `LoadText`, `LoadFileAsync` or `LoadWorkbook` shows up too. Keep the importer in a field of the page (one per user and per import), not in a singleton service.

## Next steps

- [Guides](guides.md): header detection, unknown values, existing records, batches, Excel files and tests.
- [Concepts](concepts.md): how the importer turns a file into rows, and how the two packages relate.
- [Platforms](platforms.md): render modes, .NET MAUI and NativeAOT.
- [FAQ](faq.md): what the component does not do, and why a column was not matched.
