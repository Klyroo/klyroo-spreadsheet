# Klyroo.Spreadsheet API reference

> Generated from the compiled assemblies and their XML documentation. Do not edit by hand.

## Klyroo.Spreadsheet.Core

Static classes and extensions (2) · Classes (3) · Records (13) · Enums (13)

### Static classes and extensions

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

#### `SpreadsheetColumnSchema`

`Klyroo.Spreadsheet` · class

Declares one field of the imported data: how to find it in the file, how to parse it and how to validate it.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetColumnSchema()` | Creates a column definition. Set `SpreadsheetColumnSchema.Key` in the object initializer. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `CustomParse` (init) | `Func<string, object?>?` | Replaces the built-in parsing of non-select columns. It receives the cell text and returns the value. An exception marks the cell as an error with the exception message. |
| `DateFormats` (init) | `IReadOnlyList<string>?` | Exact `DateTime` format strings accepted for `SpreadsheetColumnKind.Date` cells. When empty, the defaults are `yyyy-MM-dd`, `yyyy-MM-dd HH:mm`, `yyyy-MM-dd HH:mm:ss`, `dd/MM/yyyy` and `dd/MM/yyyy HH:mm`. |
| `DecimalSeparator` (init) | `char` | The decimal mark of `SpreadsheetColumnKind.Number` cells, `.` or `,`. The other character is accepted as a thousands separator only between groups of three digits, so `12,50` is rejected when the mark is `.`. |
| `DefaultValue` (init) | `object?` | The value used for empty cells. It only applies when `SpreadsheetColumnSchema.HasDefault` is `true`. |
| `Description` (init) | `string?` | Help text for your own UI. The importer and the built-in component do not read it. |
| `Editable` (init) | `bool` | Whether the built-in component lets the user edit this column's cells in the review table. `SpreadsheetImporter.EditCell` ignores it. |
| `EffectiveLabel` | `string` | `SpreadsheetColumnSchema.Label` when set, otherwise `SpreadsheetColumnSchema.Key` turned into words, for example `secureCode` becomes `Secure code`. |
| `Example` (init) | `string?` | A sample value written in the second row of `SpreadsheetImporter.ExportTemplateCsv`. |
| `From` (init) | `string?` | The `SpreadsheetColumnSchema.Key` of an earlier column whose cell text this column reuses instead of reading its own file column. A column with `SpreadsheetColumnSchema.From` never matches a header and is left out of the template. The source column must come first in `SpreadsheetSchema.Columns`. |
| `Group` (init) | `string?` | A grouping label for your own UI. The importer and the built-in component do not read it. |
| `HasDefault` (init) | `bool` | Set to `true` to apply `SpreadsheetColumnSchema.DefaultValue`, which lets the default itself be `null`. A column with a default is never missing. |
| `Headers` (init) | `IReadOnlyList<string>?` | Additional header spellings that match this column, on top of `SpreadsheetColumnSchema.Key` and `SpreadsheetColumnSchema.EffectiveLabel`. |
| `Key` (init) | `string` | The field name. It is the key of `SpreadsheetRow.Data` and `SpreadsheetRow.Cells`, and it also matches a file header. |
| `Kind` (init) | `SpreadsheetColumnKind` | How cells are parsed. Defaults to `SpreadsheetColumnKind.Text`. |
| `Label` (init) | `string?` | The display name, also matched against file headers. Defaults to `SpreadsheetColumnSchema.EffectiveLabel`. |
| `OnCreateOption` (init) | `Func<string, SpreadsheetOption>?` | Called once per distinct value answered with `SpreadsheetValueAnswerAction.Create`, when the import is submitted and before any row is sent. Use it to persist the new option. The rows then carry the returned option: its `SpreadsheetOption.Value` in `SpreadsheetRow.Data` and its label in the review, and later sends do not call it again for that value. Setting it also makes the component offer a create choice (`SpreadsheetValueQuestion.CanCreate`). |
| `Options` (init) | `IReadOnlyList<SpreadsheetOption>?` | The allowed values of a `SpreadsheetColumnKind.Select` column. |
| `Required` (init) | `bool` | When `true`, an empty cell is a blocking issue (code `required`) and a file with no matching column cannot be submitted, unless the column has a default. |
| `Rules` (init) | `IReadOnlyList<SpreadsheetRule>?` | Validation rules run, in order, on every non-empty parsed value. |
| `UnknownPolicy` (init) | `SpreadsheetUnknownValuePolicy` | What to do with select cells that match no option. Defaults to `SpreadsheetUnknownValuePolicy.Ask`. |

#### `SpreadsheetImporter`

`Klyroo.Spreadsheet` · class

The headless import engine. It loads delimited text or a prepared workbook, finds the sheet and header row, maps file columns to the columns of a `SpreadsheetSchema`, parses and validates every row, tracks the user's corrections, and sends the importable rows to `SpreadsheetSchema.OnSubmit` in batches.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetImporter(SpreadsheetSchema schema)` | Creates an importer for a schema, with no file loaded. |

