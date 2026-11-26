using System.IO.Compression;
using System.Text;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

public sealed class Amounts
{
    public decimal? Money { get; set; }
    public long? Big { get; set; }
    public ulong? Huge { get; set; }
    public int? Small { get; set; }
    public double? Real { get; set; }
    public float? Single { get; set; }
    public byte? Tiny { get; set; }
}

public sealed class OptionAmounts
{
    public decimal? Money { get; set; }
    public long? Big { get; set; }
}

// Numbers are read from the cell text exactly, into the type of the property, never through a double.
public sealed class ExactNumberTests
{
    private static CancellationToken Token => Xunit.TestContext.Current.CancellationToken;

    private static SpreadsheetImporter<Amounts> Load(string header, string cell, Action<SpreadsheetSchemaBuilder<Amounts>>? extra = null)
    {
        var importer = new SpreadsheetImporter<Amounts>(SpreadsheetSchema.For<Amounts>(sb =>
        {
            sb.Column(r => r.Money, c => c.DecimalSeparator('.'));
            sb.Column(r => r.Big);
            sb.Column(r => r.Huge);
            sb.Column(r => r.Small);
            sb.Column(r => r.Real);
            sb.Column(r => r.Single);
            sb.Column(r => r.Tiny);
            extra?.Invoke(sb);
        }));
        importer.LoadText($"{header}\n{cell}", "a.csv");
        return importer;
    }

    [Fact]
    public void A_decimal_keeps_every_digit_the_cell_holds_and_the_review_table_shows_it()
    {
        var importer = Load("Money", "1234567890.123456");

        var row = importer.ToTyped(Assert.Single(importer.AllRows));

        Assert.Empty(row.Issues);
        Assert.Equal(1234567890.123456m, row.Value.Money);
        Assert.Equal("1234567890.123456", row.Cells["Money"].DisplayText);
    }

    [Theory]
    [InlineData("9007199254740993", 9007199254740993L)]
    [InlineData("9223372036854775807", long.MaxValue)]
    [InlineData("-9223372036854775808", long.MinValue)]
    [InlineData("9,007,199,254,740,993", 9007199254740993L)]
    [InlineData("9007199254740993.0", 9007199254740993L)]
    public void A_long_keeps_every_digit_above_two_to_the_fifty_third(string cell, long expected)
    {
        var importer = Load("Big", $"\"{cell}\"");

        var row = importer.ToTyped(Assert.Single(importer.AllRows));

        Assert.Empty(row.Issues);
        Assert.Equal(expected, row.Value.Big);
        Assert.Equal(expected.ToString(System.Globalization.CultureInfo.InvariantCulture), row.Cells["Big"].DisplayText);
    }

    [Fact]
    public void A_ulong_reads_its_maximum()
    {
        var importer = Load("Huge", "18446744073709551615");

        var row = importer.ToTyped(Assert.Single(importer.AllRows));

        Assert.Empty(row.Issues);
        Assert.Equal(ulong.MaxValue, row.Value.Huge);
    }

    [Theory]
    [InlineData("Big", "9223372036854775808")]
    [InlineData("Big", "-9223372036854775809")]
    [InlineData("Huge", "18446744073709551616")]
    [InlineData("Huge", "-1")]
    [InlineData("Small", "2147483648")]
    [InlineData("Tiny", "256")]
    [InlineData("Money", "79228162514264337593543950336")]
    [InlineData("Money", "1e30")]
    [InlineData("Real", "1e999")]
    [InlineData("Single", "1e50")]
    [InlineData("Big", "1e99999999999999999999")]
    public void A_value_out_of_the_range_of_its_type_is_a_row_issue_never_a_rounding(string column, string cell)
    {
        var importer = Load(column, cell);

        var row = Assert.Single(importer.AllRows);

        var issue = Assert.Single(row.Issues);
        Assert.Equal(column, issue.Field);
        Assert.Contains("out of range", issue.Message, StringComparison.Ordinal);
        Assert.False(row.Importable);
    }

    [Theory]
    [InlineData("Money", "0.00000000000000000000000000001")]
    [InlineData("Money", "1.00000000000000000000000000001")]
    [InlineData("Small", "1.5")]
    [InlineData("Big", "9007199254740993.5")]
    public void A_value_the_type_cannot_hold_exactly_is_a_row_issue(string column, string cell)
    {
        var importer = Load(column, cell);

        var row = Assert.Single(importer.AllRows);

        Assert.Single(row.Issues);
        Assert.False(row.Importable);
    }

    [Fact]
    public void The_extremes_of_decimal_load_without_throwing()
    {
        var max = Load("Money", "79228162514264337593543950335");
        var min = Load("Money", "-79228162514264337593543950335");
        var over = Load("Money", "79228162514264337593543950336");

        Assert.Equal(decimal.MaxValue, max.ToTyped(Assert.Single(max.AllRows)).Value.Money);
        Assert.Equal(decimal.MinValue, min.ToTyped(Assert.Single(min.AllRows)).Value.Money);
        Assert.Single(Assert.Single(over.AllRows).Issues);
    }

    [Fact]
    public void A_column_suggestion_over_extreme_numbers_does_not_throw()
    {
        var importer = new SpreadsheetImporter<Amounts>(SpreadsheetSchema.For<Amounts>(sb => sb.Column(r => r.Money, c => c.Required())));

        importer.LoadText(
            "Label,Wide\na,79228162514264337593543950335\nb,79228162514264337593543950336\nc,-79228162514264337593543950335\nd,1.5\ne,2",
            "a.csv");

        var field = Assert.Single(importer.Fields);
        Assert.Equal(1, field.Suggestion?.HeaderIndex);
        Assert.Equal(5, importer.AllRows.Count);

        var hostile = new SpreadsheetImporter<Amounts>(SpreadsheetSchema.For<Amounts>(sb => sb.Column(r => r.Money, c => c.Required())));
        hostile.LoadText("Label,Wide\na,1e999999999999\nb,1e-9999999999\nc,-1e99999999999999999999999", "a.csv");
        Assert.Equal(3, hostile.AllRows.Count);
    }

