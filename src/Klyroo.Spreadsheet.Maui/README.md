# Klyroo.Spreadsheet.Maui

The native .NET MAUI import wizard (no WebView, no Blazor) for [Klyroo.Spreadsheet](https://github.com/klyroo/klyroo-spreadsheet): the same five steps as `KlySpreadsheetImport`, drawn with the design tokens of `Klyroo.UI.Maui` (light and dark follow the app theme) over the engine of `Klyroo.Spreadsheet.Core`. The wizard holds no import logic of its own: it draws the state of a `SpreadsheetImporter` and calls its methods.

The namespace is `Klyroo.Spreadsheet.Maui`, with the same type name as the Blazor package (`KlySpreadsheetImport`).

## Control

| Control | Built on | Notes |
| :--- | :--- | :--- |
| `KlySpreadsheetImport` | `Border` | `Importer`, `Title`, `MaxFileSize`, `PageSize`, `MaxRows`, `Wizard`; events `DownloadRequested` and `MappingProfileChanged`; `PickFileAsync` to plug in another file picker. |

The steps:

1. **File**: a button and a zone that open the file picker of the device (`FilePicker`, limited to the extensions of the schema: CSV, TSV, text, `.xlsx`, `.xls`), a paste box, the template to download, the expected columns; then the loaded file with its sheet list and its header row list, the row limit note, the saved mapping note and the rules that were stopped.
2. **Columns**: the column mapping, a list per field of the schema, with the suggestions and the samples of the engine, and Undo.
3. **Values**: the unknown values of choice columns: map to an option, create it, leave it empty, or leave the rows out.
4. **Review**: the validation summary (rows to import, with errors, left out), the tabs, the severity, issue and text filters, a preview grid with inline cell edits, selection (leave out, restore), pages, the invalid rows download, the row inspector, and the status of the checks that call your server.
5. **Import**: the summary, the progress, the result, retry and resume, the rows the server refused.

## Use

```csharp
var importer = new SpreadsheetImporter<PriceRow>(schema);
var wizard = new KlySpreadsheetImport { Importer = importer, Title = "Import prices" };
```

The app needs to register nothing. The file picker needs the platform setup of `FilePicker` (nothing on Android, the usual permissions for the document picker on iOS and Mac Catalyst). The template and the invalid rows are handed to `DownloadRequested`; when nothing listens, the wizard writes them to the cache of the app and opens the share sheet of the device.

## Differences from the Blazor wizard

- The file comes from the picker of the device. There is no drop zone and the zone is a tap target; `PickFileAsync` replaces the picker (for example by a drag and drop target on desktop).
- `OnDownload` is the `DownloadRequested` event; its default is the share sheet, not a browser download.
- `OnMappingProfileChanged` is the `MappingProfileChanged` event.
- The texts come from `SpreadsheetTexts.Current` (English by default; replace it at startup to translate). The Blazor locale sets are not ported. There is no `DropFileHere` text, and `ChooseFile` is new.
- The live region is `SemanticScreenReader.Announce`: the review counts and the progress and result of the import are announced when they change, the progress at most every 3 seconds.
- The review grid scrolls sideways and the inspector opens under it, instead of beside it. There is no keyboard handling of the cell editor (Enter and Tab on the web): it commits on the keyboard action and when it loses the focus, and Escape is not ported.
- The wizard draws a step again when the importer changes; a cell being edited commits when another change redraws the grid.

## Build

Not part of `Klyroo.Spreadsheet.slnx`: it needs the MAUI workload and the sibling checkout `klyroo-ui` (`..\..\..\klyroo-ui`). It targets `net10.0` (the unit tests run on it) and `net10.0-android`, plus iOS and Mac Catalyst outside Linux and Windows on Windows.

```bash
dotnet build src/Klyroo.Spreadsheet.Maui -f net10.0-android
dotnet test --project tests/Klyroo.Spreadsheet.Maui.Tests
```