| Field | Type | Description |
| :--- | :--- | :--- |
| `const HeaderScanRows = 20` | `int` | How many leading rows of a sheet are scanned to find the header row. |
| `const SuggestionPassRate = 0.8` | `double` | The share of sampled cells that must parse and pass the error-level rules before a file column is suggested for a missing column. |
| `const SuggestionSampleSize = 30` | `int` | The maximum number of non-empty cells sampled from a file column when looking for a suggestion. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `AllRows` | `IReadOnlyList<SpreadsheetRow>` | Every data row after the header, discarded ones included. |
| `AmbiguousDetection` | `bool` | `true` when several sheets matched the schema equally well and the first one was picked. Offer the user a sheet choice in that case. |
| `CanNextStep` | `bool` | Whether the current step is ready and is not the last one. |
| `CanPreviousStep` | `bool` | Whether there is an earlier step and no submit is running. |
| `CanSubmit` | `bool` | `true` when a file is loaded, no field is missing, no question is open and at least one row is importable. |
| `CurrentSheet` | `SpreadsheetSheet?` | The sheet being imported: the selected one, otherwise the first. |
| `CurrentStepKey` | `string` | The key of the wizard step in progress: `file`, `columns`, `values`, `review` or `submit`. |
| `DiscardedRows` | `IReadOnlyList<SpreadsheetRow>` | The rows left out of the import for any reason. |
| `Fields` | `IReadOnlyList<SpreadsheetFieldState>` | The mapping state of every schema column, in schema order. |
| `FileLoaded` | `bool` | Whether a workbook with at least one sheet is loaded. |
| `FileName` | `string?` | The name of the loaded file, or `null`. |
| `HeaderRowIndex` | `int` | The index, within `SpreadsheetSheet.Rows`, of the row used as header. |
| `HeaderRowNumber` | `int` | The file row number of the header row, for messages to the user. 1 when no file is loaded. |
| `Headers` | `IReadOnlyList<string>` | The cell text of the header row. |
| `ImportableRows` | `IReadOnlyList<SpreadsheetRow>` | The rows that would be sent on submit. Each access builds a new list. |
| `ImportedCount` | `int` | The rows accepted since the last `SpreadsheetImporter.SubmitAsync`: rows sent minus rows rejected, plus the rows each `SpreadsheetImporter.RetryRejectedAsync` got accepted. |
| `InspectedRow` | `SpreadsheetRow?` | The row open in the inspector, or `null`. |
| `InspectedRowIndex` | `int?` | The `SpreadsheetRow.Index` of the row open in the inspector, or `null`. |
| `InvalidRows` | `IReadOnlyList<SpreadsheetRow>` | The rows with at least one error that are not discarded. |
| `MissingFields` | `IReadOnlyList<SpreadsheetFieldState>` | The fields with status `SpreadsheetColumnStatus.Missing`. Submission is blocked while this is not empty. |
| `OpenQuestions` | `IReadOnlyList<SpreadsheetValueQuestion>` | The questions with no answer yet. Submission is blocked while this is not empty. |
| `Questions` | `IReadOnlyList<SpreadsheetValueQuestion>` | Every unknown select value found in the file, answered or not. |
| `RejectedCount` | `int` | The number of rows the server refused in the last submit and that have not been fixed or retried. |
| `ReviewIssueFilter` | `string` | The issue filter applied to `SpreadsheetImporter.VisibleReviewRows`, as `fieldKey`, `fieldKey\|code`, or `\|code` for issues on the whole row (such as `row.duplicate`). Empty for none. |
| `ReviewLevel` | `SpreadsheetReviewLevel` | The severity filter applied to `SpreadsheetImporter.VisibleReviewRows`. |
| `ReviewSearch` | `string` | The text searched in the cells of `SpreadsheetImporter.VisibleReviewRows`. Empty for none. |
| `ReviewTab` | `SpreadsheetReviewTab` | The row subset selected for `SpreadsheetImporter.VisibleReviewRows`. |
| `Schema` | `SpreadsheetSchema` | The schema this importer was created with. |
| `SelectedRowIndexes` | `IReadOnlySet<int>` | The indexes of the rows selected in the review table. |
| `Sheets` | `IReadOnlyList<SpreadsheetSheet>` | The sheets of the loaded file. Empty when nothing is loaded. |
| `SubmitDone` | `int` | How many rows of the current or last submit have been sent so far. |
| `SubmitError` | `string?` | The message of the exception thrown by the submit callback, or `null`. |
| `SubmitStatus` | `SpreadsheetSubmitStatus` | The state of the current or last submit. |
| `SubmitTotal` | `int` | How many rows the current or last submit sends. |
| `UnusedHeaders` | `IReadOnlyList<string>` | The non-empty headers that no schema column uses. They are not imported. |
| `VisibleReviewRows` | `IReadOnlyList<SpreadsheetRow>` | The rows to show in a review table: the selected tab, narrowed by the severity filter, the issue filter and the search text. Each access builds a new list. |

