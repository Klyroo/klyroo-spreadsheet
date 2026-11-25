# API Reference — Klyroo.Spreadsheet

Complete public API reference extracted directly from the source code of `Klyroo.Spreadsheet.Core` and `Klyroo.Spreadsheet`.

## Razor Components

### `<KlySpreadsheetImport>`
*Package*: `Klyroo.Spreadsheet` · *Source*: `src/Klyroo.Spreadsheet/KlySpreadsheetImport.razor`

| Parameter | Type | Description |
| :--- | :--- | :--- |
| `Importer` | `SpreadsheetImporter` | — |
| `Title` | `string` | — |
| `Wizard` | `bool` | — |

## C# Types, Controllers, Models & Enums

### `enum SpreadsheetColumnKind`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

Column data types in a `SpreadsheetSchema`.

| Member | Type | Description |
| :--- | :--- | :--- |
| `Text` | `enum member` | — |
| `Number` | `enum member` | — |
| `Date` | `enum member` | — |
| `Boolean` | `enum member` | — |
| `Select` | `enum member` | — |

### `enum SpreadsheetUnknownValuePolicy`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

Policy for unknown select values in `SpreadsheetColumnSchema`.

| Member | Type | Description |
| :--- | :--- | :--- |
| `Ask` | `enum member` | — |
| `Error` | `enum member` | — |
| `SkipRows` | `enum member` | — |
| `LeaveEmpty` | `enum member` | — |
| `Create` | `enum member` | — |

### `enum SpreadsheetDuplicatesPolicy`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

Duplicate row key policy in `SpreadsheetSchema`.

| Member | Type | Description |
| :--- | :--- | :--- |
| `Error` | `enum member` | — |
| `KeepFirst` | `enum member` | — |
| `KeepLast` | `enum member` | — |

### `enum SpreadsheetExistingAction`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

Action taken when an imported row matches an existing stored record.

| Member | Type | Description |
| :--- | :--- | :--- |
| `Update` | `enum member` | — |
| `Skip` | `enum member` | — |
| `Error` | `enum member` | — |

### `enum SpreadsheetColumnStatus`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

Status of a column mapping in `SpreadsheetImporter`.

| Member | Type | Description |
| :--- | :--- | :--- |
| `Matched` | `enum member` | — |
| `Default` | `enum member` | — |
| `Missing` | `enum member` | — |
| `Unmatched` | `enum member` | — |

### `enum SpreadsheetIssueLevel`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

Severity level of a `SpreadsheetRowIssue`.

| Member | Type | Description |
| :--- | :--- | :--- |
| `Info` | `enum member` | — |
| `Warning` | `enum member` | — |
| `Error` | `enum member` | — |

### `enum SpreadsheetRowStatus`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

Overall validation status of a `SpreadsheetRow`.

| Member | Type | Description |
| :--- | :--- | :--- |
| `Valid` | `enum member` | — |
| `Warning` | `enum member` | — |
| `Blocking` | `enum member` | — |
| `Discarded` | `enum member` | — |

### `enum SpreadsheetDiscardReason`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

Reason why a `SpreadsheetRow` was discarded from import.

| Member | Type | Description |
| :--- | :--- | :--- |
| `Manual` | `enum member` | — |
| `Value` | `enum member` | — |
| `Limit` | `enum member` | — |
| `Duplicate` | `enum member` | — |
| `Existing` | `enum member` | — |

### `enum SpreadsheetRowMode`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

Import mode of a `SpreadsheetRow`.

| Member | Type | Description |
| :--- | :--- | :--- |
| `Create` | `enum member` | — |
| `Update` | `enum member` | — |
| `Skip` | `enum member` | — |

### `enum SpreadsheetReviewTab`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

Review table tab filter in `SpreadsheetImporter`.

| Member | Type | Description |
| :--- | :--- | :--- |
| `All` | `enum member` | — |
| `Importable` | `enum member` | — |
| `Invalid` | `enum member` | — |
| `Discarded` | `enum member` | — |

### `enum SpreadsheetReviewLevel`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

Review issue level filter in `SpreadsheetImporter`.

| Member | Type | Description |
| :--- | :--- | :--- |
| `All` | `enum member` | — |
| `Blocking` | `enum member` | — |
| `Warnings` | `enum member` | — |

### `enum SpreadsheetValueAnswerAction`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

Action chosen for an unrecognized select value in `SpreadsheetValueQuestion`.

