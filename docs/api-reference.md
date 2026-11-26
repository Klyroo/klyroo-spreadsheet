# Klyroo.Spreadsheet API reference

> Generated from the compiled assemblies and their XML documentation. Do not edit by hand.

## Klyroo.Spreadsheet.Core

Static classes and extensions (3) · Classes (11) · Records (20) · Structs (1) · Enums (15) · Delegates (1)

### Static classes and extensions

#### `SpreadsheetColumnBuilderExtensions`

`Klyroo.Spreadsheet` · service

Settings that exist only for some column value types.

| Method | Returns | Description |
| :--- | :--- | :--- |
| `static DateFormats<TRow>(SpreadsheetColumnBuilder<TRow, DateOnly> builder, params string[] formats)` | `SpreadsheetColumnBuilder<TRow, DateOnly>` | Sets the exact formats a `DateOnly` column reads, instead of the defaults. |
| `static DateFormats<TRow>(SpreadsheetColumnBuilder<TRow, DateOnly?> builder, params string[] formats)` | `SpreadsheetColumnBuilder<TRow, DateOnly?>` | Sets the exact formats a nullable `DateOnly` column reads, instead of the defaults. |
| `static DateFormats<TRow>(SpreadsheetColumnBuilder<TRow, DateTime> builder, params string[] formats)` | `SpreadsheetColumnBuilder<TRow, DateTime>` | Sets the exact formats a `DateTime` column reads, instead of the defaults. |
| `static DateFormats<TRow>(SpreadsheetColumnBuilder<TRow, DateTime?> builder, params string[] formats)` | `SpreadsheetColumnBuilder<TRow, DateTime?>` | Sets the exact formats a nullable `DateTime` column reads, instead of the defaults. |
| `static DateFormats<TRow>(SpreadsheetColumnBuilder<TRow, DateTimeOffset> builder, params string[] formats)` | `SpreadsheetColumnBuilder<TRow, DateTimeOffset>` | Sets the exact formats a `DateTimeOffset` column reads, instead of the defaults. An offset in the cell (a format with `zzz` or `K`) is kept, so `2026-03-05 10:00 +02:00` is that instant with that offset whatever the time zone of the machine; a cell without an offset is read as UTC. |
| `static DateFormats<TRow>(SpreadsheetColumnBuilder<TRow, DateTimeOffset?> builder, params string[] formats)` | `SpreadsheetColumnBuilder<TRow, DateTimeOffset?>` | Sets the exact formats a nullable `DateTimeOffset` column reads, instead of the defaults. An offset in the cell (a format with `zzz` or `K`) is kept, so `2026-03-05 10:00 +02:00` is that instant with that offset whatever the time zone of the machine; a cell without an offset is read as UTC. |
| `static DecimalSeparator<TRow, TValue>(SpreadsheetColumnBuilder<TRow, TValue> builder, char separator)` | `SpreadsheetColumnBuilder<TRow, TValue>` | Sets the decimal mark of a number column, `.` or `,`. The other character is accepted as a thousands separator only between groups of three digits. |
| `static DecimalSeparator<TRow, TValue>(SpreadsheetColumnBuilder<TRow, TValue?> builder, char separator)` | `SpreadsheetColumnBuilder<TRow, TValue?>` | Sets the decimal mark of a nullable number column, `.` or `,`. |
| `static Options<TRow, TValue>(SpreadsheetColumnBuilder<TRow, TValue?> builder, IEnumerable<SpreadsheetOption<TValue>> options)` | `SpreadsheetColumnBuilder<TRow, TValue?>` | Makes a nullable column a select with these allowed values. |
| `static Rule<TRow, TValue>(SpreadsheetColumnBuilder<TRow, TValue?> builder, SpreadsheetRule<TValue> rule)` | `SpreadsheetColumnBuilder<TRow, TValue?>` | Adds a rule to a nullable column. The rule is typed like the underlying value because it only runs on a cell that holds one. |

#### `SpreadsheetXlsReader`

`Klyroo.Spreadsheet` · service

Reads a legacy Excel workbook (`.xls`: BIFF8 records inside an OLE2 compound file, Excel 97 to 2003) into a `SpreadsheetWorkbook`, with the base class library only. It gives the same values as `SpreadsheetXlsxReader`: every worksheet becomes a sheet (hidden ones too), strings are text, numbers keep the invariant culture, booleans become `TRUE` or `FALSE`, error cells give their text (`#DIV/0!`), and numbers formatted as dates become `yyyy-MM-dd` (with `HH:mm` or `HH:mm:ss` when they have a time). Formulas are not evaluated: the value Excel saved is used. Password-protected workbooks and the Excel 5.0/95 format (BIFF5) are refused. The content is treated as untrusted: malformed or hostile files throw `InvalidDataException` instead of looping or allocating without bound.

| Field | Type | Description |
| :--- | :--- | :--- |
| `const MaxFileLength = 268435456` | `long` | The largest file, and the largest decoded stream, 256 MB, beyond which the file is refused. |

| Method | Returns | Description |
| :--- | :--- | :--- |
| `static Read(Stream stream, string fileName)` | `SpreadsheetWorkbook` | Reads a workbook. |

#### `SpreadsheetXlsxReader`

`Klyroo.Spreadsheet` · service

