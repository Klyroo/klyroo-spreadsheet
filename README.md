# Klyroo.Spreadsheet

A .NET library that imports CSV, TSV, Excel (`.xlsx` and `.xls`) and pasted spreadsheet data into your application, with column mapping, value mapping and row validation, for developers who build onboarding and bulk-edit screens.

[![CI](https://github.com/klyroo/klyroo-spreadsheet/actions/workflows/ci.yml/badge.svg)](https://github.com/klyroo/klyroo-spreadsheet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Klyroo.Spreadsheet.svg)](https://www.nuget.org/packages/Klyroo.Spreadsheet)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/klyroo/klyroo-spreadsheet/blob/main/LICENSE)

User files rarely match your model: the header is on row 3, a column is named differently, a status says "Gold" where you expect "pro". Klyroo.Spreadsheet puts the engine that deals with this in a headless package you can unit test, and adds a Blazor wizard on top of it. The engine runs anywhere .NET 10 runs, and the wizard runs in Blazor Web Apps and .NET MAUI Blazor Hybrid.

## Features

- **Header and sheet detection**: scans the first 20 rows of a sheet and picks the row where most schema columns match, so title rows above the header are skipped.
- **Forgiving column matching**: compares headers ignoring case, accents, extra spaces, a trailing `*` and a trailing `(required)`, against the column key, its label and any aliases you list.
- **Column suggestions**: when a required column has no match, proposes an unused file column if at least 80% of up to 30 sampled cells parse and pass the rules, and applies it only when you call `AssignColumn`.
- **Typed parsing and rules**: text, number (either decimal mark), date, boolean and select columns, with built-in `Email`, `Pattern`, `Min`, `Max` and `Between` rules, custom rules and a cross-field `ValidateRow` callback.
- **Unknown value questions**: turns select cells that match no option into questions you answer by mapping, leaving empty, skipping the rows or creating the value, or resolves them automatically with a per-column policy.
- **Duplicates and existing records**: detects repeated keys inside the file, keeps the first or last, and marks rows whose key already exists as updates, skips or errors.
- **Inline correction**: edits cells, discards and restores rows, filters the review by tab, severity, issue and text, all without touching the source file.
- **12 languages**: the wizard, its validation messages and its screen reader announcements follow the Klyroo.UI locale (English and French reviewed, ten drafts), and any text can be changed per locale.
- **Large files**: parses a file once and then updates only the rows a change concerns, so editing a cell of a 50,000-row file takes milliseconds, and the wizard pages its review table and caps an import at 10,000 rows by default.
- **Saved mappings**: stores the column mapping and value answers of an import as JSON (source-generated, AOT-safe) and applies them automatically to the next file with the same headers.
- **Batched submit with retry**: sends importable rows to your callback in batches, shows the rows the server refused as errors on those rows, and sends only them again.

## Packages

| Package | Description |
| :--- | :--- |
| [`Klyroo.Spreadsheet.Core`](https://www.nuget.org/packages/Klyroo.Spreadsheet.Core) | The import engine: parsing, matching, validation and submit. No UI dependency. |
| [`Klyroo.Spreadsheet`](https://www.nuget.org/packages/Klyroo.Spreadsheet) | The `KlySpreadsheetImport` wizard for Blazor and .NET MAUI Blazor Hybrid, built on [Klyroo.UI](https://github.com/klyroo/klyroo-ui). Includes the Core package. |

## Prerequisites

To use the library in an app:

- **The .NET 10 SDK**, and an app that targets `net10.0`. The packages are prereleases, so every `dotnet add package` command needs `--prerelease`.
- **`Klyroo.Spreadsheet.Core`** alone (the importer and the `.xlsx` reader) runs in any .NET 10 app, with no other dependency.
- **A Blazor app** for `Klyroo.Spreadsheet`: a Blazor Web App or a .NET MAUI Blazor Hybrid app. The wizard, including the file picker, needs an interactive render mode (Interactive Server, WebAssembly or Auto). In a WebAssembly or Auto app, register the services in the server project and in the `.Client` project.
- **Klyroo.UI set up in the app.** `Klyroo.Spreadsheet` brings the Klyroo.UI package, and `AddKlyrooSpreadsheet()` registers its services, but the layout still needs the Klyroo.UI stylesheets in `<head>` and `<KlyApp>` around the body, as described in the [Klyroo.UI installation](https://github.com/klyroo/klyroo-ui#installation).
- **A browser with the native popover API**, which Klyroo.UI uses: Chrome and Edge 114, Safari 17 or Firefox 125, or later. In a .NET MAUI app, that means iOS or Mac Catalyst 17, or an Android System WebView 114 or later.
- **Network access on the first build** only if your app self-hosts web fonts (`KlyrooFont`), uses icons outside the built-in set, or compiles Tailwind (`KlyrooTailwind`): the build downloads them once and caches them under `obj/klyroo`. Node.js is not needed.

To build this repository you need the .NET 10 SDK (a newer SDK works too, see `global.json`) and a clone of `klyroo-ui` in the same parent folder: some projects reference it by relative path (`../klyroo-ui/...`). See [Contributing](https://github.com/klyroo/klyroo-spreadsheet/blob/main/CONTRIBUTING.md).

## Installation

```bash
dotnet add package Klyroo.Spreadsheet.Core --prerelease   # engine only
dotnet add package Klyroo.Spreadsheet --prerelease        # engine and Blazor wizard
```

For the Blazor wizard, register the services in `Program.cs` (or `MauiProgram.cs`), import the namespace in `_Imports.razor`, and load the stylesheets after the ones of Klyroo.UI:

```csharp
using Klyroo.Spreadsheet;

builder.Services.AddKlyrooSpreadsheet();
```

```razor
@using Klyroo.Spreadsheet
```

```html
<link rel="stylesheet" href="_content/Klyroo.UI/klyroo.preflight.css" />
<link rel="stylesheet" href="_content/Klyroo.UI/klyroo.css" />
<link rel="stylesheet" href="_content/Klyroo.Spreadsheet/klyroo-spreadsheet.css" />
```

## Quick start

Headless, with `Klyroo.Spreadsheet.Core`:

```csharp
using Klyroo.Spreadsheet;

var schema = SpreadsheetSchema.For<PriceRow>(s =>
{
    s.Column(r => r.Sku, c => c.Label("SKU").Required());
    s.Column(r => r.Price, c => c.Label("Unit price").DecimalSeparator(',').Required().Rule(SpreadsheetRule.Min(0m)));
    s.Column(r => r.Currency, c => c.Options(
    [
        new SpreadsheetOption<Currency>(Currency.Eur, "Euro", ["€"]),
        new SpreadsheetOption<Currency>(Currency.Usd, "US dollar", ["$"]),
    ]));
    s.OnSubmit = rows =>
    {
        foreach (var row in rows)
        {
            Console.WriteLine($"{row.Value.Sku}: {row.Value.Price} {row.Value.Currency}");
        }
        return Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>(null);
    };
});

var importer = new SpreadsheetImporter<PriceRow>(schema);
importer.LoadText("SKU;Unit price;Currency\nA-1;12,50;€\nA-2;-3;USD", "prices.csv");

Console.WriteLine(importer.ImportableRows.Count); // 1
Console.WriteLine(importer.InvalidRows.Count);    // 1: A-2 has a negative price

await importer.SubmitAsync();

public enum Currency { Eur, Usd }

public sealed class PriceRow
{
    public string? Sku { get; set; }
    public decimal Price { get; set; }
    public Currency Currency { get; set; }
}
```

The schema is typed by a row class. Each column is a property picked with a member expression, and its kind follows the property type: text for `string`, a number for `int`, `decimal` and the other numeric types, yes or no for `bool`, a date for `DateOnly`, `DateTime` and `DateTimeOffset`, and a choice for an enum or for any column with `Options`. A property that cannot hold an empty cell (`decimal`, `bool`, an enum) is required, so use `decimal?` when the cell may stay empty. The callbacks receive `SpreadsheetRow<PriceRow>`, which carries the parsed row in `Value`.

In Blazor, give the same importer to `KlySpreadsheetImport`. Users choose or drop a CSV, TSV or `.xlsx` file on its first step, or paste rows copied from a spreadsheet:

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
            s.Column(r => r.Price, c => c.Required());
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

`KlySpreadsheetImport` takes the non-generic `SpreadsheetImporter`, because it reads and edits cells as text and never touches your row type, so a `SpreadsheetImporter<PriceRow>` is passed as it is.

## Documentation

| | |
| :--- | :--- |
| [Getting started](https://github.com/klyroo/klyroo-spreadsheet/blob/main/docs/getting-started.md) | Step-by-step first integration |
| [Guides](https://github.com/klyroo/klyroo-spreadsheet/blob/main/docs/guides.md) | Task-oriented recipes |
| [Concepts](https://github.com/klyroo/klyroo-spreadsheet/blob/main/docs/concepts.md) | How the library works |
| [Platforms](https://github.com/klyroo/klyroo-spreadsheet/blob/main/docs/platforms.md) | Render modes, .NET MAUI, NativeAOT |
| [API reference](https://github.com/klyroo/klyroo-spreadsheet/blob/main/docs/api-reference.md) | Every public type and member |
| [FAQ](https://github.com/klyroo/klyroo-spreadsheet/blob/main/docs/faq.md) | Troubleshooting and limitations |

## Compatibility

- .NET 10 SDK.
- `Klyroo.Spreadsheet.Core` targets `net10.0`, has no package dependencies and is marked `IsTrimmable` and `IsAotCompatible`.
- `Klyroo.Spreadsheet` needs an interactive render mode (Interactive Server, WebAssembly or Auto): the component handles events through Blazor, so under Static SSR it renders but does not respond.
- `Klyroo.Spreadsheet` declares the `browser`, `ios`, `android`, `maccatalyst` and `windows` platforms for .NET MAUI Blazor Hybrid.
- Delimited text (CSV, TSV, semicolon-separated), `.xlsx` workbooks and legacy `.xls` workbooks (Excel 97 to 2003) are read. Formulas are not evaluated, and password-protected or Excel 5.0/95 `.xls` files are refused.

## Klyroo ecosystem

Klyroo is a family of independent .NET libraries that compose. Each one has its own repository, versions and releases.

| Library | Description |
| :--- | :--- |
| [Klyroo.UI](https://github.com/klyroo/klyroo-ui) | Accessible Blazor components for the web and .NET MAUI Blazor Hybrid, styled with Tailwind CSS v4 |
| [Klyroo.Query](https://github.com/klyroo/klyroo-query) | Typed query model, URL state sync, and IQueryable / EF Core execution |
| [Klyroo.Table](https://github.com/klyroo/klyroo-table) | Data lists with table and card-grid layouts, filters, selection and actions |
| [Klyroo.Form](https://github.com/klyroo/klyroo-form) | Schema-driven forms, validation and wizards |
| [Klyroo.Dashboard](https://github.com/klyroo/klyroo-dashboard) | Dashboard controller, filters and Blazor blocks: stats, gauges, charts, tables and feeds |
| [Klyroo.Display](https://github.com/klyroo/klyroo-display) | Code highlighting, secret and file list components, media player controls and file preview |
| **[Klyroo.Spreadsheet](https://github.com/klyroo/klyroo-spreadsheet)** | Import wizard for CSV, TSV, Excel and pasted data, with column and value mapping |
| [Klyroo.Agent](https://github.com/klyroo/klyroo-agent) | Bridge that lets AI agents read and act on Klyroo forms, dashboards and imports |
| [Klyroo.Stack](https://github.com/klyroo/klyroo-stack) | Meta-package, MCP documentation server and `dotnet new` templates |

## Contributing

Contributions are welcome. Read [CONTRIBUTING.md](https://github.com/klyroo/klyroo-spreadsheet/blob/main/CONTRIBUTING.md) before opening a pull request, and follow the
[code of conduct](https://github.com/klyroo/klyroo-spreadsheet/blob/main/CODE_OF_CONDUCT.md). Report security issues privately as described in [SECURITY.md](https://github.com/klyroo/klyroo-spreadsheet/blob/main/SECURITY.md).

## License

[MIT](https://github.com/klyroo/klyroo-spreadsheet/blob/main/LICENSE)
