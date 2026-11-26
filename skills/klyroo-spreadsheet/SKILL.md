---
name: klyroo-spreadsheet
description: Klyroo headless spreadsheet importer (layout detection, column suggestions, value mapping, row validation) and Blazor/MAUI wizard.
---

# Klyroo.Spreadsheet Skill

- Use `Klyroo.Spreadsheet.Core` (`SpreadsheetSchema.For<TRow>`, `SpreadsheetImporter<TRow>`) for deterministic CSV, TSV, Excel (`.xlsx`, `.xls`) and clipboard import pipelines in C#. It pages and caches large files, resumes a failed or cancelled submit, calls your server through async callbacks, and stores a column mapping as a `SpreadsheetMappingProfile`.
- Declare the schema against a row class: `SpreadsheetSchema.For<Row>(s => s.Column(r => r.Email, c => c.Required().Rule(SpreadsheetRule.Email())))`. The column kind follows the property type (string, numbers, bool, DateOnly, DateTime, DateTimeOffset, enums as select, nullable forms), `Default(value)`, `Parser(text => SpreadsheetParseResult<T>.Ok(value))`, `Options([new SpreadsheetOption<T>(...)])` and the rules are typed, `s.RowKey(column)` names the key column, and `ValidateRow`, `OnSubmit`, `ExistingRecords` and the async checks work on `TRow` or `SpreadsheetRow<TRow>` (`.Value` is the parsed row). Column keys are property names (`Key("...")` overrides). There is no dictionary or untyped mode.
- Use `Klyroo.Spreadsheet` (`AddKlyrooSpreadsheet()`, `KlySpreadsheetImport`) for interactive wizard or single-page spreadsheet import flows. Its texts follow the Klyroo.UI locale (`AddKlyrooSpreadsheet(s => s.Texts("fr", t => t with { ... }))`), and `OnDownload` replaces the browser download of the template and the invalid rows.
