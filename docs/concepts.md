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
  SpreadsheetSchema<TRow>          what you expect, typed by your row class
  SpreadsheetImporter<TRow>        the state machine and the rules (base: SpreadsheetImporter)
  SpreadsheetRow<TRow>, ...Issue   what you read back
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
   |  parse each cell            kind from the property type, default, select options, Parser
   |  run rules, ValidateRow     issues per cell and per row
   |  duplicates, existing, MaxRows, answers, discards
   v
SpreadsheetRow list              Importable, Invalid, Discarded
   |  SubmitAsync
   v
OnSubmit(batch) -> rejected rows
```

The rows are derived from three inputs: the loaded sheet, the schema, and the user's decisions. The decisions are the manual column choices, the answers to value questions, the cell edits and the discarded rows. Because the rows are derived and never edited in place, `RevertCell`, `ResetColumn` and `ClearAnswer` simply remove a decision. A change that can alter every row (a new file, sheet or header row, a column choice) reads the whole file again. A change that concerns some rows reads only those: `EditCell` and `RevertCell` parse the one row, `AnswerValue` and `ClearAnswer` the rows that hold the value, and `DiscardRows` and `RestoreRows` rebuild the rows they name. A duplicate-key index tells which other rows an edited key affects, so changing a key re-checks the rows of the old and the new key and no more. A change to the sheet or the header row clears the decisions that no longer make sense (column choices and discards, and the rejections).

**Detecting the layout.** For each sheet, the importer scores the first 20 rows by how many schema columns find a header in them, and keeps the best row; the earliest row wins a tie. The sheet with the highest score is selected, and if several sheets tie, `AmbiguousDetection` is set and the first one is used. `ForcedSheet` and `ForcedHeaderRow` bypass this. Columns declared with `From` do not take part, since they have no header of their own.

**Matching columns.** A header matches a column when its normalized text equals the normalized key, label or one of the `Headers`. Normalization removes accents, collapses spaces, lowercases, and drops a trailing `(required)` and a trailing `*`, so `Prénom * (required)` matches a column labeled `Prenom`. Each file column is used at most once, and your manual choices (`AssignColumn`, `IgnoreColumn`) are applied first.

**Saved mappings.** A `SpreadsheetMappingProfile` stores the result of this step, the header-to-column choices and the answers to value questions, and `SavedMapping` applies it as manual choices before the rows are read when the file's headers match, so the user's decisions of one import become the starting point of the next.

**Suggestions.** Matching by name is deterministic. A suggestion is the only fuzzy step, and it is deliberately narrow: it applies only to a required column with no match and no default, only when the column is not a plain text column (it needs a kind, a custom parser or a rule to test against), and only when the candidate file column has at least two non-empty cells, of which at least 80% (out of the first 30) parse and pass the error-level rules. The suggestion is data on the field state. Nothing applies it until you call `AssignColumn`.

**Parsing and issues.** Every cell yields a value for its row property and, when something is wrong, a `SpreadsheetRowIssue` with a stable `Code`. An `Error` issue blocks the row, a `Warning` does not, and `Info` is a note. A row is `Importable` when it is not discarded and has no error. Discarding has one reason per row, picked in this order: row limit, value answer, duplicate, existing record, manual.

## The wizard

`GetSteps()` describes five steps: `file`, `columns`, `values`, `review` and `submit`. The importer tracks the current step and decides which are needed. The `columns` step is passed over when no required column is missing, and the `values` step when no select value is unknown, unless the user has already been there. `NextStep` only moves when the current step is ready (a file is loaded, nothing is missing, no question is open, at least one row is importable), and `GoToStep` only goes back. The component's Back, Continue and Import buttons call these methods, and `Wizard="false"` hides the buttons and the step bar so you can call them yourself.

## Submit

`SubmitAsync` sends the importable rows that were not accepted yet to `OnSubmitAsync` (or `OnSubmit`) in batches, each row as a `SpreadsheetRow<TRow>` with its parsed `Value`, and collects the rows you reject. Rejected rows get an error issue with code `server` and stay in the importer, so the review can show them. The importer records what each batch got accepted as soon as your callback answers, so a failure or a cancellation in the middle loses nothing: `SubmitAsync` again sends only the rest. `SubmitStatus` moves through `Idle`, `Running`, and `Done`, `Error` or `Cancelled`. An exception from your callback sets `Error` and `SubmitError`. A normal run ends in `Done` even when some rows were rejected: check `RejectedCount`.

## Checks that call your server

`ValidateRowsAsync` and `LookupExistingRecordsAsync` cannot run inside the rule pipeline, which is synchronous and runs on every edit. They run on demand, in batches with a `CancellationToken`, through `ValidateAsync`: the lookup sets each row's mode first, then the validator sees the rows that are not discarded and have no error. The importer remembers per row what it already asked (and per key what it already looked up), so editing a row asks again for that row only. What the callbacks return is merged into the rows like a server rejection is: as issues and as modes. `SubmitAsync` runs the pending checks before it sends.

## Render modes and threading

The engine has no notion of a render mode. The component handles clicks through Blazor events, so it needs an interactive mode. Its one JavaScript call is the browser download of the template and of the invalid rows, made through a small module loaded on demand, and `OnDownload` replaces it. The importer raises `Changed` synchronously on the thread that called the method, and the component re-renders through `InvokeAsync`, so changes made from your own event handlers are safe. A `SpreadsheetImporter` is not thread-safe: keep one per user and per import (a page field, a scoped service), and do not share one between circuits or requests. See [Platforms](platforms.md) for what runs where.

## Errors

Bad data never throws. Unparseable cells, failed rules and refused rows become issues. The importer throws only for programming errors: a `null` schema or workbook, and an invalid regular expression or timeout in `SpreadsheetRule.Pattern`. A match that runs past the timeout is not an error: the cell gets a `pattern` issue, and a rule that times out `RuleTimeoutLimit` times is stopped and reported once in `DisabledRules`. Exceptions inside your callbacks are handled differently by place: `OnSubmit` is caught and reported in `SubmitError`, `CustomParse` becomes a cell error with the exception message, and an exception from the row factory, a property setter, a rule's `Validate` or `ValidateRow` is a row issue with the code `row.failed` and a generic message in the language of the user (`SpreadsheetTexts.IssueRowFailed`): the row is not importable and the other rows load. The text of the exception is never shown, because it can hold a path or a connection string. It goes to the `ILogger` you pass to the importer (`new SpreadsheetImporter<T>(schema, logger)` or `Importer.Logger`), one warning per rule with the number of rows it failed on and the first exception. A rule that throws on several rows is stopped like one that times out (`RuleTimeoutLimit`) and listed in `DisabledRules` with the reason `Failures`. A cancellation (`OperationCanceledException`) is not caught. `OnCreateOption` runs inside the submit, so its exception is caught and reported like one from `OnSubmit`. The async checks catch their callbacks' exceptions into `ValidationError`.

## Design decisions

- **The importer owns the state, the component only draws it.** A UI-free state machine can be tested in milliseconds and reused with any UI, at the price that the component has no state of its own to customize. If you need a different layout, build it on the importer's properties.
- **The rows are derived, never patched.** A row is always the result of parsing its text, the user's decisions and the schema, never a value edited in place, which keeps decisions reversible and the rules simple. The importer rebuilds only the rows a change concerns, and a test checks after random sequences of edits, discards and answers that the result equals a full pass over the same state. Loading a 50,000-row file takes about a second and an edit takes milliseconds, but a file is held in memory as text and as rows, so the wizard caps an import at 10,000 rows unless you raise `MaxRows`.
- **Matching is exact after normalization.** No edit distance or guessing, so the same file always maps the same way. The cost is that a misspelled header is reported as missing, with at most a suggestion based on its values.
- **The library parses text, not workbooks.** A format reader brings dependencies, licenses and security surface. `LoadWorkbook` lets you bring your own reader and keeps the Core package free of package dependencies.
- **The schema is typed by your row class, the engine is not.** `SpreadsheetSchema<TRow>` reads the properties of `TRow` from member expressions, so a column, its default, its rules, its parser and every callback are checked by the compiler, and the host receives a `TRow` (`SpreadsheetRow<TRow>.Value`) instead of a dictionary. Inside, the engine keeps the parsed values of a row as boxed values by column key and only builds a `TRow` when a callback needs one (`ValidateRow`, the batched checks, the submit). The review and the wizard work on cell text, so `SpreadsheetImporter` (the base of `SpreadsheetImporter<TRow>`) and `KlySpreadsheetImport` have no row type. There is no runtime dictionary mode: a column is always a property of a class.