| Method | Returns | Description |
| :--- | :--- | :--- |
| `AnswerValue(string fieldKey, string rawValue, SpreadsheetValueAnswer answer)` | `void` | Answers an unknown select value for every row that contains it. A user answer overrides the column's policy. |
| `AssignColumn(string fieldKey, int headerIndex)` | `void` | Maps a file column to a schema column. If another column was manually mapped to the same file column, that mapping is removed. |
| `ClearAnswer(string fieldKey, string rawValue)` | `void` | Removes a user answer so the value falls back to the column's policy. |
| `ClearFile()` | `void` | Unloads the file, clears every user decision and returns the wizard to the `file` step. |
| `ClearSelection()` | `void` | Empties the selection. |
| `DiscardRows(IEnumerable<int> rowIndexes)` | `void` | Leaves rows out of the import (reason `SpreadsheetDiscardReason.Manual`) and clears the selection. |
| `EditCell(int rowIndex, string fieldKey, string text)` | `void` | Replaces the text of one cell and revalidates. The edit applies in the importer only and never changes the source file. It also clears a server rejection of that row. |
| `ExportInvalidRowsCsv()` | `string` | Builds a CSV of the rows with errors so users can fix them in their own tool and import the file again. |
| `ExportTemplateCsv()` | `string` | Builds a CSV template for users to fill in: a header row and an example row for every column without `SpreadsheetColumnSchema.From`. |
| `GetSteps()` | `IReadOnlyList<SpreadsheetStepStatus>` | Returns the five wizard steps (file, columns, values, review, submit) with their current state. |
| `GoToStep(string key)` | `void` | Jumps back to an earlier step, including a step that was skipped because it had nothing to resolve, so users can still re-map a column. Forward jumps are ignored. |
| `static HumanizeKey(string key)` | `string` | Turns a camelCase, PascalCase, snake_case or kebab-case key into words, for example `secureCode` into `Secure code`. |
| `IgnoreColumn(string fieldKey)` | `void` | Declares that a schema column is not in the file, overriding automatic matching. A required column then becomes missing. |
| `InspectNextIssueRow()` | `void` | Moves the inspector to the next non-discarded row that has a warning or an error, wrapping around. Does nothing when there is none. |
| `InspectPreviousIssueRow()` | `void` | Moves the inspector to the previous non-discarded row that has a warning or an error, wrapping around. Does nothing when there is none. |
| `InspectRow(int? rowIndex)` | `void` | Opens a row in the inspector, or closes the inspector. |
| `LoadFileAsync(Stream content, string fileName, long maxBytes = 10485760, CancellationToken cancellationToken = null)` | `Task` | Reads a file and loads it: an `.xlsx` workbook through `SpreadsheetXlsxReader`, a legacy `.xls` workbook through `SpreadsheetXlsReader`, anything else as delimited text decoded as UTF-8 (with or without a byte order mark), or as Latin-1 when the bytes are not valid UTF-8. The content decides for the legacy format: a file that starts with the OLE2 signature is read as `.xls` whatever its extension, and a zip archive named `.xls` is read as `.xlsx`. |
| `LoadText(string text, string fileName = "import.csv")` | `void` | Parses delimited text as a one-sheet workbook and loads it, replacing any previous file and discarding the user's corrections. |
| `LoadWorkbook(SpreadsheetWorkbook workbook)` | `void` | Loads a prepared workbook, replacing any previous file and discarding the user's column choices, answers, edits and discards. Detects the sheet and header row unless the schema forces them. |
| `NextStep()` | `bool` | Moves to the next step that is not skipped. |
| `static NormalizeText(string? input)` | `string` | Normalizes a header or option text for comparison: removes a trailing `(required)` and trailing `*`, strips accents, collapses whitespace and lowercases. |
| `static ParseDelimitedSheet(string? content, string sheetName = "Sheet 1")` | `SpreadsheetSheet` | Parses CSV, TSV or semicolon-separated text into a sheet. Handles quoted fields with doubled quotes and line breaks, and strips a leading byte order mark. |
| `Paste(string text, string fileName = "clipboard.tsv")` | `void` | Loads text pasted from a spreadsheet. It behaves exactly like `SpreadsheetImporter.LoadText` with a different default name. |
| `PreviousStep()` | `bool` | Moves to the previous step that is not skipped. |
| `ResetColumn(string fieldKey)` | `void` | Removes a manual choice made by `SpreadsheetImporter.AssignColumn` or `SpreadsheetImporter.IgnoreColumn` so the column is matched by header again. |
| `RestoreRows(IEnumerable<int> rowIndexes)` | `void` | Brings back rows discarded with `SpreadsheetImporter.DiscardRows` and clears the selection. Rows left out for another reason stay out. |
| `RetryRejectedAsync()` | `Task` | Sends again the rows the server rejected, that are still importable. The rejection issues are cleared before the send. Does nothing while a submit is running or when nothing was rejected. |
| `RevertCell(int rowIndex, string fieldKey)` | `void` | Removes an edit made by `SpreadsheetImporter.EditCell` and restores the file text. |
| `SetHeaderRow(int rowIndex)` | `void` | Chooses a different header row of the current sheet and clears column choices and discards. |
| `SetReviewIssueFilter(string filter)` | `void` | Filters the review rows to those with an issue on one column, optionally with one code. A non-empty filter switches the Importable and Discarded tabs back to All. |
| `SetReviewLevel(SpreadsheetReviewLevel level)` | `void` | Filters the review rows by issue severity. A filter other than All switches the Importable and Discarded tabs back to All. |
| `SetReviewSearch(string search)` | `void` | Filters the review rows to those with a cell containing the text, ignoring case. |
| `SetReviewTab(SpreadsheetReviewTab tab)` | `void` | Selects the review subset and clears the selection. Choosing Importable or Discarded also resets the severity filter. |
| `SetSheet(string sheetName)` | `void` | Switches to another sheet of the loaded file, re-detects its header row and clears column choices and discards. Does nothing for an unknown name. |
| `SubmitAsync()` | `Task` | Sends every importable row to `SpreadsheetSchema.OnSubmit` in batches of `SpreadsheetSchema.BatchSize`. It first calls `SpreadsheetColumnSchema.OnCreateOption` once for each value answered with Create. Rejected rows are kept as errors on their row. An exception from a callback is caught and reported in `SpreadsheetImporter.SubmitError`. Does nothing while a submit is running or when `SpreadsheetImporter.CanSubmit` is `false`. `SpreadsheetImporter.ImportedCount` starts again from zero. |
| `ToggleRowSelection(int rowIndex, bool selected)` | `void` | Adds a row to or removes it from the selection. |

