# Concepts

This page explains how the library turns a file into rows and which part decides what. Read it when a behavior surprises you, or before you build your own UI on the engine.

## The two packages

```text
Klyroo.Spreadsheet                 Blazor / .NET MAUI Blazor Hybrid
  KlySpreadsheetImport             renders a SpreadsheetImporter, owns no state
  AddKlyrooSpreadsheet()           registers Klyroo.UI services
        |
        v
Klyroo.Spreadsheet.Core            plain C#, no UI, no package dependencies
  SpreadsheetSchema                what you expect
  SpreadsheetImporter              the state machine and the rules
  SpreadsheetRow, ...Issue, ...    what you read back
```

`Klyroo.Spreadsheet.Core` holds every decision: parsing, header detection, column matching, validation, duplicates, steps and submit. `Klyroo.Spreadsheet` contains one component, `KlySpreadsheetImport`, which reads the importer's properties, renders them, and calls the importer's methods when the user clicks. It stores nothing of its own except which cell is being edited. That split has a practical consequence: anything the component can do, your own code can do by calling the same method, and you can test it without a renderer.

## From file to rows

```text
text / workbook
   |  LoadText, Paste, LoadWorkbook
   v
sheets of rows of text           ParseDelimitedSheet (or your own reader)
   |  pick sheet + header row    scores each sheet, scans 20 rows
   v
headers
   |  match to schema columns    key, label, Headers aliases, then your manual choices
   v
field states                     Matched / Default / Missing / Unmatched (+ Suggestion)
   |  parse each cell            kind, default, select options, CustomParse
   |  run rules, ValidateRow     issues per cell and per row
   |  duplicates, existing, MaxRows, answers, discards
   v
SpreadsheetRow list              Importable, Invalid, Discarded
   |  SubmitAsync
   v
OnSubmit(batch) -> rejected rows
```

The importer recomputes this whole pipeline after every change, from three inputs: the loaded sheet, the schema, and the user's decisions. The decisions are the manual column choices, the answers to value questions, the cell edits and the discarded rows. Because the rows are derived and never edited in place, `RevertCell`, `ResetColumn` and `ClearAnswer` simply remove a decision. A change to the sheet or the header row clears the decisions that no longer make sense (column choices and discards, and the rejections).

**Detecting the layout.** For each sheet, the importer scores the first 20 rows by how many schema columns find a header in them, and keeps the best row; the earliest row wins a tie. The sheet with the highest score is selected, and if several sheets tie, `AmbiguousDetection` is set and the first one is used. `ForcedSheet` and `ForcedHeaderRow` bypass this. Columns declared with `From` do not take part, since they have no header of their own.

**Matching columns.** A header matches a column when its normalized text equals the normalized key, label or one of the `Headers`. Normalization removes accents, collapses spaces, lowercases, and drops a trailing `(required)` and a trailing `*`, so `Prénom * (required)` matches a column labeled `Prenom`. Each file column is used at most once, and your manual choices (`AssignColumn`, `IgnoreColumn`) are applied first.

**Suggestions.** Matching by name is deterministic. A suggestion is the only fuzzy step, and it is deliberately narrow: it applies only to a required column with no match and no default, only when the column is not a plain text column (it needs a kind, a custom parser or a rule to test against), and only when the candidate file column has at least two non-empty cells, of which at least 80% (out of the first 30) parse and pass the error-level rules. The suggestion is data on the field state. Nothing applies it until you call `AssignColumn`.

**Parsing and issues.** Every cell yields a value in `row.Data` and, when something is wrong, a `SpreadsheetRowIssue` with a stable `Code`. An `Error` issue blocks the row, a `Warning` does not, and `Info` is a note. A row is `Importable` when it is not discarded and has no error. Discarding has one reason per row, picked in this order: row limit, value answer, duplicate, existing record, manual.

## The wizard

`GetSteps()` describes five steps: `file`, `columns`, `values`, `review` and `submit`. The importer tracks the current step and decides which are needed. The `columns` step is passed over when no required column is missing, and the `values` step when no select value is unknown, unless the user has already been there. `NextStep` only moves when the current step is ready (a file is loaded, nothing is missing, no question is open, at least one row is importable), and `GoToStep` only goes back. The component's Back, Continue and Import buttons call these methods, and `Wizard="false"` hides the buttons and the step bar so you can call them yourself.

## Submit

`SubmitAsync` sends the importable rows to `OnSubmit` in batches and collects the rows you reject. Rejected rows get an error issue with code `server` and stay in the importer, so the review can show them. `SubmitStatus` moves through `Idle`, `Running`, `Done` or `Error`. An exception from your callback sets `Error` and `SubmitError`. A normal run ends in `Done` even when some rows were rejected: check `RejectedCount`.

## Render modes and threading

The engine has no notion of a render mode. The component handles clicks through Blazor events, so it needs an interactive mode, and the component itself makes no JavaScript calls. The importer raises `Changed` synchronously on the thread that called the method, and the component re-renders through `InvokeAsync`, so changes made from your own event handlers are safe. A `SpreadsheetImporter` is not thread-safe: keep one per user and per import (a page field, a scoped service), and do not share one between circuits or requests. See [Platforms](platforms.md) for what runs where.

## Errors

Bad data never throws. Unparseable cells, failed rules and refused rows become issues. The importer throws only for programming errors: a `null` schema or workbook, and an invalid regular expression in `SpreadsheetRule.Pattern`. Exceptions inside your callbacks are handled differently by place: `OnSubmit` is caught and reported in `SubmitError`, `CustomParse` becomes a cell error with the exception message, and an exception from a rule's `Validate` or from `ValidateRow` is a bug in your code that propagates to the caller of whichever method triggered the recompute (`LoadText`, `EditCell` and so on). `OnCreateOption` runs inside the submit, so its exception is caught and reported like one from `OnSubmit`.

## Design decisions

- **The importer owns the state, the component only draws it.** A UI-free state machine can be tested in milliseconds and reused with any UI, at the price that the component has no state of its own to customize. If you need a different layout, build it on the importer's properties.
- **The rows are recomputed, never patched.** After each change the importer re-parses every row. This keeps decisions reversible and the rules simple, and it is fast enough for thousands of rows (the test suite imports 5,000 rows and asserts it takes less than 5 seconds), but it makes each edit cost a pass over the file, so it is not meant for hundreds of thousands of rows.
- **Matching is exact after normalization.** No edit distance or guessing, so the same file always maps the same way. The cost is that a misspelled header is reported as missing, with at most a suggestion based on its values.
- **The library parses text, not workbooks.** A format reader brings dependencies, licenses and security surface. `LoadWorkbook` lets you bring your own reader and keeps the Core package free of package dependencies.
- **Values are plain objects.** `row.Data` is a dictionary of `object?` (string, double, bool, ISO date string, option value), which keeps the engine independent of your model. Mapping a row to your type is your code in `OnSubmit`.
