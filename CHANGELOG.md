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

- Submit resume and cancel: the importer records the rows each batch got accepted (`AcceptedRowIndexes`, `AcceptedRows`, `PendingRows`), so after a failed or cancelled submit `SubmitAsync` sends only the rows not accepted yet, instead of sending every batch again. New `SpreadsheetSchema.OnSubmitAsync` (the cancellable form of `OnSubmit`, receiving a `CancellationToken`), `SubmitAsync(CancellationToken)`, `RetryRejectedAsync(CancellationToken)`, `CancelSubmit()`, `ResetSubmit()` and `SpreadsheetSubmitStatus.Cancelled`. The component shows a Cancel button while submitting, the error message, and a button that resumes.

- Async extension points, each cancellable and batched, with the synchronous ones kept: `SpreadsheetColumnSchema.OnCreateOptionAsync`, `SpreadsheetSchema.ValidateRowsAsync` (with `ValidateBatchSize` and `SpreadsheetIndexedIssue`) and `SpreadsheetSchema.LookupExistingRecordsAsync` (with `LookupBatchSize`, an alternative to loading `ExistingRecords`). `SpreadsheetImporter.ValidateAsync`, `CancelValidation`, `ValidationStatus`, `ValidationError`, `HasAsyncChecks` and `NeedsAsyncValidation` run the two checks on the rows not checked yet; `SubmitAsync` runs them first. The component runs them when the review opens, shows their state and offers Cancel and Check again.

- Downloads: `KlySpreadsheetImport` has a Download a template button on the file step and a Download the invalid rows button on the review step. In a browser they save the CSV through a small JavaScript module (`klyroo-spreadsheet.js`, `downloadText`); the `OnDownload` callback (`SpreadsheetDownload`: file name, content, media type) takes the file instead, for .NET MAUI Blazor Hybrid or custom storage.

- Saved mappings: `SpreadsheetMappingProfile` (normalized header to column key, ignored columns, value answers) with `ToJson`/`FromJson` on a source-generated context (`SpreadsheetMappingProfileJsonContext`, safe for NativeAOT), `SpreadsheetImporter.GetMappingProfile()`, `ApplyMappingProfile()`, `MatchesMappingProfile()` and `SavedMapping`, which is applied automatically whenever a file, sheet or header row is loaded and every header of the profile is present. The profile also guides header row detection. The component says when a saved mapping was applied.

### Changed

- `SubmitAsync` no longer starts `ImportedCount` again from zero and resends every importable row: it resumes with the rows that were not accepted (a second call after a complete submit sends nothing; `ResetSubmit()` sends everything again). The rejections of the batches that finished are kept when a later batch throws. The old behavior was the bug: a retry after a failed batch sent the accepted batches again.
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
