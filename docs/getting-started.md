# Getting started

In this tutorial you import a small price list: first from a console app with `Klyroo.Spreadsheet.Core`, then through the Blazor wizard. It takes about ten minutes and needs the .NET 10 SDK.

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

A `SpreadsheetSchema` lists the columns your application wants. Each `SpreadsheetColumnSchema` has a `Key` (the name you read the value by), a `Kind`, and optional rules. This schema expects a SKU, a price written with a decimal comma, and a currency from a fixed list:

```csharp
using Klyroo.Spreadsheet;

var schema = new SpreadsheetSchema
{
    Columns =
    [
        new SpreadsheetColumnSchema { Key = "sku", Label = "SKU", Required = true },
        new SpreadsheetColumnSchema
        {
            Key = "price",
            Label = "Unit price",
            Kind = SpreadsheetColumnKind.Number,
            DecimalSeparator = ',',
            Required = true,
            Rules = [SpreadsheetRule.Min(0)],
        },
        new SpreadsheetColumnSchema
        {
            Key = "currency",
            Kind = SpreadsheetColumnKind.Select,
            Required = true,
            Options = [new("EUR", "Euro", ["€"]), new("USD", "US dollar", ["$"])],
        },
    ],
};
```

A column without a `Label` gets one from its key (`unitPrice` becomes `Unit price`). File headers are matched against the key, the label and the optional `Headers` aliases.

## 3. Load a file

Create a `SpreadsheetImporter` for the schema and give it the text of a file. `LoadText` detects the separator (tab, semicolon or comma), the header row and the columns:

```csharp
var importer = new SpreadsheetImporter(schema);
importer.LoadText("SKU;Unit price;Currency\nA-1;12,50;€\nA-2;-3;USD", "prices.csv");

Console.WriteLine(importer.FileLoaded);               // True
Console.WriteLine(importer.ImportableRows.Count);     // 1
Console.WriteLine(importer.InvalidRows.Count);        // 1
```

The second row has a negative price, so the `Min(0)` rule gives it an error and it is not importable. Inspect why:

```csharp
foreach (var row in importer.InvalidRows)
{
    foreach (var issue in row.Issues)
    {
        Console.WriteLine($"row {row.RowNumber}: {issue.Field}: {issue.Message}");
    }
}
// row 3: price: Value is below 0
```

The importer never changes your file. It holds what the user decided (column choices, cell edits, discarded rows) and recomputes the rows after each change.

## 4. Send the rows to your code

`OnSubmit` receives the importable rows and returns the rows your server refused, or `null` when it accepted all of them. Add it to the schema of step 2 (shown here as the full initializer, so replace that declaration rather than adding a second one):

```csharp
var schema = new SpreadsheetSchema
{
    // columns as above
    OnSubmit = rows =>
    {
        foreach (var row in rows)
        {
            Console.WriteLine($"{row.Data["sku"]}: {row.Data["price"]} {row.Data["currency"]}");
        }
        return Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>(null);
    },
};
```

Then submit:

```csharp
await importer.SubmitAsync();                // OnSubmit prints: A-1: 12.5 EUR
Console.WriteLine(importer.ImportedCount);   // 1
```

`row.Data` holds typed values: a `double` for the price, the option value (`EUR`) for the currency. The complete program of steps 2 to 4 is the sample in the [README](../README.md#quick-start).

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

`KlySpreadsheetImport` renders the five steps of the wizard (file, columns, values, review, import) for an importer you give it. Its first step lets users choose or drop a file (CSV, TSV, text or `.xlsx`, 10 MB at most by default, see `MaxFileSize`) or paste rows:

```razor
@page "/prices/import"
@using Klyroo.Spreadsheet

<KlySpreadsheetImport Importer="_importer" Title="Import prices" />

@code {
    private SpreadsheetImporter _importer = default!;

    protected override void OnInitialized()
    {
        _importer = new SpreadsheetImporter(new SpreadsheetSchema
        {
            Columns =
            [
                new SpreadsheetColumnSchema { Key = "sku", Label = "SKU", Required = true },
                new SpreadsheetColumnSchema { Key = "price", Kind = SpreadsheetColumnKind.Number, Required = true },
            ],
            OnSubmit = SaveAsync,
        });
    }

    private Task<IReadOnlyList<SpreadsheetRejectedRow>?> SaveAsync(IReadOnlyList<SpreadsheetRow> rows)
    {
        // Persist rows here. Return the rows the server refused, or null.
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