#### `SpreadsheetSchema`

`Klyroo.Spreadsheet` · class

Describes an import: its columns and the policies for limits, duplicates, existing records and submission. Pass it to `SpreadsheetImporter`.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetSchema()` | Creates a schema with no columns and the default policies. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Accept` (init) | `IReadOnlyList<string>` | The file extensions the built-in component lists in its drop zone. They are display text: the importer enforces nothing, and `SpreadsheetImporter.LoadFileAsync` reads `.xlsx` and `.xls` workbooks and delimited text whatever this list says. |
| `BatchSize` (init) | `int` | The number of rows passed to `SpreadsheetSchema.OnSubmit` per call. Zero or less sends all rows in one call. |
| `Columns` (init) | `IReadOnlyList<SpreadsheetColumnSchema>` | The columns to import, in display order. A column with `SpreadsheetColumnSchema.From` must come after its source. |
| `Duplicates` (init) | `SpreadsheetDuplicatesPolicy` | How rows that repeat a key within the file are handled. Keys are compared trimmed and case-insensitively. Needs `SpreadsheetSchema.RowKeyField`. |
| `ExistingAction` (init) | `SpreadsheetExistingAction` | What to do with rows whose key is in `SpreadsheetSchema.ExistingRecords`. |
| `ExistingRecords` (init) | `IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>?` | Records that already exist, keyed by the row key value. A row matches when its key text equals a dictionary key under the dictionary's own comparer, with no trimming or case folding applied to the row, so build the dictionary with `StringComparer.OrdinalIgnoreCase` for case-insensitive keys. Needs `SpreadsheetSchema.RowKeyField`. |
| `ForcedHeaderRow` (init) | `int?` | The 1-based file row number of the header row, which skips header detection. A number that matches no row selects the first row. |
| `ForcedSheet` (init) | `string?` | The name of the sheet to read, which skips sheet detection. `null`, empty or `auto` detects the sheet; an unknown name falls back to the first sheet. |
| `Key` (init) | `string` | A name for the import for your own use. The importer does not read it. |
| `MaxRows` (init) | `int?` | The maximum number of data rows to import. Later rows are left out with `SpreadsheetDiscardReason.Limit`. No limit when `null`. |
| `OnSubmit` (init) | `Func<IReadOnlyList<SpreadsheetRow>, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>?` | Receives each batch of importable rows and returns the rows the server refused, or `null` or an empty list when all were accepted. If it throws, the submit stops with `SpreadsheetSubmitStatus.Error`. |
| `RowKeyField` (init) | `string?` | The `SpreadsheetColumnSchema.Key` of the column that identifies a row. It enables the duplicate and existing-record checks. |
| `ValidateRow` (init) | `Func<IReadOnlyDictionary<string, object?>, IReadOnlyList<SpreadsheetRowIssue>>?` | A cross-field check run on every row after the column rules. It receives the parsed values by column key and returns extra issues, which are added to the row. |

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

