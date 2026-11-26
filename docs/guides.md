# Guides

Each guide solves one task and assumes you read [Getting started](getting-started.md). Every sample compiles against this version. Samples use `using Klyroo.Spreadsheet;` and implicit usings.

- [Parse typed columns and validate them](#parse-typed-columns-and-validate-them)
- [Cope with banners, renamed headers and wrong sheets](#cope-with-banners-renamed-headers-and-wrong-sheets)
- [Resolve unknown select values](#resolve-unknown-select-values)
- [Handle duplicates and existing records](#handle-duplicates-and-existing-records)
- [Validate across fields](#validate-across-fields)
- [Submit in batches and retry rejected rows](#submit-in-batches-and-retry-rejected-rows)
- [Check rows and records against your server](#check-rows-and-records-against-your-server)
- [Remember a column mapping](#remember-a-column-mapping)
- [Translate the wizard and its messages](#translate-the-wizard-and-its-messages)
- [Read an Excel file](#read-an-excel-file)
- [Let users paste rows in Blazor](#let-users-paste-rows-in-blazor)
- [Import large files](#import-large-files)
- [Export a template and the invalid rows](#export-a-template-and-the-invalid-rows)
- [Test an import](#test-an-import)

## Parse typed columns and validate them

Goal: turn cells into typed values and report the cells that do not fit.

```csharp
public sealed class Product
{
    public string Sku { get; set; } = "";
    public bool Available { get; set; }
    public DateOnly? Launch { get; set; }
    public double? WeightKg { get; set; }
}

var schema = SpreadsheetSchema.For<Product>(s =>
{
    s.MaxRows = 10_000;
    s.Column(r => r.Sku, c => c
        .Label("SKU")
        .Required()
        .Rule(SpreadsheetRule.Pattern("^[A-Z]{2}-[0-9]{3}$", "Use the form AB-123"))
        .Rule("not-reserved", "ZZ- codes are reserved", sku => !sku.StartsWith("ZZ-", StringComparison.Ordinal)));
    s.Column(r => r.Available, c => c.Default(true));
    s.Column(r => r.Launch, c => c.DateFormats("dd/MM/yyyy"));
    s.Column(r => r.WeightKg, c => c
        .Label("Weight (kg)")
        .Rule(SpreadsheetRule.Max(30d, "Too heavy to ship", SpreadsheetIssueLevel.Warning)));
});

var importer = new SpreadsheetImporter<Product>(schema);
importer.LoadText("SKU,Available,Launch,Weight (kg)\nAB-123,oui,01/03/2026,12\nZZ-001,,15/03/2026,45\nab-12,no,2026-03-20,3", "catalog.csv");

Console.WriteLine(importer.ToTyped(importer.AllRows[0]).Value.Launch?.ToString("O")); // 2026-03-01
Console.WriteLine(importer.ToTyped(importer.AllRows[1]).Value.Available);             // True
```

The kind of each column comes from the property type, and the value of each cell is read into that type. `Sku` is a `string`, so the cell text is the value. `Available` is a `bool`, `Launch` a date and `WeightKg` a number. A nullable property (`DateOnly?`, `double?`) stores `null` for an empty cell and makes the column optional, while a property that cannot hold `null` (`bool`, `int`, an enum) makes its column required unless you give it a `Default`, because an empty cell cannot be stored in it. `ToTyped(row)` gives the `SpreadsheetRow<Product>` of a row, whose `Value` is the filled `Product`; `row.Cells` stays the text of each cell.

What happens for each row of that file:

- Row 2 is valid. `oui` is a true value, and the date is stored as the `DateOnly` 2026-03-01.
- Row 3 is blocked by the `not-reserved` rule. Its empty `Available` cell takes the default, and its weight raises a warning (code `max`) that does not block the row.
- Row 4 is blocked twice: `ab-12` fails the pattern, and `2026-03-20` is not in the only format this column accepts, so you get a `date` error.

`Pattern` matches with a timeout (100 ms by default, the `matchTimeout` argument changes it), so a pasted value built to trigger catastrophic backtracking cannot freeze the import: that cell gets a `pattern` issue saying the format could not be checked in time.

A timeout costs the full timeout, so a hostile file of 50,000 cells could still cost 50,000 times 100 ms. A circuit breaker prevents that: after `SpreadsheetSchema.RuleTimeoutLimit` timeouts (3 by default, zero or less turns it off) the importer stops evaluating that rule for the rest of the read and lists it once in `importer.DisabledRules` (column key, rule code, timeouts). The component shows one notice naming the column and the rule. The values after the rule stopped are not waved through: each gets the same "could not be checked in time" issue, at the severity of the rule (blocking for the default), without running the regex, so a hostile file cannot make its later values importable. Reading the file again (a new file, sheet, header row or column choice) starts the count again.

Three details are easy to miss. A rule only runs on a non-empty value that parsed, and it is typed like the column: `Pattern` and `Email` are `SpreadsheetRule<string?>` rules for text columns, and `Min`, `Max` and `Between` take the number type of the column (`Max(30d)` for a `double`, `Max(30m)` for a `decimal`, `Min(0)` for an `int`). A nullable column takes the rule of its underlying type, so `Min(0)` fits an `int?`. And `Default` sets the value used for an empty cell, which for a nullable column may be `null` on purpose (`Default(null)`).

The built-in boolean values are `yes`, `y`, `oui`, `o`, `true`, `vrai`, `x`, `1` and `no`, `n`, `non`, `false`, `faux`, `0`. For anything else, give the column a `Parser`: it receives the cell text and returns `SpreadsheetParseResult<T>.Ok(value)` or `SpreadsheetParseResult<T>.Fail("message")`, and an exception becomes the generic `row.failed` error on the cell (the exception goes to the logger, never to the uploader). A parser also lets a column have a type the library does not read by itself, such as a `Guid`:

```csharp
s.Column(r => r.Token, c => c.Parser(text =>
    Guid.TryParse(text, out var token)
        ? SpreadsheetParseResult<Guid?>.Ok(token)
        : SpreadsheetParseResult<Guid?>.Fail("Not a token")));
```

## Cope with banners, renamed headers and wrong sheets

Goal: accept exports whose header is not on the first row or is spelled differently.

By default the importer scans the first 20 rows of each sheet and picks the row that matches the most columns, so a title above the header is ignored. List alternative spellings in `Headers`:

```csharp
public sealed class Person
{
    public string? Email { get; set; }
    public int Age { get; set; }
}

var schema = SpreadsheetSchema.For<Person>(s =>
{
    s.Column(r => r.Email, c => c.Headers("E-mail", "Courriel").Required());
    s.Column(r => r.Age, c => c.Rule(SpreadsheetRule.Between(0, 120)));
});

var importer = new SpreadsheetImporter<Person>(schema);
importer.LoadText("Export 2026;;\nE-mail;Years\nada@example.com;31\nalan@example.com;45", "people.csv");

Console.WriteLine(importer.HeaderRowNumber);       // 2
var missing = importer.MissingFields[0];           // the "Age" column
Console.WriteLine(missing.Suggestion?.HeaderText); // Years

if (missing.Suggestion is { } suggestion)
{
    importer.AssignColumn(missing.Column.Key, suggestion.HeaderIndex);
}
Console.WriteLine(importer.CanSubmit);             // True
```

`Years` matches no name, so `Age` is missing. Because `Age` is a number column with a rule, the importer proposes the unused `Years` column: at least 80% of up to 30 sampled values parse and pass. The suggestion is never applied for you. Show it to the user, or apply it as above when you trust it.

When detection is wrong or you know the layout, force it. `ForcedHeaderRow` is the 1-based row number in the file:

```csharp
var schema = SpreadsheetSchema.For<Product>(s =>
{
    s.ForcedSheet = "Prices";
    s.ForcedHeaderRow = 4;
    s.Column(r => r.Sku, c => c.Required());
});
```

For workbooks with several sheets, `importer.AmbiguousDetection` is `true` when more than one sheet matched equally well and the first was taken. Let the user pick with `SetSheet(name)`, and use `SetHeaderRow(index)` to move the header.

## Resolve unknown select values

Goal: decide what to do when a file says `Gold` and your list only has `pro`.

A select column (an enum property, or any column with `Options`) compares each cell, ignoring case and accents, to the option value, the option label and its aliases. A cell with no match becomes a `SpreadsheetValueQuestion`, and by default the import cannot be submitted until every question is answered:

```csharp
public sealed class Account
{
    public string? Name { get; set; }
    public string? Tier { get; set; }
}

var createdTiers = new List<string>();
var schema = SpreadsheetSchema.For<Account>(s =>
{
    s.Column(r => r.Name, c => c.Required());
    s.Column(r => r.Tier, c => c
        .Options([new SpreadsheetOption<string?>("std", "Standard", ["basic"]), new SpreadsheetOption<string?>("pro", "Professional")])
        .OnCreateOption(raw =>
        {
            createdTiers.Add(raw);
            return new SpreadsheetOption<string?>(raw, raw);
        }));
});

var importer = new SpreadsheetImporter<Account>(schema);
importer.LoadText("Name,Tier\nAcme,Basic\nBeta,Gold\nGamma,Platinum\nDelta,Legacy", "accounts.csv");

foreach (var question in importer.OpenQuestions)
{
    Console.WriteLine($"{question.RawValue}: {question.RowIndexes.Count} row(s)");
}

importer.AnswerValue("Tier", "Gold", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Map, "pro", "Professional"));
importer.AnswerValue("Tier", "Platinum", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Create));
importer.AnswerValue("Tier", "Legacy", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.SkipRows));

Console.WriteLine(importer.CanSubmit);             // True
await importer.SubmitAsync();
Console.WriteLine(string.Join(",", createdTiers)); // Platinum
```

`Basic` matched the alias, so only three questions were asked. One answer applies to every row that has the value. The four actions are `Map` (use an existing option), `LeaveEmpty`, `SkipRows` (leave those rows out) and `Create` (keep the text as a new value). `OnCreateOption` runs once per created value when you submit, before any row is sent, so it is the place to insert the new option in your database. The rows then carry the option it returns (its `Value` in the row property, its label in the review), and a retry does not create it again.

An enum property needs no `Options`: the column offers one option per distinct value (the first declared name when members share a value, the others being aliases), the member name is the value and its name in words the label (`ProMonthly` is `Pro monthly`), and a cell matches either. Give `Options` to replace them with labels and aliases of your own (`new SpreadsheetOption<Plan>(Plan.Free, "Gratuit", ["0"])`). Options can hold a `string`, an enum, a number, a `bool` or a `Guid`. `OnCreateOption` returns an option of the property type, so it is meant for text columns: a new enum member cannot be created from a cell.

To skip the questions, call `UnknownPolicy` on the column. `LeaveEmpty`, `SkipRows` and `Create` answer for the user. `Error` does not ask and blocks each row with the unknown value:

```csharp
s.Column(r => r.Country, c => c
    .Options([new SpreadsheetOption<string?>("FR", "France"), new SpreadsheetOption<string?>("DE", "Germany")])
    .UnknownPolicy(SpreadsheetUnknownValuePolicy.LeaveEmpty));
```

## Handle duplicates and existing records

Goal: avoid importing the same key twice, and update records you already store.

Name the key column with `RowKey`, which takes the column `Column` returned. Two independent checks use it. `Duplicates` decides what happens when the file repeats a key: `Error` (the default) blocks every row of the key, `KeepFirst` and `KeepLast` leave the others out. `ExistingRecords` holds the records you already have, keyed by the text of the row key, and `ExistingAction` decides what to do with a row that matches one:

```csharp
public sealed class StockLine
{
    public string? Sku { get; set; }
    public int Stock { get; set; }
}

var stored = new Dictionary<string, StockLine>(StringComparer.OrdinalIgnoreCase)
{
    ["A-1"] = new StockLine { Sku = "A-1", Stock = 4 },
};

var schema = SpreadsheetSchema.For<StockLine>(s =>
{
    s.RowKey(s.Column(r => r.Sku, c => c.Label("SKU").Required()));
    s.Column(r => r.Stock);
    s.Duplicates = SpreadsheetDuplicatesPolicy.KeepLast;
    s.ExistingRecords = stored;
    s.ExistingAction = SpreadsheetExistingAction.Update;
});

var importer = new SpreadsheetImporter<StockLine>(schema);
importer.LoadText("SKU,Stock\nA-1,10\nB-2,5\nB-2,7", "stock.csv");

foreach (var row in importer.ImportableTypedRows)
{
    Console.WriteLine($"{row.Value.Sku} {row.Mode} {row.Value.Stock}");
}
// A-1 Update 10
// B-2 Create 7
```

Each importable row carries a `Mode` of `Create` or `Update`, so your `OnSubmit` can branch on it. The two checks compare keys differently: duplicates inside the file ignore case and surrounding spaces, but the lookup in `ExistingRecords` uses the dictionary's own comparer on the exact cell text. Build the dictionary with `StringComparer.OrdinalIgnoreCase` as above if your keys are case-insensitive. The records inside `ExistingRecords` are not read by the importer, only the keys. A key that is not text is written with the invariant culture, so `42` looks up `"42"`, a `decimal` has no trailing zeros (`12.50` looks up `"12.5"`), a `DateOnly` is `yyyy-MM-dd` (`"2026-03-04"`), a `DateTime` is `yyyy-MM-dd` at midnight and otherwise `yyyy-MM-ddTHH:mm` or `yyyy-MM-ddTHH:mm:ss` (`"2026-03-04T10:30"`), and a `DateTimeOffset` is the same text for its UTC instant. `LookupExistingRecordsAsync` receives the same texts.

## Validate across fields

Goal: reject a row because two cells disagree, which no single column rule can express.

`ValidateRow` runs after the column rules and receives the row as an instance of your row class. Return the issues to add:

```csharp
public sealed class Booking
{
    public DateOnly? Start { get; set; }
    public DateOnly? End { get; set; }
}

var schema = SpreadsheetSchema.For<Booking>(s =>
{
    s.Column(r => r.Start, c => c.Required());
    s.Column(r => r.End, c => c.Required());
    s.ValidateRow = row => row is { Start: { } start, End: { } end } && end < start
        ? [new SpreadsheetRowIssue("End", "range", SpreadsheetIssueLevel.Error, "End is before start")]
        : [];
});
```

Dates arrive as real dates, so you compare them as dates. A cell that is empty or could not be read leaves its property at the default of its type, and `ValidateRow` still runs on that row, so use nullable properties and test them as above: a `DateOnly` would be `0001-01-01` and make `end < start` fire on top of the error the cell already has. Set `Field` to a column key to show the issue on that cell, or `null` to attach it to the whole row.

## Submit in batches and retry rejected rows

Goal: save rows in chunks and send again the rows the server refused.

`SubmitAsync` calls `OnSubmit` with batches of `BatchSize` importable rows (all rows at once when it is zero). Each row is a `SpreadsheetRow<TRow>`: `Value` is your parsed row, and `Index` is what the callback returns the refused rows by:

```csharp
public sealed class Subscriber
{
    public string? Email { get; set; }
}

var lockedAccounts = new HashSet<string> { "bob@example.com" };
var schema = SpreadsheetSchema.For<Subscriber>(s =>
{
    s.BatchSize = 100;
    s.Column(r => r.Email, c => c.Required().Rule(SpreadsheetRule.Email()));
    s.OnSubmit = async rows =>
    {
        var rejected = new List<SpreadsheetRejectedRow>();
        foreach (var row in rows)
        {
            if (lockedAccounts.Contains(row.Value.Email!))
            {
                rejected.Add(new SpreadsheetRejectedRow(row.Index, "Account is locked", "Email"));
            }
        }
        await Task.Delay(1); // call your API here
        return rejected;
    };
});

var importer = new SpreadsheetImporter<Subscriber>(schema);
importer.LoadText("Email\nada@example.com\nbob@example.com", "users.csv");

await importer.SubmitAsync();
Console.WriteLine($"{importer.ImportedCount} imported, {importer.RejectedCount} rejected"); // 1 imported, 1 rejected
Console.WriteLine(importer.InvalidRows[0].Issues[0].Code);                                   // server

lockedAccounts.Clear(); // the account was unlocked in the meantime
await importer.RetryRejectedAsync();
Console.WriteLine($"{importer.ImportedCount} imported, {importer.RejectedCount} rejected"); // 2 imported, 0 rejected
```

A refused row comes back as an error issue with code `server` on that row, so it appears under the Invalid tab of the review. `RetryRejectedAsync` sends those rows again as they are. `ImportedCount` is the number of rows the host accepted so far (`AcceptedRowIndexes.Count`), so it grows from `1` to `2` here and never counts a row twice.

Editing a cell of a refused row clears its rejection, which removes the row from the retry set. The edited row is importable again and is not accepted, so the next `SubmitAsync` sends it, and only it.

### Resume after a failure, and cancel

The importer records each batch as soon as your callback answers: the rows it did not refuse become `AcceptedRowIndexes`. If a later batch throws, `SubmitStatus` is `Error` (`SubmitError` holds a message only when you threw a `SpreadsheetUserMessageException`, otherwise the component shows a generic text and the exception goes to `Importer.Logger`), and calling `SubmitAsync` again sends only `PendingRows` (the importable rows not accepted yet), so a retry never sends an accepted batch twice:

```csharp
public sealed class NameRow
{
    public string? Name { get; set; }
}

var apiDown = true;
var schema = SpreadsheetSchema.For<NameRow>(s =>
{
    s.BatchSize = 2;
    s.Column(r => r.Name);
    s.OnSubmitAsync = async (rows, cancellationToken) =>
    {
        await Task.Delay(1, cancellationToken); // call your API here, passing the token
        if (apiDown && rows[0].Index == 4) throw new HttpRequestException("API unavailable");
        return null;
    };
});

var importer = new SpreadsheetImporter<NameRow>(schema);
importer.LoadText("Name\nA\nB\nC\nD\nE\nF", "names.csv");

await importer.SubmitAsync();                    // the third batch throws
Console.WriteLine(importer.SubmitStatus);        // Error
Console.WriteLine(importer.ImportedCount);       // 4: two batches were accepted
apiDown = false;
await importer.SubmitAsync();                    // sends rows E and F only
Console.WriteLine(importer.ImportedCount);       // 6
```

`OnSubmitAsync` is the cancellable form of `OnSubmit`: it also receives a `CancellationToken`, which `importer.CancelSubmit()` cancels (the Cancel button of the component calls it while a submit runs), as does the token you pass to `SubmitAsync(cancellationToken)`. A cancelled submit ends in `SpreadsheetSubmitStatus.Cancelled` without an error, keeps the accepted rows, and `SubmitAsync` resumes it. The batch in flight when you cancel counts as not accepted, since the importer cannot know whether your server finished it, so make the call safe to repeat (an upsert, or an idempotency key built from the row). When both callbacks are set, `OnSubmitAsync` is used. `ResetSubmit()` forgets what was accepted so the next submit sends every row again, and loading a file or choosing another sheet or header row does it for you.

An exception your callback throws is caught, logged, and ends the submit with the status `Error`; the uploader sees a generic text unless you threw a `SpreadsheetUserMessageException`, whose message is shown as it is. To refuse rows with a message, return `SpreadsheetRejectedRow` items instead. Batches already accepted are not undone.

Accepted rows are final. They show as Imported in the component, and `EditCell`, `RevertCell`, `DiscardRows` and `RestoreRows` ignore them (`IsRowAccepted(index)` tells). While a submit runs, rows cannot be edited either, so a batch in flight is never recorded as accepted with data that changed. Once any row is accepted, and while a submit runs, `MappingLocked` is `true` and the column mapping, the answers to value questions, the sheet and the header row cannot change; `ResetSubmit()` unlocks them. `CanSubmit` is `false` when nothing is pending, so there is no "Import 0 rows", and `RetryRejectedAsync` needs the same conditions (no check running, no open question). A refusal for a row that was not in the batch is ignored.

## Check rows and records against your server

Goal: validate rows with data only your server has, without loading it all in memory.

Three callbacks accept a `CancellationToken` and are called in batches. Their synchronous counterparts keep working. The callbacks receive `SpreadsheetRow<TRow>` rows (`LookupExistingRecordsAsync` receives the key texts and returns your `TRow` records by key), so you read `r.Value.Supplier` instead of looking a key up in a dictionary.

```csharp
public sealed class CatalogLine
{
    public string? Sku { get; set; }
    public string? Supplier { get; set; }
    public string? Category { get; set; }
}

// catalog, suppliers and categories stand for your own services.
var schema = SpreadsheetSchema.For<CatalogLine>(s =>
{
    s.RowKey(s.Column(r => r.Sku, c => c.Required()));
    s.ExistingAction = SpreadsheetExistingAction.Update;
    // Which of these SKUs exist already? Return the ones you found, by key.
    s.LookupExistingRecordsAsync = async (keys, cancellationToken) =>
    {
        IReadOnlyList<CatalogLine> found = await catalog.FindAsync(keys, cancellationToken);   // your API
        return found.ToDictionary(p => p.Sku!);
    };
    // Is the supplier known? Return the issues, each with the index of its row.
    s.ValidateRowsAsync = async (rows, cancellationToken) =>
    {
        var unknown = await suppliers.UnknownAsync(rows.Select(r => r.Value.Supplier!), cancellationToken);
        return rows.Where(r => unknown.Contains(r.Value.Supplier!))
            .Select(r => new SpreadsheetIndexedIssue(r.Index,
                new SpreadsheetRowIssue("Supplier", "supplier.unknown", SpreadsheetIssueLevel.Error, "Unknown supplier")))
            .ToList();
    };
    s.Column(r => r.Supplier, c => c.Required());
    s.Column(r => r.Category, c => c
        .Options([new SpreadsheetOption<string?>("tools", "Tools")])
        // Called once per value the user chose to create, with the token of the submit.
        .OnCreateOptionAsync(async (text, cancellationToken) => await categories.CreateAsync(text, cancellationToken)));
});
```

**Existing records by key.** `LookupExistingRecordsAsync` receives the distinct key values of the file, `LookupBatchSize` (500) at a time, and returns the records it found by key. Rows whose key it returns get the mode, discard reason or `row.exists` issue that `ExistingAction` defines, exactly as with `ExistingRecords`, and the two can be combined. A returned dictionary keeps its own comparer, so return a case-insensitive one if your keys are.

**Batched validation.** `ValidateRowsAsync` receives rows that are not discarded and have no error yet (and whose mode the lookup has set), `ValidateBatchSize` (500) at a time, and returns `SpreadsheetIndexedIssue` values: the row index and the issue to add. An error blocks the row, a warning does not.

**When they run.** `await importer.ValidateAsync(cancellationToken)` runs the lookup and then the validation, on what was not checked yet. `NeedsAsyncValidation` tells whether anything changed since: loading a file, mapping a column or answering a value makes every row unchecked, and editing a row makes that row unchecked, which is also the only row sent next time. The component runs them when the review opens and again after edits, shows "Checking the rows" with a Cancel button, and shows a generic error with a Check again button when a callback throws (the text of a `SpreadsheetUserMessageException` when you threw one; other exceptions go to `Importer.Logger`). `SubmitAsync` runs them too when they are pending (after a failed or cancelled run as well, so a retry checks again), so the rows it sends are the ones that passed: rows the validator flags are left out and show as invalid. `CanSubmit` is `false` while a check runs, `SubmitAsync` does nothing then, and the component disables Import. An answer that arrives after the user edited the row, or after the file was read again, is not applied: that row stays unchecked for the next run, and raising the row limit makes the rows it takes in unchecked too. `ValidationStatus` and `ValidationError` report the state, `CancelValidation()` stops a run, and the results of the batches that finished are kept. A failed run is not retried by itself: call `ValidateAsync` again.

**Creating an option.** `OnCreateOptionAsync` is the asynchronous `OnCreateOption`. Setting either makes the component offer the create choice. When both are set, the asynchronous one is used.

## Remember a column mapping

Goal: do not make the same user map the same export every month.

In the samples below, `Member` is a row class with the properties `Name`, `Email`, `Level` and `Note`, and `schema` is its schema.

The profile stores column keys, which are the property names unless you call `Key("...")` on a column, so set a key when you rename a property and stored profiles must keep working. A stored key is matched exactly first and, when no column has it, case-insensitively when exactly one column fits, so a profile saved with lowercase keys (`sku`) before the schema was typed still applies to the property name (`Sku`); the next `GetMappingProfile()` writes the new key.

`GetMappingProfile()` captures the mapping the user finished: each file header that feeds a column, the columns they declared absent, and their answers to unknown values. It holds header texts, column keys and answers, never cell data. Store it with `ToJson()`:

```csharp
var importer = new SpreadsheetImporter<Member>(schema);
importer.LoadText(file, "bank-march.csv");
importer.AssignColumn("Name", 0);                       // the user maps "Full name" to Name
importer.AnswerValue("Level", "Gold", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Map, "pro", "Pro"));
await importer.SubmitAsync();

var json = importer.GetMappingProfile().ToJson();       // keep it per user and per schema: a database row, local storage, a file
```

```json
{
  "version": 1,
  "schemaKey": "people",
  "columns": { "full name": "Name", "e-mail address": "Email", "plan": "Level" },
  "ignoredColumns": [ "Note" ],
  "valueAnswers": { "Level::gold": { "action": "Map", "mappedValue": "pro", "mappedLabel": "Pro" } }
}
```

Next time, give the stored profile to `SavedMapping` before loading the file:

```csharp
var next = new SpreadsheetImporter<Member>(schema) { SavedMapping = SpreadsheetMappingProfile.FromJson(json) };
next.LoadText(nextFile, "bank-april.csv");
Console.WriteLine(next.SavedMappingApplied);            // true: the columns are mapped, the "Gold" question is answered
```

A header that appears several times in the file (two `Date` columns) is stored by occurrence: `Columns` holds the first one and `RepeatedColumns` the others (`"2:date"` is the second column headed `date`), so a re-applied profile sends each occurrence to its own column. A stored profile is untrusted input: null keys and values, unknown columns, a `Map` answer that is not one of the column's options, a `Create` answer for a column that cannot create options and an undefined action are skipped without an exception.

The profile is applied whenever a file is loaded, or the user picks another sheet or header row, and every header it maps is present in the file (compared like column matching: case, accents and a trailing `*` ignored, any order, extra file columns allowed). It also helps to find the header row: a row whose headers the profile maps counts as a header row, so a renamed header under a banner is found. A file that does not match is read as usual, and the user can still change any choice, which wins over the profile. A profile made for another `SpreadsheetSchema.Key`, or of another `Version`, is never applied. `KlySpreadsheetImport` says "Your saved column mapping was applied" when it was. Use `ApplyMappingProfile(profile)` to apply one yourself and read what it did, and `MatchesMappingProfile(profile)` to test a file without applying anything.

**Knowing when to store it.** `KlySpreadsheetImport` has an `OnMappingProfileChanged` callback (`EventCallback<SpreadsheetMappingProfile>`) that receives the current profile each time the user maps a column, declares one absent, undoes a choice or answers a value question. Persist it there and give it back through `SavedMapping` next time:

```razor
<KlySpreadsheetImport Importer="_importer" OnMappingProfileChanged="SaveProfileAsync" />

@code {
    private Task SaveProfileAsync(SpreadsheetMappingProfile profile) =>
        store.SaveAsync(userId, "people", profile.ToJson());   // your storage
}
```

It is not called when a file is loaded or when a saved mapping is applied automatically, so loading a file never overwrites what you stored. Headless, the importer's `MappingChanged` event does the same, after `Changed`: read `GetMappingProfile()` in the handler. Cell edits, discards and filters do not raise it.

**Storing it.** `ToJson` and `FromJson` use `SpreadsheetMappingProfileJsonContext`, a source-generated `System.Text.Json` context, so they work under trimming and NativeAOT with no reflection. To store a profile inside your own JSON document, add `[JsonSerializable(typeof(SpreadsheetMappingProfile))]` to your own context. Save it from `OnMappingProfileChanged`, or call `GetMappingProfile()` where your `OnSubmit` finishes. Do not store it in the browser for data that should follow the user across devices; a profile is small, so a row per user and schema is enough.

## Translate the wizard and its messages

Goal: show the wizard and the validation messages in the language of the user.

`KlySpreadsheetImport` takes the locale of Klyroo.UI (the cascading `KlyLocale`, then the locale of `AddKlyrooUi`, then the current UI culture) and shows the texts of that locale. There is one set per locale of `KlyLocales.All` (English, French, German, Spanish, Italian, Portuguese, Brazilian Portuguese, Dutch, Japanese, Simplified Chinese, Arabic and Russian), found by the code of the locale: `pt-PT` gets `pt`, an unknown language gets English. English and French are reviewed; the other sets are drafts waiting for a native speaker (`SpreadsheetLocales.ReviewedCodes` lists the reviewed ones). A text that holds a number or a name is a function, so each language chooses its plural forms and word order.

Change a set in `AddKlyrooSpreadsheet`, in code shared by the server and the WebAssembly client:

```csharp
builder.Services.AddKlyrooSpreadsheet(s => s
    .Texts("fr", t => t with
    {
        ImportRows = n => n <= 1 ? $"Lancer l’import de {n} ligne" : $"Lancer l’import de {n} lignes",
        IssueRowExists = "Cette référence existe déjà",
    })
    .Texts("en", t => t with { StepReview = "Check" }));
```

A change to `"fr"` also applies to `fr-CA` and the other locales that resolve to it, and a locale you add (`KlyLocales.Extend(KlyLocales.En, code: "en-AU", ...)`) starts from the set its code resolves to. The texts become read-only once a component has resolved one, so change them before the app renders. `SpreadsheetTexts` documents every text; build a variant with a `with` expression.

**Issue messages are resolved by code.** The importer raises issues with an English `Message` and, for the messages the library writes, the values in `Args`. The component shows `texts.ResolveIssue(issue)`: the message of the locale for codes such as `required`, `number`, `boolean`, `date`, `value.unknown`, `value.not-found`, `value.skipped`, `row.duplicate`, `row.exists`, `pattern`, and for the default messages of `SpreadsheetRule.Email`, `Pattern`, `Min`, `Max` and `Between`. A message you wrote is never replaced: a rule built with your own `message`, the issues of `ValidateRow` and `ValidateRowsAsync`, a `Parser` failure and a server rejection have no `Args` and show as they are. In your own UI, call the same method:

```csharp
var texts = SpreadsheetLocales.Tables["fr"];
foreach (var issue in importer.InvalidRows.SelectMany(r => r.Issues))
{
    Console.WriteLine(texts.ResolveIssue(issue));   // « x » n’est pas un nombre
}
```

The invalid rows download is written in the locale too (`ExportInvalidRowsCsv` takes the function that turns an issue into text and the heading of the last column). The text you give to the component is never translated: `Title` (the heading defaults to the word Import of the locale), column labels and descriptions, and your own rule messages. The `InvalidDataException` messages of the file readers are English.

**Announcements.** The component has a visually hidden live region (`aria-live="polite"`) that says the counts of the review as they change ("To import: 118. With errors: 2. Left out: 0.") and the progress and the end of the import ("Importing: 40 of 120 rows", "120 rows imported"), in the locale of the user. Progress is announced at most every 3 seconds, not per batch, and the end at once; before the number of rows to send is known it says only "Importing…". The notices that appear on their own (saved mapping applied, row limit, state of the checks) are `role="status"` regions too.

## Read an Excel file

Goal: import `.xlsx` files.

`LoadFileAsync` reads an `.xlsx` workbook with `SpreadsheetXlsxReader`, which uses the base class library only and works under NativeAOT. The component calls it for a chosen or dropped file; from your own code, pass any stream:

```csharp
await using var stream = File.OpenRead("prices.xlsx");
await importer.LoadFileAsync(stream, "prices.xlsx");
```

Every worksheet becomes a sheet, and the importer picks the one whose header row matches the most columns; the component offers a sheet selector when there are several. Shared and inline strings are read as text, booleans as `TRUE` or `FALSE`, and cells formatted as dates as `yyyy-MM-dd` (with the time when there is one), which the default date formats of a `Date` column read. Numbers keep the invariant form `1234.5`, and a `Number` column reads that form whatever its `DecimalSeparator`. Formulas are not evaluated: the reader uses the value Excel saved with the file. Legacy `.xls` workbooks (Excel 97 to 2003, BIFF8) are read the same way, by `SpreadsheetXlsReader`, with the same value conventions; error cells give their text (`#DIV/0!`), and hidden sheets are read like the visible ones. The reader also uses no dependency and no reflection, and it treats the file as untrusted: a damaged or hostile file throws an `InvalidDataException` instead of looping or allocating without bound. Both readers refuse a workbook of more than 2,000,000 cells, counting the empty cells left of each value, or of more than 32 million characters of text. Password-protected workbooks and the Excel 5.0/95 format (BIFF5) are refused with an `InvalidDataException` that tells users to save as `.xlsx` or `.csv`. `LoadFileAsync` chooses by content as well as by extension: a file that starts with the OLE2 signature is read as `.xls` whatever its name, a zip archive named `.xls` is read as `.xlsx`, and a text export named `.xls` (tab-separated or CSV text, which many applications produce under that name) is read like a `.csv` file. An HTML or XML export named `.xls` (a web page table, the Excel 2003 XML format) is refused, since its rows are markup.

For another format, build a `SpreadsheetWorkbook` yourself and call `LoadWorkbook`. `RowNumbers` holds the row number of each entry of `Rows` in the original sheet, which is what the review shows to users and what `ForcedHeaderRow` refers to.

## Let users paste rows in Blazor

Goal: accept rows copied from Excel or Google Sheets instead of a file.

`Paste` is `LoadText` with a default name of `clipboard.tsv`. Cells copied from a spreadsheet arrive tab-separated, which the separator detection recognizes. With `Wizard="false"` the component renders only the current step, with no step bar and no buttons, so your page decides when to move on:

```razor
@using Klyroo.Spreadsheet

<textarea @bind="_pasted" @bind:event="oninput" rows="6" placeholder="Paste rows from Excel or Sheets"></textarea>
<button type="button" @onclick="Load">Load</button>

<KlySpreadsheetImport Importer="Importer" Wizard="false" />

@code {
    private string _pasted = "";

    [Parameter, EditorRequired] public SpreadsheetImporter Importer { get; set; } = default!;

    private void Load() => Importer.Paste(_pasted);
}
```

Without the wizard, call `Importer.NextStep()`, `PreviousStep()` and `SubmitAsync()` from your own buttons, and read `Importer.GetSteps()` to draw your own step bar.

## Import large files

Goal: let users review a file of tens of thousands of rows without a frozen page.

The importer reads the file once, then updates only the rows a change concerns. Editing a cell re-validates that row, plus the rows that share its old or new key when you change the key, so an edit in a 50,000-row file takes milliseconds. The lists it builds (`ImportableRows`, `InvalidRows`, `DiscardedRows`, `VisibleReviewRows`) are cached until a row or a filter changes, so reading them again costs nothing.

The review is paged. Build your own table on `ReviewPageRows` and move with `SetReviewPage` and `SetReviewPageSize`:

```csharp
var importer = new SpreadsheetImporter<Product>(schema);
importer.LoadText(csv, "big.csv");

importer.SetReviewPageSize(100);
importer.SetReviewPage(2);
foreach (var row in importer.ReviewPageRows)   // rows 201 to 300 of the current filter
{
    Console.WriteLine(row.RowNumber);
}
Console.WriteLine($"{importer.ReviewPage + 1} of {importer.ReviewPageCount}");
```

Changing a filter (`SetReviewTab`, `SetReviewLevel`, `SetReviewIssueFilter`, `SetReviewSearch`) goes back to the first page, and a page number is kept inside the pages that exist.

`KlySpreadsheetImport` does this for you. Its review table renders one page (`PageSize`, 50 by default, with a page size list in the pager) and the pager is made of native buttons, so it works with the keyboard. The checkbox in the table header selects the rows of the current page.

Cap the import with `SpreadsheetSchema.MaxRows`, or with the `MaxRows` parameter of the component, which is 10,000 by default and applies when the schema sets none (`MaxRows="@null"` removes it). Rows past the limit are left out with the discard reason `Limit`, and the component tells the user how many:

```razor
<KlySpreadsheetImport Importer="_importer" MaxRows="50000" PageSize="100" />
```

Headless, set `importer.DefaultMaxRows` for the same effect, and read `EffectiveMaxRows` and `LimitedRowCount` to tell your own users.

## Export a template and the invalid rows

Goal: give users a file to fill in, and give them back the rows they must fix.

```csharp
File.WriteAllText("template.csv", importer.ExportTemplateCsv());
File.WriteAllText("rejected.csv", importer.ExportInvalidRowsCsv());
```

The wizard offers both. The file step has a Download a template button, and the review step has a Download the invalid rows button as long as some row is invalid. In a browser they save the file through a small JavaScript module (`downloadText`, imported the first time you click), named `{schema key}-template.csv` and `{schema key}-invalid-rows.csv`. Set `OnDownload` to take the file yourself, which is what a .NET MAUI Blazor Hybrid app should do, or to store it elsewhere. The component then makes no JavaScript call:

```razor
<KlySpreadsheetImport Importer="_importer" OnDownload="SaveAsync" />

@code {
    private async Task SaveAsync(SpreadsheetDownload file)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, file.FileName);   // MAUI
        await File.WriteAllTextAsync(path, file.Content);
        await Share.Default.RequestAsync(new ShareFileRequest { File = new ShareFile(path) });
    }
}
```

`ExportTemplateCsv` writes a header row and an example row (`Example` of each column) for every column that is not computed with `From`. `ExportInvalidRowsCsv` lists the rows that are invalid and not discarded, with an `Issues` column. Both start with a byte order mark so Excel opens them as UTF-8, double the quotes inside a cell, and guard against CSV injection: a text that starts with `=`, `@`, a tab or a carriage return, or with `+` or `-` when it is not a plain number (`-12,50` stays a number), gets an apostrophe in front, in headers, examples, cells and the issues column, so a spreadsheet application shows it as text instead of running it. A user who fixes the file and imports it again will find the apostrophe on those cells. The invalid rows export has one column per schema column, in schema order, headed by the file header it was read from, so the user can fix the cells and import the file again.

## Test an import

Goal: test your schema and your page without a browser.

The engine is plain C#, so test it with xUnit alone. The component is tested with [bUnit](https://bunit.dev): register the services, render the component with your importer, load text, and click the buttons the component exposes through `data-slot` attributes. The tests below compile with `xunit.v3` and `bunit`:

```csharp
using Bunit;
using Klyroo.Spreadsheet;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MyApp.Tests;

public sealed class PriceRow
{
    public string? Sku { get; set; }
    public double Price { get; set; }
}

public sealed class PriceImportTests
{
    private static SpreadsheetImporter<PriceRow> CreateImporter() => new(SpreadsheetSchema.For<PriceRow>(s =>
    {
        s.Column(r => r.Sku, c => c.Label("SKU").Required());
        s.Column(r => r.Price, c => c.Rule(SpreadsheetRule.Min(0d)));
    }));

    [Fact]
    public void Negative_prices_block_the_row()
    {
        var importer = CreateImporter();

        importer.LoadText("SKU,Price\nA-1,10\nA-2,-5", "prices.csv");

        Assert.Single(importer.ImportableRows);
        var invalid = Assert.Single(importer.InvalidRows);
        Assert.Contains(invalid.Issues, i => i.Field == "Price" && i.Code == "min");
    }

    [Fact]
    public void A_missing_required_column_blocks_submission_until_it_is_assigned()
    {
        var importer = CreateImporter();

        importer.LoadText("SKU,Amount\nA-1,10\nA-2,12", "prices.csv");

        Assert.False(importer.CanSubmit);
        Assert.Equal("Amount", importer.MissingFields[0].Suggestion?.HeaderText);

        importer.AssignColumn("Price", importer.MissingFields[0].Suggestion!.HeaderIndex);

        Assert.True(importer.CanSubmit);
    }
}

public sealed class PriceImportComponentTests : BunitContext
{
    public PriceImportComponentTests()
    {
        Services.AddKlyrooSpreadsheet();
    }

    [Fact]
    public void Continue_moves_from_the_file_step_to_review_when_nothing_needs_mapping()
    {
        var importer = new SpreadsheetImporter<PriceRow>(SpreadsheetSchema.For<PriceRow>(s =>
            s.Column(r => r.Sku, c => c.Required())));
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        importer.LoadText("SKU\nA-1", "prices.csv");
        cut.Render();
        cut.Find("[data-slot=spreadsheet-nav-continue]").Click();

        Assert.Equal("review", importer.CurrentStepKey);
        Assert.Contains("A-1", cut.Markup);
    }
}
```

The suggestion in the second test needs at least two non-empty sample cells in the candidate column, which is why the file has two data rows. To test `OnSubmit` without a database, give the schema a callback that records the rows it receives and assert on them after `await importer.SubmitAsync()`. The repository's own [tests](../tests/Klyroo.Spreadsheet.Tests) cover more cases, including a 5,000-row import.
