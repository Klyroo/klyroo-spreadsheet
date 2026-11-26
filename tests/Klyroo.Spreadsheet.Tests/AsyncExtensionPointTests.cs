using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

// The extension points that call the host: OnCreateOptionAsync, ValidateRowsAsync and LookupExistingRecordsAsync.
public sealed class AsyncExtensionPointTests : BunitContext
{
    public AsyncExtensionPointTests()
    {
        Services.AddKlyrooSpreadsheet();
    }

    private static CancellationToken Token => Xunit.TestContext.Current.CancellationToken;

    private static Task<IReadOnlyList<SpreadsheetRejectedRow>?> Accepted() => Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>(null);

    // OnCreateOptionAsync

    private static SpreadsheetImporter CreateImporter(
        Func<string, CancellationToken, Task<SpreadsheetOption>>? createAsync,
        Func<string, SpreadsheetOption>? create = null,
        List<object?>? sent = null)
    {
        var importer = new SpreadsheetImporter(new SpreadsheetSchema
        {
            OnSubmit = rows =>
            {
                sent?.AddRange(rows.Select(r => r.Data["level"]));
                return Accepted();
            },
            Columns =
            [
                new SpreadsheetColumnSchema
                {
                    Key = "level",
                    Kind = SpreadsheetColumnKind.Select,
                    Options = [new SpreadsheetOption("a", "A")],
                    OnCreateOptionAsync = createAsync,
                    OnCreateOption = create,
                },
            ],
        });
        importer.LoadText("Level\nZ\nz\nA", "levels.csv");
        return importer;
    }

    [Fact]
    public async Task OnCreateOptionAsync_creates_each_value_once_with_the_submit_token_and_rows_carry_the_option()
    {
        var created = new List<string>();
        var sent = new List<object?>();
        CancellationToken seen = default;
        var importer = CreateImporter(async (text, token) =>
        {
            seen = token;
            await Task.Yield();
            created.Add(text);
            return new SpreadsheetOption("lvl-" + text.ToLowerInvariant(), text);
        }, sent: sent);
        importer.AnswerValue("level", "Z", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Create));
        Assert.True(importer.Questions.Single().CanCreate);

        await importer.SubmitAsync(Token);

