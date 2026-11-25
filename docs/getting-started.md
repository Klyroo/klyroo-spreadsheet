# Getting Started with Klyroo.Spreadsheet

100% NativeAOT spreadsheet import engine in Klyroo.Spreadsheet.Core: 20-row heuristic header layout detection, NFD accent-insensitive normalization, ≥80% pass-rate column suggestions, unknown value resolution questions, duplicate & existing record upsert policies, inline cell edits, and batched submission with rejected-row retry.

## 1. Choose Your Package

| Use Case | Package to Install |
| :--- | :--- |
| Headless C#, Minimal APIs, Workers, CLI, or native C# .NET MAUI ViewModels | `Klyroo.Spreadsheet.Core` |
| Blazor Web App (Server, WebAssembly, Auto, Static SSR) or `.NET MAUI Blazor Hybrid` | `Klyroo.Spreadsheet` |

```bash
dotnet add package Klyroo.Spreadsheet.Core
dotnet add package Klyroo.Spreadsheet
```

## 2. Register Services

Call `builder.Services.AddKlyrooSpreadsheet();` in `Program.cs` (both Server and Client projects when using Blazor Interactive Auto) or `MauiProgram.cs` (after `AddMauiBlazorWebView()`):

```csharp
builder.Services.AddKlyrooSpreadsheet();
```

## 3. Add Namespace Imports

In `_Imports.razor`:

```razor
@using Klyroo.UI
@using Klyroo.Spreadsheet

```

## 4. First Example

### Headless C# (`Klyroo.Spreadsheet.Core`)

```csharp
using Klyroo.Spreadsheet;

var schema = new SpreadsheetSchema
{
    Key = "rates",
    Columns =
    [
        new SpreadsheetColumnSchema { Key = "code", Label = "Code", Required = true },
        new SpreadsheetColumnSchema { Key = "price", Label = "Unit Price", Kind = SpreadsheetColumnKind.Number, DecimalSeparator = ',', Rules = [SpreadsheetRule.Min(0)] },
        new SpreadsheetColumnSchema { Key = "currency", Label = "Currency", Kind = SpreadsheetColumnKind.Select, Options = [new("EUR", "Euro", ["€", "EUR"]), new("USD", "US Dollar", ["$", "USD"])] }
    ]
};

var importer = new SpreadsheetImporter(schema);
importer.LoadText(csvText, "rates.csv"); // Auto-detects header row & 80%-pass suggestions
```

### Blazor / MAUI Hybrid (`Klyroo.Spreadsheet`)

```razor
<KlySpreadsheetImport Importer="@_importer"
                     Title="Import Product Rates"
                     Wizard="true" />
```

## Next Steps

- [Architecture & Two-Layer Design](./architecture.md)
- [Complete API Reference](./api-reference.md)
- [.NET MAUI & NativeAOT Guide](./maui-and-aot.md)
- [Recipes & Testing](./recipes.md)
