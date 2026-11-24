using System.Diagnostics;
using System.Globalization;
using System.Text;
using Lux.Spreadsheet;
using Xunit;

namespace Lux.Spreadsheet.Tests;

public sealed class PerformanceAndStressTests
{
    [Fact]
    public void SpreadsheetImporter_parses_normalizes_and_validates_5000_rows_fast()
    {
        var sb = new StringBuilder(256_000);
        sb.AppendLine("Code;Émail;Amount");
        for (var i = 1; i <= 5_000; i++)
        {
            var amount = (i * 1.25).ToString("F2", CultureInfo.InvariantCulture).Replace('.', ',');
            sb.Append($"SKU-{i:D5};user{i}@luxstack.dev;{amount}\n");
        }

        var schema = new SpreadsheetSchema
        {
            Key = "bulk-orders",
            RowKeyField = "code",
            Duplicates = SpreadsheetDuplicatesPolicy.Error,
            Columns =
            [
                new SpreadsheetColumnSchema { Key = "code", Label = "Code", Required = true },
                new SpreadsheetColumnSchema { Key = "email", Label = "Email", Required = true, Rules = [SpreadsheetRule.Email()] },
                new SpreadsheetColumnSchema { Key = "amount", Label = "Amount", Kind = SpreadsheetColumnKind.Number, DecimalSeparator = ',', Required = true, Rules = [SpreadsheetRule.Min(0)] }
            ]
        };

        var importer = new SpreadsheetImporter(schema);
        var sw = Stopwatch.StartNew();
        importer.LoadText(sb.ToString(), "bulk.csv");
        sw.Stop();

        Assert.True(importer.FileLoaded);
        Assert.Equal(5_000, importer.ImportableRows.Count);
        Assert.Empty(importer.InvalidRows);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"5k spreadsheet import took too long: {sw.Elapsed}");
    }
}