| Member | Type | Description |
| :--- | :--- | :--- |
| `Map` | `enum member` | — |
| `LeaveEmpty` | `enum member` | — |
| `SkipRows` | `enum member` | — |
| `Create` | `enum member` | — |

### `enum SpreadsheetSubmitStatus`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

Submission lifecycle state of `SpreadsheetImporter`.

| Member | Type | Description |
| :--- | :--- | :--- |
| `Idle` | `enum member` | — |
| `Running` | `enum member` | — |
| `Done` | `enum member` | — |
| `Error` | `enum member` | — |

### `record SpreadsheetOption`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

An allowed select option with optional aliases for exact normalized matching.

### `record SpreadsheetRule`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

A validation rule attached to a `SpreadsheetColumnSchema`.

| Member | Type | Description |
| :--- | :--- | :--- |
| `Email(string message = "Not an email address", SpreadsheetIssueLevel level = SpreadsheetIssueLevel.Error)` | `SpreadsheetRule` | — |
| `Pattern(string pattern, string message = "Unexpected format", SpreadsheetIssueLevel level = SpreadsheetIssueLevel.Error)` | `SpreadsheetRule` | — |
| `Min(double min, string? message = null, SpreadsheetIssueLevel level = SpreadsheetIssueLevel.Error)` | `SpreadsheetRule` | — |
| `Max(double max, string? message = null, SpreadsheetIssueLevel level = SpreadsheetIssueLevel.Error)` | `SpreadsheetRule` | — |
| `Between(double min, double max, string? message = null, SpreadsheetIssueLevel level = SpreadsheetIssueLevel.Error)` | `SpreadsheetRule` | — |

### `class SpreadsheetColumnSchema`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

Declarative definition of a spreadsheet column.

| Member | Type | Description |
| :--- | :--- | :--- |
| `Key` | `string` | — |
| `Label` | `string?` | — |
| `Kind` | `SpreadsheetColumnKind` | — |
| `Headers` | `IReadOnlyList<string>?` | — |
| `Description` | `string?` | — |
| `Example` | `string?` | — |
| `Group` | `string?` | — |
| `From` | `string?` | — |
| `Required` | `bool` | — |
| `Editable` | `bool` | — |
| `DefaultValue` | `object?` | — |
| `HasDefault` | `bool` | — |
| `DecimalSeparator` | `char` | — |
| `DateFormats` | `IReadOnlyList<string>?` | — |
| `Options` | `IReadOnlyList<SpreadsheetOption>?` | — |
| `UnknownPolicy` | `SpreadsheetUnknownValuePolicy` | — |
| `OnCreateOption` | `Func<string, SpreadsheetOption>?` | — |
| `Rules` | `IReadOnlyList<SpreadsheetRule>?` | — |
| `CustomParse` | `Func<string, object?>?` | — |
| `EffectiveLabel` | `string` | — |

### `class SpreadsheetSchema`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

Top-level declarative schema for `SpreadsheetImporter` and `KlySpreadsheetImport`.

| Member | Type | Description |
| :--- | :--- | :--- |
| `Key` | `string` | — |
| `Accept` | `IReadOnlyList<string>` | — |
| `MaxRows` | `int?` | — |
| `ForcedSheet` | `string?` | — |
| `ForcedHeaderRow` | `int?` | — |
| `Columns` | `IReadOnlyList<SpreadsheetColumnSchema>` | — |
| `RowKeyField` | `string?` | — |
| `Duplicates` | `SpreadsheetDuplicatesPolicy` | — |
| `ExistingRecords` | `IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>?` | — |
| `ExistingAction` | `SpreadsheetExistingAction` | — |
| `ValidateRow` | `Func<IReadOnlyDictionary<string, object?>, IReadOnlyList<SpreadsheetRowIssue>>?` | — |
| `BatchSize` | `int` | — |
| `OnSubmit` | `Func<IReadOnlyList<SpreadsheetRow>, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>?` | — |

### `record SpreadsheetRejectedRow`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

A server-side rejection reported for a specific row index during submit.

### `record SpreadsheetSheet`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

One parsed sheet in a `SpreadsheetWorkbook`.

### `record SpreadsheetWorkbook`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

A parsed workbook containing one or more sheets.

### `record SpreadsheetColumnSuggestion`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

A column suggestion offered for a missing required field when an unused header's sample values pass validation.

### `record SpreadsheetFieldState`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

State of a column field in `SpreadsheetImporter`.

### `record SpreadsheetValueAnswer`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

User or policy answer for an unrecognized select value.