Reads an Excel workbook in the Office Open XML format (`.xlsx`) into a `SpreadsheetWorkbook`, with the base class library only. Every worksheet becomes a sheet. Shared, inline and formula strings are read as text, numbers keep the invariant culture (the importer reads them whatever the column's decimal mark), booleans become `TRUE` or `FALSE`, and numbers formatted as dates become `yyyy-MM-dd` (with `HH:mm` or `HH:mm:ss` when they have a time). Formulas are not evaluated: the value Excel saved is used. The legacy binary format (`.xls`) is read by `SpreadsheetXlsReader`.

| Field | Type | Description |
| :--- | :--- | :--- |
| `const MaxPartLength = 268435456` | `long` | The largest uncompressed size of one part of the file, 256 MB, beyond which the file is refused. |

| Method | Returns | Description |
| :--- | :--- | :--- |
| `static Read(Stream stream, string fileName)` | `SpreadsheetWorkbook` | Reads a workbook. |

### Classes

#### `SpreadsheetColumn<TRow>`

`Klyroo.Spreadsheet` · class · inherits `SpreadsheetColumnSchema`

A column of a row type, with the value type erased. It is the type the schema stores.

#### `SpreadsheetColumn<TRow, TValue>`

`Klyroo.Spreadsheet` · class · inherits `SpreadsheetColumn<TRow>`

A column bound to one property of a row type. The kind of cell it reads follows `TValue`.

| Property | Type | Description |
| :--- | :--- | :--- |
| `DefaultValue` | `TValue` | The value used for empty cells when `SpreadsheetColumnSchema.HasDefault` is `true`. |

#### `SpreadsheetColumnBuilder<TRow, TValue>`

`Klyroo.Spreadsheet` · class

Configures one column. Methods return the builder so calls chain: `c => c.Required().Headers("mail")`. Settings that only make sense for some value types are extension methods on this type, so they do not compile on the others.

| Method | Returns | Description |
| :--- | :--- | :--- |
| `Default(TValue value)` | `SpreadsheetColumnBuilder<TRow, TValue>` | Sets the value used for empty cells. A column with a default is never missing. For a nullable column the default may itself be `null`. |
| `Description(string description)` | `SpreadsheetColumnBuilder<TRow, TValue>` | Sets help text shown under the column name. |
| `Editable(bool editable)` | `SpreadsheetColumnBuilder<TRow, TValue>` | Sets whether the component lets the user edit this column's cells in the review table. |
| `Example(string example)` | `SpreadsheetColumnBuilder<TRow, TValue>` | Sets the sample value written in the second row of the CSV template. |
| `From(SpreadsheetColumnSchema source)` | `SpreadsheetColumnBuilder<TRow, TValue>` | Makes the column reuse the cell text of an earlier column instead of reading its own file column. Such a column never matches a header and is left out of the template. |
| `Group(string group)` | `SpreadsheetColumnBuilder<TRow, TValue>` | Sets the grouping label the component uses in the column mapping. |
| `Headers(params string[] headers)` | `SpreadsheetColumnBuilder<TRow, TValue>` | Adds header spellings that match this column, on top of the key and the label. |
| `Key(string key)` | `SpreadsheetColumnBuilder<TRow, TValue>` | Overrides the key, which is the property name by default. The key is what a saved mapping profile stores, so set it to keep a profile valid when you rename the property. |
| `Label(string label)` | `SpreadsheetColumnBuilder<TRow, TValue>` | Sets the display name, also matched against file headers. |
| `OnCreateOption(Func<string, SpreadsheetOption<TValue>> create)` | `SpreadsheetColumnBuilder<TRow, TValue>` | Called once per distinct value answered with `SpreadsheetValueAnswerAction.Create`, when the import is submitted and before any row is sent. Use it to persist the new option. The rows then carry the returned option's value and later sends do not call it again for that value. Setting it makes the component offer a create choice. |
| `OnCreateOptionAsync(Func<string, CancellationToken, Task<SpreadsheetOption<TValue>>> create)` | `SpreadsheetColumnBuilder<TRow, TValue>` | The asynchronous form of `SpreadsheetColumnBuilder.OnCreateOption`, for a callback that calls a server. It receives the cancellation token of the submit. When both are set, this one is used. |
| `Options(IEnumerable<SpreadsheetOption<TValue>> options)` | `SpreadsheetColumnBuilder<TRow, TValue>` | Makes the column a select with these allowed values. Cells that match none become value questions governed by `SpreadsheetColumnBuilder.UnknownPolicy`. |
| `Parser(SpreadsheetParser<TValue> parser)` | `SpreadsheetColumnBuilder<TRow, TValue>` | Replaces the built-in parsing of a non-select column with your own. |
| `Required(bool required = true)` | `SpreadsheetColumnBuilder<TRow, TValue>` | Makes an empty cell a blocking issue and a missing file column a blocker, unless the column has a default. |
| `Rule(SpreadsheetRule<TValue> rule)` | `SpreadsheetColumnBuilder<TRow, TValue>` | Adds a validation rule, run on every non-empty parsed value in the order the rules were added. |
| `Rule(string code, string message, Func<TValue, bool> validate, SpreadsheetIssueLevel level = Error)` | `SpreadsheetColumnBuilder<TRow, TValue>` | Adds a validation rule written as a predicate. |
| `UnknownPolicy(SpreadsheetUnknownValuePolicy policy)` | `SpreadsheetColumnBuilder<TRow, TValue>` | Sets what happens to select cells that match no option. |

#### `SpreadsheetColumnSchema`

`Klyroo.Spreadsheet` · class

Declares one field of the imported data: how to find it in the file, how to parse it and how to validate it. Create columns with `SpreadsheetSchemaBuilder.Column``.

| Property | Type | Description |
| :--- | :--- | :--- |
| `CanCreateOption` | `bool` | Whether the column creates new options: it has an `SpreadsheetColumnBuilder.OnCreateOption` or `SpreadsheetColumnBuilder.OnCreateOptionAsync` callback, so the component offers a create choice (`SpreadsheetValueQuestion.CanCreate`). |
| `DateFormats` | `IReadOnlyList<string>?` | Exact `DateTime` format strings accepted for `SpreadsheetColumnKind.Date` cells. When empty, the defaults are `yyyy-MM-dd`, `yyyy-MM-dd HH:mm`, `yyyy-MM-dd HH:mm:ss`, `dd/MM/yyyy` and `dd/MM/yyyy HH:mm`. |
| `DecimalSeparator` | `char` | The decimal mark of `SpreadsheetColumnKind.Number` cells, `.` or `,`. The other character is accepted as a thousands separator only between groups of three digits, so `12,50` is rejected when the mark is `.`. |
| `Description` | `string?` | Help text for your own UI and for the built-in component, which shows it under the column name. |
| `Editable` | `bool` | Whether the built-in component lets the user edit this column's cells in the review table. `SpreadsheetImporter.EditCell` ignores it. |
| `EffectiveLabel` | `string` | `SpreadsheetColumnSchema.Label` when set, otherwise `SpreadsheetColumnSchema.Key` turned into words, for example `SecureCode` becomes `Secure code`. |
| `Example` | `string?` | A sample value written in the second row of `SpreadsheetImporter.ExportTemplateCsv`. |
| `From` | `string?` | The `SpreadsheetColumnSchema.Key` of an earlier column whose cell text this column reuses instead of reading its own file column. A column with `SpreadsheetColumnSchema.From` never matches a header and is left out of the template. |
| `Group` | `string?` | A grouping label for the built-in component, which groups the column mapping by it. |
| `HasDefault` | `bool` | Whether the column has a default for empty cells. A column with a default is never missing. |
| `Headers` | `IReadOnlyList<string>?` | Additional header spellings that match this column, on top of `SpreadsheetColumnSchema.Key` and `SpreadsheetColumnSchema.EffectiveLabel`. |
| `Key` | `string` | The field name. It is the key of `SpreadsheetRow.Cells` and it also matches a file header. It defaults to the name of the row property. |
| `Kind` | `SpreadsheetColumnKind` | How cells are parsed. It follows the value type: text for `String`, number for numeric types, boolean for `Boolean`, date for `DateOnly`, `DateTime` and `DateTimeOffset`, select for enums and for any column with options. |
| `Label` | `string?` | The display name, also matched against file headers. Defaults to `SpreadsheetColumnSchema.EffectiveLabel`. |
| `Options` | `IReadOnlyList<SpreadsheetOption>?` | The allowed values of a `SpreadsheetColumnKind.Select` column, as text. For an enum column without explicit options, one per member. |
| `Required` | `bool` | When `true`, an empty cell is a blocking issue (code `required`) and a file with no matching column cannot be submitted, unless the column has a default. A column whose property type is a non-nullable value type (`Int32`, `Boolean`, an enum) is always required, because an empty cell cannot be stored in it. |
| `Rules` | `IReadOnlyList<SpreadsheetRule>?` | Validation rules run, in order, on every non-empty parsed value. |
| `UnknownPolicy` | `SpreadsheetUnknownValuePolicy` | What to do with select cells that match no option. Defaults to `SpreadsheetUnknownValuePolicy.Ask`. |
| `ValueType` | `Type` | The type of the row property this column fills. |

#### `SpreadsheetImporter`

`Klyroo.Spreadsheet` · class

The headless import engine. It loads delimited text or a prepared workbook, finds the sheet and header row, maps file columns to the columns of a `SpreadsheetSchema`, parses and validates every row, tracks the user's corrections, and sends the importable rows to the schema's submit callback in batches.

| Field | Type | Description |
| :--- | :--- | :--- |
| `const DefaultReviewPageSize = 50` | `int` | How many rows `SpreadsheetImporter.DefaultReviewPageSize` and `SpreadsheetImporter.ReviewPageRows` use by default. |
| `const HeaderScanRows = 20` | `int` | How many leading rows of a sheet are scanned to find the header row. |
| `const SuggestionPassRate = 0.8` | `double` | The share of sampled cells that must parse and pass the error-level rules before a file column is suggested for a missing column. |
| `const SuggestionSampleSize = 30` | `int` | The maximum number of non-empty cells sampled from a file column when looking for a suggestion. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `AcceptedRowIndexes` | `IReadOnlySet<int>` | The `SpreadsheetRow.Index` of every row a batch sent by `SpreadsheetImporter.SubmitAsync` or `SpreadsheetImporter.RetryRejectedAsync` got accepted. It survives a failed or cancelled submit and is what a retry leaves out. A new file, sheet or header row, and `SpreadsheetImporter.ResetSubmit`, empty it. |
| `AcceptedRows` | `IReadOnlyList<SpreadsheetRow>` | The rows whose index is in `SpreadsheetImporter.AcceptedRowIndexes`, in row order, as the importer currently sees them. |
| `AllRows` | `IReadOnlyList<SpreadsheetRow>` | Every data row after the header, discarded ones included. The list is a snapshot: it does not change when you edit afterwards, and it is rebuilt on the next access after a change. |
| `AmbiguousDetection` | `bool` | `true` when several sheets matched the schema equally well and the first one was picked. Offer the user a sheet choice in that case. |
| `CanNextStep` | `bool` | Whether the current step is ready and is not the last one. |
| `CanPreviousStep` | `bool` | Whether there is an earlier step and no submit is running. |
| `CanSubmit` | `bool` | `true` when a file is loaded, no field is missing, no question is open, no async check is running and at least one importable row is still pending (not accepted yet, see `SpreadsheetImporter.PendingRows`). The component disables its Import button when this is `false`. |
| `CurrentSheet` | `SpreadsheetSheet?` | The sheet being imported: the selected one, otherwise the first. |
| `CurrentStepKey` | `string` | The key of the wizard step in progress: `file`, `columns`, `values`, `review` or `submit`. |
| `DefaultMaxRows` (get, set) | `int?` | A row limit that applies when `SpreadsheetSchema.MaxRows` is `null`. `null` (the default) means no limit. `KlySpreadsheetImport` sets it from its `MaxRows` parameter. Rows past the limit are left out with `SpreadsheetDiscardReason.Limit`. |
| `DisabledRules` | `IReadOnlyList<SpreadsheetDisabledRule>` | The rules the importer stopped evaluating in the current read because their matches kept timing out (see `SpreadsheetSchema.RuleTimeoutLimit`). Values after the rule was stopped were not checked by it. Empty in the normal case. |
| `DiscardedRows` | `IReadOnlyList<SpreadsheetRow>` | The rows left out of the import for any reason. The list is cached until a row changes. |
| `EffectiveMaxRows` | `int?` | The row limit in force: `SpreadsheetSchema.MaxRows` when set, otherwise `SpreadsheetImporter.DefaultMaxRows`. `null` when there is no limit. |
| `Fields` | `IReadOnlyList<SpreadsheetFieldState>` | The mapping state of every schema column, in schema order. |
| `FileLoaded` | `bool` | Whether a workbook with at least one sheet is loaded. |
| `FileName` | `string?` | The name of the loaded file, or `null`. |
| `HasAsyncChecks` | `bool` | `true` when the schema defines `LookupExistingRecordsAsync` or `ValidateRowsAsync`. |
| `HeaderRowIndex` | `int` | The index, within `SpreadsheetSheet.Rows`, of the row used as header. |
| `HeaderRowNumber` | `int` | The file row number of the header row, for messages to the user. 1 when no file is loaded. |
| `Headers` | `IReadOnlyList<string>` | The cell text of the header row. |
| `ImportableRows` | `IReadOnlyList<SpreadsheetRow>` | The rows that would be sent on submit. The list is cached until a row changes. |
| `ImportedCount` | `int` | How many rows the host accepted so far, across the submit, its retries and its resumptions: `AcceptedRowIndexes.Count`. A retry adds the rows it got accepted and never counts a row twice. |
| `InspectedRow` | `SpreadsheetRow?` | The row open in the inspector, or `null`. |
| `InspectedRowIndex` | `int?` | The `SpreadsheetRow.Index` of the row open in the inspector, or `null`. |
| `InvalidRows` | `IReadOnlyList<SpreadsheetRow>` | The rows with at least one error that are not discarded. The list is cached until a row changes. |
| `LimitedRowCount` | `int` | The number of rows left out because they are past `SpreadsheetImporter.EffectiveMaxRows`. |
| `Logger` (get, set) | `ILogger?` | Where the importer reports the exceptions your callbacks throw (the row factory, a property setter, a rule predicate or `ValidateRow`). The uploader never sees the text of such an exception, only a generic `row.failed` issue, so this is the one place it goes. One warning per rule, or one for the row callbacks, is written at the end of each read or edit, with the number of rows it failed on and the first exception. `null` (the default) reports nothing. |
| `MappingLocked` | `bool` | `true` while a submit runs or once some rows were accepted: the column mapping, the sheet, the header row and the answers to value questions are then fixed, because the accepted rows were sent as they were read. Methods that would change them do nothing. `SpreadsheetImporter.ResetSubmit` (or loading another file) unlocks them. |
| `MissingFields` | `IReadOnlyList<SpreadsheetFieldState>` | The fields with status `SpreadsheetColumnStatus.Missing`. Submission is blocked while this is not empty. |
| `NeedsAsyncValidation` | `bool` | `true` when async checks are defined, a file is loaded, rows changed since they last completed, and no check is running or has failed (a failed run is repeated by `SpreadsheetImporter.ValidateAsync` or by `SpreadsheetImporter.SubmitAsync`). `KlySpreadsheetImport` runs the checks itself when this is `true` on the review step. |
| `OpenQuestions` | `IReadOnlyList<SpreadsheetValueQuestion>` | The questions with no answer yet. Submission is blocked while this is not empty. |
| `PendingRows` | `IReadOnlyList<SpreadsheetRow>` | The importable rows that no accepted batch has covered: what the next `SpreadsheetImporter.SubmitAsync` sends. |
| `Questions` | `IReadOnlyList<SpreadsheetValueQuestion>` | Every unknown select value found in the file, answered or not. |
| `RejectedCount` | `int` | The number of rows the server refused in the last submit and that have not been fixed or retried. |
| `ReviewIssueFilter` | `string` | The issue filter applied to `SpreadsheetImporter.VisibleReviewRows`, as `fieldKey`, `fieldKey\|code`, or `\|code` for issues on the whole row (such as `row.duplicate`). Empty for none. |
| `ReviewLevel` | `SpreadsheetReviewLevel` | The severity filter applied to `SpreadsheetImporter.VisibleReviewRows`. |
| `ReviewPage` | `int` | The 0-based page `SpreadsheetImporter.ReviewPageRows` returns, kept inside the pages that exist. Changing a review filter goes back to the first page. |
| `ReviewPageCount` | `int` | The number of pages of `SpreadsheetImporter.VisibleReviewRows` at `SpreadsheetImporter.ReviewPageSize`. At least 1, so an empty table still has a first page. |
| `ReviewPageRows` | `IReadOnlyList<SpreadsheetRow>` | The rows of the current page of `SpreadsheetImporter.VisibleReviewRows`. A table renders this list, so a file of tens of thousands of rows renders one page of rows only. |
| `ReviewPageSize` | `int` | The number of rows `SpreadsheetImporter.ReviewPageRows` returns per page. See `SpreadsheetImporter.SetReviewPageSize`. |
| `ReviewSearch` | `string` | The text searched in the cells of `SpreadsheetImporter.VisibleReviewRows`. Empty for none. |
| `ReviewTab` | `SpreadsheetReviewTab` | The row subset selected for `SpreadsheetImporter.VisibleReviewRows`. |
| `SavedMapping` (get, set) | `SpreadsheetMappingProfile?` | A stored mapping to apply automatically. Whenever a file is loaded, or another sheet or header row is chosen, and the headers match the profile (`SpreadsheetImporter.MatchesMappingProfile`), its column mapping and answers are applied before the rows are read, so the user does not map the same export twice. Setting it while a file is loaded applies it at once when it matches. `null` (the default) applies nothing. |
| `SavedMappingApplied` | `bool` | `true` when `SpreadsheetImporter.SavedMapping` matched the headers of the loaded file and was applied. |
| `Schema` | `SpreadsheetSchema` | The schema this importer was created with. |
| `SelectedRowIndexes` | `IReadOnlySet<int>` | The indexes of the rows selected in the review table. |
| `Sheets` | `IReadOnlyList<SpreadsheetSheet>` | The sheets of the loaded file. Empty when nothing is loaded. |
| `SubmitDone` | `int` | How many rows of the current or last submit have been sent so far. |
| `SubmitError` | `string?` | The message of a `SpreadsheetUserMessageException` thrown by the submit callback, shown as it is, or `null`. It is `null` too after any other exception (the component then shows a generic text, and the exception is logged through `SpreadsheetImporter.Logger`), so check `SpreadsheetImporter.SubmitStatus` for the failure. |
| `SubmitStatus` | `SpreadsheetSubmitStatus` | The state of the current or last submit. |
| `SubmitTotal` | `int` | How many rows the current or last submit sends. |
| `UnusedHeaders` | `IReadOnlyList<string>` | The non-empty headers that no schema column uses. They are not imported. |
| `ValidationError` | `string?` | The message of a `SpreadsheetUserMessageException` thrown by an async check callback, shown as it is, or `null`. It is `null` too after any other exception (the component then shows a generic text, and the exception is logged through `SpreadsheetImporter.Logger`), so check `SpreadsheetImporter.ValidationStatus` for the failure. |
| `ValidationStatus` | `SpreadsheetValidationStatus` | The state of the last `SpreadsheetImporter.ValidateAsync` run (or of the check `SpreadsheetImporter.SubmitAsync` ran before sending). |
| `VisibleReviewRows` | `IReadOnlyList<SpreadsheetRow>` | The rows to show in a review table: the selected tab, narrowed by the severity filter, the issue filter and the search text. The list is cached until a row or a filter changes, so reading it again is free. |

| Method | Returns | Description |
| :--- | :--- | :--- |
| `AnswerValue(string fieldKey, string rawValue, SpreadsheetValueAnswer answer)` | `void` | Answers an unknown select value for every row that contains it. A user answer overrides the column's policy. |
| `ApplyMappingProfile(SpreadsheetMappingProfile profile)` | `SpreadsheetMappingApplyResult` | Applies a profile to the loaded file: maps the file headers it knows to their columns, declares its ignored columns absent, and records its value answers. It overrides earlier manual choices for the columns and answers it names and keeps the others. It does not check `SpreadsheetImporter.MatchesMappingProfile`: headers the file lacks are skipped. |
| `AssignColumn(string fieldKey, int headerIndex)` | `void` | Maps a file column to a schema column. If another column was manually mapped to the same file column, that mapping is removed. |
| `CancelSubmit()` | `void` | Asks the running submit to stop before its next batch and cancels the batch in flight. The rows accepted so far stay accepted, and `SpreadsheetImporter.SubmitAsync` resumes with the rest. Does nothing when no submit is running. |
| `CancelValidation()` | `void` | Stops the running `SpreadsheetImporter.ValidateAsync` before its next batch and cancels the callback in flight. Does nothing when no check is running. |
| `ClearAnswer(string fieldKey, string rawValue)` | `void` | Removes a user answer so the value falls back to the column's policy. |
| `ClearFile()` | `void` | Unloads the file, clears every user decision and returns the wizard to the `file` step. |
| `ClearSelection()` | `void` | Empties the selection. |
| `DiscardRows(IEnumerable<int> rowIndexes)` | `void` | Leaves rows out of the import (reason `SpreadsheetDiscardReason.Manual`) and clears the selection. |
| `EditCell(int rowIndex, string fieldKey, string text)` | `void` | Replaces the text of one cell and revalidates that row (and, when the edit changes the row key, the other rows that share the old or the new key). The edit applies in the importer only and never changes the source file. It also clears a server rejection of that row. |
| `ExportInvalidRowsCsv(Func<SpreadsheetRowIssue, string>? issueText = null, string issuesHeader = "Issues")` | `string` | Builds a CSV of the rows with errors so users can fix them in their own tool and import the file again. |
| `ExportTemplateCsv()` | `string` | Builds a CSV template for users to fill in: a header row and an example row for every column without `SpreadsheetColumnSchema.From`. |
| `GetMappingProfile()` | `SpreadsheetMappingProfile` | Captures the current mapping: each file header that feeds a column (whether matched by name, by suggestion or by the user), the columns declared absent, and the answers the user gave to unknown values. Store it with `SpreadsheetMappingProfile.ToJson`. |
| `GetSteps()` | `IReadOnlyList<SpreadsheetStepStatus>` | Returns the five wizard steps (file, columns, values, review, submit) with their current state. |
| `GoToStep(string key)` | `void` | Jumps back to an earlier step, including a step that was skipped because it had nothing to resolve, so users can still re-map a column. Forward jumps are ignored. |
| `static HumanizeKey(string key)` | `string` | Turns a camelCase, PascalCase, snake_case or kebab-case key into words, for example `secureCode` into `Secure code`. |
| `IgnoreColumn(string fieldKey)` | `void` | Declares that a schema column is not in the file, overriding automatic matching. A required column then becomes missing. |
| `InspectNextIssueRow()` | `void` | Moves the inspector to the next non-discarded row that has a warning or an error, wrapping around. Does nothing when there is none. |
| `InspectPreviousIssueRow()` | `void` | Moves the inspector to the previous non-discarded row that has a warning or an error, wrapping around. Does nothing when there is none. |
| `InspectRow(int? rowIndex)` | `void` | Opens a row in the inspector, or closes the inspector. |
| `IsRowAccepted(int rowIndex)` | `bool` | Whether a row was accepted by the host. An accepted row is final: it cannot be edited, left out or restored. |
| `LoadFileAsync(Stream content, string fileName, long maxBytes = 10485760, CancellationToken cancellationToken = null)` | `Task` | Reads a file and loads it: an `.xlsx` workbook through `SpreadsheetXlsxReader`, a legacy `.xls` workbook through `SpreadsheetXlsReader`, anything else as delimited text decoded as UTF-8 (with or without a byte order mark), or as Latin-1 when the bytes are not valid UTF-8. The content decides for the legacy format: a file that starts with the OLE2 signature is read as `.xls` whatever its extension, a zip archive named `.xls` is read as `.xlsx`, and a text export named `.xls` (tab-separated or CSV, as many applications produce) is read as delimited text. An HTML or XML export named `.xls` is refused. |
| `LoadText(string text, string fileName = "import.csv")` | `void` | Parses delimited text as a one-sheet workbook and loads it, replacing any previous file and discarding the user's corrections. |
| `LoadWorkbook(SpreadsheetWorkbook workbook)` | `void` | Loads a prepared workbook, replacing any previous file and discarding the user's column choices, answers, edits and discards. Detects the sheet and header row unless the schema forces them. |
| `MatchesMappingProfile(SpreadsheetMappingProfile profile)` | `bool` | Whether a profile fits the loaded file: it has the version and schema key of this importer, and every header it maps is a header of the file (compared after `SpreadsheetImporter.NormalizeText`, in any order, extra file headers allowed). |
| `NextStep()` | `bool` | Moves to the next step that is not skipped. |
| `static NormalizeText(string? input)` | `string` | Normalizes a header or option text for comparison: removes a trailing `(required)` and trailing `*`, strips accents, collapses whitespace and lowercases. |
| `static ParseDelimitedSheet(string? content, string sheetName = "Sheet 1")` | `SpreadsheetSheet` | Parses CSV, TSV or semicolon-separated text into a sheet. Handles quoted fields with doubled quotes and line breaks, and strips a leading byte order mark. |
| `Paste(string text, string fileName = "clipboard.tsv")` | `void` | Loads text pasted from a spreadsheet. It behaves exactly like `SpreadsheetImporter.LoadText` with a different default name. |
| `PreviousStep()` | `bool` | Moves to the previous step that is not skipped. |
| `ResetColumn(string fieldKey)` | `void` | Removes a manual choice made by `SpreadsheetImporter.AssignColumn` or `SpreadsheetImporter.IgnoreColumn` so the column is matched by header again. |
| `ResetSubmit()` | `void` | Forgets what was accepted and rejected, and returns `SpreadsheetImporter.SubmitStatus` to `SpreadsheetSubmitStatus.Idle`, so the next `SpreadsheetImporter.SubmitAsync` sends every importable row again. Does nothing while a submit is running. |
| `RestoreRows(IEnumerable<int> rowIndexes)` | `void` | Brings back rows discarded with `SpreadsheetImporter.DiscardRows` and clears the selection. Rows left out for another reason stay out. |
| `RetryRejectedAsync(CancellationToken cancellationToken = null)` | `Task` | Sends again the rows the server rejected, that are still importable. The rejection issues are cleared before the send. Does nothing while a submit is running or when nothing was rejected. |
| `RevertCell(int rowIndex, string fieldKey)` | `void` | Removes an edit made by `SpreadsheetImporter.EditCell` and restores the file text. |
| `SetHeaderRow(int rowIndex)` | `void` | Chooses a different header row of the current sheet and clears column choices and discards. |
| `SetReviewIssueFilter(string filter)` | `void` | Filters the review rows to those with an issue on one column, optionally with one code. A non-empty filter switches the Importable and Discarded tabs back to All. |
| `SetReviewLevel(SpreadsheetReviewLevel level)` | `void` | Filters the review rows by issue severity. A filter other than All switches the Importable and Discarded tabs back to All. |
| `SetReviewPage(int page)` | `void` | Shows another page of the review rows. The number is clamped to the pages that exist. |
| `SetReviewPageSize(int pageSize)` | `void` | Changes the number of rows per page and goes back to the first page. |
| `SetReviewSearch(string search)` | `void` | Filters the review rows to those with a cell containing the text, ignoring case. |
| `SetReviewTab(SpreadsheetReviewTab tab)` | `void` | Selects the review subset and clears the selection. Choosing Importable or Discarded also resets the severity filter. |
| `SetSheet(string sheetName)` | `void` | Switches to another sheet of the loaded file, re-detects its header row and clears column choices and discards. Does nothing for an unknown name. |
| `SubmitAsync(CancellationToken cancellationToken = null)` | `Task` | Sends every importable row that was not accepted yet to the schema's submit callback in batches of `SpreadsheetSchema.BatchSize`. It first calls the column's create-option callback once for each value answered with Create, then runs the async checks of `SpreadsheetImporter.ValidateAsync` when `SpreadsheetImporter.NeedsAsyncValidation` (rows the checks flag with an error are left out of the send and shown as invalid). Rejected rows are kept as errors on their row. Calling it again after a failure or a cancellation resumes: the rows already accepted are not sent twice. An exception from a callback is caught and the status becomes `SpreadsheetSubmitStatus.Error` (`SpreadsheetImporter.SubmitError` holds the text of a `SpreadsheetUserMessageException`, the others go to `SpreadsheetImporter.Logger`); a cancellation the host throws on its own is not caught; a cancellation ends in `SpreadsheetSubmitStatus.Cancelled`. Does nothing while a submit is running or when `SpreadsheetImporter.CanSubmit` is `false`. |
| `ToggleRowSelection(int rowIndex, bool selected)` | `void` | Adds a row to or removes it from the selection. |
| `ValidateAsync(CancellationToken cancellationToken = null)` | `Task` | Runs the async checks the schema defines, on the rows that were not checked yet: first the existing-records lookup for the row keys that were not looked up (`LookupExistingRecordsAsync`, which sets each row's `SpreadsheetRow.Mode` and applies `SpreadsheetSchema.ExistingAction`), then the batched validation of the rows that are not discarded and have no error (`ValidateRowsAsync`). Editing a row, or changing the column mapping or an answer, makes it unchecked again. Rows already checked are not sent twice, so calling it repeatedly is cheap. An exception is caught and the status becomes `SpreadsheetValidationStatus.Error` (`SpreadsheetImporter.ValidationError` holds the text of a `SpreadsheetUserMessageException`, the others go to `SpreadsheetImporter.Logger`); a cancellation the host throws on its own is not caught; the results of the batches that finished are kept. |

#### `SpreadsheetImporter<TRow>`

`Klyroo.Spreadsheet` · class · inherits `SpreadsheetImporter`

The headless import engine for a row type. It is a `SpreadsheetImporter` whose schema is typed, so the schema callbacks receive `TRow` instances, and which can hand you the rows it holds as `SpreadsheetRow`.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetImporter<TRow>(SpreadsheetSchema<TRow> schema, ILogger? logger = null)` | Creates an importer for a schema, with no file loaded. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `ImportableTypedRows` | `IReadOnlyList<SpreadsheetRow<TRow>>` | The rows that would be sent on submit, typed. It builds one `TRow` per row on each call. It never throws for a row the host refused: such a row is not importable. |
| `Schema` | `SpreadsheetSchema<TRow>` | The schema this importer was created with. |

| Method | Returns | Description |
| :--- | :--- | :--- |
| `ToTyped(SpreadsheetRow row)` | `SpreadsheetRow<TRow>` | Gives a row of this importer the typed form: the same row with its parsed values as a `TRow`. |

#### `SpreadsheetMappingProfileJsonContext`

`Klyroo.Spreadsheet` · class · inherits `JsonSerializerContext`

The source-generated JSON context of `SpreadsheetMappingProfileJsonContext.SpreadsheetMappingProfile`, safe for NativeAOT and trimming.

| Property | Type | Description |
| :--- | :--- | :--- |
| `static Default` | `SpreadsheetMappingProfileJsonContext` | The default `JsonSerializerContext` associated with a default `JsonSerializerOptions` instance. |
| `Int32` | `JsonTypeInfo<int>` | Defines the source generated JSON serialization contract metadata for a given type. |
| `IReadOnlyDictionaryStringSpreadsheetValueAnswer` | `JsonTypeInfo<IReadOnlyDictionary<string, SpreadsheetValueAnswer>>` | Defines the source generated JSON serialization contract metadata for a given type. |
| `IReadOnlyDictionaryStringString` | `JsonTypeInfo<IReadOnlyDictionary<string, string>>` | Defines the source generated JSON serialization contract metadata for a given type. |
| `IReadOnlyListString` | `JsonTypeInfo<IReadOnlyList<string>>` | Defines the source generated JSON serialization contract metadata for a given type. |
| `SpreadsheetMappingProfile` | `JsonTypeInfo<SpreadsheetMappingProfile>` | Defines the source generated JSON serialization contract metadata for a given type. |
| `SpreadsheetValueAnswer` | `JsonTypeInfo<SpreadsheetValueAnswer>` | Defines the source generated JSON serialization contract metadata for a given type. |
| `SpreadsheetValueAnswerAction` | `JsonTypeInfo<SpreadsheetValueAnswerAction>` | Defines the source generated JSON serialization contract metadata for a given type. |
| `String` | `JsonTypeInfo<string>` | Defines the source generated JSON serialization contract metadata for a given type. |

#### `SpreadsheetSchema`

`Klyroo.Spreadsheet` · class

Describes an import: its columns and the policies for limits, duplicates, existing records and submission. Create one with `SpreadsheetSchema.For`` and pass it to `SpreadsheetImporter`.

| Property | Type | Description |
| :--- | :--- | :--- |
| `Accept` | `IReadOnlyList<string>` | The file extensions the built-in component lists in its drop zone. They are display text: the importer enforces nothing, and `SpreadsheetImporter.LoadFileAsync` reads `.xlsx` and `.xls` workbooks and delimited text whatever this list says. |
| `BatchSize` | `int` | The number of rows passed to the submit callback per call. Zero or less sends all rows in one call. |
| `Columns` | `IReadOnlyList<SpreadsheetColumnSchema>` | The columns to import, in display order. A column with `SpreadsheetColumnSchema.From` comes after its source. |
| `Duplicates` | `SpreadsheetDuplicatesPolicy` | How rows that repeat a key within the file are handled. Keys are compared trimmed and case-insensitively. Needs `SpreadsheetSchema.RowKeyField`. |
| `ExistingAction` | `SpreadsheetExistingAction` | What to do with rows whose key is in the existing records. |
| `ForcedHeaderRow` | `int?` | The 1-based file row number of the header row, which skips header detection. A number that matches no row selects the first row. |
| `ForcedSheet` | `string?` | The name of the sheet to read, which skips sheet detection. `null`, empty or `auto` detects the sheet; an unknown name falls back to the first sheet. |
| `Key` | `string` | A name for the import. The component uses it to name the downloads and a saved mapping profile is tied to it. |
| `LookupBatchSize` | `int` | The number of keys passed to the existing-record lookup per call. Zero or less sends all keys in one call. |
| `MaxRows` | `int?` | The maximum number of data rows to import. Later rows are left out with `SpreadsheetDiscardReason.Limit`. No limit when `null`. |
| `RowKeyField` | `string?` | The `SpreadsheetColumnSchema.Key` of the column that identifies a row. It enables the duplicate and existing-record checks. |
| `RuleTimeoutLimit` | `int` | How many match timeouts one rule may cause in a file before the importer stops evaluating it (a circuit breaker, so a hostile file cannot cost one `SpreadsheetRule.DefaultPatternTimeout` per cell). The rule is then listed once in `SpreadsheetImporter.DisabledRules` and the component shows a notice. Zero or less never stops a rule. |
| `ValidateBatchSize` | `int` | The number of rows passed to the batched row check per call. Zero or less sends all rows in one call. |

| Method | Returns | Description |
| :--- | :--- | :--- |
| `static For<TRow>(Action<SpreadsheetSchemaBuilder<TRow>> configure)` | `SpreadsheetSchema<TRow>` | Creates a schema for a row type. Declare each column with `SpreadsheetSchemaBuilder.Column``. |
| `static For<TRow>(Func<TRow> factory, Action<SpreadsheetSchemaBuilder<TRow>> configure)` | `SpreadsheetSchema<TRow>` | Creates a schema for a row type that has no public parameterless constructor, for example a type with `required` members. |

#### `SpreadsheetSchema<TRow>`

`Klyroo.Spreadsheet` · class · inherits `SpreadsheetSchema`

A schema bound to a row type: the columns map to its properties, and the callbacks receive its rows.

| Property | Type | Description |
| :--- | :--- | :--- |
| `ExistingRecords` | `IReadOnlyDictionary<string, TRow>?` | Records that already exist, keyed by the row key text (invariant: a `DateOnly` is `yyyy-MM-dd`, a `DateTime` is `yyyy-MM-ddTHH:mm` or with seconds, a `DateTimeOffset` is the same for its UTC instant, and a `Decimal` has no trailing zeros). A row matches when its key text equals a dictionary key under the dictionary's own comparer, with no trimming or case folding applied to the row, so build the dictionary with `StringComparer.OrdinalIgnoreCase` for case-insensitive keys. Needs a row key. |
| `LookupExistingRecordsAsync` | `Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyDictionary<string, TRow>>>?` | Looks up existing records by key on demand, instead of loading them all into `SpreadsheetSchema.ExistingRecords`. It receives the distinct row key texts of the file, `SpreadsheetSchema.LookupBatchSize` at a time, and returns the records found by key (keys it does not return are new). A row matches when its key text equals a returned key under the dictionary's own comparer, like `SpreadsheetSchema.ExistingRecords`. Needs a row key. See `SpreadsheetImporter.ValidateAsync` for when it runs. |
| `OnSubmit` | `Func<IReadOnlyList<SpreadsheetRow<TRow>>, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>?` | Receives each batch of importable rows and returns the rows the server refused, or `null` or an empty list when all were accepted. If it throws, the submit stops with `SpreadsheetSubmitStatus.Error`. |
| `OnSubmitAsync` | `Func<IReadOnlyList<SpreadsheetRow<TRow>>, CancellationToken, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>?` | The cancellable form of `SpreadsheetSchema.OnSubmit`: it also receives a `CancellationToken` that is cancelled by `SpreadsheetImporter.CancelSubmit` (the Cancel button of the component) or by the token passed to `SpreadsheetImporter.SubmitAsync`. A batch that throws `OperationCanceledException` counts as not accepted, so make the call safe to repeat. When both are set, this one is used. |
| `ValidateRow` | `Func<TRow, IReadOnlyList<SpreadsheetRowIssue>>?` | A cross-field check run on every row after the column rules. It receives the parsed row (a cell that is empty or could not be read leaves its property at the default) and returns extra issues, which are added to the row. |
| `ValidateRowsAsync` | `Func<IReadOnlyList<SpreadsheetRow<TRow>>, CancellationToken, Task<IReadOnlyList<SpreadsheetIndexedIssue>?>>?` | A batched check that needs the host, for example "do these SKUs exist". It receives rows that are not discarded and have no error, `SpreadsheetSchema.ValidateBatchSize` at a time, and returns the issues it found with the index of their row, or `null` or an empty list for none. See `SpreadsheetImporter.ValidateAsync` for when it runs. |

#### `SpreadsheetSchemaBuilder<TRow>`

`Klyroo.Spreadsheet` · class

Declares the columns and policies of a `SpreadsheetSchema`. Columns are declared in display order.

| Property | Type | Description |
| :--- | :--- | :--- |
| `Accept` (get, set) | `IReadOnlyList<string>` | The file extensions the built-in component lists in its drop zone. They are display text: the importer enforces nothing. |
| `BatchSize` (get, set) | `int` | The number of rows passed to `SpreadsheetSchemaBuilder.OnSubmit` per call. Zero or less sends all rows in one call. |
| `Duplicates` (get, set) | `SpreadsheetDuplicatesPolicy` | How rows that repeat a key within the file are handled. Needs `SpreadsheetSchemaBuilder.RowKey`. Defaults to `SpreadsheetDuplicatesPolicy.Error`. |
| `ExistingAction` (get, set) | `SpreadsheetExistingAction` | What to do with rows whose key is in `SpreadsheetSchemaBuilder.ExistingRecords`. Defaults to `SpreadsheetExistingAction.Update`. |
| `ExistingRecords` (get, set) | `IReadOnlyDictionary<string, TRow>?` | Records that already exist, keyed by the row key text. Needs `SpreadsheetSchemaBuilder.RowKey`. See `SpreadsheetSchema.ExistingRecords`. |
| `ForcedHeaderRow` (get, set) | `int?` | The 1-based file row number of the header row, which skips header detection. |
| `ForcedSheet` (get, set) | `string?` | The name of the sheet to read, which skips sheet detection. |
| `Key` (get, set) | `string` | A name for the import. The component uses it to name the downloads and a saved mapping profile is tied to it. Defaults to `import`. |
| `LookupBatchSize` (get, set) | `int` | The number of keys passed to `SpreadsheetSchemaBuilder.LookupExistingRecordsAsync` per call. Zero or less sends all keys in one call. Defaults to 500. |
| `LookupExistingRecordsAsync` (get, set) | `Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyDictionary<string, TRow>>>?` | Looks up existing records by key on demand. See `SpreadsheetSchema.LookupExistingRecordsAsync`. |
| `MaxRows` (get, set) | `int?` | The maximum number of data rows to import. Later rows are left out with `SpreadsheetDiscardReason.Limit`. No limit when `null`. |
| `OnSubmit` (get, set) | `Func<IReadOnlyList<SpreadsheetRow<TRow>>, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>?` | Receives each batch of importable rows. See `SpreadsheetSchema.OnSubmit`. |
| `OnSubmitAsync` (get, set) | `Func<IReadOnlyList<SpreadsheetRow<TRow>>, CancellationToken, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>?` | The cancellable form of `SpreadsheetSchemaBuilder.OnSubmit`. See `SpreadsheetSchema.OnSubmitAsync`. |
| `RuleTimeoutLimit` (get, set) | `int` | How many match timeouts one rule may cause in a file before the importer stops evaluating it. Defaults to 3. Zero or less never stops a rule. |
| `ValidateBatchSize` (get, set) | `int` | The number of rows passed to `SpreadsheetSchemaBuilder.ValidateRowsAsync` per call. Zero or less sends all rows in one call. Defaults to 500. |
| `ValidateRow` (get, set) | `Func<TRow, IReadOnlyList<SpreadsheetRowIssue>>?` | A cross-field check run on every row after the column rules. See `SpreadsheetSchema.ValidateRow`. |
| `ValidateRowsAsync` (get, set) | `Func<IReadOnlyList<SpreadsheetRow<TRow>>, CancellationToken, Task<IReadOnlyList<SpreadsheetIndexedIssue>?>>?` | A batched check that needs the host. See `SpreadsheetSchema.ValidateRowsAsync`. |

| Method | Returns | Description |
| :--- | :--- | :--- |
| `Column<TValue>(Expression<Func<TRow, TValue>> member, Action<SpreadsheetColumnBuilder<TRow, TValue>>? configure = null)` | `SpreadsheetColumn<TRow, TValue>` | Declares a column bound to a property of the row. Its kind follows the property type: text for `String`, number for numeric types, boolean for `Boolean`, date for `DateOnly`, `DateTime` and `DateTimeOffset`, select for an enum. A nullable type reads the same way and stores `null` for an empty cell. Other types need `SpreadsheetColumnBuilder.Parser`. |
| `RowKey(SpreadsheetColumnSchema column)` | `void` | Sets the column that identifies a row, which enables the duplicate and existing-record checks. |

#### `SpreadsheetUserMessageException`

`Klyroo.Spreadsheet` · class · inherits `Exception`

Throw it from a callback of the import (the submit, an async check) when the text of the exception is meant for the uploader: `SpreadsheetImporter.SubmitError` or `SpreadsheetImporter.ValidationError` then holds `Exception.Message` as it is, and the component shows it. Any other exception is logged and the uploader sees a generic, localized text, because the message of an exception can hold a path or a connection string. To refuse some rows with a message, prefer the typed way, `SpreadsheetRejectedRow` returned by the submit callback, or `SpreadsheetIndexedIssue` returned by `ValidateRowsAsync`; this exception is for a failure of the whole call, such as a closed import window.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetUserMessageException(string message)` | Creates the exception. |

### Records

#### `SpreadsheetCellView`

`Klyroo.Spreadsheet` · record

How one cell of a row is read and displayed.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetCellView(string Text, string OriginalText, string DisplayText, bool Edited, bool Defaulted, bool Created, SpreadsheetRowIssue? Issue)` | How one cell of a row is read and displayed. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Created` (init) | `bool` | Whether the value is a new option answered with `SpreadsheetValueAnswerAction.Create`. |
| `Defaulted` (init) | `bool` | Whether the column default replaced an empty cell. |
| `DisplayText` (init) | `string` | The text to show: the option label for selects, the parsed value for other kinds, or the default. |
| `Edited` (init) | `bool` | Whether the user edited the cell. |
| `Issue` (init) | `SpreadsheetRowIssue?` | The first issue found on this cell, or `null`. |
| `OriginalText` (init) | `string` | The text in the file. |
| `Text` (init) | `string` | The text being parsed: the user's edit when there is one, otherwise the file text. |

#### `SpreadsheetColumnSuggestion`

`Klyroo.Spreadsheet` · record

A file column offered for a missing required column because its sample values look right. The importer never applies it on its own.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetColumnSuggestion(int HeaderIndex, string HeaderText, IReadOnlyList<string> Samples)` | A file column offered for a missing required column because its sample values look right. The importer never applies it on its own. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `HeaderIndex` (init) | `int` | The index to pass to `SpreadsheetImporter.AssignColumn`. |
| `HeaderText` (init) | `string` | The header text of the suggested file column. |
| `Samples` (init) | `IReadOnlyList<string>` | Up to three distinct sample values from that column. |

#### `SpreadsheetDisabledRule`

`Klyroo.Spreadsheet` · record

A rule the importer stopped evaluating because its matches timed out, or its predicate threw, too often. See `SpreadsheetImporter.DisabledRules`.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetDisabledRule(string ColumnKey, string RuleCode, int Timeouts, SpreadsheetDisabledReason Reason = Timeouts)` | A rule the importer stopped evaluating because its matches timed out, or its predicate threw, too often. See `SpreadsheetImporter.DisabledRules`. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `ColumnKey` (init) | `string` | The key of the column the rule belongs to. |
| `Reason` (init) | `SpreadsheetDisabledReason` | Whether the rule was stopped for its timeouts or for its exceptions. |
| `RuleCode` (init) | `string` | The `SpreadsheetRule.Code` of the rule. |
| `Timeouts` (init) | `int` | The number of timeouts and exceptions that stopped it (the one that came last decides `Reason`). |

#### `SpreadsheetFieldState`

`Klyroo.Spreadsheet` · record

The current mapping of one schema column to the loaded file.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetFieldState(SpreadsheetColumnSchema Column, SpreadsheetColumnStatus Status, int? HeaderIndex, string? HeaderText, string? AssignedBy, IReadOnlyList<string> Samples, SpreadsheetColumnSuggestion? Suggestion)` | The current mapping of one schema column to the loaded file. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `AssignedBy` (init) | `string?` | `file` when the header matched by name, `user` after `SpreadsheetImporter.AssignColumn`, otherwise `null`. |
| `Column` (init) | `SpreadsheetColumnSchema` | The schema column. |
| `HeaderIndex` (init) | `int?` | The index of the file column read for this field, or `null` when none is. |
| `HeaderText` (init) | `string?` | The header text of that file column. |
| `Samples` (init) | `IReadOnlyList<string>` | Up to three distinct non-empty values of the mapped file column. |
| `Status` (init) | `SpreadsheetColumnStatus` | Whether the column is matched, defaulted, missing or unmatched. |
| `Suggestion` (init) | `SpreadsheetColumnSuggestion?` | A suggested file column for a missing field, or `null`. |

#### `SpreadsheetIndexedIssue`

`Klyroo.Spreadsheet` · record

An issue found by `ValidateRowsAsync`, with the row it belongs to.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetIndexedIssue(int RowIndex, SpreadsheetRowIssue Issue)` | An issue found by `ValidateRowsAsync`, with the row it belongs to. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Issue` (init) | `SpreadsheetRowIssue` | The issue added to that row. Only `SpreadsheetIssueLevel.Error` blocks the row. |
| `RowIndex` (init) | `int` | The `SpreadsheetRow.Index` of the row. |

#### `SpreadsheetMappingApplyResult`

`Klyroo.Spreadsheet` · record

What `SpreadsheetImporter.ApplyMappingProfile` changed.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetMappingApplyResult(int ColumnsApplied, int ColumnsIgnored, int ValueAnswersApplied)` | What `SpreadsheetImporter.ApplyMappingProfile` changed. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Applied` | `bool` | `true` when anything was applied. |
| `ColumnsApplied` (init) | `int` | The columns mapped to a file header of the profile. |
| `ColumnsIgnored` (init) | `int` | The columns declared absent from the file. |
| `ValueAnswersApplied` (init) | `int` | The answers recorded. |

#### `SpreadsheetMappingProfile`

`Klyroo.Spreadsheet` · record

A column mapping and the answers to unknown values, in a form you can store and apply to the next file with the same headers: `SpreadsheetImporter.GetMappingProfile` builds it from the current state, `SpreadsheetImporter.ApplyMappingProfile` applies it, and `SpreadsheetImporter.SavedMapping` applies it automatically when a file's headers match. It holds no cell data, only header texts, column keys and answers.

| Field | Type | Description |
| :--- | :--- | :--- |
| `const CurrentVersion = 1` | `int` | The format version of `SpreadsheetMappingProfile.Version` this library writes. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Columns` (init) | `IReadOnlyDictionary<string, string>` | The file headers and the column they were mapped to: the header after `SpreadsheetImporter.NormalizeText` (lower case, no accents, no trailing `*`) mapped to the `SpreadsheetColumnSchema.Key`. When a header appears several times in the file, this holds its first occurrence and `SpreadsheetMappingProfile.RepeatedColumns` the others. |
| `IgnoredColumns` (init) | `IReadOnlyList<string>` | The keys of the columns the user declared absent from the file with `SpreadsheetImporter.IgnoreColumn`. |
| `RepeatedColumns` (init) | `IReadOnlyDictionary<string, string>` | The columns mapped to the second, third and later occurrence of a header that repeats in the file (two `Date` columns, for example): `"2:date"` is the second column headed `date`, mapped to a column key. It keeps a re-applied profile from sending both occurrences to the same column. |
| `SchemaKey` (init) | `string?` | The `SpreadsheetSchema.Key` the profile was made for. A profile with a key is applied only to a schema with the same key. `null` applies to any schema. |
| `ValueAnswers` (init) | `IReadOnlyDictionary<string, SpreadsheetValueAnswer>` | The answers to unknown select values, by question id (`columnKey::normalizedValue`, see `SpreadsheetValueQuestion.Id`). |
| `Version` (init) | `int` | The format version. A profile of an unknown version is not applied. |

| Method | Returns | Description |
| :--- | :--- | :--- |
| `static FromJson(string json)` | `SpreadsheetMappingProfile?` | Reads a profile written by `SpreadsheetMappingProfile.ToJson`. |
| `ToJson()` | `string` | Writes the profile as indented JSON (camelCase names, enums as strings). |

#### `SpreadsheetOption`

`Klyroo.Spreadsheet` · record

An allowed value of a `SpreadsheetColumnKind.Select` column, as the importer and the component see it: the value is its text.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetOption(string Value, string Label, IReadOnlyList<string>? Aliases = null)` | An allowed value of a `SpreadsheetColumnKind.Select` column, as the importer and the component see it: the value is its text. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Aliases` (init) | `IReadOnlyList<string>?` | Extra spellings that also match this option, for example a symbol or an abbreviation. |
| `Label` (init) | `string` | The text shown to the user. Cells are matched against it as well as against the value. |
| `Value` (init) | `string` | The option's value as text (the enum member name for an enum column). It is what a value answer maps to. |

#### `SpreadsheetOption<TValue>`

`Klyroo.Spreadsheet` · record

An allowed value of a select column, typed like the column.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetOption<TValue>(TValue Value, string Label, IReadOnlyList<string>? Aliases = null)` | An allowed value of a select column, typed like the column. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Aliases` (init) | `IReadOnlyList<string>?` | Extra spellings that also match this option, for example a symbol or an abbreviation. |
| `Label` (init) | `string` | The text shown to the user. Cells are matched against it as well as against the value. |
| `Value` (init) | `TValue` | The value written to the row property when this option matches. |

#### `SpreadsheetRejectedRow`

`Klyroo.Spreadsheet` · record

A row that `OnSubmit` refused. It comes back as an error issue with code `server` on that row.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetRejectedRow(int RowIndex, string Message, string? Field = null)` | A row that `OnSubmit` refused. It comes back as an error issue with code `server` on that row. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Field` (init) | `string?` | The column key the reason relates to, or `null` for the whole row. |
| `Message` (init) | `string` | The reason, shown to the user. |
| `RowIndex` (init) | `int` | The `SpreadsheetRow.Index` of the refused row. |

#### `SpreadsheetRow`

`Klyroo.Spreadsheet` · record

One data row after parsing and validation, as the importer currently sees it. The typed form of a row is `SpreadsheetRow`.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetRow(int Index, int RowNumber, IReadOnlyDictionary<string, SpreadsheetCellView> Cells, IReadOnlyList<SpreadsheetRowIssue> Issues, SpreadsheetRowStatus Status, SpreadsheetDiscardReason? DiscardReason, SpreadsheetRowMode Mode, bool Edited, bool Importable)` | One data row after parsing and validation, as the importer currently sees it. The typed form of a row is `SpreadsheetRow`. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Cells` (init) | `IReadOnlyDictionary<string, SpreadsheetCellView>` | The display state of each cell by column key. |
| `DiscardReason` (init) | `SpreadsheetDiscardReason?` | Why the row is left out, or `null` when it is not. |
| `Edited` (init) | `bool` | Whether the user edited any cell of the row. |
| `Importable` (init) | `bool` | Whether the row is sent on submit: not discarded and without an error. |
| `Index` (init) | `int` | The 0-based position among the data rows (the rows after the header). Edit, discard and rejection methods use it. |
| `Issues` (init) | `IReadOnlyList<SpreadsheetRowIssue>` | Every issue found on the row. |
| `Mode` (init) | `SpreadsheetRowMode` | Whether the row creates a record or updates an existing one. |
| `RowNumber` (init) | `int` | The 1-based row number in the file, for messages to the user. |
| `Status` (init) | `SpreadsheetRowStatus` | The overall validation state. |

#### `SpreadsheetRow<TRow>`

`Klyroo.Spreadsheet` · record · inherits `SpreadsheetRow`

A row with the parsed values as an instance of the row type. It is what the schema callbacks receive.

| Property | Type | Description |
| :--- | :--- | :--- |
| `Value` | `TRow` | The parsed values. A cell that is empty without a default, or that could not be read, leaves its property at the default of its type, so check `SpreadsheetRow.Importable` before trusting a value. The rows the callbacks receive are importable. |

#### `SpreadsheetRowIssue`

`Klyroo.Spreadsheet` · record

A problem or note about a row or one of its cells.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetRowIssue(string? Field, string Code, SpreadsheetIssueLevel Level, string Message, IReadOnlyList<string>? Args = null)` | A problem or note about a row or one of its cells. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Args` (init) | `IReadOnlyList<string>?` | Set for the messages the library writes itself: the values the message contains (the cell text of a `number` issue, the row number of a `row.duplicate` issue), possibly none. With it, the component shows the message of the user's locale for the `Code`, see `SpreadsheetTexts`. `null` for a message you wrote (a rule with your own text, `ValidateRow`, a server rejection), which is never replaced. |
| `Code` (init) | `string` | A stable identifier. Built-in codes are `required`, `number`, `boolean`, `date`, `text`, `value.unknown`, `value.not-found`, `value.skipped`, `row.duplicate`, `row.exists`, `row.failed` (the host's factory, setter, rule predicate or `ValidateRow` threw; the message is generic and never the exception's text, which goes to `SpreadsheetImporter.Logger`) and `server`, plus the code of each failed `SpreadsheetRule`. |
| `Field` (init) | `string?` | The column key the issue relates to, or `null` for the whole row. |
| `Level` (init) | `SpreadsheetIssueLevel` | The severity. |
| `Message` (init) | `string` | The text of the issue in English. `KlySpreadsheetImport` shows it as it is when `Args` is `null`. |

#### `SpreadsheetRule`

`Klyroo.Spreadsheet` · record

A validation rule attached to a column. A rule runs on the parsed value of a non-empty cell. Create one with the factory methods of this type or with `SpreadsheetRule`.

| Field | Type | Description |
| :--- | :--- | :--- |
| `static readonly DefaultPatternTimeout` | `TimeSpan` | The match timeout `SpreadsheetRule.Pattern` uses when you pass none: 100 milliseconds per cell. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Code` (init) | `string` | A stable identifier copied to `SpreadsheetRowIssue.Code`, usable for filtering. |
| `Level` (init) | `SpreadsheetIssueLevel` | The severity of the issue. Only `SpreadsheetIssueLevel.Error` blocks the row. |
| `Message` (init) | `string` | The text of the issue shown to the user when the rule fails. |
| `MessageArgs` (init) | `IReadOnlyList<string>?` | Set by the built-in rules when `Message` is their default text: the values it contains (the minimum of `SpreadsheetRule.Min``, for example). It is copied to `SpreadsheetRowIssue.Args`, which tells the component to show the message of the user's locale instead of the English default. `null` for a rule with your own message, which is shown as it is. |

| Method | Returns | Description |
| :--- | :--- | :--- |
| `static Between<TValue>(TValue min, TValue max, string? message = null, SpreadsheetIssueLevel level = Error)` | `SpreadsheetRule<TValue>` | Creates a rule that accepts numbers inside an inclusive range. |
| `static Email(string? message = null, SpreadsheetIssueLevel level = Error)` | `SpreadsheetRule<string?>` | Creates a rule that accepts text shaped like an email address (something before and after one `@`, and a dot followed by at least two characters). |
| `static Max<TValue>(TValue max, string? message = null, SpreadsheetIssueLevel level = Error)` | `SpreadsheetRule<TValue>` | Creates a rule that accepts numbers less than or equal to a maximum. |
| `static Min<TValue>(TValue min, string? message = null, SpreadsheetIssueLevel level = Error)` | `SpreadsheetRule<TValue>` | Creates a rule that accepts numbers greater than or equal to a minimum. |
| `static Pattern(string pattern, string? message = null, SpreadsheetIssueLevel level = Error, TimeSpan? matchTimeout = null)` | `SpreadsheetRule<string?>` | Creates a rule that accepts text matching a regular expression. A cell that makes the match run past the timeout (catastrophic backtracking on a hostile value) does not hang the import: it gets a `pattern` issue saying the format could not be checked in time. |

#### `SpreadsheetRule<TValue>`

`Klyroo.Spreadsheet` · record · inherits `SpreadsheetRule`

A validation rule on the parsed value of a column.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetRule<TValue>(string Code, string Message, Func<TValue, bool> Validate, SpreadsheetIssueLevel Level = Error, IReadOnlyList<string>? MessageArgs = null)` | A validation rule on the parsed value of a column. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Validate` (init) | `Func<TValue, bool>` | Receives the parsed value and returns `true` when it is acceptable. |

#### `SpreadsheetSheet`

`Klyroo.Spreadsheet` · record

One sheet of a workbook, as rows of cell text.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetSheet(string Name, IReadOnlyList<IReadOnlyList<string>> Rows, IReadOnlyList<int> RowNumbers)` | One sheet of a workbook, as rows of cell text. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` (init) | `string` | The sheet name. |
| `RowNumbers` (init) | `IReadOnlyList<int>` | The 1-based row number in the source file of each entry of `Rows`, so the same index in both lists describes one row. |
| `Rows` (init) | `IReadOnlyList<IReadOnlyList<string>>` | The non-empty rows, each a list of trimmed cell text. |

#### `SpreadsheetStepStatus`

`Klyroo.Spreadsheet` · record

The state of one step of the import wizard.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetStepStatus(string Key, string Label, string Hint, string State, int Position, bool Ready, bool Skipped, int Blockers)` | The state of one step of the import wizard. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Blockers` (init) | `int` | The number of items to resolve before the step is ready. |
| `Hint` (init) | `string` | A short description of the step. |
| `Key` (init) | `string` | One of `file`, `columns`, `values`, `review` or `submit`. |
| `Label` (init) | `string` | The step title. |
| `Position` (init) | `int` | The 1-based position in the wizard. |
| `Ready` (init) | `bool` | Whether the step has nothing left to resolve, so the wizard can move on from it. |
| `Skipped` (init) | `bool` | Whether the step needs no action and is passed over: `columns` without missing fields, `values` without questions. |
| `State` (init) | `string` | `current`, `done`, `skipped` or `upcoming`. |

#### `SpreadsheetValueAnswer`

`Klyroo.Spreadsheet` · record

The decision for one unknown select value, given by the user or by the column's policy.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetValueAnswer(SpreadsheetValueAnswerAction Action, string? MappedValue = null, string? MappedLabel = null)` | The decision for one unknown select value, given by the user or by the column's policy. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Action` (init) | `SpreadsheetValueAnswerAction` | What to do with the value. |
| `MappedLabel` (init) | `string?` | For `SpreadsheetValueAnswerAction.Map`, the label to display. Defaults to the value. |
| `MappedValue` (init) | `string?` | For `SpreadsheetValueAnswerAction.Map`, the `SpreadsheetOption.Value` to use. |

#### `SpreadsheetValueQuestion`

`Klyroo.Spreadsheet` · record

An unknown select value found in the file, with the answer given so far. One question exists per column and normalized value.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetValueQuestion(string Id, string FieldKey, string FieldLabel, string RawValue, string NormalizedKey, IReadOnlyList<int> RowIndexes, IReadOnlyList<SpreadsheetOption> Choices, SpreadsheetValueAnswer? Answer, string? AnsweredBy, bool CanCreate)` | An unknown select value found in the file, with the answer given so far. One question exists per column and normalized value. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Answer` (init) | `SpreadsheetValueAnswer?` | The current answer, or `null` while the question is open. |
| `AnsweredBy` (init) | `string?` | `user` or `policy` when answered, otherwise `null`. |
| `CanCreate` (init) | `bool` | Whether the column defines `SpreadsheetColumnSchema.OnCreateOption`, so creating the value is allowed. |
| `Choices` (init) | `IReadOnlyList<SpreadsheetOption>` | The options the value can be mapped to. |
| `FieldKey` (init) | `string` | The key of the select column. |
| `FieldLabel` (init) | `string` | The display name of the select column. |
| `Id` (init) | `string` | The column key and the normalized value joined by `::`. |
| `IsOpen` | `bool` | `true` while no answer exists. Open questions block submission. |
| `NormalizedKey` (init) | `string` | The value after `SpreadsheetImporter.NormalizeText`. |
| `RawValue` (init) | `string` | The value as it first appeared in the file. |
| `RowIndexes` (init) | `IReadOnlyList<int>` | The `SpreadsheetRow.Index` of every row that contains the value. |

#### `SpreadsheetWorkbook`

`Klyroo.Spreadsheet` · record

A parsed file made of one or more sheets. Build one yourself to import a format the library does not parse, then pass it to `SpreadsheetImporter.LoadWorkbook`.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetWorkbook(string Name, IReadOnlyList<SpreadsheetSheet> Sheets)` | A parsed file made of one or more sheets. Build one yourself to import a format the library does not parse, then pass it to `SpreadsheetImporter.LoadWorkbook`. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `InvariantNumbers` (init) | `bool` | `true` when number cells hold invariant-culture text (`1234.5`), as in a workbook read by `SpreadsheetXlsxReader`. Number columns then read that form whatever their `SpreadsheetColumnSchema.DecimalSeparator`, and still accept text in the column's own convention. |
| `Name` (init) | `string` | The file name, shown by the component. |
| `Sheets` (init) | `IReadOnlyList<SpreadsheetSheet>` | The sheets of the file. |

### Structs

#### `SpreadsheetParseResult<TValue>`

`Klyroo.Spreadsheet` · struct

The outcome of a `SpreadsheetParser`: a value or an error message.

| Property | Type | Description |
| :--- | :--- | :--- |
| `Error` | `string?` | The message shown on the cell when `SpreadsheetParseResult.IsOk` is `false`. |
| `IsOk` | `bool` | `true` when the text was read into `SpreadsheetParseResult.Value`. |
| `Value` | `TValue` | The parsed value, meaningful when `SpreadsheetParseResult.IsOk` is `true`. |

| Method | Returns | Description |
| :--- | :--- | :--- |
| `static Fail(string message)` | `SpreadsheetParseResult<TValue>` | The text cannot be read. The cell gets an error issue with this message. |
| `static Ok(TValue value)` | `SpreadsheetParseResult<TValue>` | The text was read as `value`. |

### Enums

#### `SpreadsheetColumnKind`

`Klyroo.Spreadsheet` · enum

The data type a `SpreadsheetColumnSchema` parses its cells into.

| Value | Description |
| :--- | :--- |
| `Text` | Free text. The trimmed cell text is the value. |
| `Number` | A number parsed to `Double`. Spaces, non-breaking spaces and apostrophes are removed first, and the decimal separator comes from `SpreadsheetColumnSchema.DecimalSeparator`. |
| `Date` | A date parsed with `SpreadsheetColumnSchema.DateFormats` (or the default formats) and stored as an ISO 8601 string, `yyyy-MM-dd` or `yyyy-MM-ddTHH:mm` when the time is not midnight. |
| `Boolean` | A yes/no value parsed to `Boolean`. Accepts yes, y, oui, o, true, vrai, x and 1 for true, and no, n, non, false, faux and 0 for false, ignoring case and accents. |
| `Select` | A value chosen from `SpreadsheetColumnSchema.Options`. Cells that match no option become value questions governed by the column's unknown-value policy. |

#### `SpreadsheetColumnStatus`

`Klyroo.Spreadsheet` · enum

How a schema column relates to the loaded file. See `SpreadsheetFieldState.Status`.

| Value | Description |
| :--- | :--- |
| `Matched` | The column is read from a file column, or is computed from another column through `SpreadsheetColumnSchema.From`. |
| `Default` | No file column matches, but the column has a default value (`SpreadsheetColumnSchema.HasDefault`). |
| `Missing` | No file column matches and the column is required without a default. The importer cannot submit until a column is assigned. |
| `Unmatched` | No file column matches and the column is optional. Its cells are empty. |

#### `SpreadsheetDisabledReason`

`Klyroo.Spreadsheet` · enum

Why the importer stopped a rule.

| Value | Description |
| :--- | :--- |
| `Timeouts` | Its matches timed out too often. |
| `Failures` | Its predicate threw too often. |

#### `SpreadsheetDiscardReason`

`Klyroo.Spreadsheet` · enum

Why a `SpreadsheetRow` is left out of the import. When several reasons apply, the first in this order wins: limit, value, duplicate, existing, manual.

| Value | Description |
| :--- | :--- |
| `Manual` | The user left the row out, through `SpreadsheetImporter.DiscardRows`. |
| `Value` | A value answer of `SpreadsheetValueAnswerAction.SkipRows` applies to the row. |
| `Limit` | The row is past `SpreadsheetSchema.MaxRows`. |
| `Duplicate` | The row repeats a key and the policy keeps only the first or the last row. |
| `Existing` | The key already exists and `SpreadsheetExistingAction.Skip` is set. |

#### `SpreadsheetDuplicatesPolicy`

`Klyroo.Spreadsheet` · enum

How rows that share the same `SpreadsheetSchema.RowKeyField` value inside one file are handled.

| Value | Description |
| :--- | :--- |
| `Error` | Every row of the duplicated key gets a blocking issue (code `row.duplicate`), so none of them is importable until you edit or discard some. |
| `KeepFirst` | Keep the first row of each key and leave out the others (discard reason `SpreadsheetDiscardReason.Duplicate`). |
| `KeepLast` | Keep the last row of each key and leave out the others (discard reason `SpreadsheetDiscardReason.Duplicate`). |

#### `SpreadsheetExistingAction`

`Klyroo.Spreadsheet` · enum

What happens to an imported row whose key is already present in `ExistingRecords`.

| Value | Description |
| :--- | :--- |
| `Update` | Import the row in `SpreadsheetRowMode.Update` mode. This is the default. |
| `Skip` | Leave the row out (mode `SpreadsheetRowMode.Skip`, discard reason `SpreadsheetDiscardReason.Existing`). |
| `Error` | Mark the row with a blocking issue (code `row.exists`) so it is not importable. |

#### `SpreadsheetIssueLevel`

`Klyroo.Spreadsheet` · enum

How serious a `SpreadsheetRowIssue` is.

| Value | Description |
| :--- | :--- |
| `Info` | Informational. Does not affect whether the row can be imported. |
| `Warning` | Worth a look, but the row stays importable. |
| `Error` | Blocks the row from being imported. |

#### `SpreadsheetReviewLevel`

`Klyroo.Spreadsheet` · enum

Filters the review table by the severity of the issues a row has. See `SpreadsheetImporter.SetReviewLevel`.

| Value | Description |
| :--- | :--- |
| `All` | No severity filter. |
| `Blocking` | Only rows with at least one error issue. |
| `Warnings` | Only rows with at least one warning issue. |

#### `SpreadsheetReviewTab`

`Klyroo.Spreadsheet` · enum

The row subset shown by the review table. See `SpreadsheetImporter.SetReviewTab`.

| Value | Description |
| :--- | :--- |
| `All` | Every row, discarded ones included. |
| `Importable` | Rows that would be submitted. |
| `Invalid` | Rows with at least one error that are not discarded. |
| `Discarded` | Rows left out of the import, whatever the reason. |

#### `SpreadsheetRowMode`

`Klyroo.Spreadsheet` · enum

Whether an importable row creates a record or updates an existing one.

| Value | Description |
| :--- | :--- |
| `Create` | The key is new, or the schema has no key. |
| `Update` | The key matches an entry of `ExistingRecords`. |
| `Skip` | The key matches an existing record and the schema skips those rows. |

#### `SpreadsheetRowStatus`

`Klyroo.Spreadsheet` · enum

The validation state of a `SpreadsheetRow`.

| Value | Description |
| :--- | :--- |
| `Valid` | No error or warning, and the row is not discarded. |
| `Warning` | At least one warning and no error. The row is importable. |
| `Blocking` | At least one error. The row is not importable until it is edited or discarded. |
| `Discarded` | The row is left out of the import. See `SpreadsheetRow.DiscardReason`. |

#### `SpreadsheetSubmitStatus`

`Klyroo.Spreadsheet` · enum

Where `SpreadsheetImporter.SubmitAsync` is in its lifecycle.

| Value | Description |
| :--- | :--- |
| `Idle` | Nothing has been submitted since the file was loaded. |
| `Running` | A submit or retry is in progress. |
| `Done` | The last submit finished. Rows may still have been rejected, see `SpreadsheetImporter.RejectedCount`. |
| `Error` | The submit callback threw. `SpreadsheetImporter.SubmitError` holds the message when it was meant for the uploader (`SpreadsheetUserMessageException`); any other exception goes to `SpreadsheetImporter.Logger`. The rows accepted before the failure stay accepted, and `SpreadsheetImporter.SubmitAsync` resumes with the rest. |
| `Cancelled` | The submit was cancelled, through `SpreadsheetImporter.CancelSubmit` or the token given to `SpreadsheetImporter.SubmitAsync`. The rows accepted so far stay accepted, and `SpreadsheetImporter.SubmitAsync` resumes with the rest. |

#### `SpreadsheetUnknownValuePolicy`

`Klyroo.Spreadsheet` · enum

What the importer does with a cell of a `SpreadsheetColumnKind.Select` column that matches none of the column's options.

| Value | Description |
| :--- | :--- |
| `Ask` | Raise a `SpreadsheetValueQuestion` and block the import until it is answered. This is the default. |
| `Error` | Do not ask: every row with the unknown value gets an error issue (code `value.not-found`) and is not importable. |
| `SkipRows` | Answer automatically by leaving out every row that contains the unknown value (discard reason `SpreadsheetDiscardReason.Value`). |
| `LeaveEmpty` | Answer automatically by importing the row with an empty value for the cell. |
| `Create` | Answer automatically by keeping the cell text as a new option. `SpreadsheetColumnSchema.OnCreateOption` is called once per new value on submit. |

#### `SpreadsheetValidationStatus`

`Klyroo.Spreadsheet` · enum

Where the async checks of `SpreadsheetImporter.ValidateAsync` are in their lifecycle.

| Value | Description |
| :--- | :--- |
| `Idle` | No check has run since the file was loaded. |
| `Running` | The checks are running. |
| `Done` | The last run finished. |
| `Error` | A callback threw. `SpreadsheetImporter.ValidationError` holds the message when it was meant for the uploader (`SpreadsheetUserMessageException`); any other exception goes to `SpreadsheetImporter.Logger`. |
| `Cancelled` | The last run was cancelled before it finished. |

#### `SpreadsheetValueAnswerAction`

`Klyroo.Spreadsheet` · enum

The decision recorded in a `SpreadsheetValueAnswer` for an unknown select value.

| Value | Description |
| :--- | :--- |
| `Map` | Replace the value with one of the column's options. |
| `LeaveEmpty` | Import the row with an empty cell. |
| `SkipRows` | Leave out every row that contains the value. |
| `Create` | Keep the value as a new option. |

### Delegates

#### `SpreadsheetParser<TValue>`

`Klyroo.Spreadsheet` · delegate

Reads the text of a cell into a value. Replaces the built-in parsing of a column, see `SpreadsheetColumnBuilder.Parser`.

## Klyroo.Spreadsheet

Components (1) · Static classes and extensions (2) · Classes (1) · Records (2)

### Components

#### `KlySpreadsheetImport`

`Klyroo.Spreadsheet` · component · inherits `KlyComponentBase`

| Parameter | Type | Description |
| :--- | :--- | :--- |
| `Importer` | `SpreadsheetImporter` | The importer that holds the import state. The component renders it and calls its methods. Users load a file by choosing or dropping it on the file step (CSV, TSV, text, `.xlsx` or `.xls`, through `SpreadsheetImporter.LoadFileAsync`) or by pasting rows; your code can also call `SpreadsheetImporter.LoadText` or `SpreadsheetImporter.LoadWorkbook`. |
| `MaxFileSize` | `long` | The largest file users can choose, in bytes. Defaults to 10 MB. |
| `MaxRows` | `int?` | The most rows an import takes when `SpreadsheetSchema.MaxRows` is not set, 10,000 by default. Rows past the limit are left out and the component says how many. `null` removes the limit. A `SpreadsheetSchema.MaxRows` in the schema wins over this value. |
| `OnDownload` | `EventCallback<SpreadsheetDownload>` | Called with the file when the user chooses to download the template or the invalid rows, instead of the browser download. Set it in .NET MAUI Blazor Hybrid, or to save the file your own way: the component then makes no JavaScript call. |
| `OnMappingProfileChanged` | `EventCallback<SpreadsheetMappingProfile>` | Called with the current `SpreadsheetMappingProfile` when the user changes the column mapping or answers a value question (and when your code does it through the importer), so the app can store it and give it back through `SpreadsheetImporter.SavedMapping`. It is not called when a file is loaded or when a saved mapping is applied automatically. |
| `PageSize` | `int` | The number of rows the review table shows per page, 50 by default. Users can change it with the page size list of the pager. |
| `Title` | `string?` | The heading shown above the wizard. Defaults to the word Import in the language of the locale (`SpreadsheetTexts.Import`). Your text is shown as it is. |
| `Wizard` | `bool` | Shows the step bar and the Back, Continue and Import buttons. When `false` only the current step is rendered and your code moves between steps with the importer's step methods. |

Also accepts the parameters of `KlyComponentBase`.

| Field | Type | Description |
| :--- | :--- | :--- |
| `const DefaultMaxRows = 10000` | `int` | The value of `KlySpreadsheetImport.MaxRows` when you do not set it: 10,000 rows. |

### Static classes and extensions

#### `KlyrooSpreadsheetServiceCollectionExtensions`

`Klyroo.Spreadsheet` · service

Service registration for Klyroo.Spreadsheet.

| Method | Returns | Description |
| :--- | :--- | :--- |
| `static AddKlyrooSpreadsheet(IServiceCollection services, Action<KlyrooSpreadsheetOptions>? configure = null)` | `IServiceCollection` | Registers the services `KlySpreadsheetImport` needs. It calls `AddKlyrooUi()` with its defaults, so call `AddKlyrooUi` with your own options before or after it, not instead of it. Call it once in `Program.cs`, and also in the client project of an Interactive WebAssembly or Auto app. |

#### `SpreadsheetLocales`

`Klyroo.Spreadsheet` · service

The built-in texts of Klyroo.Spreadsheet, one set per locale of `KlyLocales.All` and keyed by the same codes. The component picks the set of its current `KlyLocale.Code` with `KlyLocales.ResolveCode`, so "pt-PT" gets "pt" and an unknown language gets English. Change a set with `KlyrooSpreadsheetOptions.Texts`.

| Property | Type | Description |
| :--- | :--- | :--- |
| `static Ar` | `SpreadsheetTexts` | العربية (ar). Needs review. |
| `static De` | `SpreadsheetTexts` | Deutsch (de). Needs review. |
| `static En` | `SpreadsheetTexts` | English. |
| `static Es` | `SpreadsheetTexts` | Español (es). Needs review. |
| `static Fr` | `SpreadsheetTexts` | Français (fr). Reviewed. |
| `static It` | `SpreadsheetTexts` | Italiano (it). Needs review. |
| `static Ja` | `SpreadsheetTexts` | 日本語 (ja). Needs review. |
| `static Nl` | `SpreadsheetTexts` | Nederlands (nl). Needs review. |
| `static Pt` | `SpreadsheetTexts` | Português (pt). Needs review. |
| `static PtBr` | `SpreadsheetTexts` | Português do Brasil (pt-BR). Needs review. |
| `static ReviewedCodes` | `IReadOnlyList<string>` | The codes whose texts were reviewed by a native speaker. The other sets are drafts waiting for review. |
| `static Ru` | `SpreadsheetTexts` | Русский (ru). Needs review. |
| `static Tables` | `IReadOnlyDictionary<string, SpreadsheetTexts>` | Every set by locale code: the codes of `KlyLocales.All`. |
| `static ZhCn` | `SpreadsheetTexts` | 简体中文 (zh-Hans). Needs review. |

### Classes

#### `KlyrooSpreadsheetOptions`

`Klyroo.Spreadsheet` · class

Configuration of Klyroo.Spreadsheet: the built-in texts of each locale. It is an extension of `KlyrooOptions` (`KlyrooOptions.Extension``). Configure it with `services.AddKlyrooSpreadsheet(s => ...)`, in code shared by the server and the WebAssembly client.

| Constructor | Description |
| :--- | :--- |
| `KlyrooSpreadsheetOptions()` | Creates the options with the built-in texts of `SpreadsheetLocales`. |

| Method | Returns | Description |
| :--- | :--- | :--- |
| `Texts(string localeCode, Func<SpreadsheetTexts, SpreadsheetTexts> change)` | `KlyrooSpreadsheetOptions` | Changes the texts of a locale, like `KlyLocales.Extend` does for Klyroo.UI: `change` receives the set the locale resolves to and returns the one to use, usually `t => t with { ... }`. `localeCode` is a code of `KlyLocales.All` ("fr"), or the code of a locale you added ("en-AU", "tlh"): a locale without a set of its own starts from the set `KlyLocales.ResolveCode` picks for it, then its changes apply. The texts become read-only the first time a component resolves one. |

### Records

#### `SpreadsheetDownload`

`Klyroo.Spreadsheet` · record

A file `KlySpreadsheetImport` offers to the user: the template or the invalid rows. Passed to `KlySpreadsheetImport.OnDownload`.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetDownload(string FileName, string Content, string MimeType)` | A file `KlySpreadsheetImport` offers to the user: the template or the invalid rows. Passed to `KlySpreadsheetImport.OnDownload`. |

| Field | Type | Description |
| :--- | :--- | :--- |
| `const CsvMimeType = "text/csv;charset=utf-8"` | `string` | The media type of the CSV files the component produces. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Content` (init) | `string` | The text of the file. CSV starts with a byte order mark so Excel reads it as UTF-8. |
| `FileName` (init) | `string` | The suggested file name, for example `people-template.csv`. |
| `MimeType` (init) | `string` | The media type, `text/csv;charset=utf-8`. |

#### `SpreadsheetTexts`

`Klyroo.Spreadsheet` · record

The built-in texts of `KlySpreadsheetImport`: button labels, accessible names, empty states, counters, the messages of the issues the importer raises, and what the live region announces. There is one set per locale of Klyroo.UI (`SpreadsheetLocales`), and the component shows the set of its current locale, the one its `Messages` come from. Change a set with `KlyrooSpreadsheetOptions.Texts` in `AddKlyrooSpreadsheet(s => ...)`.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetTexts()` | Creates the texts with their English defaults. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `AllIssues` (init) | `string` | The choice of the issue filter that shows every row (`"All issues"`). |
| `AllSeverities` (init) | `string` | The choice of the severity filter that shows every row (`"All severities"`). |
| `AllValuesFound` (init) | `string` | The text shown when no value is unknown (`"Every value was found in the list"`). |
| `AlreadyImported` (init) | `Func<int, string>` | The note of a failed or cancelled import, given the number of rows already accepted (`"40 rows are already imported and will not be sent again."`). |
| `AnswerFor` (init) | `Func<string, string>` | The accessible name of the answer list, given the value (`"Answer for Gold"`). |
| `Back` (init) | `string` | The Back button (`"Back"`). |
| `Cancel` (init) | `string` | The button that cancels a running import or check (`"Cancel"`). |
| `CheckAgain` (init) | `string` | The button that runs the checks again (`"Check again"`). |
| `CheckCancelled` (init) | `string` | The state of the checks after a cancellation (`"The check was cancelled"`). |
| `CheckFailed` (init) | `Func<string, string>` | The state of the checks after a failure whose text is meant for the user (`SpreadsheetUserMessageException`), given that text (`"The check failed: ..."`). |
| `CheckFailedGeneric` (init) | `string` | The state of the checks after any other failure (`"The check failed"`); the exception goes to `SpreadsheetImporter.Logger`. |
| `CheckingRows` (init) | `string` | The state of the checks while they run (`"Checking the rows…"`). |
| `Choose` (init) | `string` | The first choice of an unanswered question (`"Choose…"`). |
| `ChooseFileLabel` (init) | `string` | The accessible name of the file input (`"Choose a file to import"`). |
| `ChooseFileToContinue` (init) | `string` | The hint next to the disabled Continue button of the file step (`"Choose a file to continue"`). |
| `ClearSelection` (init) | `string` | The button that empties the selection (`"Clear selection"`). |
| `CloseInspector` (init) | `string` | The accessible name of the button that closes the inspector (`"Close inspector"`). |
| `ColumnFor` (init) | `Func<string, string>` | The accessible name of the file column list, given the field label (`"Column for Email"`). |
| `ColumnNamesOnRow` (init) | `string` | The label of the header row list (`"Column names on row"`). |
| `ColumnRow` (init) | `string` | The heading of the row number column (`"Row"`). |
| `ColumnStatus` (init) | `string` | The heading of the status column (`"Status"`). |
| `Continue` (init) | `string` | The Continue button (`"Continue"`). |
| `CreateValue` (init) | `Func<string, string>` | The choice that creates the value, given the value (`"Create \"Gold\""`). |
| `DownloadFailed` (init) | `string` | The error shown when the browser download fails (`"The file could not be downloaded"`). It never carries the text of the exception. |
| `DownloadInvalidRows` (init) | `Func<int, string>` | The button that downloads the invalid rows, given their count (`"Download the 2 invalid rows"`). |
| `DownloadTemplate` (init) | `string` | The button that downloads the CSV template (`"Download a template"`). |
| `DropFileHere` (init) | `string` | The text of the drop zone (`"Drop your file here or click to choose one"`). |
| `EditCell` (init) | `Func<string, string>` | The accessible name of the cell editor, given the field label (`"Edit Email"`). |
| `EmptyCell` (init) | `string` | The text of a cell with no value (`"empty"`). |
| `Errors` (init) | `string` | The choice of the severity filter that shows rows with errors (`"Errors"`). |
| `ExpectedColumns` (init) | `string` | The label above the columns the schema expects (`"Expected columns:"`). |
| `FileReadFailed` (init) | `string` | The error shown when the file the user chose could not be read from the browser (`"The file could not be read"`). A file that is not a valid workbook has its own message, written by the library. |
| `FileSummary` (init) | `Func<int, int, int, string>` | The summary of the loaded file, given the number of rows, the number of sheets and the row number of the header (`"120 rows · 2 sheets · Column names on row 1"`). |
| `FileTooLarge` (init) | `Func<int, string>` | The error for a file over `MaxFileSize`, given the limit in megabytes (`"The file is larger than 10 MB."`). |
| `FirstPage` (init) | `string` | The accessible name of the first page button (`"First page"`). |
| `Import` (init) | `string` | The heading of the wizard when you set no `Title` (`"Import"`). |
| `ImportAnotherFile` (init) | `string` | The button shown once the import is done (`"Import another file"`). |
| `ImportCancelled` (init) | `string` | The progress label of a cancelled import (`"The import was cancelled"`). |
| `ImportFailed` (init) | `string` | The progress label of a failed import (`"The import failed"`). |
| `Importing` (init) | `string` | The progress label of a running import (`"Importing…"`). |
| `ImportRows` (init) | `Func<int, string>` | The import button, given the number of rows it sends (`"Import 120 rows"`). |
| `InspectorRow` (init) | `Func<int, string>` | The title of the inspector, given the row number (`"Row 12"`). |
| `InspectRow` (init) | `Func<int, string>` | The accessible name of the button that opens a row, given the row number (`"Inspect row 12"`). |
| `Issue` (init) | `string` | The accessible name of the issue filter (`"Issue"`). |
| `IssueBetween` (init) | `Func<string, string, string>` | The default message of `SpreadsheetRule.Between``, given the minimum and the maximum (`"Value is not between 0 and 100"`). |
| `IssueBoolean` (init) | `Func<string, string>` | The message of the `boolean` issue, given the cell text (`"\"maybe\" is not yes or no"`). |
| `IssueDate` (init) | `Func<string, string>` | The message of the `date` issue, given the cell text (`"\"31/02/2026\" is not a date this column reads"`). |
| `IssueEmail` (init) | `string` | The default message of `SpreadsheetRule.Email` (`"Not an email address"`). |
| `IssueMax` (init) | `Func<string, string>` | The default message of `SpreadsheetRule.Max``, given the maximum (`"Value is above 100"`). |
| `IssueMin` (init) | `Func<string, string>` | The default message of `SpreadsheetRule.Min``, given the minimum (`"Value is below 0"`). |
| `IssueNumber` (init) | `Func<string, string>` | The message of the `number` issue, given the cell text (`"\"abc\" is not a number"`). |
| `IssuePattern` (init) | `string` | The default message of `SpreadsheetRule.Pattern` (`"Unexpected format"`). |
| `IssuePatternTimeout` (init) | `string` | The message of a `pattern` issue raised because the check timed out (`"The format could not be checked in time"`). |
| `IssueRequired` (init) | `string` | The message of the `required` issue (`"Required value is missing"`). |
| `IssueRowDuplicate` (init) | `Func<string, string>` | The message of the `row.duplicate` issue, given the number of the other row (`"Same key as row 4"`). |
| `IssueRowExists` (init) | `string` | The message of the `row.exists` issue (`"Already stored"`). |
| `IssueRowFailed` (init) | `string` | The message of the `row.failed` issue, which the library writes when a callback of yours (the row factory, a property setter, a rule predicate or `ValidateRow`) threw. It never contains the text of the exception, which goes to `SpreadsheetImporter.Logger` (`"The row could not be checked"`). |
| `IssuesColumn` (init) | `string` | The heading of the last column of the invalid rows download (`"Issues"`). |
| `IssueValueNotFound` (init) | `Func<string, string>` | The message of the `value.not-found` issue, given the cell text (`"\"Gold\" is not a known value"`). |
| `IssueValueSkipped` (init) | `Func<string, string>` | The message of the `value.skipped` issue, given the cell text (`"Left out: \"Gold\" is set to skip its rows"`). |
| `IssueValueUnknown` (init) | `Func<string, string>` | The message of the `value.unknown` issue, given the cell text (`"\"Gold\" is not a known value · choose it in Values"`). |
| `LastPage` (init) | `string` | The accessible name of the last page button (`"Last page"`). |
| `LeaveEmpty` (init) | `string` | The choice that imports the rows with an empty cell (`"Leave empty"`). |
| `LeaveOut` (init) | `string` | The button that leaves the selected rows out (`"Leave out"`). |
| `LeaveOutRow` (init) | `string` | The button that leaves the inspected row out (`"Leave out this row"`). |
| `LiveImporting` (init) | `Func<int, int, string>` | What the live region announces while importing, given the rows sent and the rows to send (`"Importing: 40 of 120 rows"`). |
| `LiveReview` (init) | `Func<int, int, int, string>` | What the polite live region announces on the review step, given the rows to import, the rows with errors and the rows left out (`"To import: 118. With errors: 2. Left out: 0."`). |
| `NextIssueRow` (init) | `string` | The accessible name of the button that goes to the next row with issues (`"Next row with issues"`). |
| `NextPage` (init) | `string` | The accessible name of the next page button (`"Next page"`). |
| `NoRows` (init) | `string` | The text of a review table with no row to show (`"No rows to show"`). |
| `NothingToDo` (init) | `string` | The note on a step that was passed over because it had nothing to resolve (`"(nothing to do)"`). |
| `NotImported` (init) | `Func<string, string>` | The list of file columns no field uses, given the list (`"Not imported: Notes, Id"`). |
| `NotInFile` (init) | `string` | The choice that declares a field absent from the file (`"Not in the file"`). |
| `Optional` (init) | `string` | The mark of an optional expected column (`"optional"`). |
| `OriginalValue` (init) | `Func<string, string>` | The tooltip of an edited cell, given the text in the file (`"Original: ada@exmaple.com"`). |
| `PageOf` (init) | `Func<int, int, string>` | The page position, given the 1-based page and the page count (`"Page 2 of 3"`). |
| `Pagination` (init) | `string` | The accessible name of the pager (`"Pagination"`). |
| `PasteLabel` (init) | `string` | The accessible name of the paste box (`"Rows to import"`). |
| `PastePlaceholder` (init) | `string` | The placeholder of the paste box (`"Paste the rows here, header row first"`). |
| `PasteSummary` (init) | `string` | The heading of the paste area (`"Or paste rows copied from a spreadsheet"`). |
| `PreviousIssueRow` (init) | `string` | The accessible name of the button that goes to the previous row with issues (`"Previous row with issues"`). |
| `PreviousPage` (init) | `string` | The accessible name of the previous page button (`"Previous page"`). |
| `Progress` (init) | `Func<int, int, string>` | The progress count, given the rows sent and the rows to send (`"40 of 120"`). |
| `ReadingFile` (init) | `string` | The text of the drop zone while a file is read (`"Reading the file…"`). |
| `Replace` (init) | `string` | The button that unloads the file to choose another (`"Replace"`). |
| `RequiredColumnsMissing` (init) | `Func<int, string>` | The hint of the columns step, given the number of required columns without a file column (`"Required columns missing: 2"`). |
| `Restore` (init) | `string` | The button that brings the selected rows back (`"Restore"`). |
| `RestoreRow` (init) | `string` | The button that brings the inspected row back (`"Restore this row"`). |
| `ResumeImport` (init) | `string` | The button that continues a cancelled import (`"Resume the import"`). |
| `RowCount` (init) | `Func<int, string>` | The number of rows that hold an unknown value, given the count (`"3 rows"`). |
| `RowLimitNotice` (init) | `Func<int, int, string>` | The note shown when rows are past the row limit, given the limit and the number of rows left out (`"Only the first 10,000 rows are imported. Rows left out because of the limit: 2,345."`). |
| `RowsChecked` (init) | `string` | The state of the checks once they finished (`"The rows were checked"`). |
| `RowsImported` (init) | `Func<int, string>` | The progress label of a finished import, given the number of rows accepted (`"120 rows imported"`). |
| `RowsLeftOut` (init) | `string` | The label of the statistic that counts the rows left out (`"rows left out"`). |
| `RowsPerPage` (init) | `string` | The label of the page size list (`"Rows per page"`). |
| `RowsRange` (init) | `Func<int, int, int, string>` | The rows on the page, given the first row, the last row and the total (`"Rows 51 to 100 of 120"`). |
| `RowsRefused` (init) | `Func<int, string>` | The count of rows the server refused, given the count (`"Rows refused: 2"`). |
| `RowsToImport` (init) | `string` | The label of the statistic that counts the rows to import (`"rows to import"`). |
| `RowsWithErrors` (init) | `string` | The label of the statistic that counts the rows with errors (`"rows with errors"`). |
| `RowsWithErrorsLeftOut` (init) | `Func<int, string>` | The hint of the review step, given the number of invalid rows (`"Rows with errors are left out: 4"`). |
| `RuleDisabledNotice` (init) | `Func<string, string, string>` | The note shown when a rule was stopped because its matches kept timing out, given the column label and the rule code (`"The \"pattern\" check of Code was stopped after repeated timeouts. Its remaining values were not checked."`). |
| `RuleFailedNotice` (init) | `Func<string, string, string>` | The note shown when a rule was stopped because its predicate kept throwing, given the column label and the rule code (`"The \"min\" check of Qty was stopped after repeated failures. Its remaining values were not checked."`). |
| `SamplesText` (init) | `Func<string, string>` | The sample values of a mapped column, given the list (`"Samples: A-1, B-2"`). |
| `SavedMappingApplied` (init) | `string` | The note shown when `SpreadsheetImporter.SavedMapping` was applied (`"Your saved column mapping was applied."`). |
| `SearchPlaceholder` (init) | `string` | The placeholder of the search box (`"Search rows…"`). |
| `SearchRows` (init) | `string` | The accessible name of the search box (`"Search rows"`). |
| `SeeThem` (init) | `string` | The button that shows the refused rows (`"See them"`). |
| `SelectedCount` (init) | `Func<int, string>` | The size of the selection, given the number of rows (`"3 selected"`). |
| `SelectPageRows` (init) | `string` | The accessible name of the checkbox that selects the rows of the page (`"Select the rows of this page"`). |
| `SelectRow` (init) | `Func<int, string>` | The accessible name of a row checkbox, given the row number (`"Select row 12"`). |
| `SendAgain` (init) | `string` | The button that sends the refused rows again (`"Send them again"`). |
| `Severity` (init) | `string` | The accessible name of the severity filter (`"Severity"`). |
| `Sheet` (init) | `string` | The label of the sheet list (`"Sheet"`). |
| `SkipRows` (init) | `string` | The choice that leaves out the rows that hold the value (`"Don't import these rows"`). |
| `StatusBlocking` (init) | `string` | The status of a row with errors (`"Blocking"`). |
| `StatusDiscarded` (init) | `string` | The status of a row left out of the import (`"Discarded"`). |
| `StatusImported` (init) | `string` | The status of a row the host accepted. It is final: the row can no longer be edited or left out (`"Imported"`). |
| `StatusValid` (init) | `string` | The status of a row without issues (`"Valid"`). |
| `StatusWarning` (init) | `string` | The status of a row with warnings only (`"Warning"`). |
| `StepColumns` (init) | `string` | The title of the columns step (`"Columns"`). |
| `StepFile` (init) | `string` | The title of the file step in the step bar (`"File"`). |
| `StepReview` (init) | `string` | The title of the review step (`"Review"`). |
| `StepSubmit` (init) | `string` | The title of the last step (`"Import"`). |
| `StepValues` (init) | `string` | The title of the values step (`"Values"`). |
| `SuggestionText` (init) | `Func<string, string, string>` | The suggestion, given the header and the sample values (`"\"Code\" has values like A-1, B-2"`). |
| `SummaryEdited` (init) | `string` | The summary label of the rows with an edited cell (`"Cells edited"`). |
| `SummaryInvalid` (init) | `string` | The summary label of the invalid rows (`"Invalid, not imported"`). |
| `SummaryLeftOut` (init) | `string` | The summary label of the rows left out (`"Left out"`). |
| `SummaryToImport` (init) | `string` | The summary label of the rows to import (`"Rows to import"`). |
| `TabAll` (init) | `Func<int, string>` | The tab of all rows, given their count (`"All (120)"`). |
| `TabDiscarded` (init) | `Func<int, string>` | The tab of the rows left out, given their count (`"Left out (0)"`). |
| `TabImportable` (init) | `Func<int, string>` | The tab of the rows to import, given their count (`"To import (118)"`). |
| `TabInvalid` (init) | `Func<int, string>` | The tab of the invalid rows, given their count (`"Invalid (2)"`). |
| `TryAgain` (init) | `string` | The button that sends the rest again after a failed import (`"Try again"`). |
| `Undo` (init) | `string` | The button that undoes a manual choice or an edit (`"Undo"`). |
| `UpToRows` (init) | `Func<int, string>` | The row limit shown under the drop zone, given the limit (`"up to 10,000 rows"`). |
| `UseSuggestion` (init) | `string` | The button that accepts a suggested file column (`"Use it"`). |
| `UseTheseRows` (init) | `string` | The button that loads the pasted rows (`"Use these rows"`). |
| `ValuesToChoose` (init) | `Func<int, string>` | The hint of the values step, given the number of open questions (`"Values to choose: 3"`). |
| `Warnings` (init) | `string` | The choice of the severity filter that shows rows with warnings (`"Warnings"`). |
| `WholeRow` (init) | `string` | The label of an issue that belongs to the whole row (`"Row"`). |

| Method | Returns | Description |
| :--- | :--- | :--- |
| `ResolveIssue(SpreadsheetRowIssue issue)` | `string` | The message of an issue in the language of this set: the text for its `SpreadsheetRowIssue.Code` when the library wrote the message, or `SpreadsheetRowIssue.Message` as it is when you did. |
| `StatusText(SpreadsheetRowStatus status)` | `string` | The text of a row status. |
| `StepTitle(string stepKey, string fallback = "")` | `string` | The title of a wizard step in the step bar. |
