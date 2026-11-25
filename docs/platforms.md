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

The engine works in every mode, since it is plain C#. The component `KlySpreadsheetImport` reacts to clicks and input through Blazor events and makes no JavaScript calls of its own, so what matters is whether the page is interactive.

| Render mode | Component | Notes |
| :--- | :--- | :--- |
| Static SSR | Renders, does not respond | There is no interactivity, so buttons do nothing. You can still use `SpreadsheetImporter` in the request handler or a minimal API and render the result yourself. |
| Interactive Server | Works | The importer lives in the circuit. Read uploads with `InputFile` and `OpenReadStream(maxAllowedSize)`. |
| Interactive WebAssembly | Works | The importer and the file text stay in the browser. Register the services in the client project. |
| Interactive Auto | Works | Call `AddKlyrooSpreadsheet()` in the server and in the client project, because the component can run on either. State held in a page field does not move when the runtime changes. |

Register the services with `builder.Services.AddKlyrooSpreadsheet()`. It calls `AddKlyrooUi()`, which the base class of the component needs, so `Klyroo.UI` configuration (theme, locale) is set through `AddKlyrooUi` options as the [Klyroo.UI](https://github.com/klyroo/klyroo-ui) documentation describes.

Reading a file is your job. `OpenReadStream` allows 500 KB by default and throws for a larger file, so pass a `maxAllowedSize` that fits your data, as the samples do with `5 * 1024 * 1024`. The importer keeps the whole file text and every parsed row in memory, and it re-parses all rows after each change, so on Interactive Server size the limit with the number of concurrent circuits in mind.

## .NET MAUI Blazor Hybrid

`Klyroo.Spreadsheet` declares the platforms `browser`, `ios`, `android`, `maccatalyst` and `windows`, which are the targets of a MAUI Blazor Hybrid app plus the browser. Use it as in a Blazor app: register the services in `MauiProgram.CreateMauiApp`, next to `AddMauiBlazorWebView()`:

```csharp
using Klyroo.Spreadsheet;

builder.Services.AddKlyrooSpreadsheet();
```

Then add the three stylesheet links of the [README](../README.md#installation) to `wwwroot/index.html` and use the component in a Razor page of the app. To load a file in a hybrid app, read it with the platform file picker and pass its text to `LoadText`. This repository has not been run on a device, so test the file-picking flow on each platform you ship.

If your MAUI app uses XAML pages and no `BlazorWebView`, install `Klyroo.Spreadsheet.Core` only and bind your view models to a `SpreadsheetImporter`: its `Changed` event and its read-only properties are enough to drive a custom UI.

## Trimming and NativeAOT

`Klyroo.Spreadsheet.Core` sets `IsTrimmable` and `IsAotCompatible`. That turns on the trimming and AOT analyzers for the project, and the build treats warnings as errors, so the project builds without trim or AOT warnings. The engine uses no reflection over your types: schemas and rows are plain objects and dictionaries. Two things to keep in mind: `SpreadsheetRule.Email` and `SpreadsheetRule.Pattern` use `System.Text.RegularExpressions`, and a Regex built at run time with `RegexOptions.Compiled` falls back to the interpreter on NativeAOT, where it still works. This repository does not run `dotnet publish -p:PublishAot=true` on a sample, so the AOT claim rests on the analyzers, not on a published binary.

`Klyroo.Spreadsheet` sets `IsTrimmable` only, and it depends on `Klyroo.UI`, so trimming of a Blazor app that uses it follows the rules of that package. Do not assume Blazor WebAssembly AOT or NativeAOT for the component without testing your app.

## Not supported

- Reading `.xlsx` or `.xls`. See [Read an Excel file](guides.md#read-an-excel-file).
- A built-in file picker or drag and drop target: the component draws a drop zone label, but it does not handle drops or file input.
- Translated text. The component's labels and messages are English strings in the component, and the issue messages created by the importer are English too. You can supply your own text for rule messages (`SpreadsheetRule` takes a `Message`) and for `Label`.
