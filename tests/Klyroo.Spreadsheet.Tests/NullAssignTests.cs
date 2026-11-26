using Xunit;

namespace Klyroo.Spreadsheet.Tests;

public sealed class Prefilled
{
    public string? Note { get; set; } = "initial note";
    public int? Seats { get; set; } = 7;
    public Plan? Upgrade { get; set; } = Plan.Enterprise;
    public int Quantity { get; set; } = 5;
    public string? Country { get; set; } = "FR";
}

// An empty cell stores null in a nullable property, over what the property initializer put there.
public sealed class NullAssignTests
{
    [Fact]
    public void An_empty_cell_stores_null_over_the_initializer_of_a_nullable_property()
    {
        var importer = new SpreadsheetImporter<Prefilled>(SpreadsheetSchema.For<Prefilled>(sb =>
        {
            sb.Column(r => r.Note);
            sb.Column(r => r.Seats);
            sb.Column(r => r.Upgrade);
            sb.Column(r => r.Quantity, c => c.Default(1));
            sb.Column(r => r.Country, c => c.Options([new SpreadsheetOption<string?>("FR", "France")]));
        }));

        importer.LoadText("Note,Seats,Upgrade,Quantity,Country\n,,,2,\nhello,3,free,4,fr", "p.csv");

        var empty = importer.ToTyped(importer.AllRows[0]);
        Assert.Empty(empty.Issues);
        Assert.Null(empty.Value.Note);
        Assert.Null(empty.Value.Seats);
        Assert.Null(empty.Value.Upgrade);
        Assert.Null(empty.Value.Country);
        Assert.Equal(2, empty.Value.Quantity);
        var full = importer.ToTyped(importer.AllRows[1]);
        Assert.Equal(("hello", 3, Plan.Free, "FR"), (full.Value.Note, full.Value.Seats, full.Value.Upgrade, full.Value.Country));
    }

    [Fact]
    public void A_default_of_null_applies_over_the_initializer()
    {
        var importer = new SpreadsheetImporter<Prefilled>(SpreadsheetSchema.For<Prefilled>(sb =>
        {
            sb.Column(r => r.Seats, c => c.Default(null));
            sb.Column(r => r.Note, c => c.Default(null));
            sb.Column(r => r.Quantity);
        }));

        importer.LoadText("Quantity\n1", "p.csv");

        var row = importer.ToTyped(Assert.Single(importer.AllRows));
        Assert.Null(row.Value.Seats);
        Assert.Null(row.Value.Note);
        Assert.True(row.Cells["Seats"].Defaulted);
    }

    [Fact]
    public void A_column_missing_from_the_file_stores_null_in_a_nullable_property_and_keeps_the_initializer_of_the_others()
    {
        var importer = new SpreadsheetImporter<Prefilled>(SpreadsheetSchema.For<Prefilled>(sb =>
        {
            sb.Column(r => r.Seats);
            sb.Column(r => r.Quantity, c => c.Default(9));
        }));

        importer.LoadText("Seats,Other\n,x", "p.csv");

        var row = importer.ToTyped(Assert.Single(importer.AllRows));
        Assert.Null(row.Value.Seats);
        Assert.Equal(9, row.Value.Quantity);
    }
}
