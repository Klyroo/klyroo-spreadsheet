using System.IO.Compression;
using System.Text;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

public sealed class XlsImportTests : BunitContext
{
    private static SpreadsheetSchema PriceSchema() => new()
    {
        Key = "prices",
        Columns =
        [
            new SpreadsheetColumnSchema { Key = "product", Label = "Product", Required = true },
            new SpreadsheetColumnSchema { Key = "price", Label = "Price", Kind = SpreadsheetColumnKind.Number, DecimalSeparator = ',' },
            new SpreadsheetColumnSchema { Key = "since", Label = "Since", Kind = SpreadsheetColumnKind.Date },
        ]
    };

    // The same data as the .xlsx fixture of FilesAndSubmitTests: a header row, a gap, then a product with a price and a date.
    private static byte[] PricesXls()
    {
        var book = new XlsBook
        {
            Sst = SstWriter.Build(SstEntry.Plain("Product"), SstEntry.Plain("Price"), SstEntry.Plain("Since"), SstEntry.Plain("Café crème")),
        };
        book.Sheets.Add(new XlsSheet("Prices", Biff.Cat(
            Biff.LabelSst(0, 0, 0, 0), Biff.LabelSst(0, 1, 0, 1), Biff.LabelSst(0, 2, 0, 2),
            Biff.LabelSst(2, 0, 0, 3), Biff.Number(2, 1, 0, 1234.5), Biff.Number(2, 2, 1, 45658))));
        book.Sheets.Add(new XlsSheet("Notes", Biff.Label(0, 0, 0, "hello")));
        return book.BuildFile();
    }

