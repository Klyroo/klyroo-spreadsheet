# FAQ

Troubleshooting first, then known limitations, then common questions. Everything here follows from the code of this version.

## Troubleshooting

**An `.xls` file is refused**

Legacy `.xls` workbooks from Excel 97 to 2003 are read, and a tab-separated or CSV export given the `.xls` extension is read as text, so a refusal has one of four causes, and the `InvalidDataException` message says which. The file is password-protected, which is not supported; the file is in the Excel 5.0/95 format (BIFF5), which is not supported either; the file is damaged; or it is an HTML or XML export (a web page table, the Excel 2003 XML format) given the `.xls` extension. In each case ask users to save the file as `.xlsx` or `.csv`. See [Read an Excel file](guides.md#read-an-excel-file).

**A required column is reported as missing but the file has it**

The importer matches headers exactly after normalization, so look at what differs. Normalization ignores case, accents, repeated spaces, a trailing `*` and a trailing `(required)`, and nothing else: `Unit price (EUR)` does not match `Unit price`. Check these in order:

1. Add the header spelling to the column's `Headers`.
2. Check `importer.HeaderRowNumber`. The header is searched in the first 20 rows of the sheet, and when no row matches any column the first row is used. For a file with a longer banner, set `ForcedHeaderRow` to the row number of the header.
3. Check `importer.Headers` and `importer.Fields`. A file column serves one schema column only, and an earlier `AssignColumn` or `IgnoreColumn` call takes precedence over matching until you call `ResetColumn`.
4. Call `AssignColumn(fieldKey, headerIndex)` yourself.

**No column suggestion appears for a missing column**

A suggestion needs all of these: the column is required with no default and has no match, it is not a plain text column (a number, date, yes or no or select property, a `Parser` or a rule), the candidate file column is not already used, it has at least two non-empty cells, and at least 80% of its first 30 non-empty cells parse and pass the error-level rules. A file with a single data row never gets a suggestion.

**Numbers are rejected**

A `Number` column uses one convention. With the default `DecimalSeparator('.')`, a comma is accepted only as a thousands separator between groups of three digits, so `1,234.50` reads as `1234.5` while `12,50` and `0,125` raise a `number` issue. A value such as `1,500` is still read as `1500`: three digits after the comma look exactly like a thousands group. With `DecimalSeparator(',')` the roles swap: `1.234,50` is accepted and `1,234.50` is rejected. Set the separator that matches your users' files, or use `Parser` to normalize the text yourself. A column for a whole number type (`int`, `long`, ...) also refuses a value with decimals (`1.5`) or one too large for the type, with a message that says so. The text is read exactly into the property type, never through a `double`: a `decimal` keeps every digit (`1234567890.123456`), a `long` or `ulong` keeps values above 2^53, and `decimal.MaxValue` or `ulong.MaxValue` load. A value outside the range of the type (`ulong` 18446744073709551616, `byte` 256, a `double` past `1e308`) raises a `number` issue that says it is out of range, and a `decimal` with more decimals than it holds (29 digits in all) is refused instead of rounded. A `double` or `float` column rounds to the nearest value of its type, as any double does. The review table shows the value the host receives, in invariant form (`12.50` for a `decimal` read from `12,50`), and a typed `Default` is shown the same way (a `DateOnly` as `2026-03-04`, a select value as its label), never in the culture of the server. Spaces, non-breaking spaces and apostrophes are always removed, so `1 234,50` and `1'234,50` work with `DecimalSeparator(',')`. Percent signs and currency symbols are not accepted.

**Dates are rejected**

A date column (a `DateOnly`, `DateTime` or `DateTimeOffset` property) accepts exact formats only: `yyyy-MM-dd`, `yyyy-MM-dd HH:mm`, `yyyy-MM-dd HH:mm:ss`, `dd/MM/yyyy` and `dd/MM/yyyy HH:mm`, or the ones you list with `DateFormats` (which replace the defaults). `02/13/2026` and Excel serial numbers fail. A `DateTimeOffset` column reads a cell without an offset as UTC, and keeps an offset the cell writes when a format of `DateFormats` has `zzz` or `K` (`DateFormats("yyyy-MM-dd HH:mm zzz")` reads `2026-03-05 10:00 +02:00` as that instant with the offset `+02:00`), whatever the time zone of the server. A `DateOnly` or `DateTime` column keeps the date and time as written and ignores an offset, so it does not move with the server's time zone either. The property gets a real date: a `DateOnly` keeps the date part, a `DateTime` has an unspecified kind, and a `DateTimeOffset` reads the cell as UTC, since a cell carries no offset.

**Accented characters come out garbled**

The importer receives a `string`, so the problem is how you decoded the bytes. `StreamReader` assumes UTF-8 unless the file starts with a byte order mark, while many Windows exports are Windows-1252. Pass the right `Encoding` when you read the stream.

**`RetryRejectedAsync` does nothing after I edited the rejected row**

Editing a cell clears that row's rejection, so the row leaves the retry set. See [Submit in batches and retry rejected rows](guides.md#submit-in-batches-and-retry-rejected-rows).

**Two users see each other's file**

A `SpreadsheetImporter` holds the state of one import. If you registered it as a singleton or kept it in a static field, everyone shares it. Create one per user and per import, in a page field or a scoped service, and do not use it from several threads.

**`InvalidOperationException`: cannot provide a value for a property of type `ThemeResolver` or `KlyrooOptions`**

`AddKlyrooSpreadsheet()` is not called, or not called in the project that runs the component. Call it in `Program.cs`, in both the server and the client project for WebAssembly and Auto.

**The component renders unstyled**

Link `_content/Klyroo.Spreadsheet/klyroo-spreadsheet.css` after the two Klyroo.UI stylesheets, see the [README](../README.md#installation). The Tailwind build of Klyroo.UI (`KlyrooTailwind`) compiles the classes found in your own project files, so it does not include the classes of this component: keep the precompiled stylesheet.

**Buttons do nothing**

The page is rendered with Static SSR. The component needs an interactive render mode, see [Platforms](platforms.md#blazor-render-modes). With `Wizard="false"` the component has no buttons for moving between steps: call `NextStep()` yourself.

## Known limitations

- **Workbook size.** Besides `maxBytes`, a workbook is refused when it holds more than 2,000,000 cells, counting the empty cells left of each value (a value in column Z costs 26 cells on its row), or more than 32 million characters of text. The limits keep a small hostile file from holding gigabytes of memory, which matters most when the import runs on the server (Interactive Server). Save a larger sheet as `.csv`, which is read without them.
- **No formula evaluation, no encrypted or Excel 5.0/95 `.xls`.** `.xlsx` and `.xls` cells give the value Excel saved. Password-protected workbooks and `.xls` files in the BIFF5 format (Excel 5.0 and 95) are refused.
- **Translations.** The component's texts and the messages of the issues the importer raises follow the locale of Klyroo.UI in 12 languages, English and French reviewed and the others drafts, see [Translate the wizard](guides.md#translate-the-wizard-and-its-messages). The text you give (`Title`, column `Label` and `Description`, your own rule messages) is shown as you wrote it, and the headless `SpreadsheetImporter` writes English messages.
- **No rollback.** If `OnSubmit` throws midway, batches already accepted stay accepted and the importer does not say which.
- **The importer holds the whole file.** Every row is parsed again after each change, and the test suite covers 5,000 rows. Measure with your own schema before accepting much larger files, and set `MaxRows` to cap what users can upload.
- **Row numbers count records, not lines.** `RowNumber` is the position of the record in the file, blank records included. A quoted field with a line break counts as one record.

## Questions

**Can I use the engine without Blazor?**
Yes. `Klyroo.Spreadsheet.Core` has no UI and no package dependencies, and the [getting started](getting-started.md) tutorial begins with a console program.

**Can I build my own UI instead of `KlySpreadsheetImport`?**
Yes. Everything the component displays comes from the importer's properties (`Fields`, `OpenQuestions`, `VisibleReviewRows`, `GetSteps()`) and everything it does is a method call on the importer. Subscribe to `Changed` to know when to redraw.

**Does the importer change the uploaded file?**
No. Edits, discards and mapping choices live in the importer. The result is what `OnSubmit` receives.

**What do I get in `OnSubmit`?**
A `SpreadsheetRow<TRow>` per row: its `Value` is an instance of your row class with each column property set (a `string`, a number, a `bool`, a date, an enum member), and the cells as text are in `Cells`. An empty cell leaves the property at the default of its type (`null` for a nullable one), which is why a property that cannot hold `null` makes its column required. The `null` is stored, so it replaces an initializer such as `public int? Seats { get; set; } = 7;` (and `Default(null)` does the same), while a property that cannot hold `null` keeps its initializer. Your row class runs on every row as the file is read, so a factory, a setter or a `ValidateRow` that throws does not fail the load: the row gets an error issue with the code `row.failed` and the message of the exception, and is not importable. `ToTyped` of such a row (an invalid row you ask for yourself) throws an `InvalidOperationException` that names the row, with the exception as `InnerException`, and `ImportableTypedRows` and the submit batches never contain one.

**How are CSV separators and quotes handled?**
The separator is the most frequent of tab, semicolon and comma in the first row outside quotes (comma when none appears, and on a tie tab wins over semicolon, and semicolon over comma). Quoted fields can hold separators, doubled quotes and line breaks. A leading byte order mark is dropped, blank rows are skipped, and cells are trimmed.

**Can a column be computed from another one?**
Yes: set `From` to the key of an earlier column. The new column reuses that column's text and applies its own kind, rules and select options, which lets you read one file column as two fields of different kinds. A column with `From` never matches a header.

**Does it work offline and in the browser?**
There are no network calls in the library. In WebAssembly, the file text stays in the browser until your `OnSubmit` sends it.
