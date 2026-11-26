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
        var schema = SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Key = "rules";
            sb.Column(r => r.Code, col => col.Label("Code").Required().Rule(SpreadsheetRule.Pattern("^[A-Z0-9]{3,5}$")));
            sb.Column(r => r.Score, col => col.Label("Score").Required().Rule(SpreadsheetRule.Between(0d, 100d)));
            sb.Column(r => r.Note, col => col
                .Label("Note")
                .Rule(SpreadsheetRule.Pattern("^.{5,}$", "Short note", level: SpreadsheetIssueLevel.Warning)));
        });

        var importer = new SpreadsheetImporter<ImportRow>(schema);
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
        var keepFirstSchema = SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Key = "dup-first";
            sb.Duplicates = SpreadsheetDuplicatesPolicy.KeepFirst;
            var colSku = sb.Column(r => r.Sku, col => col.Label("SKU").Required());
            sb.Column(r => r.Qty, col => col.Label("Qty"));
            sb.RowKey(colSku);
        });
        var impFirst = new SpreadsheetImporter<ImportRow>(keepFirstSchema);
        impFirst.LoadText("SKU,Qty\nA1,10\nA1,20\nB2,5", "d.csv");
        Assert.Equal(2, impFirst.ImportableRows.Count);
        Assert.Equal(10, impFirst.ImportableTypedRows[0].Value.Qty);

        var keepLastSchema = SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Key = "dup-last";
            sb.Duplicates = SpreadsheetDuplicatesPolicy.KeepLast;
            var colSku = sb.Column(r => r.Sku, col => col.Label("SKU").Required());
            sb.Column(r => r.Qty, col => col.Label("Qty"));
            sb.RowKey(colSku);
        });
        var impLast = new SpreadsheetImporter<ImportRow>(keepLastSchema);
        impLast.LoadText("SKU,Qty\nA1,10\nA1,20\nB2,5", "d.csv");
        Assert.Equal(2, impLast.ImportableRows.Count);
        Assert.Equal(20, impLast.ImportableTypedRows[0].Value.Qty);

        var skipExisting = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Key = "skip-ex";
            sb.ExistingAction = SpreadsheetExistingAction.Skip;
            sb.ExistingRecords = new Dictionary<string, ImportRow>
            {
                ["A1"] = new ImportRow { Sku = "A1" }
            };
            var colSku = sb.Column(r => r.Sku, col => col.Label("SKU").Required());
            sb.RowKey(colSku);
        }));
        skipExisting.LoadText("SKU\nA1\nB2", "s.csv");
        Assert.Single(skipExisting.ImportableRows);
        Assert.Single(skipExisting.DiscardedRows);
    }

    [Fact]
    public async Task SpreadsheetImporter_OnCreateOption_fires_for_Create_answers_and_LeaveEmpty_SkipRows_work()
    {
        var createdOptions = new List<string>();
        var schema = SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Key = "tags";
            sb.Column(r => r.Name, col => col.Label("Name").Required());
            sb.Column(r => r.Tier, col => col
                .Label("Tier")
                .UnknownPolicy(SpreadsheetUnknownValuePolicy.Ask)
                .Options([new SpreadsheetOption<string?>("Std", "Standard")])
                .OnCreateOption(raw =>
                    {
                        createdOptions.Add(raw);
                        return new SpreadsheetOption<string?>(raw, raw);
                    }));
        });

        var importer = new SpreadsheetImporter<ImportRow>(schema);
        importer.LoadText("Name,Tier\nAcme,VIP\nBeta,Legacy\nGamma,Deprecated", "tags.csv");

        Assert.Equal(3, importer.OpenQuestions.Count);
        importer.AnswerValue("Tier", "VIP", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Create));
        importer.AnswerValue("Tier", "Legacy", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.LeaveEmpty));
        importer.AnswerValue("Tier", "Deprecated", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.SkipRows));

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
        var schema = SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Key = "review-ops";
            sb.Column(r => r.Email, col => col.Label("Email").Required().Rule(SpreadsheetRule.Email()));
            sb.Column(r => r.City, col => col.Label("City").Required());
        });

        var importer = new SpreadsheetImporter<ImportRow>(schema);
        importer.LoadText("Email,City\nbad-email,Paris\nok@klyroo.dev,Lyon\nwrong,Bordeaux", "r.csv");

        // Issue row inspection cycling
        importer.InspectNextIssueRow();
        Assert.Equal(0, importer.InspectedRowIndex);
        importer.InspectNextIssueRow();
        Assert.Equal(2, importer.InspectedRowIndex);
        importer.InspectPreviousIssueRow();
        Assert.Equal(0, importer.InspectedRowIndex);

        // Edit & Revert cell
        importer.EditCell(0, "Email", "fixed@klyroo.dev");
        Assert.Equal(2, importer.ImportableRows.Count);
        importer.RevertCell(0, "Email");
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
        importer.IgnoreColumn("City");
        Assert.Single(importer.MissingFields);
        importer.ResetColumn("City");
        Assert.Empty(importer.MissingFields);
    }

    [Fact]
    public async Task SpreadsheetImporter_captures_OnSubmit_exception_in_SubmitError()
    {
        var schema = SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Key = "fail-submit";
            sb.OnSubmit = _ => throw new InvalidOperationException("Database unavailable");
            sb.Column(r => r.Name, col => col.Label("Name").Required());
        });

        var importer = new SpreadsheetImporter<ImportRow>(schema);
        importer.LoadText("Name\nAlice", "a.csv");
        await importer.SubmitAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(SpreadsheetSubmitStatus.Error, importer.SubmitStatus);
        Assert.Equal("Database unavailable", importer.SubmitError);
    }

    [Fact]
    public void KlySpreadsheetImport_renders_columns_step_when_required_field_is_unmatched()
    {
        var schema = SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Key = "unmatched-ui";
            sb.Column(r => r.Sku, col => col.Label("SKU").Required());
            sb.Column(r => r.Price, col => col.Label("Unit Price").Required());
        });

        var importer = new SpreadsheetImporter<ImportRow>(schema);
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));
        importer.LoadText("SKU,MysteryCol\nA1,abc", "u.csv");
        cut.Render();

        cut.Find("[data-slot=spreadsheet-nav-continue]").Click();
        Assert.Equal("columns", importer.CurrentStepKey);
        Assert.Contains("Unit Price", cut.Markup);
    }
}
