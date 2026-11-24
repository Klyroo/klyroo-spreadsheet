# Getting Started with Lux.Spreadsheet

100% NativeAOT spreadsheet import engine in Lux.Spreadsheet.Core: 20-row heuristic header layout detection, NFD accent-insensitive normalization, ≥80% pass-rate column suggestions, unknown value resolution questions, duplicate & existing record upsert policies, inline cell edits, and batched submission with rejected-row retry.

## 1. Choose Your Package

| Use Case | Package to Install |
| :--- | :--- |
| Headless C#, Minimal APIs, Workers, CLI, or native C# .NET MAUI ViewModels | `Lux.Spreadsheet.Core` |
| Blazor Web App (Server, WebAssembly, Auto, Static SSR) or `.NET MAUI Blazor Hybrid` | `Lux.Spreadsheet` |

```bash
dotnet add package Lux.Spreadsheet.Core
dotnet add package Lux.Spreadsheet
```

## 2. Register Services

Call `builder.Services.AddLuxSpreadsheet();` in `Program.cs` (both Server and Client projects when using Blazor Interactive Auto) or `MauiProgram.cs` (after `AddMauiBlazorWebView()`):

```csharp
builder.Services.AddLuxSpreadsheet();
```

## 3. Add Namespace Imports

In `_Imports.razor`:

```razor
@using Lux.UI
@using Lux.Spreadsheet

```

## 4. First Example

### Headless C# (`Lux.Spreadsheet.Core`)

```csharp
using Lux.Spreadsheet;

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

### Blazor / MAUI Hybrid (`Lux.Spreadsheet`)

```razor
<LxSpreadsheetImport Importer="@_importer"
                     Title="Import Product Rates"
                     Wizard="true" />
```

## Next Steps

- [Architecture & Two-Layer Design](./architecture.md)
- [Complete API Reference](./api-reference.md)
- [.NET MAUI & NativeAOT Guide](./maui-and-aot.md)
- [Recipes & Testing](./recipes.md)
