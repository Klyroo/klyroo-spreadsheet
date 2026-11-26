using Xunit;

namespace Klyroo.Spreadsheet.Tests;

public sealed class DayRecord
{
    public DateOnly Day { get; set; }
    public string? Note { get; set; }
}

public sealed class StampRecord
{
    public DateTime Stamp { get; set; }
    public DateTimeOffset? At { get; set; }
    public decimal Amount { get; set; }
}

// The text of a date or number row key, which ExistingRecords and LookupExistingRecordsAsync are matched on.
public sealed class DateKeyTests
{
    private static CancellationToken Token => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public void A_date_key_is_the_iso_text_in_ExistingRecords()
    {
        var importer = new SpreadsheetImporter<DayRecord>(SpreadsheetSchema.For<DayRecord>(s =>
        {
            s.RowKey(s.Column(r => r.Day));
            s.ExistingRecords = new Dictionary<string, DayRecord> { ["2026-03-04"] = new() };
        }));

        importer.LoadText("Day\n2026-03-04\n2026-03-05", "d.csv");

        Assert.Equal([SpreadsheetRowMode.Update, SpreadsheetRowMode.Create], importer.AllRows.Select(r => r.Mode));
    }

    [Fact]
    public async Task A_date_key_is_the_iso_text_given_to_the_lookup()
    {
        var asked = new List<string>();
        var importer = new SpreadsheetImporter<DayRecord>(SpreadsheetSchema.For<DayRecord>(s =>
        {
            s.RowKey(s.Column(r => r.Day));
            s.LookupExistingRecordsAsync = (keys, token) =>
            {
                asked.AddRange(keys);
                return Task.FromResult<IReadOnlyDictionary<string, DayRecord>>(new Dictionary<string, DayRecord> { ["2026-03-04"] = new() });
            };
        }));
        importer.LoadText("Day\n2026-03-04\n04/03/2026\n2026-03-05", "d.csv");

        await importer.ValidateAsync(Token);

        Assert.Equal(["2026-03-04", "2026-03-05"], asked);
        Assert.Equal([SpreadsheetRowMode.Update, SpreadsheetRowMode.Update, SpreadsheetRowMode.Create], importer.AllRows.Select(r => r.Mode));
    }

    [Fact]
    public void A_date_time_key_keeps_the_text_the_importer_always_gave_it()
    {
        var importer = new SpreadsheetImporter<StampRecord>(SpreadsheetSchema.For<StampRecord>(s =>
        {
            s.RowKey(s.Column(r => r.Stamp));
            s.Column(r => r.Amount, c => c.Default(0m));
            s.ExistingRecords = new Dictionary<string, StampRecord>
            {
                ["2026-03-04"] = new(),
                ["2026-03-04T10:30"] = new(),
                ["2026-03-04T10:30:15"] = new(),
            };
        }));

        importer.LoadText("Stamp\n2026-03-04\n2026-03-04 10:30\n2026-03-04 10:30:15\n2026-03-04 10:31", "d.csv");

        Assert.Equal(
            [SpreadsheetRowMode.Update, SpreadsheetRowMode.Update, SpreadsheetRowMode.Update, SpreadsheetRowMode.Create],
            importer.AllRows.Select(r => r.Mode));
    }

    [Fact]
    public void A_decimal_key_has_no_trailing_zeros_and_no_culture()
    {
        var importer = new SpreadsheetImporter<StampRecord>(SpreadsheetSchema.For<StampRecord>(s =>
        {
            s.Column(r => r.Stamp, c => c.Default(new DateTime(2026, 1, 1)));
            s.RowKey(s.Column(r => r.Amount));
            s.ExistingRecords = new Dictionary<string, StampRecord> { ["12.5"] = new(), ["3"] = new() };
        }));

        importer.LoadText("Amount\n12.50\n3.0\n4", "d.csv");

        Assert.Equal([SpreadsheetRowMode.Update, SpreadsheetRowMode.Update, SpreadsheetRowMode.Create], importer.AllRows.Select(r => r.Mode));
    }
}
