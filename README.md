# Klyroo.Spreadsheet

*Part of [Klyroo](https://klyroo.dev): Build better. Compose freely.*

[![CI](https://github.com/klyroo/klyroo-spreadsheet/actions/workflows/ci.yml/badge.svg)](https://github.com/klyroo/klyroo-spreadsheet/actions/workflows/ci.yml) [![NuGet](https://img.shields.io/badge/NuGet-0.1.0--preview.1-004880.svg)](https://www.nuget.org/packages/Klyroo.Spreadsheet) [![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/) [![NativeAOT](https://img.shields.io/badge/NativeAOT-Compatible-10b981.svg)](https://klyroo.dev/why) [![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](./LICENSE)

> **Headless CSV/TSV importer, 20-row header detection, 80%-pass suggestions & batch retry**

100% NativeAOT spreadsheet import engine in Klyroo.Spreadsheet.Core: 20-row heuristic header layout detection, NFD accent-insensitive normalization, ≥80% pass-rate column suggestions, unknown value resolution questions, duplicate & existing record upsert policies, inline cell edits, and batched submission with rejected-row retry.

[![MAUI Hybrid](https://img.shields.io/badge/.NET%20MAUI-Blazor%20Hybrid-8b5cf6.svg)](./docs/maui-and-aot.md)

---

## Architecture (Two-Layer Headless Split)

- **`Klyroo.Spreadsheet.Core`** — 100% headless C#, zero UI dependencies, `<IsAotCompatible>true</IsAotCompatible>`, `<IsTrimmable>true</IsTrimmable>`. Runs in Minimal APIs, background workers, CLI apps, and native C# .NET MAUI ViewModels.
- **`Klyroo.Spreadsheet`** — Multiplatform Blazor Web (Static SSR, Interactive Server, WebAssembly, Auto) & `.NET MAUI Blazor Hybrid` (`browser`, `ios`, `android`, `maccatalyst`, `windows`).

## Highlights

- **20-Row Heuristic Header Detection** (`Auto Header`) — Automatically skips export banners and title rows across the first 20 rows of a CSV/TSV or clipboard paste to locate the real header row.
- **NFD Normalization & 80%-Pass Matching** (`Smart Mapping`) — Normalizes headers (accents, *, (required)) and suggests unmapped columns only when ≥80% of sample values pass type parsing and validation rules.
- **Unknown Value Questions & Upsert** (`Reconciliation`) — Surfaces unknown select values as explicit mapping questions and resolves intra-file duplicates or updates against existing database records.
- **Batch Submit & Retry Rejected Rows** (`Batch Retry`) — Submits validated rows in configurable batches and isolates server-rejected rows for inline correction and targeted retry.

## Prerequisites

- .NET 10 SDK or later.
- A Blazor Web App (Static SSR, Interactive Server, WebAssembly or Auto) or a .NET MAUI Blazor Hybrid app for the UI
  package; the `.Core` package runs anywhere .NET 10 runs, NativeAOT included.
- For .NET MAUI: the `maui` workload (`dotnet workload install maui`).

## Installation

```bash
dotnet add package Klyroo.Spreadsheet.Core   # Headless C# / NativeAOT / MAUI C#
dotnet add package Klyroo.Spreadsheet        # Blazor Web & .NET MAUI Blazor Hybrid
```

Register services in `Program.cs` (Blazor Web App) or `MauiProgram.cs` (.NET MAUI Blazor Hybrid):

```csharp
builder.Services.AddKlyrooSpreadsheet();
```

## Quick Start

### Headless Core (`Klyroo.Spreadsheet.Core`)

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

### Blazor Web & .NET MAUI Hybrid (`Klyroo.Spreadsheet`)

```razor
<KlySpreadsheetImport Importer="@_importer"
                     Title="Import Product Rates"
                     Wizard="true" />
```

## Documentation

| Guide | Description |
| :--- | :--- |
| [Online documentation](https://docs.klyroo.dev/spreadsheet) | Guides, live examples and API reference |
| [Getting Started](./docs/getting-started.md) | Installation, DI registration, stylesheets, and first working example |
| [Architecture](./docs/architecture.md) | Headless `.Core` vs UI split, NativeAOT guarantees, and platform matrix |
| [API Reference](./docs/api-reference.md) | Exhaustive reference of every public class, enum, property, method, and Razor parameter |
| [MAUI & NativeAOT](./docs/maui-and-aot.md) | `.NET MAUI Blazor Hybrid` (`iOS`, `Android`, `macOS`, `Windows`) and NativeAOT deployment |
| [Recipes & Testing](./docs/recipes.md) | End-to-end production patterns and `xUnit` / `bUnit` testing recipes |
| [AI Agent Skill](./skills/klyroo-spreadsheet/SKILL.md) | Machine-readable guidance for coding agents |

## Key Public Types

| Type | Kind | Package | Summary |
| :--- | :--- | :--- | :--- |
| `SpreadsheetColumnKind` | `enum` | `Klyroo.Spreadsheet.Core` | Column data types in a `SpreadsheetSchema`. |
| `SpreadsheetUnknownValuePolicy` | `enum` | `Klyroo.Spreadsheet.Core` | Policy for unknown select values in `SpreadsheetColumnSchema`. |
| `SpreadsheetDuplicatesPolicy` | `enum` | `Klyroo.Spreadsheet.Core` | Duplicate row key policy in `SpreadsheetSchema`. |
| `SpreadsheetExistingAction` | `enum` | `Klyroo.Spreadsheet.Core` | Action taken when an imported row matches an existing stored record. |
| `SpreadsheetColumnStatus` | `enum` | `Klyroo.Spreadsheet.Core` | Status of a column mapping in `SpreadsheetImporter`. |
| `SpreadsheetIssueLevel` | `enum` | `Klyroo.Spreadsheet.Core` | Severity level of a `SpreadsheetRowIssue`. |
| `SpreadsheetRowStatus` | `enum` | `Klyroo.Spreadsheet.Core` | Overall validation status of a `SpreadsheetRow`. |
| `SpreadsheetDiscardReason` | `enum` | `Klyroo.Spreadsheet.Core` | Reason why a `SpreadsheetRow` was discarded from import. |
| `SpreadsheetRowMode` | `enum` | `Klyroo.Spreadsheet.Core` | Import mode of a `SpreadsheetRow`. |
| `SpreadsheetReviewTab` | `enum` | `Klyroo.Spreadsheet.Core` | Review table tab filter in `SpreadsheetImporter`. |
| `SpreadsheetReviewLevel` | `enum` | `Klyroo.Spreadsheet.Core` | Review issue level filter in `SpreadsheetImporter`. |
| `SpreadsheetValueAnswerAction` | `enum` | `Klyroo.Spreadsheet.Core` | Action chosen for an unrecognized select value in `SpreadsheetValueQuestion`. |
| `SpreadsheetSubmitStatus` | `enum` | `Klyroo.Spreadsheet.Core` | Submission lifecycle state of `SpreadsheetImporter`. |
| `SpreadsheetOption` | `record` | `Klyroo.Spreadsheet.Core` | An allowed select option with optional aliases for exact normalized matching. |
| `SpreadsheetRule` | `record` | `Klyroo.Spreadsheet.Core` | A validation rule attached to a `SpreadsheetColumnSchema`. |
| `SpreadsheetColumnSchema` | `class` | `Klyroo.Spreadsheet.Core` | Declarative definition of a spreadsheet column. |
| `SpreadsheetSchema` | `class` | `Klyroo.Spreadsheet.Core` | Top-level declarative schema for `SpreadsheetImporter` and `KlySpreadsheetImport`. |
| `SpreadsheetRejectedRow` | `record` | `Klyroo.Spreadsheet.Core` | A server-side rejection reported for a specific row index during submit. |

## Razor Components

| Component | Package | Parameters | Source |
| :--- | :--- | :--- | :--- |
| `<KlySpreadsheetImport>` | `Klyroo.Spreadsheet` | 3 parameters | `src/Klyroo.Spreadsheet/KlySpreadsheetImport.razor` |

## Building & Testing

```bash
dotnet build
dotnet test
dotnet pack -c Release
```

## Contributing

Issues and pull requests are welcome. Read [CONTRIBUTING.md](./CONTRIBUTING.md) for the workflow (Conventional
Commits, tests, documentation), follow the [Code of Conduct](./CODE_OF_CONDUCT.md), and report vulnerabilities
privately as described in [SECURITY.md](./SECURITY.md).

## Migrating from Lux

This library was previously named `lux-spreadsheet`. [MIGRATION.md](./MIGRATION.md) lists every renamed package, namespace,
component, CSS token and build setting.

## License

MIT — see [LICENSE](./LICENSE). Part of the **[Klyroo](../klyroo-stack/README.md)** ecosystem.
