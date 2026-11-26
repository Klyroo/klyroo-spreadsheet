# Changelog

All notable changes to this project are documented in this file. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Scale: the importer updates only the rows a change concerns (`EditCell`, `RevertCell`, `AnswerValue`, `ClearAnswer`, `DiscardRows`, `RestoreRows`) instead of reading the whole file again, keeps an index of duplicate keys to re-check only the rows of the keys involved, and caches `ImportableRows`, `InvalidRows`, `DiscardedRows` and `VisibleReviewRows` until a row or a filter changes. Editing a cell of a 50,000-row file now takes milliseconds instead of about 400.
- Paged review: `ReviewPageRows`, `ReviewPage`, `ReviewPageCount`, `ReviewPageSize`, `SetReviewPage`, `SetReviewPageSize` and `SpreadsheetImporter.DefaultReviewPageSize`. `KlySpreadsheetImport` renders one page of the review table (`PageSize`, 50 by default) with a pager made of buttons (first, previous, next, last, rows per page).
- Row limit for the wizard: `KlySpreadsheetImport.MaxRows` (10,000 by default, `null` for no limit) applies when the schema has no `MaxRows`, and the component says how many rows were left out. Headless: `SpreadsheetImporter.DefaultMaxRows`, `EffectiveMaxRows` and `LimitedRowCount`.
- `SpreadsheetXlsxReader` and `SpreadsheetImporter.LoadFileAsync`: `.xlsx` workbooks are read with the base class library only (shared and inline strings, numbers, booleans, dates, every worksheet), and text files are decoded as UTF-8 or Latin-1.
- `KlySpreadsheetImport` reads files itself: users choose or drop a CSV, TSV, `.xlsx` or `.xls` file on the file step (`MaxFileSize`, 10 MB by default) or paste rows. It also offers a sheet and a header row selector, row selection with Leave out and Restore, severity and issue filters in the review, undo of edited cells in the row inspector, and shows column `Description` and `Group`.
- `SpreadsheetXlsReader`: legacy `.xls` workbooks (BIFF8 in an OLE2 compound file, Excel 97 to 2003) are read with the base class library only, with the same values as `.xlsx` (shared and inline strings, numbers, booleans, error text, dates, every worksheet, cached formula results). `SpreadsheetImporter.LoadFileAsync` and `KlySpreadsheetImport` accept them, choosing the reader by content as well as by extension; a tab-separated or CSV export named `.xls` is read as delimited text, and an HTML or XML export named `.xls` is refused. Password-protected workbooks and the Excel 5.0/95 format (BIFF5) are refused with an `InvalidDataException`, and damaged or hostile files are refused without looping or large allocations.
- `SpreadsheetWorkbook.InvariantNumbers`, so number cells of a typed workbook read whatever the column's decimal mark.

### Changed

- `ImportableRows`, `InvalidRows`, `DiscardedRows` and `AllRows` are cached lists (a list you took earlier does not change afterwards). `VisibleReviewRows` is cached too.
- The select-all checkbox of the review table selects the rows of the current page instead of every visible row.
- Matching a select cell to its option uses an index built once per column instead of normalizing every option for every cell.

### Fixed

- `SpreadsheetRule.Pattern` builds its regular expression with a match timeout (`matchTimeout`, 100 ms by default, `SpreadsheetRule.DefaultPatternTimeout`). A cell that makes a match time out, or a custom rule that throws `RegexMatchTimeoutException`, gets a `pattern` issue instead of hanging or aborting the import.

- `ImportedCount` is a running total: a retry adds the rows it got accepted instead of replacing the count.
- `SubmitAsync` does nothing unless `CanSubmit`, instead of completing as `Done` with zero rows.
- The option returned by `OnCreateOption` is used: rows carry its value and label, and a retry does not create it again.
- A step skipped because it had nothing to resolve, such as the columns step, can be reopened from the step bar to re-map a column.
- An issue filter of `|code` matches issues on the whole row, such as `row.duplicate`.
- A `Number` cell with a misplaced thousands separator, such as `12,50` with `DecimalSeparator = '.'`, is now rejected with a `number` issue instead of being read as `1250`.
- `SpreadsheetXlsxReader` padded each row up to its last value with no limit, so a small file with values in a far column (`XFD`) could hold gigabytes of memory, and its shared strings were only bounded by the 256 MB part size. Both workbook readers now refuse more than 2,000,000 cells (padding included) or 32 million characters of text.
- A cell formatted as an elapsed time (`[h]:mm`, `[mm]:ss`, built-in format 46) is read as its number of days, like other numbers, instead of as a date.
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
