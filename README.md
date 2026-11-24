# Lux.Spreadsheet (LuxStack)

> **Headless CSV/TSV importer, 20-row header detection, 80%-pass suggestions & batch retry**

100% NativeAOT spreadsheet import engine in Lux.Spreadsheet.Core: 20-row heuristic header layout detection, NFD accent-insensitive normalization, ≥80% pass-rate column suggestions, unknown value resolution questions, duplicate & existing record upsert policies, inline cell edits, and batched submission with rejected-row retry.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](./LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![NativeAOT](https://img.shields.io/badge/Core-NativeAOT%20Ready-10b981.svg)](./docs/maui-and-aot.md)
[![MAUI Hybrid](https://img.shields.io/badge/.NET%20MAUI-Blazor%20Hybrid-8b5cf6.svg)](./docs/maui-and-aot.md)

---

## Architecture (Two-Layer Headless Split)

- **`Lux.Spreadsheet.Core`** — 100% headless C#, zero UI dependencies, `<IsAotCompatible>true</IsAotCompatible>`, `<IsTrimmable>true</IsTrimmable>`. Runs in Minimal APIs, background workers, CLI apps, and native C# .NET MAUI ViewModels.
- **`Lux.Spreadsheet`** — Multiplatform Blazor Web (Static SSR, Interactive Server, WebAssembly, Auto) & `.NET MAUI Blazor Hybrid` (`browser`, `ios`, `android`, `maccatalyst`, `windows`).

## Highlights

- **20-Row Heuristic Header Detection** (`Auto Header`) — Automatically skips export banners and title rows across the first 20 rows of a CSV/TSV or clipboard paste to locate the real header row.
- **NFD Normalization & 80%-Pass Matching** (`Smart Mapping`) — Normalizes headers (accents, *, (required)) and suggests unmapped columns only when ≥80% of sample values pass type parsing and validation rules.
- **Unknown Value Questions & Upsert** (`Reconciliation`) — Surfaces unknown select values as explicit mapping questions and resolves intra-file duplicates or updates against existing database records.
- **Batch Submit & Retry Rejected Rows** (`Batch Retry`) — Submits validated rows in configurable batches and isolates server-rejected rows for inline correction and targeted retry.

## Installation

```bash
dotnet add package Lux.Spreadsheet.Core   # Headless C# / NativeAOT / MAUI C#
dotnet add package Lux.Spreadsheet        # Blazor Web & .NET MAUI Blazor Hybrid
```

Register services in `Program.cs` (Blazor Web App) or `MauiProgram.cs` (.NET MAUI Blazor Hybrid):

```csharp
builder.Services.AddLuxSpreadsheet();
```

## Quick Start

### Headless Core (`Lux.Spreadsheet.Core`)

```csharp
using Blux.UI.Tools;

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

### Blazor Web & .NET MAUI Hybrid (`Lux.Spreadsheet`)

```razor
<BxSpreadsheetImport Importer="@_importer"
                     Title="Import Product Rates"
                     Wizard="true" />
```

## Documentation

| Guide | Description |
| :--- | :--- |
| [Getting Started](./docs/getting-started.md) | Installation, DI registration, stylesheets, and first working example |
| [Architecture](./docs/architecture.md) | Headless `.Core` vs UI split, NativeAOT guarantees, and platform matrix |
| [API Reference](./docs/api-reference.md) | Exhaustive reference of every public class, enum, property, method, and Razor parameter |
| [MAUI & NativeAOT](./docs/maui-and-aot.md) | `.NET MAUI Blazor Hybrid` (`iOS`, `Android`, `macOS`, `Windows`) and NativeAOT deployment |
| [Recipes & Testing](./docs/recipes.md) | End-to-end production patterns and `xUnit` / `bUnit` testing recipes |
| [AI Agent Skill](./skills/lux-spreadsheet/SKILL.md) | Machine-readable guidance for coding agents |

## Key Public Types

| Type | Kind | Package | Summary |
| :--- | :--- | :--- | :--- |
| `SpreadsheetColumnKind` | `enum` | `Lux.Spreadsheet.Core` | Column data types in a `SpreadsheetSchema`. |
| `SpreadsheetUnknownValuePolicy` | `enum` | `Lux.Spreadsheet.Core` | Policy for unknown select values in `SpreadsheetColumnSchema`. |
| `SpreadsheetDuplicatesPolicy` | `enum` | `Lux.Spreadsheet.Core` | Duplicate row key policy in `SpreadsheetSchema`. |
| `SpreadsheetExistingAction` | `enum` | `Lux.Spreadsheet.Core` | Action taken when an imported row matches an existing stored record. |
| `SpreadsheetColumnStatus` | `enum` | `Lux.Spreadsheet.Core` | Status of a column mapping in `SpreadsheetImporter`. |
| `SpreadsheetIssueLevel` | `enum` | `Lux.Spreadsheet.Core` | Severity level of a `SpreadsheetRowIssue`. |
| `SpreadsheetRowStatus` | `enum` | `Lux.Spreadsheet.Core` | Overall validation status of a `SpreadsheetRow`. |
| `SpreadsheetDiscardReason` | `enum` | `Lux.Spreadsheet.Core` | Reason why a `SpreadsheetRow` was discarded from import. |
| `SpreadsheetRowMode` | `enum` | `Lux.Spreadsheet.Core` | Import mode of a `SpreadsheetRow`. |
| `SpreadsheetReviewTab` | `enum` | `Lux.Spreadsheet.Core` | Review table tab filter in `SpreadsheetImporter`. |
| `SpreadsheetReviewLevel` | `enum` | `Lux.Spreadsheet.Core` | Review issue level filter in `SpreadsheetImporter`. |
| `SpreadsheetValueAnswerAction` | `enum` | `Lux.Spreadsheet.Core` | Action chosen for an unrecognized select value in `SpreadsheetValueQuestion`. |
| `SpreadsheetSubmitStatus` | `enum` | `Lux.Spreadsheet.Core` | Submission lifecycle state of `SpreadsheetImporter`. |
| `SpreadsheetOption` | `record` | `Lux.Spreadsheet.Core` | An allowed select option with optional aliases for exact normalized matching. |
| `SpreadsheetRule` | `record` | `Lux.Spreadsheet.Core` | A validation rule attached to a `SpreadsheetColumnSchema`. |
| `SpreadsheetColumnSchema` | `class` | `Lux.Spreadsheet.Core` | Declarative definition of a spreadsheet column. |
| `SpreadsheetSchema` | `class` | `Lux.Spreadsheet.Core` | Top-level declarative schema for `SpreadsheetImporter` and `BxSpreadsheetImport`. |
| `SpreadsheetRejectedRow` | `record` | `Lux.Spreadsheet.Core` | A server-side rejection reported for a specific row index during submit. |

## Razor Components

| Component | Package | Parameters | Source |
| :--- | :--- | :--- | :--- |
| `<BxSpreadsheetImport>` | `Lux.Spreadsheet` | 3 parameters | `src/Lux.Spreadsheet/BxSpreadsheetImport.razor` |

## Building & Testing

```bash
dotnet build
dotnet test
dotnet pack -c Release
```

## License

MIT — see [LICENSE](./LICENSE). Part of the **[LuxStack](../lux.stack/README.md)** ecosystem.
