# Changelog

All notable changes to this project are documented in this file. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- A `Number` cell with a misplaced thousands separator, such as `12,50` with `DecimalSeparator = '.'`, is now rejected with a `number` issue instead of being read as `1250`.
- `ExportInvalidRowsCsv` wrote the file's own headers above values in schema order, so columns were shifted when the file used another order. Headers now follow the schema columns.

## [0.1.0-preview.1] - 2026-11-24

### Added

- `Klyroo.Spreadsheet.Core`: a headless `SpreadsheetImporter` that loads CSV, TSV and pasted text, or a workbook you parse yourself, and detects the sheet, the separator and the header row.
- Column mapping by normalized header names and aliases, manual assignment, and suggestions for missing required columns based on sample values.
- Typed parsing for text, number, date, boolean and select columns, with built-in and custom rules, a cross-field `ValidateRow` callback and per-cell issues with stable codes.
- Resolution of unknown select values by answering, or by a per-column policy to leave empty, skip rows, create the value or fail.
- Duplicate key detection inside a file and handling of rows that match existing records as updates, skips or errors.
- Inline cell editing, row discard and restore, review filters, and CSV export of a template and of invalid rows.
- Batched submit through `OnSubmit` with per-row server rejections and a retry of the rejected rows.
- `Klyroo.Spreadsheet`: the `KlySpreadsheetImport` wizard for Blazor and .NET MAUI Blazor Hybrid, with column and value mapping, an editable review table, a row inspector and a precompiled stylesheet, registered with `AddKlyrooSpreadsheet()`.

[Unreleased]: https://github.com/klyroo/klyroo-spreadsheet/compare/v0.1.0-preview.1...HEAD
[0.1.0-preview.1]: https://github.com/klyroo/klyroo-spreadsheet/releases/tag/v0.1.0-preview.1