### `record SpreadsheetValueQuestion`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

An open or answered question for an unknown select token in the imported file.

| Member | Type | Description |
| :--- | :--- | :--- |
| `IsOpen` | `bool` | — |

### `record SpreadsheetRowIssue`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

An issue attached to a specific field or row in `SpreadsheetRow`.

### `record SpreadsheetCellView`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

View model for one cell in a `SpreadsheetRow`.

### `record SpreadsheetRow`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

Parsed state of one data row in `SpreadsheetImporter`.

### `record SpreadsheetStepStatus`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

Status of a wizard step in `SpreadsheetImporter`.

### `class SpreadsheetImporter`
*Package*: `Klyroo.Spreadsheet.Core` · *Source*: `src/Klyroo.Spreadsheet.Core/SpreadsheetModels.cs`

Headless spreadsheet import engine: handles delimited file/clipboard parsing, automatic sheet &amp; header-row detection, deterministic normalized column matching, 80%-pass column suggestions, unknown value resolution, duplicate &amp; existing key checks, inline cell editing, review filtering, wizard steps, and batched submission.

| Member | Type | Description |
| :--- | :--- | :--- |
| `Schema` | `SpreadsheetSchema` | — |
| `FileLoaded` | `bool` | — |
| `FileName` | `string?` | — |
| `Sheets` | `IReadOnlyList<SpreadsheetSheet>` | — |
| `CurrentSheet` | `SpreadsheetSheet?` | — |
| `HeaderRowIndex` | `int` | — |
| `HeaderRowNumber` | `int` | — |
| `AmbiguousDetection` | `bool` | — |
| `Headers` | `IReadOnlyList<string>` | — |
| `UnusedHeaders` | `IReadOnlyList<string>` | — |
| `Fields` | `IReadOnlyList<SpreadsheetFieldState>` | — |
| `MissingFields` | `IReadOnlyList<SpreadsheetFieldState>` | — |
| `Questions` | `IReadOnlyList<SpreadsheetValueQuestion>` | — |
| `OpenQuestions` | `IReadOnlyList<SpreadsheetValueQuestion>` | — |
| `AllRows` | `IReadOnlyList<SpreadsheetRow>` | — |
| `ImportableRows` | `IReadOnlyList<SpreadsheetRow>` | — |
| `InvalidRows` | `IReadOnlyList<SpreadsheetRow>` | — |
| `DiscardedRows` | `IReadOnlyList<SpreadsheetRow>` | — |
| `CanSubmit` | `bool` | — |
| `ReviewTab` | `SpreadsheetReviewTab` | — |
| `ReviewLevel` | `SpreadsheetReviewLevel` | — |
| `ReviewIssueFilter` | `string` | — |
| `ReviewSearch` | `string` | — |
| `InspectedRowIndex` | `int?` | — |
| `SelectedRowIndexes` | `IReadOnlySet<int>` | — |
| `InspectedRow` | `SpreadsheetRow?` | — |
| `SubmitStatus` | `SpreadsheetSubmitStatus` | — |
| `SubmitDone` | `int` | — |
| `SubmitTotal` | `int` | — |
| `ImportedCount` | `int` | — |
| `RejectedCount` | `int` | — |
| `SubmitError` | `string?` | — |
| `CurrentStepKey` | `string` | — |
| `LoadText(string text, string fileName = "import.csv")` | `void` | Loads a CSV/TSV text file into the importer and auto-detects the sheet and header row. |
| `Paste(string text, string fileName = "clipboard.tsv")` | `void` | Pastes clipboard TSV/CSV text into the importer. |
| `LoadWorkbook(SpreadsheetWorkbook workbook)` | `void` | Loads a pre-parsed `SpreadsheetWorkbook` into the importer. |
| `ClearFile()` | `void` | Clears the loaded file and resets the wizard to the `file` step. |
| `SetSheet(string sheetName)` | `void` | — |
| `SetHeaderRow(int rowIndex)` | `void` | — |
| `AssignColumn(string fieldKey, int headerIndex)` | `void` | — |

### `class KlyrooSpreadsheetServiceCollectionExtensions`
*Package*: `Klyroo.Spreadsheet` · *Source*: `src/Klyroo.Spreadsheet/KlyrooSpreadsheetServiceCollectionExtensions.cs`

| Member | Type | Description |
| :--- | :--- | :--- |
| `AddKlyrooSpreadsheet(this IServiceCollection services)` | `IServiceCollection` | — |
