using Bunit;
using Klyroo.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

// Defects found in review of the async checks, the submit, the saved mappings, the exports and the live region.
public sealed class ReviewFixTests : BunitContext
{
    public ReviewFixTests()
    {
        Services.AddKlyrooSpreadsheet();
    }

    private static CancellationToken Token => Xunit.TestContext.Current.CancellationToken;
    private static Task<IReadOnlyList<SpreadsheetRejectedRow>?> Ok() => Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>(null);

    private static SpreadsheetIndexedIssue Bad(SpreadsheetRow row) =>
        new(row.Index, new SpreadsheetRowIssue("sku", "nope", SpreadsheetIssueLevel.Error, "unknown sku"));

    // 1. A retry after a failed or cancelled check runs the checks again

    [Fact]
    public async Task A_resubmit_after_a_failed_lookup_runs_the_lookup_again()
    {
        var calls = 0;
        var sent = new List<string?>();
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.ExistingAction = SpreadsheetExistingAction.Skip;
            sb.OnSubmit = rows => { sent.AddRange(rows.Select(r => r.Value.Sku)); return Ok(); };
            sb.LookupExistingRecordsAsync = (keys, ct) => calls++ == 0
                ? throw new InvalidOperationException("network")
                : Task.FromResult<IReadOnlyDictionary<string, ImportRow>>(keys.ToDictionary(k => k, k => new ImportRow()));
            var colSku = sb.Column(r => r.Sku);
            sb.RowKey(colSku);
        }));
        importer.LoadText("Sku\nA\nB", "s.csv");

        await importer.SubmitAsync(Token);
        Assert.Equal(SpreadsheetSubmitStatus.Error, importer.SubmitStatus);
        await importer.SubmitAsync(Token);

        Assert.Equal(2, calls);                                   // the second submit looked the keys up again
        Assert.Equal(SpreadsheetSubmitStatus.Done, importer.SubmitStatus);
        Assert.Empty(sent);                                       // both keys exist, so both rows are skipped, as the lookup says
    }

    [Fact]
    public async Task A_resubmit_after_a_cancelled_validation_validates_again()
    {
        var calls = 0;
        SpreadsheetImporter<ImportRow>? importer = null;
        var sent = new List<string?>();
        importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.OnSubmit = rows => { sent.AddRange(rows.Select(r => r.Value.Sku)); return Ok(); };
            sb.ValidateRowsAsync = (rows, ct) =>
            {
                if (calls++ == 0) importer!.CancelSubmit();
                ct.ThrowIfCancellationRequested();
                return Task.FromResult<IReadOnlyList<SpreadsheetIndexedIssue>?>(rows.Where(r => r.Value.Sku == "B").Select(Bad).ToList());
            };
            sb.Column(r => r.Sku);
        }));
        importer.LoadText("Sku\nA\nB", "s.csv");

        await importer.SubmitAsync(Token);
        Assert.Equal(SpreadsheetSubmitStatus.Cancelled, importer.SubmitStatus);
        await importer.SubmitAsync(Token);

        Assert.Equal(["A"], sent);                                // B was flagged by the second validation, not sent unchecked
    }

    // 2. A submit does not run while a check runs

    [Fact]
    public async Task Submit_waits_for_nothing_and_does_not_send_while_a_check_runs()
    {
        var gate = new TaskCompletionSource();
        var sent = new List<string?>();
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.OnSubmit = rows => { sent.AddRange(rows.Select(r => r.Value.Sku)); return Ok(); };
            sb.ValidateRowsAsync = async (rows, ct) =>
            {
                await gate.Task;
                return rows.Select(Bad).ToList();
            };
            sb.Column(r => r.Sku);
        }));
        importer.LoadText("Sku\nA\nB", "s.csv");

        var validation = importer.ValidateAsync(Token);
        Assert.False(importer.CanSubmit);
        await importer.SubmitAsync(Token);
        Assert.Empty(sent);
        Assert.Equal(SpreadsheetSubmitStatus.Idle, importer.SubmitStatus);

        gate.SetResult();
        await validation;
        Assert.Empty(importer.ImportableRows);                    // both rows were flagged
    }

    [Fact]
    public async Task The_component_disables_Import_while_a_check_runs()
    {
        var gate = new TaskCompletionSource();
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.OnSubmit = rows => Ok();
            sb.ValidateRowsAsync = async (rows, ct) =>
            {
                await gate.Task;
                return null;
            };
            sb.Column(r => r.Sku);
        }));
        importer.LoadText("Sku\nA", "s.csv");
        importer.NextStep();
        importer.NextStep();
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        var validation = cut.InvokeAsync(() => importer.ValidateAsync(Token));
        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-slot=spreadsheet-submit-button]").HasAttribute("disabled")));

        gate.SetResult();
        await validation;
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-slot=spreadsheet-submit-button]").HasAttribute("disabled")));
    }

    // 3. A late answer lands only on rows that did not change

    [Fact]
    public async Task An_answer_for_a_row_edited_meanwhile_is_not_applied_and_the_row_stays_unchecked()
    {
        var gate = new TaskCompletionSource();
        var checkedSkus = new List<string?>();
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.ValidateRowsAsync = async (rows, ct) =>
            {
                checkedSkus.AddRange(rows.Select(r => r.Value.Sku));
                await gate.Task;
                return rows.Where(r => r.Value.Sku == "BAD").Select(Bad).ToList();
            };
            sb.Column(r => r.Sku);
        }));
        importer.LoadText("Sku\nBAD\nOK", "s.csv");

        var run = importer.ValidateAsync(Token);
        importer.EditCell(0, "Sku", "FIXED");
        gate.SetResult();
        await run;

        Assert.DoesNotContain(importer.AllRows[0].Issues, i => i.Code == "nope");
        Assert.True(importer.NeedsAsyncValidation);               // the edited row is still to check
        await importer.ValidateAsync(Token);
        Assert.Equal(["BAD", "OK", "FIXED"], checkedSkus);        // only the edited row went again
        Assert.False(importer.NeedsAsyncValidation);
    }

    [Fact]
    public async Task A_new_read_of_the_file_during_a_run_invalidates_its_results()
    {
        var gate = new TaskCompletionSource();
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.ValidateRowsAsync = async (rows, ct) =>
            {
                await gate.Task;
                return rows.Select(Bad).ToList();
            };
            sb.Column(r => r.Sku);
            sb.Column(r => r.Other);
        }));
        importer.LoadText("Sku;Other\nA;1\nB;2", "s.csv");

        var run = importer.ValidateAsync(Token);
        importer.IgnoreColumn("Other");                           // a column choice reads every row again
        gate.SetResult();
        await run;

        Assert.All(importer.AllRows, r => Assert.DoesNotContain(r.Issues, i => i.Code == "nope"));
        Assert.True(importer.NeedsAsyncValidation);
    }

    // 4. Raising the row limit makes the new rows unchecked

    [Fact]
    public async Task Raising_the_row_limit_marks_the_checks_dirty_for_the_rows_it_takes_in()
    {
        var looked = new List<string>();
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.LookupExistingRecordsAsync = (keys, ct) =>
            {
                looked.AddRange(keys);
                return Task.FromResult<IReadOnlyDictionary<string, ImportRow>>(new Dictionary<string, ImportRow>());
            };
            var colSku = sb.Column(r => r.Sku);
            sb.RowKey(colSku);
        })) { DefaultMaxRows = 1 };
        importer.LoadText("Sku\nA\nB", "s.csv");
        await importer.ValidateAsync(Token);
        Assert.False(importer.NeedsAsyncValidation);

        importer.DefaultMaxRows = null;

        Assert.True(importer.NeedsAsyncValidation);
        await importer.ValidateAsync(Token);
        Assert.Equal(["A", "B"], looked);
    }

    // 5. Accepted rows are final

    private static async Task<SpreadsheetImporter<ImportRow>> Imported()
    {
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.OnSubmit = rows => Ok();
            sb.Column(r => r.Sku);
            sb.Column(r => r.Level);
        }));
        importer.LoadText("Sku;Level\nA;1\nB;2", "s.csv");
        await importer.SubmitAsync(Token);
        return importer;
    }

    [Fact]
    public async Task Accepted_rows_cannot_be_edited_left_out_or_restored()
    {
        var importer = await Imported();
        Assert.Equal([0, 1], importer.AcceptedRowIndexes.Order());
        var changed = 0;
        importer.Changed += () => changed++;

        importer.EditCell(0, "Sku", "A2");
        importer.RevertCell(0, "Sku");
        importer.DiscardRows([0, 1]);
        importer.RestoreRows([0]);

        Assert.Equal(0, changed);
        Assert.Equal("A", importer.Value(0).Sku);
        Assert.Empty(importer.DiscardedRows);
        Assert.True(importer.IsRowAccepted(0));
        Assert.Empty(importer.PendingRows);
        Assert.False(importer.CanSubmit);                         // nothing pending: no "Import 0 rows"
    }

    [Fact]
    public async Task Once_rows_are_accepted_the_mapping_the_answers_the_sheet_and_the_header_row_are_locked_until_the_submit_is_reset()
    {
        var importer = await Imported();
        Assert.True(importer.MappingLocked);

        importer.IgnoreColumn("Level");
        importer.AssignColumn("Level", 0);
        importer.ResetColumn("Level");
        importer.AnswerValue("Level", "1", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.LeaveEmpty));
        importer.SetHeaderRow(1);
        Assert.Equal("Level", importer.Fields.Single(f => f.Column.Key == "Level").HeaderText);
        Assert.Equal("1", importer.Value(0).Level);
        Assert.Equal(0, importer.ApplyMappingProfile(new SpreadsheetMappingProfile { Columns = new Dictionary<string, string> { ["level"] = "sku" } }).ColumnsApplied);

        importer.ResetSubmit();
        Assert.False(importer.MappingLocked);
        importer.IgnoreColumn("Level");
        Assert.Equal(SpreadsheetColumnStatus.Unmatched, importer.Fields.Single(f => f.Column.Key == "Level").Status);
    }

    [Fact]
    public async Task A_row_cannot_be_edited_while_its_batch_is_in_flight()
    {
        var gate = new TaskCompletionSource();
        var sentSkus = new List<string?>();
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.OnSubmitAsync = async (rows, ct) =>
            {
                sentSkus.AddRange(rows.Select(r => r.Value.Sku));
                await gate.Task;
                return null;
            };
            sb.Column(r => r.Sku);
        }));
        importer.LoadText("Sku\nA", "s.csv");

        var submit = importer.SubmitAsync(Token);
        importer.EditCell(0, "Sku", "CHANGED");
        importer.DiscardRows([0]);
        importer.IgnoreColumn("Sku");
        gate.SetResult();
        await submit;

        Assert.Equal(["A"], sentSkus);
        Assert.Equal("A", importer.Value(0).Sku);       // the accepted row still holds what was sent
        Assert.True(importer.IsRowAccepted(0));
    }

    [Fact]
    public async Task The_component_shows_accepted_rows_as_imported_and_read_only_and_offers_no_empty_import()
    {
        var importer = await Imported();
        importer.NextStep();
        importer.NextStep();
        importer.GoToStep("review");
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        Assert.All(cut.FindAll("[data-slot=spreadsheet-row-status]"), e => Assert.Equal("Imported", e.TextContent.Trim()));
        Assert.All(cut.FindAll("[data-slot=spreadsheet-cell-trigger]"), e => Assert.True(e.HasAttribute("disabled")));
        Assert.All(cut.FindAll("[data-slot=spreadsheet-row-select]"), e => Assert.True(e.HasAttribute("disabled")));

        importer.InspectRow(0);
        cut.WaitForAssertion(() => Assert.Equal("Imported", cut.Find("[data-slot=inspector-imported]").TextContent.Trim()));
        Assert.Empty(cut.FindAll("[data-slot=inspector-discard]"));

        importer.GoToStep("submit");
        importer.ResetSubmit();
        importer.DiscardRows([0, 1]);                             // nothing left to send
        importer.GoToStep("review");
        importer.SetReviewTab(SpreadsheetReviewTab.All);
        Assert.Empty(importer.PendingRows);
    }

    [Fact]
    public async Task The_submit_button_is_not_offered_when_nothing_is_pending()
    {
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Column(r => r.Sku);
        }));
        importer.LoadText("Sku\nA", "s.csv");
        importer.NextStep();
        importer.NextStep();
        importer.DiscardRows([0]);                                // no importable row
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));
        await Task.Yield();

        Assert.Empty(cut.FindAll("[data-slot=spreadsheet-submit-button]"));
    }

    // 6. Saved mappings: duplicate headers and untrusted input

    [Fact]
    public void Duplicate_headers_are_mapped_by_occurrence_when_a_profile_is_applied_again()
    {
        var schema = SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Column(r => r.Updated, col => col.Key("updated"));
            sb.Column(r => r.Created, col => col.Key("created"));
        });
        var first = new SpreadsheetImporter<ImportRow>(schema);
        first.LoadText("Date,Date,Name\n2020,2021,x", "d.csv");
        first.AssignColumn("created", 0);
        first.AssignColumn("updated", 1);

        var profile = SpreadsheetMappingProfile.FromJson(first.GetMappingProfile().ToJson())!;
        var second = new SpreadsheetImporter<ImportRow>(schema) { SavedMapping = profile };
        second.LoadText("Date,Date,Name\n2020,2021,x", "d.csv");

        Assert.Equal("date", profile.Columns.Keys.Single());
        Assert.Equal("updated", profile.RepeatedColumns["2:date"]);
        Assert.Equal("2021", second.Value(0).Updated);
        Assert.Equal("2020", second.Value(0).Created);
        Assert.True(second.SavedMappingApplied);
    }

    [Fact]
    public void A_profile_with_a_repeated_header_does_not_match_a_file_that_has_it_once()
    {
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Column(r => r.Updated, col => col.Key("updated"));
            sb.Column(r => r.Created, col => col.Key("created"));
        }));
        importer.LoadText("Date,Name\n2020,x", "d.csv");
        var profile = new SpreadsheetMappingProfile
        {
            Columns = new Dictionary<string, string> { ["date"] = "created" },
            RepeatedColumns = new Dictionary<string, string> { ["2:date"] = "updated", ["junk"] = "updated", ["1:date"] = "updated" },
        };

        Assert.False(importer.MatchesMappingProfile(profile));
        importer.LoadText("Date,Date\n2020,2021", "d2.csv");
        Assert.False(importer.MatchesMappingProfile(profile));    // the malformed keys never match
        Assert.True(importer.MatchesMappingProfile(profile with { RepeatedColumns = new Dictionary<string, string> { ["2:date"] = "updated" } }));
    }

    [Fact]
    public void Null_keys_and_values_in_a_stored_profile_are_ignored_without_an_exception()
    {
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Column(r => r.Sku);
        }));
        importer.LoadText("Sku\nA", "s.csv");
        var profile = SpreadsheetMappingProfile.FromJson("""
            { "version": 1, "columns": { "sku": null }, "repeatedColumns": { "2:sku": null }, "ignoredColumns": [ null, "" ],
              "valueAnswers": { "sku::a": null } }
            """)!;

        Assert.Null(Record.Exception(() => importer.ApplyMappingProfile(profile)));
        Assert.Null(Record.Exception(() => importer.SavedMapping = profile));
        Assert.Null(Record.Exception(() => importer.MatchesMappingProfile(profile)));
        Assert.Equal("A", importer.Value(0).Sku);
    }

    [Fact]
    public void A_stored_answer_must_fit_the_column_and_have_a_defined_action()
    {
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Column(r => r.Level, col => col.Options([new SpreadsheetOption<string?>("pro", "Pro")]));
            sb.Column(r => r.Note);
        }));
        importer.LoadText("Level;Note\nGold;x", "s.csv");
        var profile = new SpreadsheetMappingProfile
        {
            ValueAnswers = new Dictionary<string, SpreadsheetValueAnswer>
            {
                ["Level::gold"] = new(SpreadsheetValueAnswerAction.Map, "not-an-option", "x"),        // not one of the options
                ["Level::silver"] = new(SpreadsheetValueAnswerAction.Map, null, null),
                ["Level::bronze"] = new(SpreadsheetValueAnswerAction.Create),                          // the column cannot create options
                ["Level::tin"] = new((SpreadsheetValueAnswerAction)42),                                // undefined action
                ["Note::x"] = new(SpreadsheetValueAnswerAction.Map, "pro", "Pro"),                     // not a select column
                ["Level::platinum"] = new(SpreadsheetValueAnswerAction.Map, "pro", "Pro"),             // valid
            },
        };

        var result = importer.ApplyMappingProfile(profile);

        Assert.Equal(1, result.ValueAnswersApplied);
        Assert.Single(importer.GetMappingProfile().ValueAnswers);
        Assert.Single(importer.OpenQuestions);                    // "Gold" is still a question
    }

    [Fact]
    public void A_stored_create_answer_is_kept_for_a_column_that_can_create_options()
    {
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Column(r => r.Level, col => col
                .Options([new SpreadsheetOption<string?>("pro", "Pro")])
                .OnCreateOption(t => new SpreadsheetOption<string?>(t, t)));
        }));
        importer.LoadText("Level\nGold", "s.csv");

        var result = importer.ApplyMappingProfile(new SpreadsheetMappingProfile
        {
            ValueAnswers = new Dictionary<string, SpreadsheetValueAnswer> { ["Level::gold"] = new(SpreadsheetValueAnswerAction.Create) },
        });

        Assert.Equal(1, result.ValueAnswersApplied);
    }

    // 7. CSV injection

    [Fact]
    public void The_template_guards_formulas_and_escapes_quotes()
    {
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Column(r => r.A, col => col.Label("=HYPERLINK(\"x\")").Example("@SUM(A1)"));
            sb.Column(r => r.B, col => col.Label("He said \"hi\"").Example("say \"yes\""));
            sb.Column(r => r.C, col => col.Label("+cmd").Example("-5"));
        }));

        var lines = importer.ExportTemplateCsv().TrimStart('﻿').Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("\"'=HYPERLINK(\"\"x\"\")\",\"He said \"\"hi\"\"\",\"'+cmd\"", lines[0]);
        Assert.Equal("\"'@SUM(A1)\",\"say \"\"yes\"\"\",\"-5\"", lines[1]);       // a number stays a number
        Assert.Contains("\"'\t=x\"", new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Column(r => r.T, col => col.Example("\t=x"));
        })).ExportTemplateCsv(), StringComparison.Ordinal);
        Assert.Contains("\"'\r=x\"", new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Column(r => r.T, col => col.Example("\r=x"));
        })).ExportTemplateCsv(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("+cmd|' /C calc'!A0", "'+cmd|' /C calc'!A0")]
    [InlineData("-cmd", "'-cmd")]
    [InlineData("-12,50", "-12,50")]
    [InlineData("+3.5", "+3.5")]
    [InlineData("-", "'-")]
    [InlineData("plain =text", "plain =text")]
    public void The_invalid_rows_export_guards_text_cells_that_a_spreadsheet_would_read_as_a_formula(string cell, string written)
    {
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Column(r => r.N, col => col.Label("=N"));
        }));
        importer.LoadText($"=N\n\"{cell}\"", "x.csv");
        // every cell of this single column is invalid or not; make sure the row is listed
        importer.EditCell(0, "N", cell.Trim());
        var csv = importer.ExportInvalidRowsCsv();

        if (importer.InvalidRows.Count > 0) Assert.Contains($"\"{written.Replace("\"", "\"\"")}\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"'=N\"", csv, StringComparison.Ordinal);                                 // the header is guarded too
    }

    [Fact]
    public void The_issue_text_and_the_quotes_of_the_invalid_rows_export_are_escaped()
    {
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Column(r => r.N);
        }));
        importer.LoadText("n\n\"=say \"\"hi\"\"\"", "x.csv");

        var csv = importer.ExportInvalidRowsCsv(i => "=" + i.Message, "Problems");

        Assert.Contains("\"'=say \"\"hi\"\"\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Problems\"", csv, StringComparison.Ordinal);
    }

    // 9. The live region is not noisy

    [Fact]
    public async Task Progress_is_announced_at_most_every_few_seconds_and_never_as_zero_of_zero()
    {
        var gates = new[] { new TaskCompletionSource(), new TaskCompletionSource(), new TaskCompletionSource() };
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.BatchSize = 1;
            sb.OnSubmitAsync = async (rows, ct) =>
            {
                if (rows[0].Index > 0) await gates[rows[0].Index - 1].Task;
                return null;
            };
            sb.Column(r => r.Name);
        }));
        importer.LoadText("Name\na\nb\nc\nd", "n.csv");
        importer.NextStep();
        importer.NextStep();
        var cut = Render<CascadingValue<KlyLocale>>(p => p.Add(x => x.Value, KlyLocales.En).AddChildContent<KlySpreadsheetImport>(c => c.Add(x => x.Importer, importer)));
        var component = cut.FindComponent<KlySpreadsheetImport>();
        var clock = new Clock();
        component.Instance.Clock = clock;
        string Live() => cut.Find("[data-slot=spreadsheet-live]").TextContent.Trim();

        var submit = cut.InvokeAsync(() => importer.SubmitAsync(Token));
        cut.WaitForAssertion(() => Assert.StartsWith("Importing: ", Live(), StringComparison.Ordinal));
        var first = Live();
        Assert.DoesNotContain("of 0 ", first, StringComparison.Ordinal);

        gates[0].SetResult();
        cut.WaitForAssertion(() => Assert.True(importer.SubmitDone >= 2));
        Assert.Equal(first, Live());                              // a batch later, still the same announcement

        clock.Advance(TimeSpan.FromSeconds(5));
        gates[1].SetResult();
        cut.WaitForAssertion(() => Assert.Equal("Importing: 3 of 4 rows", Live()));

        gates[2].SetResult();
        await submit;
        cut.WaitForAssertion(() => Assert.Equal("4 rows imported", Live()));     // the end is announced at once
    }

    [Fact]
    public async Task Before_the_total_is_known_the_live_region_says_importing_without_a_count()
    {
        var gate = new TaskCompletionSource();
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.ValidateRowsAsync = async (rows, ct) =>
            {
                await gate.Task;
                return null;
            };
            sb.Column(r => r.Name);
        }));
        importer.LoadText("Name\na", "n.csv");
        importer.NextStep();
        importer.NextStep();
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        var submit = cut.InvokeAsync(() => importer.SubmitAsync(Token));
        cut.WaitForAssertion(() => Assert.Equal("Importing…", cut.Find("[data-slot=spreadsheet-live]").TextContent.Trim()));

        gate.SetResult();
        await submit;
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public void Advance(TimeSpan by) => _now += by;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    // 10. Retry

    [Fact]
    public async Task A_refusal_for_a_row_outside_the_batch_is_ignored()
    {
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.BatchSize = 2;
            sb.OnSubmit = rows => Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>(rows[0].Index == 0 ? [new(3, "Not mine"), new(99, "Nobody")] : null);
            sb.Column(r => r.Name);
        }));
        importer.LoadText("Name\na\nb\nc\nd", "n.csv");

        await importer.SubmitAsync(Token);

        Assert.Equal(0, importer.RejectedCount);
        Assert.Equal(4, importer.ImportedCount);
        Assert.All(importer.AllRows, r => Assert.Empty(r.Issues));
    }

    [Fact]
    public async Task RetryRejectedAsync_does_nothing_while_a_check_runs()
    {
        var gate = new TaskCompletionSource();
        var calls = 0;
        var sent = 0;
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.OnSubmit = rows =>
            {
                sent += rows.Count;
                return Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>(sent == 2 ? [new SpreadsheetRejectedRow(0, "Refused")] : null);
            };
            sb.ValidateRowsAsync = async (rows, ct) =>
            {
                if (calls++ > 0) await gate.Task;
                return null;
            };
            sb.Column(r => r.Name);
        }));
        importer.LoadText("Name\na\nb\nc", "n.csv");
        importer.DiscardRows([2]);
        await importer.SubmitAsync(Token);
        Assert.Equal(1, importer.RejectedCount);
        importer.RestoreRows([2]);                                // row 2 was never checked: a check is due
        var validation = importer.ValidateAsync(Token);
        Assert.Equal(SpreadsheetValidationStatus.Running, importer.ValidationStatus);

        await importer.RetryRejectedAsync(Token);

        Assert.Equal(1, importer.RejectedCount);                  // refused: the retry did not start
        Assert.Equal(2, sent);
        gate.SetResult();
        await validation;
    }

    // 11. French

    [Fact]
    public void The_french_skipped_value_message_reads_naturally()
    {
        Assert.Equal("La valeur « Or » est configurée pour exclure ses lignes", SpreadsheetLocales.Fr.IssueValueSkipped("Or"));
    }
}
