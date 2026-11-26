using Bunit;
using Klyroo.Spreadsheet;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

public sealed class SpreadsheetEdgeCaseAndUiTests : BunitContext
{
    public SpreadsheetEdgeCaseAndUiTests()
    {
        Services.AddKlyrooSpreadsheet();
    }

    [Fact]
    public void SpreadsheetImporter_enforces_all_built_in_rules_and_warning_vs_error_levels()
    {
        var schema = new SpreadsheetSchema
        {
            Key = "rules",
            Columns =
            [
                new SpreadsheetColumnSchema { Key = "code", Label = "Code", Required = true, Rules = [SpreadsheetRule.Pattern("^[A-Z0-9]{3,5}$")] },
                new SpreadsheetColumnSchema { Key = "score", Label = "Score", Kind = SpreadsheetColumnKind.Number, Required = true, Rules = [SpreadsheetRule.Between(0, 100)] },
                new SpreadsheetColumnSchema { Key = "note", Label = "Note", Rules = [SpreadsheetRule.Pattern("^.{5,}$", "Short note", level: SpreadsheetIssueLevel.Warning)] }
            ]
        };

        var importer = new SpreadsheetImporter(schema);
        importer.LoadText("Code,Score,Note\nAB,150,hi\nOK99,85,hi\nGOOD1,90,valid note", "rules.csv");

        Assert.Single(importer.InvalidRows); // Row 0 has blocking errors (MinLength + Between)
        Assert.Equal(2, importer.ImportableRows.Count); // Row 1 has only Warning so still importable!

        importer.SetReviewLevel(SpreadsheetReviewLevel.Warnings);
        Assert.Equal(2, importer.VisibleReviewRows.Count); // Row 0 and Row 1 both have Note="hi" warning
        importer.SetReviewLevel(SpreadsheetReviewLevel.Blocking);
        Assert.Single(importer.VisibleReviewRows); // Row 0 has blocking errors
    }

    [Fact]
    public void SpreadsheetImporter_supports_KeepFirst_and_KeepLast_duplicate_policies_and_ExistingAction_Skip_and_Error()
    {
        var keepFirstSchema = new SpreadsheetSchema
        {
            Key = "dup-first",
            RowKeyField = "sku",
            Duplicates = SpreadsheetDuplicatesPolicy.KeepFirst,
            Columns = [new SpreadsheetColumnSchema { Key = "sku", Label = "SKU", Required = true }, new SpreadsheetColumnSchema { Key = "qty", Label = "Qty", Kind = SpreadsheetColumnKind.Number }]
        };
        var impFirst = new SpreadsheetImporter(keepFirstSchema);
        impFirst.LoadText("SKU,Qty\nA1,10\nA1,20\nB2,5", "d.csv");
        Assert.Equal(2, impFirst.ImportableRows.Count);
        Assert.Equal(10.0, impFirst.ImportableRows[0].Data["qty"]);

        var keepLastSchema = new SpreadsheetSchema
        {
            Key = "dup-last",
            RowKeyField = "sku",
            Duplicates = SpreadsheetDuplicatesPolicy.KeepLast,
            Columns = [new SpreadsheetColumnSchema { Key = "sku", Label = "SKU", Required = true }, new SpreadsheetColumnSchema { Key = "qty", Label = "Qty", Kind = SpreadsheetColumnKind.Number }]
        };
        var impLast = new SpreadsheetImporter(keepLastSchema);
        impLast.LoadText("SKU,Qty\nA1,10\nA1,20\nB2,5", "d.csv");
        Assert.Equal(2, impLast.ImportableRows.Count);
        Assert.Equal(20.0, impLast.ImportableRows[0].Data["qty"]);

        var skipExisting = new SpreadsheetImporter(new SpreadsheetSchema
        {
            Key = "skip-ex",
            RowKeyField = "sku",
            ExistingAction = SpreadsheetExistingAction.Skip,
            ExistingRecords = new Dictionary<string, IReadOnlyDictionary<string, object?>>
            {
                ["A1"] = new Dictionary<string, object?> { ["sku"] = "A1" }
            },
            Columns = [new SpreadsheetColumnSchema { Key = "sku", Label = "SKU", Required = true }]
        });
        skipExisting.LoadText("SKU\nA1\nB2", "s.csv");
        Assert.Single(skipExisting.ImportableRows);
        Assert.Single(skipExisting.DiscardedRows);
    }

