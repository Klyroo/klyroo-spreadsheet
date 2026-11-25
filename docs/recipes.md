# Recipes & Testing — Klyroo.Spreadsheet

## Recipe 1: Resolving Unknown Values & Retrying Rejected Batches

```csharp
importer.AnswerValue("level", "C1-Advanced", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Map, "B2", "B2"));
importer.EditCell(rowIndex: 1, fieldKey: "email", rawText: "fixed@example.com");
await importer.SubmitAsync();
if (importer.RejectedRows.Count > 0) await importer.RetryRejectedAsync();
```

## Recipe 2: Headless Unit Testing (`xUnit`)

Test all business logic in `Klyroo.Spreadsheet.Core` without spinning up a browser or Blazor renderer:

```csharp
using Xunit;

public sealed class SpreadsheetUnitTests
{
    [Fact]
    public void Headless_core_runs_without_ui_host()
    {
        // Arrange & Act using Klyroo.Spreadsheet.Core directly
        // Assert deterministic outputs with zero JSInterop setup
    }
}
```

## Recipe 3: Component Testing (`bUnit`)

```csharp
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class SpreadsheetComponentTests : BunitContext
{
    public SpreadsheetComponentTests()
    {
        Services.AddKlyrooSpreadsheet();
    }
}
```

> **Tip**: Always register Klyroo services on `Services` **before** calling `SetRendererInfo(...)` in bUnit tests.
