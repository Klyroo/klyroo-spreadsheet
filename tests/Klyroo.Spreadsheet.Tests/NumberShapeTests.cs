using Xunit;

namespace Klyroo.Spreadsheet.Tests;

public sealed class Shapes
{
    public decimal? Dot { get; set; }
    public decimal? Comma { get; set; }
    public int? Int { get; set; }
    public byte? Byte { get; set; }
    public ulong? Ulong { get; set; }
}

// The shape of a number cell (separators, groups of three, signs, exponent) and the limits of each type, one case per rule, so a change to how a cell is scanned shows here.
public sealed class NumberShapeTests
{
    private static SpreadsheetRow<Shapes> Read(string column, string cell)
    {
        var importer = new SpreadsheetImporter<Shapes>(SpreadsheetSchema.For<Shapes>(sb =>
        {
            sb.Column(r => r.Dot, c => c.DecimalSeparator('.'));
            sb.Column(r => r.Comma, c => c.DecimalSeparator(','));
            sb.Column(r => r.Int);
            sb.Column(r => r.Byte);
            sb.Column(r => r.Ulong);
        }));
        importer.LoadText($"{column}\n\"{cell}\"", "a.csv");
        return importer.ToTyped(Assert.Single(importer.AllRows));
    }

    [Theory]
    [InlineData("Dot", "1,234.5", "1234.5")]
    [InlineData("Dot", "1,234,567", "1234567")]
    [InlineData("Dot", "1 234.5", "1234.5")]
    [InlineData("Dot", "1'234.5", "1234.5")]
    [InlineData("Dot", "1 234.5", "1234.5")]
    [InlineData("Dot", "+.5", "0.5")]
    [InlineData("Dot", "12.", "12")]
    [InlineData("Dot", "-1.5e2", "-150")]
    [InlineData("Dot", "1.50", "1.50")]
    [InlineData("Comma", "1.234,5", "1234.5")]
    [InlineData("Comma", "12,50", "12.50")]
    [InlineData("Comma", "1.234.567", "1234567")]
    public void A_cell_in_the_shape_of_the_column_is_read(string column, string cell, string shown)
    {
        var row = Read(column, cell);

        Assert.Empty(row.Issues);
        Assert.Equal(shown, row.Cells[column].DisplayText);
    }

    [Theory]
    [InlineData("Dot", "12,50")]
    [InlineData("Dot", "1,23")]
    [InlineData("Dot", "1,2345")]
    [InlineData("Dot", "1234,567")]
    [InlineData("Dot", "01,234")]
    [InlineData("Dot", ".")]
    [InlineData("Dot", "e5")]
    [InlineData("Dot", "1e")]
    [InlineData("Dot", "1e+")]
    [InlineData("Dot", "1.2.3")]
    [InlineData("Dot", "abc")]
    [InlineData("Comma", "1,234.5")]
    [InlineData("Comma", "12.50")]
    [InlineData("Comma", "1.23")]
    public void A_cell_that_is_not_in_the_shape_of_the_column_is_refused_with_its_text(string column, string cell)
    {
        var row = Read(column, cell);

        var issue = Assert.Single(row.Issues);
        Assert.Equal(column, issue.Field);
        Assert.Equal($"\"{cell}\" is not a number", issue.Message);
        Assert.Equal([cell], issue.Args);
    }

    [Theory]
    [InlineData("Int", "-2147483648", true)]
    [InlineData("Int", "2147483647", true)]
    [InlineData("Int", "2147483648", false)]
    [InlineData("Int", "-2147483649", false)]
    [InlineData("Int", "1e3", true)]
    [InlineData("Int", "1.0", true)]
    [InlineData("Int", "0e5", true)]
    [InlineData("Byte", "255", true)]
    [InlineData("Byte", "256", false)]
    [InlineData("Byte", "-0", true)]
    [InlineData("Byte", "-1", false)]
    [InlineData("Ulong", "18446744073709551615", true)]
    [InlineData("Ulong", "18446744073709551616", false)]
    [InlineData("Ulong", "1.8446744073709551615e19", true)]
    public void A_whole_number_is_held_exactly_or_refused_as_out_of_range(string column, string cell, bool fits)
    {
        var row = Read(column, cell);

        if (fits) Assert.Empty(row.Issues);
        else Assert.Equal($"\"{cell}\" is out of range", Assert.Single(row.Issues).Message);
    }

    [Fact]
    public void A_number_with_decimals_is_not_a_whole_number()
    {
        var row = Read("Int", "1.5");

        Assert.Equal("\"1.5\" is not a whole number", Assert.Single(row.Issues).Message);
    }

    [Fact]
    public void A_decimal_keeps_28_decimals_and_refuses_the_29th_instead_of_rounding_it()
    {
        Assert.Empty(Read("Dot", "0.0000000000000000000000000001").Issues);
        Assert.Equal("\"0.00000000000000000000000000001\" has more decimals than this column can hold", Assert.Single(Read("Dot", "0.00000000000000000000000000001").Issues).Message);
        Assert.Equal("\"1.00000000000000000000000000001\" has more decimals than this column can hold", Assert.Single(Read("Dot", "1.00000000000000000000000000001").Issues).Message);
        Assert.Empty(Read("Dot", "1.000000000000000000000000000000").Issues);
    }
}
