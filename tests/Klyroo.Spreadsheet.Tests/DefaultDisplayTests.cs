using System.Globalization;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

public enum Level
{
    Low = 0,
    Normal = 1,
    Medium = 1,
    High = 2,
}

public sealed class Leveled
{
    public Level Level { get; set; }
}

public sealed class Defaulted
{
    public decimal Price { get; set; }
    public double Ratio { get; set; }
    public DateOnly Day { get; set; }
    public DateTime Stamp { get; set; }
    public DateTimeOffset At { get; set; }
    public Plan Plan { get; set; }
    public bool Flag { get; set; }
    public string? Country { get; set; }
}

public sealed class DefaultDisplayTests
{
    [Fact]
    public void An_enum_with_duplicate_values_has_one_option_per_value_labelled_by_the_first_name()
    {
        var importer = new SpreadsheetImporter<Leveled>(SpreadsheetSchema.For<Leveled>(sb => sb.Column(r => r.Level)));

        var options = Assert.Single(importer.Schema.Columns).Options!;

        Assert.Equal(["Low", "Normal", "High"], options.Select(o => o.Label));
        Assert.Equal(["Low", "Normal", "High"], options.Select(o => o.Value));
    }

    [Fact]
    public void The_other_names_of_a_duplicate_value_still_match_the_cell()
    {
        var importer = new SpreadsheetImporter<Leveled>(SpreadsheetSchema.For<Leveled>(sb => sb.Column(r => r.Level)));

        importer.LoadText("Level\nMedium\nnormal\nhigh", "l.csv");

        Assert.Empty(importer.OpenQuestions);
        Assert.Equal([Level.Normal, Level.Normal, Level.High], importer.AllRows.Select(r => importer.ToTyped(r).Value.Level));
        Assert.Equal("Normal", importer.AllRows[0].Cells["Level"].DisplayText);
    }

    [Fact]
    public void A_typed_default_is_displayed_like_a_cell_of_the_column_whatever_the_culture()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            var importer = new SpreadsheetImporter<Defaulted>(SpreadsheetSchema.For<Defaulted>(sb =>
            {
                sb.Column(r => r.Price, c => c.Default(1234.5m));
                sb.Column(r => r.Ratio, c => c.Default(0.25));
                sb.Column(r => r.Day, c => c.Default(new DateOnly(2026, 3, 4)));
                sb.Column(r => r.Stamp, c => c.Default(new DateTime(2026, 3, 4, 10, 30, 0)));
                sb.Column(r => r.At, c => c.Default(new DateTimeOffset(2026, 3, 4, 10, 30, 0, TimeSpan.FromHours(2))));
                sb.Column(r => r.Plan, c => c.Default(Plan.ProMonthly));
                sb.Column(r => r.Flag, c => c.Default(true));
                sb.Column(r => r.Country, c => c.Options([new SpreadsheetOption<string?>("FR", "France")]).Default("FR"));
            }));

            importer.LoadText("Other\nx", "d.csv");

            var cells = importer.AllRows[0].Cells;
            Assert.Equal("1234.5", cells["Price"].DisplayText);
            Assert.Equal("0.25", cells["Ratio"].DisplayText);
            Assert.Equal("2026-03-04", cells["Day"].DisplayText);
            Assert.Equal("2026-03-04T10:30", cells["Stamp"].DisplayText);
            Assert.Equal("2026-03-04T10:30+02:00", cells["At"].DisplayText);
            Assert.Equal("Pro monthly", cells["Plan"].DisplayText);
            Assert.Equal("True", cells["Flag"].DisplayText);
            Assert.Equal("France", cells["Country"].DisplayText);
            Assert.All(cells.Values, c => Assert.True(c.Defaulted || c.Text == "x"));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void A_default_is_displayed_as_the_same_text_a_cell_with_that_value_shows()
    {
        var importer = new SpreadsheetImporter<Defaulted>(SpreadsheetSchema.For<Defaulted>(sb =>
        {
            sb.Column(r => r.Price, c => c.Default(12.5m));
            sb.Column(r => r.Day, c => c.Default(new DateOnly(2026, 3, 4)));
            sb.Column(r => r.Plan, c => c.Default(Plan.Enterprise));
        }));

        importer.LoadText("Price,Day,Plan,Other\n12.5,2026-03-04,Enterprise,x\n,,,y", "d.csv");

        foreach (var key in new[] { "Price", "Day", "Plan" })
        {
            Assert.Equal(importer.AllRows[0].Cells[key].DisplayText, importer.AllRows[1].Cells[key].DisplayText);
        }
    }
}
