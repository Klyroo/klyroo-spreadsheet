# Platforms

What runs where. Everything below comes from the project files (`IsTrimmable`, `IsAotCompatible`, `SupportedPlatform`) and from reading the code: the repository has no tests on devices or in a browser, and no NativeAOT publish in its CI, so those are called out where it matters.

| | `Klyroo.Spreadsheet.Core` | `Klyroo.Spreadsheet` |
| :--- | :--- | :--- |
| Target framework | `net10.0` | `net10.0` (Razor class library) |
| Package dependencies | none | `Klyroo.Spreadsheet.Core`, `Klyroo.UI` |
| `IsTrimmable` | yes | yes |
| `IsAotCompatible` | yes | not set |
| `SupportedPlatform` | not declared | `browser`, `ios`, `android`, `maccatalyst`, `windows` |

## Blazor render modes

The engine works in every mode, since it is plain C#. The component `KlySpreadsheetImport` reacts to clicks and input through Blazor events, so what matters is whether the page is interactive. Its only JavaScript is a download helper (`_content/Klyroo.Spreadsheet/klyroo-spreadsheet.js`) that it imports the first time the user downloads the template or the invalid rows.

| Render mode | Component | Notes |
| :--- | :--- | :--- |
| Static SSR | Renders, does not respond | There is no interactivity, so buttons do nothing. You can still use `SpreadsheetImporter` in the request handler or a minimal API and render the result yourself. |
| Interactive Server | Works | The importer lives in the circuit. Read uploads with `InputFile` and `OpenReadStream(maxAllowedSize)`. |
| Interactive WebAssembly | Works | The importer and the file text stay in the browser. Register the services in the client project. |
| Interactive Auto | Works | Call `AddKlyrooSpreadsheet()` in the server and in the client project, because the component can run on either. State held in a page field does not move when the runtime changes. |

Register the services with `builder.Services.AddKlyrooSpreadsheet()`. It calls `AddKlyrooUi()`, which the base class of the component needs, so `Klyroo.UI` configuration (theme, locale) is set through `AddKlyrooUi` options as the [Klyroo.UI](https://github.com/klyroo/klyroo-ui) documentation describes.

Reading a file is your job. `OpenReadStream` allows 500 KB by default and throws for a larger file, so pass a `maxAllowedSize` that fits your data, as the samples do with `5 * 1024 * 1024`. The importer keeps the whole file text and every parsed row in memory (the component caps an import at 10,000 rows by default, see `MaxRows`), so on Interactive Server size the limit with the number of concurrent circuits in mind.

## .NET MAUI Blazor Hybrid

`Klyroo.Spreadsheet` declares the platforms `browser`, `ios`, `android`, `maccatalyst` and `windows`, which are the targets of a MAUI Blazor Hybrid app plus the browser. Use it as in a Blazor app: register the services in `MauiProgram.CreateMauiApp`, next to `AddMauiBlazorWebView()`:

```csharp
using Klyroo.Spreadsheet;

builder.Services.AddKlyrooSpreadsheet();
```

Then add the three stylesheet links of the [README](../README.md#installation) to `wwwroot/index.html` and use the component in a Razor page of the app. The file step uses Blazor's `InputFile`, which opens the platform file picker in a hybrid app; you can also read a file with the MAUI `FilePicker` and pass its stream to `LoadFileAsync`. The browser download of the template and the invalid rows (a Blob and a link click in JavaScript) is not something to rely on in a web view: set `OnDownload` and save the file with the platform, for example by writing it to the app's cache folder and handing it to `Share.Default.RequestAsync`. This repository has not been run on a device, so test the file-picking flow on each platform you ship.

### Native C# MAUI without a web view

If your MAUI app uses XAML pages and no `BlazorWebView`, install `Klyroo.Spreadsheet.Core` alone if you want to bind your own view models to `SpreadsheetImporter<TRow>`: its `Changed` event and read-only properties are enough to drive a custom UI.

The repository also includes [`Klyroo.Spreadsheet.Maui`](../src/Klyroo.Spreadsheet.Maui/README.md) (`src/Klyroo.Spreadsheet.Maui`): the native .NET MAUI five-step `KlySpreadsheetImport` wizard over `Klyroo.Spreadsheet.Core`, drawn with the design tokens of `Klyroo.UI.Maui` and no WebView. It requires the MAUI workload and a sibling checkout of `klyroo-ui`, and is built and unit-tested by `.github/workflows/maui.yml`.

## Trimming and NativeAOT

`Klyroo.Spreadsheet.Core` sets `IsTrimmable` and `IsAotCompatible`. That turns on the trimming and AOT analyzers for the project, and the build treats warnings as errors, so the project builds without trim or AOT warnings. The schema is typed by your row class and reads its properties from member expressions: the expression is read (never compiled), each property setter becomes a delegate with `MethodInfo.CreateDelegate`, and the row class is created with `new()` or the factory you pass. The row type argument carries `DynamicallyAccessedMembers(PublicProperties)`, so the trimmer keeps the property setters of the row classes you use, and the analyzers accept it without a suppression. Two things to keep in mind: `SpreadsheetRule.Email` and `SpreadsheetRule.Pattern` use `System.Text.RegularExpressions`, and a Regex built at run time with `RegexOptions.Compiled` falls back to the interpreter on NativeAOT, where it still works. This repository does not run `dotnet publish -p:PublishAot=true` on a sample, so the AOT claim rests on the analyzers, not on a published binary.

`Klyroo.Spreadsheet` sets `IsTrimmable` only, and it depends on `Klyroo.UI`, so trimming of a Blazor app that uses it follows the rules of that package. Do not assume Blazor WebAssembly AOT or NativeAOT for the component without testing your app.

## Not supported

- Reading password-protected workbooks, and `.xls` files in the Excel 5.0/95 format (BIFF5). `.xlsx` and `.xls` from Excel 97 to 2003 are supported, see [Read an Excel file](guides.md#read-an-excel-file).
- Translated text from the headless engine. The component shows its texts and the library's issue messages in the locale of Klyroo.UI, but `SpreadsheetImporter` itself writes English messages (`SpreadsheetRowIssue.Message`), and so do the `InvalidDataException`s of the file readers. Resolve a message in your own language with `SpreadsheetTexts.ResolveIssue`, see [Translate the wizard](guides.md#translate-the-wizard-and-its-messages).