    [Fact]
    public void A_comma_decimal_column_reads_exactly_too()
    {
        var importer = Load("Money", "\"1.234.567.890,123456\"", sb => { });
        // The shared helper declares a dot column; read the same cell in a comma column.
        var comma = new SpreadsheetImporter<Amounts>(SpreadsheetSchema.For<Amounts>(sb => sb.Column(r => r.Money, c => c.DecimalSeparator(','))));
        comma.LoadText("Money\n\"1.234.567.890,123456\"", "a.csv");

        var row = comma.ToTyped(Assert.Single(comma.AllRows));

        Assert.Empty(row.Issues);
        Assert.Equal(1234567890.123456m, row.Value.Money);
        Assert.Single(importer.AllRows);
    }

    [Fact]
    public void A_double_column_still_reads_a_double()
    {
        var importer = Load("Real", "0.1");

        Assert.Equal(0.1, importer.ToTyped(Assert.Single(importer.AllRows)).Value.Real);
    }

    [Fact]
    public void Option_texts_of_a_number_column_are_read_exactly()
    {
        var importer = new SpreadsheetImporter<OptionAmounts>(SpreadsheetSchema.For<OptionAmounts>(sb =>
        {
            sb.Column(r => r.Money, c => c.Options([new SpreadsheetOption<decimal?>(1234567890.123456m, "Big money"), new SpreadsheetOption<decimal?>(79228162514264337593543950335m, "Max")]));
            sb.Column(r => r.Big, c => c.Options([new SpreadsheetOption<long?>(9007199254740993L, "Large")]));
        }));
        importer.LoadText("Money,Big\n1234567890.123456,9007199254740993\n79228162514264337593543950335,9007199254740993", "a.csv");

        Assert.Empty(importer.OpenQuestions);
        Assert.All(importer.AllRows, r => Assert.Empty(r.Issues));
        Assert.Equal(1234567890.123456m, importer.ToTyped(importer.AllRows[0]).Value.Money);
        Assert.Equal(decimal.MaxValue, importer.ToTyped(importer.AllRows[1]).Value.Money);
        Assert.Equal(9007199254740993L, importer.ToTyped(importer.AllRows[0]).Value.Big);
    }

    [Fact]
    public async Task A_numeric_xlsx_cell_is_read_from_its_stored_text()
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string path, string xml)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
                writer.Write(xml);
            }
            const string main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            const string rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            Add("xl/workbook.xml", $"""<workbook xmlns="{main}" xmlns:r="{rel}"><sheets><sheet name="S" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Add("xl/_rels/workbook.xml.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Target="worksheets/sheet1.xml"/></Relationships>""");
            Add("xl/worksheets/sheet1.xml", $"""<worksheet xmlns="{main}"><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>Money</t></is></c><c r="B1" t="inlineStr"><is><t>Big</t></is></c><c r="C1" t="inlineStr"><is><t>Huge</t></is></c></row><row r="2"><c r="A2"><v>1234567890.123456</v></c><c r="B2"><v>9007199254740993</v></c><c r="C2"><v>18446744073709551615</v></c></row></sheetData></worksheet>""");
        }
        stream.Position = 0;
        var importer = new SpreadsheetImporter<Amounts>(SpreadsheetSchema.For<Amounts>(sb =>
        {
            sb.Column(r => r.Money);
            sb.Column(r => r.Big);
            sb.Column(r => r.Huge);
        }));

        await importer.LoadFileAsync(stream, "a.xlsx", cancellationToken: Token);

        var row = importer.ToTyped(Assert.Single(importer.AllRows));
        Assert.Empty(row.Issues);
        Assert.Equal(1234567890.123456m, row.Value.Money);
        Assert.Equal(9007199254740993L, row.Value.Big);
        Assert.Equal(ulong.MaxValue, row.Value.Huge);
    }

    [Fact]
    public async Task A_numeric_xls_cell_keeps_a_whole_number_a_double_holds_up_to_the_ulong_range()
    {
        var book = new XlsBook
        {
            Sst = SstWriter.Build(SstEntry.Plain("Huge"), SstEntry.Plain("Money")),
        };
        // 2^63 + 2^11 is a double, and a whole number above the range of long.
        var big = 9223372036854777856d;
        book.Sheets.Add(new XlsSheet("S", Biff.Cat(
            Biff.LabelSst(0, 0, 0, 0), Biff.LabelSst(0, 1, 0, 1),
            Biff.Number(1, 0, 0, big), Biff.Number(1, 1, 0, 1234567890.123456))));
        var importer = new SpreadsheetImporter<Amounts>(SpreadsheetSchema.For<Amounts>(sb =>
        {
            sb.Column(r => r.Huge);
            sb.Column(r => r.Money);
        }));

        await importer.LoadFileAsync(new MemoryStream(book.BuildFile()), "a.xls", cancellationToken: Token);

        var row = importer.ToTyped(Assert.Single(importer.AllRows));
        Assert.Empty(row.Issues);
        Assert.Equal(9223372036854777856UL, row.Value.Huge);
        Assert.Equal(1234567890.123456m, row.Value.Money);
    }
}
