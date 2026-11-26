using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

// A failed or cancelled submit keeps what the host accepted, and a retry sends only the rest.
[PinCulture]
public sealed class SubmitResumeTests : BunitContext
{
    public SubmitResumeTests()
    {
        Services.AddKlyrooSpreadsheet();
    }

    private static CancellationToken Token => Xunit.TestContext.Current.CancellationToken;

    private static string Csv(int rows) => "Name\n" + string.Join('\n', Enumerable.Range(0, rows).Select(i => $"row{i}"));

    private static SpreadsheetImporter<ImportRow> NewImporter(
        Func<IReadOnlyList<SpreadsheetRow<ImportRow>>, CancellationToken, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>? onSubmitAsync = null,
        Func<IReadOnlyList<SpreadsheetRow<ImportRow>>, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>? onSubmit = null,
        int batchSize = 2,
        int rows = 6)
    {
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.BatchSize = batchSize;
            sb.OnSubmit = onSubmit;
            sb.OnSubmitAsync = onSubmitAsync;
            sb.Column(r => r.Name);
        }));
        importer.LoadText(Csv(rows), "names.csv");
        return importer;
    }

    private static Task<IReadOnlyList<SpreadsheetRejectedRow>?> Accepted() => Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>(null);

    [Fact]
    public async Task A_failed_batch_does_not_make_a_retry_send_the_accepted_batches_again()
    {
        var sent = new List<int>();
        var fail = true;
        var importer = NewImporter(onSubmit: rows =>
        {
            if (fail && rows[0].Index == 4) throw new InvalidOperationException("Database unavailable");
            sent.AddRange(rows.Select(r => r.Index));
            return Accepted();
        });

        await importer.SubmitAsync(Token);

        Assert.Equal(SpreadsheetSubmitStatus.Error, importer.SubmitStatus);
        Assert.Null(importer.SubmitError);
        Assert.Equal([0, 1, 2, 3], sent);
        Assert.Equal(4, importer.ImportedCount);
        Assert.Equal([0, 1, 2, 3], importer.AcceptedRowIndexes.Order());
        Assert.Equal([4, 5], importer.PendingRows.Select(r => r.Index));
        Assert.Equal([0, 1, 2, 3], importer.AcceptedRows.Select(r => r.Index));

        fail = false;
        await importer.SubmitAsync(Token);

        Assert.Equal(SpreadsheetSubmitStatus.Done, importer.SubmitStatus);
        Assert.Null(importer.SubmitError);
        Assert.Equal([0, 1, 2, 3, 4, 5], sent);          // each row was sent once
        Assert.Equal(6, importer.ImportedCount);
        Assert.Empty(importer.PendingRows);
        Assert.Equal(2, importer.SubmitTotal);
        Assert.Equal(2, importer.SubmitDone);
    }

    [Fact]
    public async Task The_rejections_of_the_batches_before_a_failure_are_kept()
    {
        var importer = NewImporter(onSubmit: rows =>
        {
            if (rows[0].Index == 2) throw new InvalidOperationException("boom");
            return Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>([new SpreadsheetRejectedRow(rows[0].Index, "Duplicate name", "name")]);
        });

        await importer.SubmitAsync(Token);

        Assert.Equal(SpreadsheetSubmitStatus.Error, importer.SubmitStatus);
        Assert.Equal(1, importer.RejectedCount);
        Assert.Equal(1, importer.ImportedCount);          // row 1 was accepted, row 0 refused
        Assert.Equal("server", Assert.Single(importer.AllRows[0].Issues).Code);
        Assert.False(importer.AllRows[0].Importable);
    }

    [Fact]
    public async Task Cancelling_stops_before_the_next_batch_and_a_later_submit_resumes()
    {
        var sent = new List<int>();
        SpreadsheetImporter<ImportRow>? importer = null;
        importer = NewImporter(onSubmitAsync: (rows, token) =>
        {
            sent.AddRange(rows.Select(r => r.Index));
            if (rows[0].Index == 2) importer!.CancelSubmit();
            return Accepted();
        });

        await importer.SubmitAsync(Token);

        Assert.Equal(SpreadsheetSubmitStatus.Cancelled, importer.SubmitStatus);
        Assert.Null(importer.SubmitError);
        Assert.Equal([0, 1, 2, 3], sent);                 // the batch that was running finished, the next one never started
        Assert.Equal(4, importer.ImportedCount);

        await importer.SubmitAsync(Token);

        Assert.Equal(SpreadsheetSubmitStatus.Done, importer.SubmitStatus);
        Assert.Equal([0, 1, 2, 3, 4, 5], sent);
        Assert.Equal(6, importer.ImportedCount);
    }

    [Fact]
    public async Task Cancelling_cancels_the_batch_in_flight_through_its_token_and_that_batch_counts_as_not_accepted()
    {
        var started = new TaskCompletionSource();
        var sent = new List<int>();
        var importer = NewImporter(onSubmitAsync: async (rows, token) =>
        {
            if (rows[0].Index == 2)
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);   // a slow call that honors the token
            }
            sent.AddRange(rows.Select(r => r.Index));
            return null;
        });

        var submit = importer.SubmitAsync(Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
        Assert.Equal(SpreadsheetSubmitStatus.Running, importer.SubmitStatus);
        importer.CancelSubmit();
        await submit;

        Assert.Equal(SpreadsheetSubmitStatus.Cancelled, importer.SubmitStatus);
        Assert.Equal([0, 1], sent);
        Assert.Equal([0, 1], importer.AcceptedRowIndexes.Order());
        Assert.Equal([2, 3, 4, 5], importer.PendingRows.Select(r => r.Index));
    }

    [Fact]
    public async Task A_token_passed_to_SubmitAsync_cancels_like_the_Cancel_button()
    {
        using var source = new CancellationTokenSource();
        var importer = NewImporter(onSubmitAsync: (rows, token) =>
        {
            if (rows[0].Index == 0) source.Cancel();
            token.ThrowIfCancellationRequested();
            return Accepted();
        });

        await importer.SubmitAsync(source.Token);

        Assert.Equal(SpreadsheetSubmitStatus.Cancelled, importer.SubmitStatus);
        Assert.Equal(0, importer.ImportedCount);
    }

    [Fact]
    public async Task An_OperationCanceledException_the_host_throws_on_its_own_is_not_caught_and_leaves_an_error_status()
    {
        var importer = NewImporter(onSubmitAsync: (rows, token) => throw new OperationCanceledException("The server timed out"));

        await Assert.ThrowsAsync<OperationCanceledException>(() => importer.SubmitAsync(Token));

        Assert.Equal(SpreadsheetSubmitStatus.Error, importer.SubmitStatus);
        Assert.Null(importer.SubmitError);
    }

    [Fact]
    public async Task OnSubmitAsync_wins_over_OnSubmit()
    {
        var which = new List<string>();
        var importer = NewImporter(
            onSubmitAsync: (rows, token) => { which.Add("async"); return Accepted(); },
            onSubmit: rows => { which.Add("sync"); return Accepted(); },
            batchSize: 0);

        await importer.SubmitAsync(Token);

        Assert.Equal(["async"], which);
    }

    [Fact]
    public async Task Rows_a_batch_refused_are_not_accepted_and_the_others_of_that_batch_are()
    {
        var importer = NewImporter(onSubmit: rows => Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>(
            rows.Any(r => r.Index == 3) ? [new SpreadsheetRejectedRow(3, "Refused")] : null));

        await importer.SubmitAsync(Token);

        Assert.Equal(SpreadsheetSubmitStatus.Done, importer.SubmitStatus);
        Assert.Equal([0, 1, 2, 4, 5], importer.AcceptedRowIndexes.Order());
        Assert.Equal(1, importer.RejectedCount);
        Assert.Empty(importer.PendingRows);                // the refused row is blocked, not pending
    }

    [Fact]
    public async Task A_second_submit_after_a_complete_one_sends_nothing_until_the_submit_is_reset()
    {
        var sent = 0;
        var importer = NewImporter(onSubmit: rows => { sent += rows.Count; return Accepted(); });

        await importer.SubmitAsync(Token);
        await importer.SubmitAsync(Token);
        Assert.Equal(6, sent);

        importer.ResetSubmit();
        Assert.Equal(SpreadsheetSubmitStatus.Idle, importer.SubmitStatus);
        Assert.Equal(0, importer.ImportedCount);
        await importer.SubmitAsync(Token);
        Assert.Equal(12, sent);
    }

    [Fact]
    public async Task Loading_another_file_or_choosing_another_header_row_forgets_what_was_accepted()
    {
        var importer = NewImporter(onSubmit: rows => Accepted());
        await importer.SubmitAsync(Token);
        Assert.Equal(6, importer.ImportedCount);

        importer.LoadText(Csv(3), "other.csv");

        Assert.Equal(0, importer.ImportedCount);
        Assert.Empty(importer.AcceptedRowIndexes);
        Assert.Equal(SpreadsheetSubmitStatus.Idle, importer.SubmitStatus);
        Assert.Equal(3, importer.PendingRows.Count);
    }

    [Fact]
    public async Task A_row_fixed_after_a_server_rejection_is_sent_by_the_next_submit_without_the_accepted_rows()
    {
        var sent = new List<int>();
        var importer = NewImporter(onSubmit: rows =>
        {
            sent.AddRange(rows.Select(r => r.Index));
            return Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>(rows.Any(r => r.Index == 1) && sent.Count(i => i == 1) == 1 ? [new SpreadsheetRejectedRow(1, "Refused")] : null);
        }, batchSize: 3);

        await importer.SubmitAsync(Token);
        Assert.Equal(1, importer.RejectedCount);

        importer.EditCell(1, "Name", "fixed");
        Assert.Equal(0, importer.RejectedCount);
        await importer.SubmitAsync(Token);

        Assert.Equal([0, 1, 2, 3, 4, 5, 1], sent);
        Assert.Equal(6, importer.ImportedCount);
    }

    [Fact]
    public async Task A_failing_OnCreateOption_sends_nothing_and_the_retry_creates_the_options_again()
    {
        var sent = 0;
        var attempts = 0;
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.OnSubmit = rows => { sent += rows.Count; return Accepted(); };
            sb.Column(r => r.Level, col => col
                .Options([new SpreadsheetOption<string?>("a", "A")])
                .OnCreateOption(text => attempts++ == 0 ? throw new InvalidOperationException("no") : new SpreadsheetOption<string?>(text.ToLowerInvariant(), text)));
        }));
        importer.LoadText("Level\nZ\nA", "levels.csv");
        importer.AnswerValue("Level", "Z", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Create));

        await importer.SubmitAsync(Token);
        Assert.Equal(SpreadsheetSubmitStatus.Error, importer.SubmitStatus);
        Assert.Equal(0, sent);

        await importer.SubmitAsync(Token);
        Assert.Equal(SpreadsheetSubmitStatus.Done, importer.SubmitStatus);
        Assert.Equal(2, sent);
    }

    // The component

    [Fact]
    public async Task The_Cancel_button_shows_while_submitting_and_stops_the_import()
    {
        var release = new TaskCompletionSource();
        var importer = NewImporter(onSubmitAsync: async (rows, token) =>
        {
            if (rows[0].Index == 2) await release.Task.WaitAsync(token);
            return null;
        });
        importer.NextStep();
        importer.NextStep();
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        var submit = cut.InvokeAsync(() => importer.SubmitAsync(Token));
        cut.WaitForAssertion(() => Assert.Equal("Cancel", cut.Find("[data-slot=spreadsheet-cancel-button]").TextContent.Trim()));
        Assert.Empty(cut.FindAll("[data-slot=spreadsheet-submit-button]"));

        cut.Find("[data-slot=spreadsheet-cancel-button]").Click();
        await submit;

        Assert.Equal(SpreadsheetSubmitStatus.Cancelled, importer.SubmitStatus);
        cut.WaitForAssertion(() => Assert.Contains("cancelled", cut.Find("[data-slot=spreadsheet-progress-label]").TextContent, StringComparison.Ordinal));
        Assert.Contains("2 rows are already imported", cut.Find("[data-slot=spreadsheet-submit-resume]").TextContent, StringComparison.Ordinal);
        Assert.Equal("Resume the import", cut.Find("[data-slot=spreadsheet-submit-button]").TextContent.Trim());
    }

    [Fact]
    public async Task A_failed_import_shows_a_generic_error_and_a_button_that_resumes_without_resending()
    {
        var sent = new List<int>();
        var fail = true;
        var importer = NewImporter(onSubmit: rows =>
        {
            if (fail && rows[0].Index == 4) throw new InvalidOperationException("Database unavailable");
            sent.AddRange(rows.Select(r => r.Index));
            return Accepted();
        });
        importer.NextStep();
        importer.NextStep();
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        await cut.InvokeAsync(() => importer.SubmitAsync(Token));

        Assert.Equal("The import failed", cut.Find("[data-slot=spreadsheet-submit-error]").TextContent.Trim());
        Assert.DoesNotContain("Database unavailable", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("Try again", cut.Find("[data-slot=spreadsheet-submit-button]").TextContent.Trim());

        fail = false;
        await cut.InvokeAsync(() => cut.Find("[data-slot=spreadsheet-submit-button]").Click());
        cut.WaitForAssertion(() => Assert.Equal(SpreadsheetSubmitStatus.Done, importer.SubmitStatus));

        Assert.Equal([0, 1, 2, 3, 4, 5], sent);
        Assert.Empty(cut.FindAll("[data-slot=spreadsheet-submit-error]"));
    }

    [Fact]
    public void The_import_button_counts_the_rows_still_to_send()
    {
        var importer = NewImporter(onSubmit: rows => Accepted());
        importer.NextStep();
        importer.NextStep();
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        Assert.Equal("Import 6 rows", cut.Find("[data-slot=spreadsheet-submit-button]").TextContent.Trim());
    }
}
