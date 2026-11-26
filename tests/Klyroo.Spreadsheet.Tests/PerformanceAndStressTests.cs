using System.Diagnostics;
using System.Globalization;
using System.Text;
using Klyroo.Spreadsheet;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

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
            sb.Append($"SKU-{i:D5};user{i}@klyroo.dev;{amount}\n");
        }

        var schema = SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Key = "bulk-orders";
            sb.Duplicates = SpreadsheetDuplicatesPolicy.Error;
            var colCode = sb.Column(r => r.Code, col => col.Label("Code").Required());
            sb.Column(r => r.Email, col => col.Label("Email").Required().Rule(SpreadsheetRule.Email()));
            sb.Column(r => r.Amount, col => col.Label("Amount").DecimalSeparator(',').Required().Rule(SpreadsheetRule.Min(0d)));
            sb.RowKey(colCode);
        });

        var importer = new SpreadsheetImporter<ImportRow>(schema);
        var sw = Stopwatch.StartNew();
        importer.LoadText(sb.ToString(), "bulk.csv");
        sw.Stop();

        Assert.True(importer.FileLoaded);
        Assert.Equal(5_000, importer.ImportableRows.Count);
        Assert.Empty(importer.InvalidRows);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"5k spreadsheet import took too long: {sw.Elapsed}");
    }
}
