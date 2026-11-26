# FAQ

Troubleshooting first, then known limitations, then common questions. Everything here follows from the code of this version.

## Troubleshooting

**An `.xls` file is refused**

Only `.xlsx` workbooks are read; the legacy binary `.xls` format is not. Ask users to save the file as `.xlsx` or `.csv`. See [Read an Excel file](guides.md#read-an-excel-file).

**A required column is reported as missing but the file has it**

The importer matches headers exactly after normalization, so look at what differs. Normalization ignores case, accents, repeated spaces, a trailing `*` and a trailing `(required)`, and nothing else: `Unit price (EUR)` does not match `Unit price`. Check these in order:

1. Add the header spelling to the column's `Headers`.
2. Check `importer.HeaderRowNumber`. The header is searched in the first 20 rows of the sheet, and when no row matches any column the first row is used. For a file with a longer banner, set `ForcedHeaderRow` to the row number of the header.
3. Check `importer.Headers` and `importer.Fields`. A file column serves one schema column only, and an earlier `AssignColumn` or `IgnoreColumn` call takes precedence over matching until you call `ResetColumn`.
4. Call `AssignColumn(fieldKey, headerIndex)` yourself.

**No column suggestion appears for a missing column**

A suggestion needs all of these: the column is required with no default and has no match, it is not a plain text column (set a `Kind`, a `CustomParse` or a rule), the candidate file column is not already used, it has at least two non-empty cells, and at least 80% of its first 30 non-empty cells parse and pass the error-level rules. A file with a single data row never gets a suggestion.

**Numbers are rejected**

A `Number` column uses one convention. With the default `DecimalSeparator = '.'`, a comma is accepted only as a thousands separator between groups of three digits, so `1,234.50` reads as `1234.5` while `12,50` and `0,125` raise a `number` issue. A value such as `1,500` is still read as `1500`: three digits after the comma look exactly like a thousands group. With `DecimalSeparator = ','` the roles swap: `1.234,50` is accepted and `1,234.50` is rejected. Set the separator that matches your users' files, or use `CustomParse` to normalize the text yourself. Spaces, non-breaking spaces and apostrophes are always removed, so `1 234,50` and `1'234,50` work with `DecimalSeparator = ','`. Percent signs and currency symbols are not accepted.

**Dates are rejected**

A `Date` column accepts exact formats only: `yyyy-MM-dd`, `yyyy-MM-dd HH:mm`, `yyyy-MM-dd HH:mm:ss`, `dd/MM/yyyy` and `dd/MM/yyyy HH:mm`, or the ones you list in `DateFormats` (which replace the defaults). `02/13/2026` and Excel serial numbers fail. The parsed value is an ISO string, not a `DateTime`.

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

- **No `.xls` and no formula evaluation.** `.xlsx` cells give the value Excel saved.
- **English only.** Labels in the component and the messages of issues created by the importer are English. Rule messages and labels are the text you give.
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

**What do I get in `row.Data`?**
A `string` for text and dates (ISO 8601), a `double` for numbers, a `bool` for booleans, and the `SpreadsheetOption.Value` for selects. Empty cells are `null`. Map the dictionary to your own type in `OnSubmit`.

**How are CSV separators and quotes handled?**
The separator is the most frequent of tab, semicolon and comma in the first row outside quotes (comma when none appears, and on a tie tab wins over semicolon, and semicolon over comma). Quoted fields can hold separators, doubled quotes and line breaks. A leading byte order mark is dropped, blank rows are skipped, and cells are trimmed.

**Can a column be computed from another one?**
Yes: set `From` to the key of an earlier column. The new column reuses that column's text and applies its own kind, rules and select options, which lets you read one file column as two fields of different kinds. A column with `From` never matches a header.

**Does it work offline and in the browser?**
There are no network calls in the library. In WebAssembly, the file text stays in the browser until your `OnSubmit` sends it.