#### `SpreadsheetOption`

`Klyroo.Spreadsheet` · record

An allowed value of a `SpreadsheetColumnKind.Select` column.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetOption(string Value, string Label, IReadOnlyList<string>? Aliases = null)` | An allowed value of a `SpreadsheetColumnKind.Select` column. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Aliases` (init) | `IReadOnlyList<string>?` | Extra spellings that also match this option, for example a symbol or an abbreviation. |
| `Label` (init) | `string` | The text shown to the user. Cells are matched against it as well as against the value. |
| `Value` (init) | `string` | The value stored in `SpreadsheetRow.Data` when this option matches. |

#### `SpreadsheetRejectedRow`

`Klyroo.Spreadsheet` · record

A row that `SpreadsheetSchema.OnSubmit` refused. It comes back as an error issue with code `server` on that row.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetRejectedRow(int RowIndex, string Message, string? Field = null)` | A row that `SpreadsheetSchema.OnSubmit` refused. It comes back as an error issue with code `server` on that row. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Field` (init) | `string?` | The column key the reason relates to, or `null` for the whole row. |
| `Message` (init) | `string` | The reason, shown to the user. |
| `RowIndex` (init) | `int` | The `SpreadsheetRow.Index` of the refused row. |

#### `SpreadsheetRow`