        Assert.Equal(["Z"], created);                           // "Z" and "z" are one value
        Assert.True(seen.CanBeCanceled);
        Assert.Equal(SpreadsheetSubmitStatus.Done, importer.SubmitStatus);
        Assert.Equal(["lvl-z", "lvl-z", "a"], sent.Select(v => v?.ToString()));
    }

    [Fact]
    public async Task OnCreateOptionAsync_wins_over_OnCreateOption_and_either_one_makes_the_choice_available()
    {
        var which = new List<string>();
        var importer = CreateImporter(
            (text, token) => { which.Add("async"); return Task.FromResult(new SpreadsheetOption("x", text)); },
            text => { which.Add("sync"); return new SpreadsheetOption("y", text); });
        importer.AnswerValue("level", "Z", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Create));

        await importer.SubmitAsync(Token);

        Assert.Equal(["async"], which);
        Assert.True(CreateImporter(null, text => new SpreadsheetOption(text, text)).Questions.Single().CanCreate);
        Assert.False(CreateImporter(null).Questions.Single().CanCreate);
    }

    [Fact]
    public async Task Cancelling_during_OnCreateOptionAsync_cancels_the_submit_before_any_row_is_sent()
    {
        var started = new TaskCompletionSource();
        var sent = new List<object?>();
        var importer = CreateImporter(async (text, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new SpreadsheetOption(text, text);
        }, sent: sent);
        importer.AnswerValue("level", "Z", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Create));

        var submit = importer.SubmitAsync(Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
        importer.CancelSubmit();
        await submit;

        Assert.Equal(SpreadsheetSubmitStatus.Cancelled, importer.SubmitStatus);
        Assert.Empty(sent);
    }

    [Fact]
    public async Task An_exception_from_OnCreateOptionAsync_is_reported_in_SubmitError()
    {
        var importer = CreateImporter((text, token) => throw new InvalidOperationException("Cannot create"));
        importer.AnswerValue("level", "Z", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Create));

        await importer.SubmitAsync(Token);

        Assert.Equal(SpreadsheetSubmitStatus.Error, importer.SubmitStatus);
        Assert.Equal("Cannot create", importer.SubmitError);
    }

    // ValidateRowsAsync

    private static SpreadsheetImporter ValidatingImporter(
        Func<IReadOnlyList<SpreadsheetRow>, CancellationToken, Task<IReadOnlyList<SpreadsheetIndexedIssue>?>> validate,
        int batchSize = 2,
        Func<IReadOnlyList<SpreadsheetRow>, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>? onSubmit = null)
    {
        var importer = new SpreadsheetImporter(new SpreadsheetSchema
        {
            ValidateRowsAsync = validate,
            ValidateBatchSize = batchSize,
            OnSubmit = onSubmit,
            Columns =
            [
                new SpreadsheetColumnSchema { Key = "sku", Required = true },
                new SpreadsheetColumnSchema { Key = "qty", Kind = SpreadsheetColumnKind.Number },
            ],
        });
        importer.LoadText("SKU;Qty\nA;1\nB;2\nC;x\nD;4\nE;5", "stock.csv");
        return importer;
    }

    private static SpreadsheetIndexedIssue Unknown(SpreadsheetRow row, SpreadsheetIssueLevel level = SpreadsheetIssueLevel.Error) =>
        new(row.Index, new SpreadsheetRowIssue("sku", "sku.unknown", level, $"{row.Data["sku"]} does not exist"));

    [Fact]
    public async Task ValidateRowsAsync_checks_the_clean_rows_in_batches_and_adds_the_issues_to_their_rows()
    {
        var batches = new List<int[]>();
        var importer = ValidatingImporter((rows, token) =>
        {
            batches.Add(rows.Select(r => r.Index).ToArray());
            return Task.FromResult<IReadOnlyList<SpreadsheetIndexedIssue>?>(rows.Where(r => (string)r.Data["sku"]! == "B").Select(r => Unknown(r)).ToList());
        });
        Assert.True(importer.HasAsyncChecks);
        Assert.True(importer.NeedsAsyncValidation);

        await importer.ValidateAsync(Token);

        Assert.Equal(SpreadsheetValidationStatus.Done, importer.ValidationStatus);
        Assert.Equal([[0, 1], [3, 4]], batches);                 // row 2 has a parse error: not sent
        Assert.False(importer.NeedsAsyncValidation);
        var flagged = importer.AllRows[1];
        Assert.Equal("sku.unknown", Assert.Single(flagged.Issues).Code);
        Assert.False(flagged.Importable);
        Assert.Equal([1, 2], importer.InvalidRows.Select(r => r.Index));
    }

    [Fact]
    public async Task A_warning_from_ValidateRowsAsync_does_not_block_the_row()
    {
        var importer = ValidatingImporter((rows, token) =>
            Task.FromResult<IReadOnlyList<SpreadsheetIndexedIssue>?>([Unknown(rows[0], SpreadsheetIssueLevel.Warning)]));

        await importer.ValidateAsync(Token);

        Assert.Equal(SpreadsheetRowStatus.Warning, importer.AllRows[0].Status);
        Assert.True(importer.AllRows[0].Importable);
    }

    [Fact]
    public async Task Rows_already_checked_are_not_sent_again_and_an_edited_row_is_checked_alone()
    {
        var batches = new List<int[]>();
        var importer = ValidatingImporter((rows, token) =>
        {
            batches.Add(rows.Select(r => r.Index).ToArray());
            return Task.FromResult<IReadOnlyList<SpreadsheetIndexedIssue>?>(null);
        });
        await importer.ValidateAsync(Token);
        await importer.ValidateAsync(Token);
        Assert.Equal(2, batches.Count);                          // the second call found nothing to check

        importer.EditCell(1, "sku", "B2");
        Assert.True(importer.NeedsAsyncValidation);
        importer.EditCell(2, "qty", "3");                        // row 2 had a parse error and was never sent: now it is clean
        await importer.ValidateAsync(Token);

        Assert.Equal([1, 2], batches[^1]);
        Assert.Equal(3, batches.Count);
    }

    [Fact]
    public async Task Fixing_a_row_clears_the_issue_the_validator_found_until_it_is_checked_again()
    {
        var importer = ValidatingImporter((rows, token) =>
            Task.FromResult<IReadOnlyList<SpreadsheetIndexedIssue>?>(rows.Where(r => (string)r.Data["sku"]! == "B").Select(r => Unknown(r)).ToList()));
        await importer.ValidateAsync(Token);
        Assert.False(importer.AllRows[1].Importable);

        importer.EditCell(1, "sku", "B2");
        Assert.True(importer.AllRows[1].Importable);             // stale until the validator has seen it

        await importer.ValidateAsync(Token);
        Assert.True(importer.AllRows[1].Importable);
        Assert.Equal(SpreadsheetValidationStatus.Done, importer.ValidationStatus);
    }

    [Fact]
    public async Task Cancelling_the_validation_keeps_the_batches_that_finished()
    {
        var calls = 0;
        SpreadsheetImporter? importer = null;
        importer = ValidatingImporter((rows, token) =>
        {
            if (++calls == 2) importer!.CancelValidation();
            token.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<SpreadsheetIndexedIssue>?>(rows.Where(r => r.Index == 0).Select(r => Unknown(r)).ToList());
        });

        await importer.ValidateAsync(Token);

        Assert.Equal(SpreadsheetValidationStatus.Cancelled, importer.ValidationStatus);
        Assert.Equal("sku.unknown", Assert.Single(importer.AllRows[0].Issues).Code);   // the first batch was applied
        Assert.True(importer.NeedsAsyncValidation is false);                           // a cancelled run is repeated on demand, not automatically

        calls = int.MinValue;
        await importer.ValidateAsync(Token);
        Assert.Equal(SpreadsheetValidationStatus.Done, importer.ValidationStatus);
    }

    [Fact]
    public async Task An_exception_from_ValidateRowsAsync_is_reported_and_the_next_call_tries_again()
    {
        var fail = true;
        var importer = ValidatingImporter((rows, token) => fail
            ? throw new InvalidOperationException("Validation service is down")
            : Task.FromResult<IReadOnlyList<SpreadsheetIndexedIssue>?>(null));

        await importer.ValidateAsync(Token);
        Assert.Equal(SpreadsheetValidationStatus.Error, importer.ValidationStatus);
        Assert.Equal("Validation service is down", importer.ValidationError);

        fail = false;
        await importer.ValidateAsync(Token);
        Assert.Equal(SpreadsheetValidationStatus.Done, importer.ValidationStatus);
        Assert.Null(importer.ValidationError);
    }

    [Fact]
    public async Task ValidateAsync_does_nothing_without_async_checks()
    {
        var importer = new SpreadsheetImporter(new SpreadsheetSchema { Columns = [new SpreadsheetColumnSchema { Key = "a" }] });
        importer.LoadText("A\n1", "a.csv");

        await importer.ValidateAsync(Token);

        Assert.False(importer.HasAsyncChecks);
        Assert.False(importer.NeedsAsyncValidation);
        Assert.Equal(SpreadsheetValidationStatus.Idle, importer.ValidationStatus);
    }

    [Fact]
    public async Task SubmitAsync_runs_the_checks_first_and_sends_only_the_rows_that_passed()
    {
        var sent = new List<string>();
        var importer = ValidatingImporter(
            (rows, token) => Task.FromResult<IReadOnlyList<SpreadsheetIndexedIssue>?>(rows.Where(r => (string)r.Data["sku"]! == "D").Select(r => Unknown(r)).ToList()),
            onSubmit: rows => { sent.AddRange(rows.Select(r => (string)r.Data["sku"]!)); return Accepted(); });
        importer.EditCell(2, "qty", "3");

        await importer.SubmitAsync(Token);

        Assert.Equal(SpreadsheetSubmitStatus.Done, importer.SubmitStatus);
        Assert.Equal(["A", "B", "C", "E"], sent);                // D was flagged by the validator and left out
        Assert.Equal(4, importer.ImportedCount);
        Assert.Equal([3], importer.InvalidRows.Select(r => r.Index));
    }

    [Fact]
    public async Task A_failing_validator_stops_the_submit_with_its_message()
    {
        var sent = 0;
        var importer = ValidatingImporter(
            (rows, token) => throw new InvalidOperationException("Validation service is down"),
            onSubmit: rows => { sent += rows.Count; return Accepted(); });

        await importer.SubmitAsync(Token);

        Assert.Equal(SpreadsheetSubmitStatus.Error, importer.SubmitStatus);
        Assert.Equal("Validation service is down", importer.SubmitError);
        Assert.Equal(0, sent);
    }

    [Fact]
    public async Task Changing_the_column_mapping_makes_every_row_unchecked_again()
    {
        var checkedRows = 0;
        var importer = ValidatingImporter((rows, token) =>
        {
            checkedRows += rows.Count;
            return Task.FromResult<IReadOnlyList<SpreadsheetIndexedIssue>?>(null);
        }, batchSize: 0);
        await importer.ValidateAsync(Token);
        Assert.Equal(4, checkedRows);

        importer.IgnoreColumn("qty");
        Assert.True(importer.NeedsAsyncValidation);
        await importer.ValidateAsync(Token);

        Assert.Equal(9, checkedRows);                            // the 5 rows, none of them has a parse error any more
    }

    // LookupExistingRecordsAsync

    private static SpreadsheetImporter LookupImporter(
        Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>> lookup,
        SpreadsheetExistingAction action = SpreadsheetExistingAction.Update,
        int batchSize = 2,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? existing = null)
    {
        var importer = new SpreadsheetImporter(new SpreadsheetSchema
        {
            RowKeyField = "sku",
            ExistingAction = action,
            ExistingRecords = existing,
            LookupExistingRecordsAsync = lookup,
            LookupBatchSize = batchSize,
            Columns = [new SpreadsheetColumnSchema { Key = "sku", Required = true }],
        });
        importer.LoadText("SKU\nA\nB\nA\nC\nD", "stock.csv");
        return importer;
    }

    private static Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>> Found(params string[] keys) =>
        Task.FromResult<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            keys.ToDictionary(k => k, k => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(), StringComparer.Ordinal));

    [Fact]
    public async Task The_lookup_receives_the_distinct_keys_in_batches_and_marks_the_rows_that_exist()
    {
        var asked = new List<string[]>();
        var importer = LookupImporter((keys, token) =>
        {
            asked.Add(keys.ToArray());
            return Found(keys.Where(k => k is "B" or "D").ToArray());
        });
        Assert.True(importer.NeedsAsyncValidation);

        await importer.ValidateAsync(Token);

        Assert.Equal([["A", "B"], ["C", "D"]], asked);
        Assert.Equal(
            [SpreadsheetRowMode.Create, SpreadsheetRowMode.Update, SpreadsheetRowMode.Create, SpreadsheetRowMode.Create, SpreadsheetRowMode.Update],
            importer.AllRows.Select(r => r.Mode));
    }

    [Fact]
    public async Task The_existing_action_applies_to_looked_up_records()
    {
        var skip = LookupImporter((keys, token) => Found("B"), SpreadsheetExistingAction.Skip);
        await skip.ValidateAsync(Token);
        Assert.Equal(SpreadsheetDiscardReason.Existing, skip.AllRows[1].DiscardReason);
        Assert.Equal(SpreadsheetRowMode.Skip, skip.AllRows[1].Mode);

        var error = LookupImporter((keys, token) => Found("B"), SpreadsheetExistingAction.Error);
        await error.ValidateAsync(Token);
        Assert.Contains(error.AllRows[1].Issues, i => i.Code == "row.exists");
        Assert.False(error.AllRows[1].Importable);
    }

    [Fact]
    public async Task Looked_up_records_add_to_the_synchronous_ExistingRecords()
    {
        var importer = LookupImporter((keys, token) => Found("B"), existing: new Dictionary<string, IReadOnlyDictionary<string, object?>>
        {
            ["C"] = new Dictionary<string, object?>(),
        });
        Assert.Equal(SpreadsheetRowMode.Update, importer.AllRows[3].Mode);   // the synchronous dictionary applies at once

        await importer.ValidateAsync(Token);

        Assert.Equal(SpreadsheetRowMode.Update, importer.AllRows[1].Mode);
        Assert.Equal(SpreadsheetRowMode.Update, importer.AllRows[3].Mode);
    }

    [Fact]
    public async Task A_key_is_looked_up_once_and_an_edited_key_is_looked_up_alone()
    {
        var asked = new List<string>();
        var importer = LookupImporter((keys, token) =>
        {
            asked.AddRange(keys);
            return Found(keys.Where(k => k == "Z").ToArray());
        }, batchSize: 0);
        await importer.ValidateAsync(Token);
        await importer.ValidateAsync(Token);
        Assert.Equal(["A", "B", "C", "D"], asked);

        importer.EditCell(0, "sku", "Z");
        Assert.True(importer.NeedsAsyncValidation);
        await importer.ValidateAsync(Token);

        Assert.Equal(["A", "B", "C", "D", "Z"], asked);
        Assert.Equal(SpreadsheetRowMode.Update, importer.AllRows[0].Mode);
        importer.EditCell(0, "sku", "A");
        Assert.Equal(SpreadsheetRowMode.Create, importer.AllRows[0].Mode);
    }

    [Fact]
    public async Task A_returned_dictionary_keeps_its_own_comparer()
    {
        var importer = LookupImporter((keys, token) => Task.FromResult<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase) { ["b"] = new Dictionary<string, object?>() }));

        await importer.ValidateAsync(Token);

        Assert.Equal(SpreadsheetRowMode.Update, importer.AllRows[1].Mode);   // the row says "B", the server key is "b"
    }

    [Fact]
    public async Task Cancelling_the_lookup_keeps_the_batches_that_finished_and_the_next_run_asks_for_the_rest()
    {
        var asked = new List<string[]>();
        SpreadsheetImporter? importer = null;
        importer = LookupImporter((keys, token) =>
        {
            asked.Add(keys.ToArray());
            if (asked.Count == 1) importer!.CancelValidation();
            return Found(keys.Where(k => k == "B").ToArray());
        });

        await importer.ValidateAsync(Token);

        Assert.Equal(SpreadsheetValidationStatus.Cancelled, importer.ValidationStatus);
        Assert.Equal([["A", "B"]], asked);
        Assert.Equal(SpreadsheetRowMode.Update, importer.AllRows[1].Mode);

        await importer.ValidateAsync(Token);
        Assert.Equal([["A", "B"], ["C", "D"]], asked);
        Assert.Equal(SpreadsheetValidationStatus.Done, importer.ValidationStatus);
    }

    [Fact]
    public async Task Both_checks_run_in_one_pass_and_the_validator_sees_the_row_mode()
    {
        var modes = new List<SpreadsheetRowMode>();
        var importer = new SpreadsheetImporter(new SpreadsheetSchema
        {
            RowKeyField = "sku",
            LookupExistingRecordsAsync = (keys, token) => Found("B"),
            ValidateRowsAsync = (rows, token) =>
            {
                modes.AddRange(rows.Select(r => r.Mode));
                return Task.FromResult<IReadOnlyList<SpreadsheetIndexedIssue>?>(null);
            },
            Columns = [new SpreadsheetColumnSchema { Key = "sku" }],
        });
        importer.LoadText("SKU\nA\nB", "stock.csv");

        await importer.ValidateAsync(Token);

        Assert.Equal([SpreadsheetRowMode.Create, SpreadsheetRowMode.Update], modes);
    }

    // The component

    [Fact]
    public async Task The_component_runs_the_checks_when_the_review_opens_and_shows_their_state()
    {
        var gate = new TaskCompletionSource();
        var importer = ValidatingImporter(async (rows, token) =>
        {
            await gate.Task.WaitAsync(token);
            return rows.Where(r => (string)r.Data["sku"]! == "A").Select(r => Unknown(r)).ToList();
        }, batchSize: 0);
        importer.NextStep();                                     // columns are fine: lands on the review
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        cut.WaitForAssertion(() => Assert.Equal("running", cut.Find("[data-slot=spreadsheet-validation]").GetAttribute("data-state")));
        Assert.Contains("Checking", cut.Find("[data-slot=spreadsheet-validation]").TextContent, StringComparison.Ordinal);

        gate.SetResult();

        cut.WaitForAssertion(() => Assert.Equal("done", cut.Find("[data-slot=spreadsheet-validation]").GetAttribute("data-state")));
        Assert.Equal("sku.unknown", importer.AllRows[0].Issues.Single().Code);
        Assert.Empty(cut.FindAll("[data-slot=spreadsheet-validation-retry]"));
    }

    [Fact]
    public async Task A_failed_check_shows_its_message_and_a_button_to_check_again_without_looping()
    {
        var calls = 0;
        var importer = ValidatingImporter((rows, token) =>
        {
            calls++;
            return calls == 1 ? throw new InvalidOperationException("Service down") : Task.FromResult<IReadOnlyList<SpreadsheetIndexedIssue>?>(null);
        }, batchSize: 0);
        importer.NextStep();
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        cut.WaitForAssertion(() => Assert.Equal("error", cut.Find("[data-slot=spreadsheet-validation]").GetAttribute("data-state")));
        Assert.Contains("Service down", cut.Find("[data-slot=spreadsheet-validation]").TextContent, StringComparison.Ordinal);
        await Task.Delay(100, Token);
        Assert.Equal(1, calls);                                  // an error is not retried by itself

        await cut.InvokeAsync(() => cut.Find("[data-slot=spreadsheet-validation-retry]").Click());

        cut.WaitForAssertion(() => Assert.Equal("done", cut.Find("[data-slot=spreadsheet-validation]").GetAttribute("data-state")));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Without_async_checks_the_component_shows_no_check_state()
    {
        var importer = new SpreadsheetImporter(new SpreadsheetSchema { Columns = [new SpreadsheetColumnSchema { Key = "a" }] });
        importer.LoadText("A\n1", "a.csv");
        importer.NextStep();
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        Assert.Empty(cut.FindAll("[data-slot=spreadsheet-validation]"));
    }
}