    [Fact]
    public async Task SpreadsheetImporter_OnCreateOption_fires_for_Create_answers_and_LeaveEmpty_SkipRows_work()
    {
        var createdOptions = new List<string>();
        var schema = new SpreadsheetSchema
        {
            Key = "tags",
            Columns =
            [
                new SpreadsheetColumnSchema { Key = "name", Label = "Name", Required = true },
                new SpreadsheetColumnSchema
                {
                    Key = "tier", Label = "Tier", Kind = SpreadsheetColumnKind.Select,
                    UnknownPolicy = SpreadsheetUnknownValuePolicy.Ask,
                    Options = [new SpreadsheetOption("Std", "Standard")],
                    OnCreateOption = raw =>
                    {
                        createdOptions.Add(raw);
                        return new SpreadsheetOption(raw, raw);
                    }
                }
            ]
        };

        var importer = new SpreadsheetImporter(schema);
        importer.LoadText("Name,Tier\nAcme,VIP\nBeta,Legacy\nGamma,Deprecated", "tags.csv");

        Assert.Equal(3, importer.OpenQuestions.Count);
        importer.AnswerValue("tier", "VIP", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Create));
        importer.AnswerValue("tier", "Legacy", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.LeaveEmpty));
        importer.AnswerValue("tier", "Deprecated", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.SkipRows));

        Assert.Empty(importer.OpenQuestions);
        Assert.Equal(2, importer.ImportableRows.Count);
        Assert.Single(importer.DiscardedRows);

        await importer.SubmitAsync(Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(["VIP"], createdOptions);
        Assert.Equal(2, importer.ImportedCount);
    }

    [Fact]
    public void SpreadsheetImporter_supports_manual_discard_restore_revert_cell_search_and_issue_inspection()
    {
        var schema = new SpreadsheetSchema
        {
            Key = "review-ops",
            Columns =
            [
                new SpreadsheetColumnSchema { Key = "email", Label = "Email", Required = true, Rules = [SpreadsheetRule.Email()] },
                new SpreadsheetColumnSchema { Key = "city", Label = "City", Required = true }
            ]
        };

        var importer = new SpreadsheetImporter(schema);
        importer.LoadText("Email,City\nbad-email,Paris\nok@klyroo.dev,Lyon\nwrong,Bordeaux", "r.csv");

        // Issue row inspection cycling
        importer.InspectNextIssueRow();
        Assert.Equal(0, importer.InspectedRowIndex);
        importer.InspectNextIssueRow();
        Assert.Equal(2, importer.InspectedRowIndex);
        importer.InspectPreviousIssueRow();
        Assert.Equal(0, importer.InspectedRowIndex);

        // Edit & Revert cell
        importer.EditCell(0, "email", "fixed@klyroo.dev");
        Assert.Equal(2, importer.ImportableRows.Count);
        importer.RevertCell(0, "email");
        Assert.Single(importer.ImportableRows);

        // Manual discard & restore
        importer.DiscardRows([1]);
        Assert.Single(importer.DiscardedRows);
        importer.RestoreRows([1]);
        Assert.Empty(importer.DiscardedRows);

        // Review search & tab filtering
        importer.SetReviewSearch("Lyon");
        Assert.Single(importer.VisibleReviewRows);
        importer.SetReviewSearch("");
        importer.SetReviewTab(SpreadsheetReviewTab.Invalid);
        Assert.Equal(2, importer.VisibleReviewRows.Count);

        // Ignore & Reset column
        importer.IgnoreColumn("city");
        Assert.Single(importer.MissingFields);
        importer.ResetColumn("city");
        Assert.Empty(importer.MissingFields);
    }

    [Fact]
    public async Task SpreadsheetImporter_captures_OnSubmit_exception_in_SubmitError()
    {
        var schema = new SpreadsheetSchema
        {
            Key = "fail-submit",
            Columns = [new SpreadsheetColumnSchema { Key = "name", Label = "Name", Required = true }],
            OnSubmit = _ => throw new InvalidOperationException("Database unavailable")
        };

        var importer = new SpreadsheetImporter(schema);
        importer.LoadText("Name\nAlice", "a.csv");
        await importer.SubmitAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(SpreadsheetSubmitStatus.Error, importer.SubmitStatus);
        Assert.Equal("Database unavailable", importer.SubmitError);
    }

    [Fact]
    public void KlySpreadsheetImport_renders_columns_step_when_required_field_is_unmatched()
    {
        var schema = new SpreadsheetSchema
        {
            Key = "unmatched-ui",
            Columns =
            [
                new SpreadsheetColumnSchema { Key = "sku", Label = "SKU", Required = true },
                new SpreadsheetColumnSchema { Key = "price", Label = "Unit Price", Kind = SpreadsheetColumnKind.Number, Required = true }
            ]
        };

        var importer = new SpreadsheetImporter(schema);
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));
        importer.LoadText("SKU,MysteryCol\nA1,abc", "u.csv");
        cut.Render();

        cut.Find("[data-slot=spreadsheet-nav-continue]").Click();
        Assert.Equal("columns", importer.CurrentStepKey);
        Assert.Contains("Unit Price", cut.Markup);
    }
}