    private static MemoryStream MinimalXlsx()
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
            Add("xl/worksheets/sheet1.xml", $"""<worksheet xmlns="{main}"><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>Product</t></is></c></row><row r="2"><c r="A2" t="inlineStr"><is><t>Zipped</t></is></c></row></sheetData></worksheet>""");
        }
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task LoadFileAsync_imports_an_xls_like_the_same_xlsx()
    {
        var importer = new SpreadsheetImporter(PriceSchema());

        await importer.LoadFileAsync(new MemoryStream(PricesXls()), "prices.xls", cancellationToken: Xunit.TestContext.Current.CancellationToken);

        var row = Assert.Single(importer.AllRows);
        Assert.Empty(row.Issues);
        Assert.Equal("Café crème", row.Data["product"]);
        Assert.Equal(1234.5, row.Data["price"]);
        Assert.Equal("2025-01-01", row.Data["since"]);
        Assert.Equal(3, row.RowNumber);
        Assert.Equal("prices.xls", importer.FileName);
    }

    [Theory]
    [InlineData("prices.csv")]
    [InlineData("prices.xlsx")]
    [InlineData("prices")]
    public async Task LoadFileAsync_reads_a_legacy_workbook_by_its_content_whatever_the_extension(string fileName)
    {
        var importer = new SpreadsheetImporter(PriceSchema());

        await importer.LoadFileAsync(new MemoryStream(PricesXls()), fileName, cancellationToken: Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("Café crème", Assert.Single(importer.AllRows).Data["product"]);
    }

    [Fact]
    public async Task LoadFileAsync_reads_a_zip_archive_named_xls_as_an_xlsx()
    {
        var importer = new SpreadsheetImporter(PriceSchema());

        await importer.LoadFileAsync(MinimalXlsx(), "renamed.xls", cancellationToken: Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("Zipped", Assert.Single(importer.AllRows).Data["product"]);
    }

    [Fact]
    public async Task LoadFileAsync_refuses_an_xls_that_is_not_a_workbook_and_says_why()
    {
        var importer = new SpreadsheetImporter(PriceSchema());

        var html = await Assert.ThrowsAsync<InvalidDataException>(() => importer.LoadFileAsync(new MemoryStream("<table><tr><td>a</td></tr></table>"u8.ToArray()), "export.xls", cancellationToken: Xunit.TestContext.Current.CancellationToken));
        Assert.Contains(".xls", html.Message);

        var book = new XlsBook { Version = 0x0500 };
        book.Sheets.Add(new XlsSheet("S", Biff.Label(0, 0, 0, "x")));
        var biff5 = await Assert.ThrowsAsync<InvalidDataException>(() => importer.LoadFileAsync(new MemoryStream(book.BuildFile()), "old.xls", cancellationToken: Xunit.TestContext.Current.CancellationToken));
        Assert.Contains("BIFF5", biff5.Message);

        book.Version = 0x0600;
        book.Encrypted = true;
        var encrypted = await Assert.ThrowsAsync<InvalidDataException>(() => importer.LoadFileAsync(new MemoryStream(book.BuildFile()), "locked.xls", cancellationToken: Xunit.TestContext.Current.CancellationToken));
        Assert.Contains("password", encrypted.Message);
        Assert.False(importer.FileLoaded);
    }

    [Theory]
    [InlineData("Product\tPrice\tSince\r\nCafé crème\t1234,5\t01/01/2025\r\n")]
    [InlineData("\uFEFFProduct;Price;Since\nCafé crème;1234,5;01/01/2025\n")]
    public async Task LoadFileAsync_reads_a_text_export_named_xls_as_delimited_text(string text)
    {
        var importer = new SpreadsheetImporter(PriceSchema());

        await importer.LoadFileAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)), "export.xls", cancellationToken: Xunit.TestContext.Current.CancellationToken);

        var row = Assert.Single(importer.AllRows);
        Assert.Equal("Café crème", row.Data["product"]);
        Assert.Equal("export.xls", importer.FileName);
    }

    [Fact]
    public async Task LoadFileAsync_reads_a_Latin1_text_export_named_xls_and_refuses_binary_or_markup()
    {
        var importer = new SpreadsheetImporter(PriceSchema());

        await importer.LoadFileAsync(new MemoryStream(System.Text.Encoding.Latin1.GetBytes("Product\tPrice\nCafé\t2\n")), "export.xls", cancellationToken: Xunit.TestContext.Current.CancellationToken);
        Assert.Equal("Café", Assert.Single(importer.AllRows).Data["product"]);

        var xml = await Assert.ThrowsAsync<InvalidDataException>(() => importer.LoadFileAsync(new MemoryStream("  <?xml version=\"1.0\"?><Workbook/>"u8.ToArray()), "export.xls", cancellationToken: Xunit.TestContext.Current.CancellationToken));
        Assert.Contains("HTML or XML", xml.Message);
        await Assert.ThrowsAsync<InvalidDataException>(() => importer.LoadFileAsync(new MemoryStream([0x00, 0x13, 0x41, 0x42]), "broken.xls", cancellationToken: Xunit.TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadFileAsync_still_applies_the_size_limit_to_an_xls()
    {
        var importer = new SpreadsheetImporter(PriceSchema());

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => importer.LoadFileAsync(new MemoryStream(PricesXls()), "prices.xls", maxBytes: 1024, cancellationToken: Xunit.TestContext.Current.CancellationToken));

        Assert.Contains("larger", error.Message);
    }

    [Fact]
    public void Choosing_an_xls_file_in_the_component_loads_it()
    {
        Services.AddKlyrooSpreadsheet();
        var importer = new SpreadsheetImporter(PriceSchema());
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(PricesXls(), "legacy.xls"));

        cut.WaitForAssertion(() => Assert.True(importer.FileLoaded));
        Assert.Equal("legacy.xls", importer.FileName);
        Assert.Equal("Café crème", Assert.Single(importer.AllRows).Data["product"]);
    }

    [Fact]
    public async Task The_golden_file_loads_through_the_importer()
    {
        var importer = new SpreadsheetImporter(PriceSchema());

        await importer.LoadFileAsync(new MemoryStream(XlsGoldenFixture.Bytes()), "golden.xls", cancellationToken: Xunit.TestContext.Current.CancellationToken);

        Assert.True(importer.FileLoaded);
    }
}