`Klyroo.Spreadsheet` · record

One data row after parsing and validation, as the importer currently sees it.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetRow(int Index, int RowNumber, IReadOnlyDictionary<string, object?> Data, IReadOnlyDictionary<string, SpreadsheetCellView> Cells, IReadOnlyList<SpreadsheetRowIssue> Issues, SpreadsheetRowStatus Status, SpreadsheetDiscardReason? DiscardReason, SpreadsheetRowMode Mode, bool Edited, bool Importable)` | One data row after parsing and validation, as the importer currently sees it. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Cells` (init) | `IReadOnlyDictionary<string, SpreadsheetCellView>` | The display state of each cell by column key. |
| `Data` (init) | `IReadOnlyDictionary<string, object?>` | The parsed values by column key: `String` for text, `Double` for numbers, `Boolean` for booleans, an ISO 8601 `String` for dates and the option value for selects. Empty cells are `null`. |
| `DiscardReason` (init) | `SpreadsheetDiscardReason?` | Why the row is left out, or `null` when it is not. |
| `Edited` (init) | `bool` | Whether the user edited any cell of the row. |
| `Importable` (init) | `bool` | Whether the row is sent on submit: not discarded and without an error. |
| `Index` (init) | `int` | The 0-based position among the data rows (the rows after the header). Edit, discard and rejection methods use it. |
| `Issues` (init) | `IReadOnlyList<SpreadsheetRowIssue>` | Every issue found on the row. |
| `Mode` (init) | `SpreadsheetRowMode` | Whether the row creates a record or updates an existing one. |
| `RowNumber` (init) | `int` | The 1-based row number in the file, for messages to the user. |
| `Status` (init) | `SpreadsheetRowStatus` | The overall validation state. |

#### `SpreadsheetRowIssue`

`Klyroo.Spreadsheet` · record

A problem or note about a row or one of its cells.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetRowIssue(string? Field, string Code, SpreadsheetIssueLevel Level, string Message)` | A problem or note about a row or one of its cells. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Code` (init) | `string` | A stable identifier. Built-in codes are `required`, `number`, `boolean`, `date`, `text`, `value.unknown`, `value.not-found`, `value.skipped`, `row.duplicate`, `row.exists` and `server`, plus the code of each failed `SpreadsheetRule`. |
| `Field` (init) | `string?` | The column key the issue relates to, or `null` for the whole row. |
| `Level` (init) | `SpreadsheetIssueLevel` | The severity. |
| `Message` (init) | `string` | The text shown to the user. |

#### `SpreadsheetRule`

`Klyroo.Spreadsheet` · record

A validation rule attached to a column. A rule runs on the parsed value of a non-empty cell.

| Constructor | Description |
| :--- | :--- |
| `SpreadsheetRule(string Code, string Message, Func<object?, bool> Validate, SpreadsheetIssueLevel Level = Error)` | A validation rule attached to a column. A rule runs on the parsed value of a non-empty cell. |

