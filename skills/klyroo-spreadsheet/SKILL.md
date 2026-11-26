---
name: klyroo-spreadsheet
description: Klyroo headless spreadsheet importer (layout detection, column suggestions, value mapping, row validation) and Blazor/MAUI wizard.
---

# Klyroo.Spreadsheet Skill

- Use `Klyroo.Spreadsheet.Core` (`SpreadsheetSchema`, `SpreadsheetImporter`) for deterministic CSV, TSV, Excel (`.xlsx`, `.xls`) and clipboard import pipelines in C#. It pages and caches large files, resumes a failed or cancelled submit, calls your server through async callbacks, and stores a column mapping as a `SpreadsheetMappingProfile`.
- Use `Klyroo.Spreadsheet` (`AddKlyrooSpreadsheet()`, `KlySpreadsheetImport`) for interactive wizard or single-page spreadsheet import flows. Its texts follow the Klyroo.UI locale (`AddKlyrooSpreadsheet(s => s.Texts("fr", t => t with { ... }))`), and `OnDownload` replaces the browser download of the template and the invalid rows.
