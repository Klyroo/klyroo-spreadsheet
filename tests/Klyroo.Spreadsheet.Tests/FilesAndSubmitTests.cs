using System.IO.Compression;
using System.Text;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

public sealed class FilesAndSubmitTests
{
    // A minimal .xlsx: a shared string table, a custom date format (style 1) and two sheets.
    private static MemoryStream BuildXlsx()
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
            Add("xl/workbook.xml", $"""<workbook xmlns="{main}" xmlns:r="{rel}"><sheets><sheet name="Prices" sheetId="1" r:id="rId1"/><sheet name="Notes" sheetId="2" r:id="rId2"/></sheets></workbook>""");
            Add("xl/_rels/workbook.xml.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Target="worksheets/sheet2.xml"/></Relationships>""");
            Add("xl/sharedStrings.xml", $"""<sst xmlns="{main}"><si><t>Product</t></si><si><t>Price</t></si><si><t>Since</t></si><si><r><t>Café </t></r><r><t>crème</t></r></si></sst>""");
            Add("xl/styles.xml", $"""<styleSheet xmlns="{main}"><numFmts><numFmt numFmtId="164" formatCode="dd/mm/yyyy"/></numFmts><cellXfs><xf numFmtId="0"/><xf numFmtId="164"/></cellXfs></styleSheet>""");
            Add("xl/worksheets/sheet1.xml", $"""<worksheet xmlns="{main}"><sheetData><row r="1"><c r="A1" t="s"><v>0</v></c><c r="B1" t="s"><v>1</v></c><c r="C1" t="s"><v>2</v></c></row><row r="3"><c r="A3" t="s"><v>3</v></c><c r="B3"><v>1234.5</v></c><c r="C3" s="1"><v>45658</v></c></row></sheetData></worksheet>""");
            Add("xl/worksheets/sheet2.xml", $"""<worksheet xmlns="{main}"><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>hello</t></is></c><c r="B1" t="b"><v>1</v></c></row></sheetData></worksheet>""");
        }
        stream.Position = 0;
        return stream;
    }

    private static SpreadsheetSchema<ImportRow> PriceSchema(char decimalSeparator = ',', Func<IReadOnlyList<SpreadsheetRow<ImportRow>>, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>? onSubmit = null) =>
        SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Key = "prices";
            sb.OnSubmit = onSubmit;
            sb.Column(r => r.Product, col => col.Label("Product").Required());
            sb.Column(r => r.Price, col => col.Label("Price").DecimalSeparator(decimalSeparator));
            sb.Column(r => r.Since, col => col.Label("Since"));
        });

    [Fact]
    public void XlsxReader_reads_shared_strings_numbers_dates_and_every_sheet()
    {
        var workbook = SpreadsheetXlsxReader.Read(BuildXlsx(), "prices.xlsx");

        Assert.True(workbook.InvariantNumbers);
        Assert.Equal(["Prices", "Notes"], workbook.Sheets.Select(s => s.Name));
        var prices = workbook.Sheets[0];
        Assert.Equal([1, 3], prices.RowNumbers);
        Assert.Equal(["Café crème", "1234.5", "2025-01-01"], prices.Rows[1]);
        Assert.Equal(["hello", "TRUE"], workbook.Sheets[1].Rows[0]);
    }

    [Fact]
    public async Task LoadFileAsync_imports_an_xlsx_whatever_the_decimal_mark_of_the_column()
    {
        var importer = new SpreadsheetImporter<ImportRow>(PriceSchema(decimalSeparator: ','));

        await importer.LoadFileAsync(BuildXlsx(), "prices.xlsx", cancellationToken: TestContext.Current.CancellationToken);

        var row = Assert.Single(importer.AllRows);
        Assert.Empty(row.Issues);
        Assert.Equal(1234.5, importer.Value(0).Price);
        Assert.Equal(new DateOnly(2025, 1, 1), importer.Value(0).Since);
        Assert.Equal(3, row.RowNumber);
    }

    [Fact]
    public async Task LoadFileAsync_decodes_latin1_text_and_refuses_xls_and_oversized_files()
    {
        var importer = new SpreadsheetImporter<ImportRow>(PriceSchema());
        var latin1 = Encoding.Latin1.GetBytes("Product;Price\nCafé;12,50");

        await importer.LoadFileAsync(new MemoryStream(latin1), "prices.csv", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("Café", importer.Value(0).Product);

        await Assert.ThrowsAsync<InvalidDataException>(() => importer.LoadFileAsync(new MemoryStream([1, 2]), "old.xls", cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidDataException>(() => importer.LoadFileAsync(new MemoryStream(new byte[2048]), "big.csv", maxBytes: 1024, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SubmitAsync_does_nothing_without_importable_rows()
    {
        var sent = 0;
        var importer = new SpreadsheetImporter<ImportRow>(PriceSchema(onSubmit: rows => { sent += rows.Count; return Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>(null); }));

        await importer.SubmitAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(0, sent);
        Assert.Equal(SpreadsheetSubmitStatus.Idle, importer.SubmitStatus);
    }

    [Fact]
    public async Task ImportedCount_adds_the_rows_a_retry_gets_accepted()
    {
        var rejectFirst = true;
        var importer = new SpreadsheetImporter<ImportRow>(PriceSchema(onSubmit: rows =>
        {
            IReadOnlyList<SpreadsheetRejectedRow>? rejected = rejectFirst ? [new(rows[0].Index, "Server busy")] : null;
            rejectFirst = false;
            return Task.FromResult(rejected);
        }));
        importer.LoadText("Product;Price\nA;1\nB;2\nC;3", "p.csv");

        await importer.SubmitAsync(Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(2, importer.ImportedCount);

        await importer.RetryRejectedAsync(Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(3, importer.ImportedCount);
    }

    [Fact]
    public async Task OnCreateOption_result_is_sent_and_created_once()
    {
        var created = 0;
        var sentLevels = new List<object?>();
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Key = "people";
            sb.OnSubmit = rows =>
            {
                sentLevels.AddRange(rows.Select(r => r.Value.Level));
                return Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>([new(rows[0].Index, "retry me")]);
            };
            sb.Column(r => r.Name, col => col.Label("Name"));
            sb.Column(r => r.Level, col => col
                .Label("Level")
                .Options([new SpreadsheetOption<string?>("b1", "B1")])
                .OnCreateOption(text => { created++; return new SpreadsheetOption<string?>("lvl-" + text.ToLowerInvariant(), text); }));
        }));
        importer.LoadText("Name;Level\nAda;C2\nBob;C2", "people.csv");
        importer.AnswerValue("Level", "C2", new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Create));

        await importer.SubmitAsync(Xunit.TestContext.Current.CancellationToken);
        await importer.RetryRejectedAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(1, created);
        Assert.All(sentLevels, v => Assert.Equal("lvl-c2", v));
    }

    [Fact]
    public void A_skipped_columns_step_can_be_reopened_from_a_later_step()
    {
        var importer = new SpreadsheetImporter<ImportRow>(PriceSchema());
        importer.LoadText("Product;Price\nA;1", "p.csv");
        Assert.True(importer.NextStep());
        Assert.Equal("review", importer.CurrentStepKey);

        importer.GoToStep("columns");

        Assert.Equal("columns", importer.CurrentStepKey);
    }
}
