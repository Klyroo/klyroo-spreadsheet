using Klyroo.Spreadsheet;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

public sealed class ComprehensiveSpreadsheetTests
{
    [Fact]
    public void SpreadsheetImporter_detects_header_row_normalizes_NFD_and_suggests_at_80_percent()
    {
        Assert.Equal("prenom", SpreadsheetImporter.NormalizeText("Prénom * (required)"));

        var schema = SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Key = "rates";
            sb.Column(r => r.Code, col => col.Label("Code").Required());
            sb.Column(r => r.Price, col => col.Label("Unit Price").DecimalSeparator(',').Required().Rule(SpreadsheetRule.Min(0d)));
        });

        var importer = new SpreadsheetImporter<ImportRow>(schema);
        importer.LoadText("Export Report 2026;\nCode;Amount\nA1;12,50\nA2;45,00", "rates.csv");

        Assert.True(importer.FileLoaded);
        Assert.Equal(2, importer.HeaderRowNumber);
        Assert.Single(importer.MissingFields);
        Assert.Equal("Amount", importer.MissingFields[0].Suggestion?.HeaderText);

        importer.AssignColumn("Price", importer.MissingFields[0].Suggestion!.HeaderIndex);
        Assert.Empty(importer.MissingFields);
        Assert.Equal(2, importer.ImportableRows.Count);
        Assert.Contains("Code", importer.ExportTemplateCsv());
    }

    [Fact]
    public async Task SpreadsheetImporter_reconciles_unknowns_duplicates_upsert_and_retries_rejected_batches()
    {
        var rejectRow1 = true;
        var schema = SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Key = "users";
            sb.Duplicates = SpreadsheetDuplicatesPolicy.Error;
            sb.ExistingAction = SpreadsheetExistingAction.Update;
            sb.ExistingRecords = new Dictionary<string, ImportRow>
            {
                ["bob@example.com"] = new ImportRow { Email = "bob@example.com", Level = "B1" }
            };
            sb.BatchSize = 2;
            sb.OnSubmit = batch =>
            {
                var rej = new List<SpreadsheetRejectedRow>();
                if (rejectRow1 && batch.Any(r => r.Index == 1))
                    rej.Add(new SpreadsheetRejectedRow(1, "Server rejected row 1", "email"));
                return Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>(rej);
            };
            var colEmail = sb.Column(r => r.Email, col => col.Label("Email").Required().Rule(SpreadsheetRule.Email()));
            sb.Column(r => r.Level, col => col
                .Label("Level")
                .Required()
                .UnknownPolicy(SpreadsheetUnknownValuePolicy.Ask)
                .Options([new SpreadsheetOption<string?>("B2", "B2")]));
            sb.RowKey(colEmail);
        });

        var importer = new SpreadsheetImporter<ImportRow>(schema);
        importer.LoadText("Email;Level\nalice@example.com;C1-Adv\nalice@example.com;B2\nbob@example.com;B2", "users.csv");

        Assert.Single(importer.OpenQuestions);
        importer.AnswerValue("Level", "C1-Adv", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Map, "B2", "B2"));
        Assert.Empty(importer.OpenQuestions);

        // Fix duplicate email on row 1
        Assert.NotEmpty(importer.InvalidRows);
        importer.EditCell(1, "Email", "claire@example.com");
        Assert.Empty(importer.InvalidRows);
        Assert.Equal(SpreadsheetRowMode.Update, importer.AllRows[2].Mode);

        // Submit -> row 1 rejected by server -> retry rejected batch
        await importer.SubmitAsync(Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(1, importer.RejectedCount);
        Assert.Equal(2, importer.ImportedCount);

        rejectRow1 = false;
        await importer.RetryRejectedAsync(Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(0, importer.RejectedCount);
        // A running total: 2 rows from the submit plus the 1 the retry got accepted.
        Assert.Equal(3, importer.ImportedCount);
        Assert.Equal(SpreadsheetSubmitStatus.Done, importer.SubmitStatus);
    }
}
