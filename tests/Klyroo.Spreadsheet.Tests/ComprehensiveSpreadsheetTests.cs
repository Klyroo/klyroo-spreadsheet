using Klyroo.Spreadsheet;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

public sealed class ComprehensiveSpreadsheetTests
{
    [Fact]
    public void SpreadsheetImporter_detects_header_row_normalizes_NFD_and_suggests_at_80_percent()
    {
        Assert.Equal("prenom", SpreadsheetImporter.NormalizeText("Prénom * (required)"));

        var schema = new SpreadsheetSchema
        {
            Key = "rates",
            Columns =
            [
                new SpreadsheetColumnSchema { Key = "code", Label = "Code", Required = true },
                new SpreadsheetColumnSchema { Key = "price", Label = "Unit Price", Kind = SpreadsheetColumnKind.Number, DecimalSeparator = ',', Required = true, Rules = [SpreadsheetRule.Min(0)] }
            ]
        };

        var importer = new SpreadsheetImporter(schema);
        importer.LoadText("Export Report 2026;\nCode;Amount\nA1;12,50\nA2;45,00", "rates.csv");

        Assert.True(importer.FileLoaded);
        Assert.Equal(2, importer.HeaderRowNumber);
        Assert.Single(importer.MissingFields);
        Assert.Equal("Amount", importer.MissingFields[0].Suggestion?.HeaderText);

        importer.AssignColumn("price", importer.MissingFields[0].Suggestion!.HeaderIndex);
        Assert.Empty(importer.MissingFields);
        Assert.Equal(2, importer.ImportableRows.Count);
        Assert.Contains("Code", importer.ExportTemplateCsv());
    }

    [Fact]
    public async Task SpreadsheetImporter_reconciles_unknowns_duplicates_upsert_and_retries_rejected_batches()
    {
        var rejectRow1 = true;
        var schema = new SpreadsheetSchema
        {
            Key = "users",
            RowKeyField = "email",
            Duplicates = SpreadsheetDuplicatesPolicy.Error,
            ExistingAction = SpreadsheetExistingAction.Update,
            ExistingRecords = new Dictionary<string, IReadOnlyDictionary<string, object?>>
            {
                ["bob@example.com"] = new Dictionary<string, object?> { ["email"] = "bob@example.com", ["level"] = "B1" }
            },
            BatchSize = 2,
            Columns =
            [
                new SpreadsheetColumnSchema { Key = "email", Label = "Email", Required = true, Rules = [SpreadsheetRule.Email()] },
                new SpreadsheetColumnSchema
                {
                    Key = "level", Label = "Level", Kind = SpreadsheetColumnKind.Select, Required = true,
                    UnknownPolicy = SpreadsheetUnknownValuePolicy.Ask,
                    Options = [new SpreadsheetOption("B2", "B2")]
                }
            ],
            OnSubmit = batch =>
            {
                var rej = new List<SpreadsheetRejectedRow>();
                if (rejectRow1 && batch.Any(r => r.Index == 1))
                    rej.Add(new SpreadsheetRejectedRow(1, "Server rejected row 1", "email"));
                return Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>(rej);
            }
        };

        var importer = new SpreadsheetImporter(schema);
        importer.LoadText("Email;Level\nalice@example.com;C1-Adv\nalice@example.com;B2\nbob@example.com;B2", "users.csv");

        Assert.Single(importer.OpenQuestions);
        importer.AnswerValue("level", "C1-Adv", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Map, "B2", "B2"));
        Assert.Empty(importer.OpenQuestions);

        // Fix duplicate email on row 1
        Assert.NotEmpty(importer.InvalidRows);
        importer.EditCell(1, "email", "claire@example.com");
        Assert.Empty(importer.InvalidRows);
        Assert.Equal(SpreadsheetRowMode.Update, importer.AllRows[2].Mode);

        // Submit -> row 1 rejected by server -> retry rejected batch
        await importer.SubmitAsync();
        Assert.Equal(1, importer.RejectedCount);
        Assert.Equal(2, importer.ImportedCount);

        rejectRow1 = false;
        await importer.RetryRejectedAsync();
        Assert.Equal(0, importer.RejectedCount);
        Assert.Equal(1, importer.ImportedCount);
        Assert.Equal(SpreadsheetSubmitStatus.Done, importer.SubmitStatus);
    }
}