| Property | Type | Description |
| :--- | :--- | :--- |
| `Code` (init) | `string` | A stable identifier copied to `SpreadsheetRowIssue.Code`, usable for filtering. |
| `Level` (init) | `SpreadsheetIssueLevel` | The severity of the issue. Only `SpreadsheetIssueLevel.Error` blocks the row. |
| `Message` (init) | `string` | The text of the issue shown to the user when the rule fails. |
| `Validate` (init) | `Func<object?, bool>` | Receives the parsed value and returns `true` when it is acceptable. |

| Method | Returns | Description |
| :--- | :--- | :--- |
| `static Between(double min, double max, string? message = null, SpreadsheetIssueLevel level = Error)` | `SpreadsheetRule` | Creates a rule that accepts numbers inside an inclusive range. |
| `static Email(string message = "Not an email address", SpreadsheetIssueLevel level = Error)` | `SpreadsheetRule` | Creates a rule that accepts text shaped like an email address (something before and after one `@`, and a dot followed by at least two characters). |
| `static Max(double max, string? message = null, SpreadsheetIssueLevel level = Error)` | `SpreadsheetRule` | Creates a rule that accepts numbers less than or equal to a maximum. |
| `static Min(double min, string? message = null, SpreadsheetIssueLevel level = Error)` | `SpreadsheetRule` | Creates a rule that accepts numbers greater than or equal to a minimum. |
| `static Pattern(string pattern, string message = "Unexpected format", SpreadsheetIssueLevel level = Error)` | `SpreadsheetRule` | Creates a rule that accepts text matching a regular expression. |

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

What happens to an imported row whose key is already present in `SpreadsheetSchema.ExistingRecords`.

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
| `Update` | The key matches an entry of `SpreadsheetSchema.ExistingRecords`. |
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
| `Error` | The submit callback threw. `SpreadsheetImporter.SubmitError` holds the message. |

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

#### `SpreadsheetValueAnswerAction`

`Klyroo.Spreadsheet` · enum

The decision recorded in a `SpreadsheetValueAnswer` for an unknown select value.

| Value | Description |
| :--- | :--- |
| `Map` | Replace the value with one of the column's options. |
| `LeaveEmpty` | Import the row with an empty cell. |
| `SkipRows` | Leave out every row that contains the value. |
| `Create` | Keep the value as a new option. |

## Klyroo.Spreadsheet

Components (1) · Static classes and extensions (1)

### Components

#### `KlySpreadsheetImport`

`Klyroo.Spreadsheet` · component · inherits `KlyComponentBase`

| Parameter | Type | Description |
| :--- | :--- | :--- |
| `Importer` | `SpreadsheetImporter` | The importer that holds the import state. The component renders it and calls its methods. Users load a file by choosing or dropping it on the file step (CSV, TSV, text, `.xlsx` or `.xls`, through `SpreadsheetImporter.LoadFileAsync`) or by pasting rows; your code can also call `SpreadsheetImporter.LoadText` or `SpreadsheetImporter.LoadWorkbook`. |
| `MaxFileSize` | `long` | The largest file users can choose, in bytes. Defaults to 10 MB. |
| `Title` | `string` | The heading shown above the wizard. Defaults to `Import`. |
| `Wizard` | `bool` | Shows the step bar and the Back, Continue and Import buttons. When `false` only the current step is rendered and your code moves between steps with the importer's step methods. |

Also accepts the parameters of `KlyComponentBase`.

### Static classes and extensions

#### `KlyrooSpreadsheetServiceCollectionExtensions`

`Klyroo.Spreadsheet` · service

Service registration for Klyroo.Spreadsheet.

| Method | Returns | Description |
| :--- | :--- | :--- |
| `static AddKlyrooSpreadsheet(IServiceCollection services)` | `IServiceCollection` | Registers the services `KlySpreadsheetImport` needs. It calls `AddKlyrooUi()` with its defaults, so call `AddKlyrooUi` with your own options before or after it, not instead of it. |
