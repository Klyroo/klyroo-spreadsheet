using Xunit;

namespace Klyroo.Spreadsheet.Tests;

[CollectionDefinition("TimeZone", DisableParallelization = true)]
public sealed class TimeZoneCollection;

public sealed class OffsetRecord
{
    public DateTimeOffset? At { get; set; }
    public DateTime? Local { get; set; }
    public DateOnly? Day { get; set; }
}

// A cell with an explicit offset is the instant it says, whatever the time zone of the machine; one without is read as UTC.
[Collection("TimeZone")]
public sealed class OffsetTests : IDisposable
{
    private readonly string? _previous = Environment.GetEnvironmentVariable("TZ");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("TZ", _previous);
        TimeZoneInfo.ClearCachedData();
    }

    private static SpreadsheetImporter<OffsetRecord> Importer() => new(SpreadsheetSchema.For<OffsetRecord>(s =>
    {
        s.Column(r => r.At, c => c.DateFormats("yyyy-MM-dd HH:mm zzz", "yyyy-MM-ddTHH:mm:ssK", "yyyy-MM-dd HH:mm"));
        s.Column(r => r.Local, c => c.DateFormats("yyyy-MM-dd HH:mm zzz", "yyyy-MM-dd HH:mm"));
        s.Column(r => r.Day, c => c.DateFormats("yyyy-MM-dd HH:mm zzz", "yyyy-MM-dd HH:mm"));
    }));

    [Theory]
    [InlineData("America/New_York")]
    [InlineData("Asia/Kolkata")]
    [InlineData("Pacific/Auckland")]
    [InlineData("UTC")]
    public void An_offset_in_the_cell_is_honored_in_every_time_zone(string zone)
    {
        Environment.SetEnvironmentVariable("TZ", zone);
        TimeZoneInfo.ClearCachedData();
        var importer = Importer();

        importer.LoadText("At,Local,Day\n2026-03-05 10:00 +02:00,2026-03-05 10:00 +02:00,2026-03-05 23:30 -05:00\n2026-03-05 10:00,2026-03-05 10:00,2026-03-05 10:00", "o.csv");

        var first = importer.ToTyped(importer.AllRows[0]);
        Assert.Empty(first.Issues);
        Assert.Equal(new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.FromHours(2)), first.Value.At);
        Assert.Equal(TimeSpan.FromHours(2), first.Value.At!.Value.Offset);
        Assert.Equal(new DateTimeOffset(2026, 3, 5, 8, 0, 0, TimeSpan.Zero).UtcTicks, first.Value.At!.Value.UtcTicks);
        // A column without an offset keeps the date and time as the cell wrote them.
        Assert.Equal(new DateTime(2026, 3, 5, 10, 0, 0), first.Value.Local);
        Assert.Equal(new DateOnly(2026, 3, 5), first.Value.Day);
        Assert.Equal("2026-03-05T10:00+02:00", first.Cells["At"].DisplayText);

        var second = importer.ToTyped(importer.AllRows[1]);
        Assert.Equal(new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero), second.Value.At);
        Assert.Equal(TimeSpan.Zero, second.Value.At!.Value.Offset);
    }

    [Fact]
    public void A_zulu_suffix_is_utc()
    {
        Environment.SetEnvironmentVariable("TZ", "America/New_York");
        TimeZoneInfo.ClearCachedData();
        var importer = Importer();

        importer.LoadText("At\n2026-03-05T10:00:00Z", "o.csv");

        Assert.Equal(new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero), importer.ToTyped(importer.AllRows[0]).Value.At);
    }
}
