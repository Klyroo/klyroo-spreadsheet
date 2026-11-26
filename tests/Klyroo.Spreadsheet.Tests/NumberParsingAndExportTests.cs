using Xunit;

namespace Klyroo.Spreadsheet.Tests;

public sealed class NumberParsingAndExportTests
{
    private static SpreadsheetRow ImportPrice(string text, char decimalSeparator)
    {
        var schema = new SpreadsheetSchema
        {
            Key = "prices",
            Columns = [new SpreadsheetColumnSchema { Key = "price", Label = "Price", Kind = SpreadsheetColumnKind.Number, DecimalSeparator = decimalSeparator }]
        };
        var importer = new SpreadsheetImporter(schema);
        importer.LoadText($"Price\n\"{text}\"", "prices.csv");
        return Assert.Single(importer.AllRows);
    }

    [Theory]
    [InlineData("12.50", '.', 12.5)]
    [InlineData("1,234.5", '.', 1234.5)]
    [InlineData("1,234,567", '.', 1234567)]
    [InlineData("-3", '.', -3)]
    [InlineData(".5", '.', 0.5)]
    [InlineData("1e3", '.', 1000)]
    [InlineData("12,50", ',', 12.5)]
    [InlineData("1.234,5", ',', 1234.5)]
    [InlineData("1 234,50", ',', 1234.5)]
    [InlineData("1'234,50", ',', 1234.5)]
    public void Number_cells_accept_well_formed_values(string text, char separator, double expected)
    {
        var row = ImportPrice(text, separator);
        Assert.Empty(row.Issues);
        Assert.Equal(expected, row.Data["price"]);
    }

    [Theory]
    [InlineData("12,50", '.')]
    [InlineData("1,23", '.')]
    [InlineData("1,234.50.1", '.')]
    [InlineData("1,234.50", ',')]
    [InlineData("12.5", ',')]
    [InlineData("12,5,0", ',')]
    public void Number_cells_reject_a_misplaced_thousands_separator(string text, char separator)
    {
        var row = ImportPrice(text, separator);
        Assert.Null(row.Data["price"]);
        Assert.Contains(row.Issues, i => i.Code == "number");
    }

    [Fact]
    public void ExportInvalidRowsCsv_aligns_values_with_headers_whatever_the_file_order()
    {
        var schema = new SpreadsheetSchema
        {
            Key = "users",
            Columns =
            [
                new SpreadsheetColumnSchema { Key = "email", Label = "Email", Required = true, Rules = [SpreadsheetRule.Email()] },
                new SpreadsheetColumnSchema { Key = "age", Label = "Age", Kind = SpreadsheetColumnKind.Number },
            ]
        };
        var importer = new SpreadsheetImporter(schema);
        importer.LoadText("Age;Notes;Email\n41;vip;not-an-email\n30;;ok@example.com", "users.csv");

        var lines = importer.ExportInvalidRowsCsv().TrimStart('﻿').Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.Equal("\"Email\",\"Age\",\"Issues\"", lines[0]);
        Assert.StartsWith("\"not-an-email\",\"41\",\"email: ", lines[1]);
    }
}
