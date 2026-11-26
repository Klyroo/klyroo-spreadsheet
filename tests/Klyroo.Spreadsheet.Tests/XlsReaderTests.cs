using Xunit;

namespace Klyroo.Spreadsheet.Tests;

public sealed class XlsReaderTests
{
    private static SpreadsheetWorkbook Read(byte[] file, string name = "test.xls") => SpreadsheetXlsReader.Read(new MemoryStream(file), name);

    private static XlsBook Book(byte[] sst, params byte[][] cells)
    {
        var book = new XlsBook { Sst = sst };
        book.Sheets.Add(new XlsSheet("Sheet1", Biff.Cat(cells)));
        return book;
    }

    private static string[][] Rows(SpreadsheetSheet sheet) => [.. sheet.Rows.Select(r => r.ToArray())];

    [Fact]
    public void Golden_file_assembled_by_hand_from_the_format_specifications_is_read()
    {
        var workbook = Read(XlsGoldenFixture.Bytes(), "golden.xls");

        Assert.True(workbook.InvariantNumbers);
        Assert.Equal("golden.xls", workbook.Name);
        var sheet = Assert.Single(workbook.Sheets);
        Assert.Equal("Data", sheet.Name);
        Assert.Equal([1, 2, 3], sheet.RowNumbers);
        Assert.Equal([["Name", "Qty", "Date"], ["Café", "12.5", "2025-01-01"], ["Ünï", "7"]], Rows(sheet));
    }

    [Fact]
    public void Shared_strings_are_read_across_continue_records_rich_text_and_phonetic_data()
    {
        var sst = SstWriter.Build(
            SstEntry.Plain("Product"),
            SstEntry.Plain("Price"),
            new SstEntry([("abc", false), ("日本", true)]),
            new SstEntry([("日本", true), ("xyz", false)]),
            new SstEntry([("Rich", false)], Runs: 2, Phonetic: new byte[10], BreakInTail: 5),
            new SstEntry([("After break", false)], BreakBefore: true),
            new SstEntry([("Phon", false)], Phonetic: new byte[8], BreakInTail: 3),
            SstEntry.Plain("Last"),
            new SstEntry([("Edge", false)], Runs: 1, BreakInTail: 0),
            new SstEntry([("Wide", true), ("er", true), ("!", false)]));
        var workbook = Read(Book(sst,
            Biff.LabelSst(0, 0, 0, 0), Biff.LabelSst(0, 1, 0, 1),
            Biff.LabelSst(1, 0, 0, 2), Biff.LabelSst(1, 1, 0, 3),
            Biff.LabelSst(2, 0, 0, 4), Biff.LabelSst(2, 1, 0, 5),
            Biff.LabelSst(3, 0, 0, 6), Biff.LabelSst(3, 1, 0, 7),
            Biff.LabelSst(4, 0, 0, 8), Biff.LabelSst(4, 1, 0, 9), Biff.LabelSst(4, 2, 0, 99),
            Biff.Label(5, 0, 0, "Label ü"), Biff.RString(5, 1, 0, "R")).BuildFile());

        Assert.Equal(
            [["Product", "Price"], ["abc日本", "日本xyz"], ["Rich", "After break"], ["Phon", "Last"], ["Edge", "Wideer!"], ["Label ü", "R"]],
            Rows(workbook.Sheets[0]));
    }

    [Fact]
    public void Numbers_come_from_NUMBER_RK_and_MULRK_records_in_invariant_form()
    {
        var workbook = Read(Book([],
            Biff.Number(0, 0, 0, 1234.5),
            Biff.Rk(0, 1, 0, Biff.RkInt(42)),
            Biff.Rk(0, 2, 0, Biff.RkInt(-7)),
            Biff.Rk(0, 3, 0, Biff.RkInt(1234, times100: true)),
            Biff.Rk(0, 4, 0, Biff.RkDouble(0.5)),
            Biff.Rk(0, 5, 0, Biff.RkDouble(100.0, times100: true)),
            Biff.MulRk(1, 0, (0, Biff.RkInt(1)), (0, Biff.RkInt(-2, times100: true)), (0, Biff.RkDouble(0.25))),
            Biff.Number(1, 3, 0, 1234567890123456d),
            Biff.Number(1, 4, 0, -0.25),
            Biff.Number(1, 5, 0, 0),
            Biff.Number(1, 6, 0, 0.1 + 0.2),
            Biff.Number(1, 7, 0, double.NaN)).BuildFile());

        Assert.Equal(
            [
                ["1234.5", "42", "-7", "12.34", "0.5", "1"],
                ["1", "-0.02", "0.25", "1234567890123456", "-0.25", "0", "0.30000000000000004", "#NUM!"],
            ],
            Rows(workbook.Sheets[0]));
    }

    [Fact]
    public void Booleans_and_error_cells_are_read_like_the_xlsx_reader_reads_them()
    {
        var workbook = Read(Book([],
            Biff.BoolErr(0, 0, 0, 1, false), Biff.BoolErr(0, 1, 0, 0, false),
            Biff.BoolErr(0, 2, 0, 0x07, true), Biff.BoolErr(0, 3, 0, 0x2A, true)).BuildFile());

        Assert.Equal([["TRUE", "FALSE", "#DIV/0!", "#N/A"]], Rows(workbook.Sheets[0]));
    }

    [Fact]
    public void Formulas_give_the_cached_number_string_boolean_error_or_nothing()
    {
        var longText = new string('a', 3000) + new string('ж', 2000);
        var longString = new ChunkedRecord(0x0207);
        longString.Current.U16(longText.Length).U8(0).Raw(Enumerable.Repeat((byte)'a', 3000));
        longString.Break();
        longString.Current.U8(1);
        foreach (var _ in Enumerable.Range(0, 2000)) longString.Current.U16('ж');

        var workbook = Read(Book([],
            Biff.Formula(1, 0, 0, Biff.FormulaNumber(3.5)),
            Biff.Formula(1, 1, 0, Biff.FormulaString()), Biff.SharedFormula(), Biff.StringRecord("hello"),
            Biff.Formula(1, 2, 0, Biff.FormulaBool(true)),
            Biff.Formula(1, 3, 0, Biff.FormulaError(0x17)),
            Biff.Formula(1, 4, 0, Biff.FormulaEmpty()),
            Biff.Formula(1, 5, 0, Biff.FormulaString()), longString.Build(),
            Biff.Formula(1, 6, 1, Biff.FormulaNumber(45658))).BuildFile());

        Assert.Equal([["3.5", "hello", "TRUE", "#REF!", "", longText, "2025-01-01"]], Rows(workbook.Sheets[0]));
    }

    [Fact]
    public void Dates_come_from_built_in_and_custom_formats_and_time_is_kept()
    {
        var book = Book([],
            Biff.Number(0, 0, 1, 45658),
            Biff.Number(0, 1, 2, 45658),
            Biff.Number(0, 2, 4, 45658.5),
            Biff.Number(0, 3, 1, 45658.75 + 1.0 / 86400),
            Biff.Number(0, 4, 3, 1234.5),
            Biff.Number(0, 5, 5, 1.5),
            Biff.Rk(0, 6, 1, Biff.RkInt(45658)),
            Biff.MulRk(0, 7, (1, Biff.RkInt(45659)), (0, Biff.RkInt(45659))),
            Biff.Number(0, 9, 99, 45658));
        book.XfFormats.AddRange([164, 165, 22, 166]);
        book.Formats.AddRange([(164, "dd/mm/yyyy"), (165, "0.00"), (166, "#,##0.0")]);

        var workbook = Read(book.BuildFile());

        Assert.Equal([["2025-01-01", "2025-01-01", "2025-01-01 12:00", "2025-01-01 18:00:01", "1234.5", "1.5", "2025-01-01", "2025-01-02", "45659", "45658"]], Rows(workbook.Sheets[0]));
    }

    [Fact]
    public void The_1904_date_system_is_honoured()
    {
        var book = Book([], Biff.Number(0, 0, 1, 44196), Biff.Number(0, 1, 1, 0));
        book.Date1904 = true;

        Assert.Equal([["2025-01-01", "1904-01-01"]], Rows(Read(book.BuildFile()).Sheets[0]));
    }

    [Fact]
    public void Every_worksheet_is_read_in_workbook_order_including_hidden_ones_and_chart_sheets_are_skipped()
    {
        var book = new XlsBook { Sst = SstWriter.Build(SstEntry.Plain("one"), SstEntry.Plain("two")) };
        book.Sheets.Add(new XlsSheet("Visible", Biff.LabelSst(0, 0, 0, 0)));
        book.Sheets.Add(new XlsSheet("Hidden", Biff.LabelSst(0, 0, 0, 1), State: 1));
        book.Sheets.Add(new XlsSheet("Chart", [], Type: 2));
        book.Sheets.Add(new XlsSheet("Very hidden", Biff.Number(0, 0, 0, 3), State: 2));
        book.Sheets.Add(new XlsSheet("Ünï 日", []));

        var workbook = Read(book.BuildFile());

        Assert.Equal(["Visible", "Hidden", "Very hidden", "Ünï 日"], workbook.Sheets.Select(s => s.Name));
        Assert.Equal([["one"]], Rows(workbook.Sheets[0]));
        Assert.Equal([["two"]], Rows(workbook.Sheets[1]));
        Assert.Equal([["3"]], Rows(workbook.Sheets[2]));
        Assert.Empty(workbook.Sheets[3].Rows);
    }

    [Fact]
    public void A_workbook_without_worksheet_is_refused()
    {
        var book = new XlsBook();
        book.Sheets.Add(new XlsSheet("Chart", [], Type: 2));

        Assert.Throws<InvalidDataException>(() => Read(book.BuildFile()));
    }

    [Fact]
    public void Sparse_cells_keep_their_column_and_row_numbers_and_blank_cells_are_ignored()
    {
        var workbook = Read(Book([],
            Biff.Label(3, 0, 0, "  padded  "), Biff.Label(3, 5, 0, ""),
            Biff.Label(0, 2, 0, "x"),
            Biff.Blank(5, 1, 0), Biff.MulBlank(5, 3, 2),
            Biff.Label(7, 0, 0, "end")).BuildFile());

        var sheet = workbook.Sheets[0];
        Assert.Equal([1, 4, 8], sheet.RowNumbers);
        Assert.Equal([["", "", "x"], ["padded"], ["end"]], Rows(sheet));
    }

    private static byte[] ManyCells(out string[][] expected, int rows = 300)
    {
        var cells = new List<byte[]>();
        var lines = new List<string[]>();
        for (var r = 0; r < rows; r++)
        {
            var line = new List<string>();
            for (var c = 0; c < 10; c++)
            {
                cells.Add(Biff.Number(r, c, 0, r * 10 + c + 0.5));
                line.Add((r * 10 + c + 0.5).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            lines.Add([.. line]);
        }
        expected = [.. lines];
        return Biff.Cat([.. cells]);
    }

    [Theory]
    [InlineData(512, 10)]
    [InlineData(512, 300)]
    [InlineData(4096, 10)]
    [InlineData(4096, 300)]
    public void Small_workbooks_come_from_the_mini_stream_and_larger_ones_from_the_regular_sectors(int sectorSize, int rows)
    {
        var body = ManyCells(out var expected, rows);
        var book = new XlsBook();
        book.Sheets.Add(new XlsSheet("Big", body));
        var biff = book.BuildBiff();
        Assert.Equal(rows == 10, biff.Length < 4096);

        var workbook = Read(book.BuildFile(sectorSize));

        Assert.Equal(expected, Rows(workbook.Sheets[0]));
    }

    [Fact]
    public void A_file_with_a_DIFAT_chain_is_read()
    {
        // 8.8 MB of records the reader does not know: more FAT sectors than the header can list.
        var padding = Biff.Cat([.. Enumerable.Range(0, 1100).Select(_ => Biff.Unknown(8000))]);
        var book = new XlsBook();
        book.Sheets.Add(new XlsSheet("Padded", Biff.Cat(padding, Biff.Label(0, 0, 0, "after the padding"))));
        var file = book.BuildFile();
        Assert.True(BitConverter.ToUInt32(file, 72) > 0, "the fixture must use a DIFAT sector");

        Assert.Equal([["after the padding"]], Rows(Read(file).Sheets[0]));
    }

    [Fact]
    public void The_stream_is_found_whatever_the_case_of_its_name()
    {
        var book = Book([], Biff.Label(0, 0, 0, "ok"));

        Assert.Equal([["ok"]], Rows(Read(book.BuildFile(streamName: "WORKBOOK")).Sheets[0]));
    }

    [Fact]
    public void An_encrypted_workbook_is_refused_with_a_clear_message()
    {
        var book = Book([], Biff.Label(0, 0, 0, "secret"));
        book.Encrypted = true;

        var error = Assert.Throws<InvalidDataException>(() => Read(book.BuildFile()));
        Assert.Contains("password", error.Message);
    }

    [Fact]
    public void A_password_protected_xlsx_container_is_refused_with_a_clear_message()
    {
        var file = Cfb.Build([("EncryptionInfo", new byte[100]), ("EncryptedPackage", new byte[5000])]);

        var error = Assert.Throws<InvalidDataException>(() => Read(file));
        Assert.Contains("password", error.Message);
    }

    [Fact]
    public void An_Excel_95_workbook_is_refused_with_a_clear_message()
    {
        var book = Book([], Biff.Label(0, 0, 0, "old"));

        var asBook = Assert.Throws<InvalidDataException>(() => Read(book.BuildFile(streamName: "Book")));
        Assert.Contains("BIFF5", asBook.Message);
        book.Version = 0x0500;
        var biff5 = Assert.Throws<InvalidDataException>(() => Read(book.BuildFile()));
        Assert.Contains("BIFF5", biff5.Message);
        book.Version = 0x0400;
        Assert.Throws<InvalidDataException>(() => Read(book.BuildFile()));
    }

    [Fact]
    public void A_compound_file_without_a_workbook_is_refused()
    {
        var file = Cfb.Build([("Other", new byte[100])]);

        Assert.Throws<InvalidDataException>(() => Read(file));
    }

    [Fact]
    public void A_huge_declared_string_count_does_not_allocate_and_the_strings_present_are_read()
    {
        var sst = SstWriter.Build(SstEntry.Plain("only"));
        BitConverter.GetBytes(uint.MaxValue).CopyTo(sst, 4 + 4);
        BitConverter.GetBytes(uint.MaxValue).CopyTo(sst, 4 + 0);

        var workbook = Read(Book(sst, Biff.LabelSst(0, 0, 0, 0)).BuildFile());

        Assert.Equal([["only"]], Rows(workbook.Sheets[0]));
    }

    [Fact]
    public void Cells_outside_the_grid_of_the_format_are_refused()
    {
        Assert.Throws<InvalidDataException>(() => Read(Book([], Biff.Label(0, 300, 0, "x")).BuildFile()));
        Assert.Throws<InvalidDataException>(() => Read(Book([], Biff.MulRk(0, 250, Enumerable.Repeat((0, Biff.RkInt(1)), 10).ToArray())).BuildFile()));
    }

    [Fact]
    public void Worksheets_that_overlap_and_sheets_starting_outside_the_stream_are_refused()
    {
        var book = new XlsBook();
        book.Sheets.Add(new XlsSheet("A", Biff.Label(0, 0, 0, "a")));
        book.Sheets.Add(new XlsSheet("B", Biff.Label(0, 0, 0, "b")));
        var biff = book.BuildBiff();
        var sheetRecords = new List<int>();
        for (var pos = 0; pos + 4 <= biff.Length && sheetRecords.Count < 2; pos += 4 + BitConverter.ToUInt16(biff, pos + 2))
        {
            if (BitConverter.ToUInt16(biff, pos) == 0x0085) sheetRecords.Add(pos + 4);
        }

        var overlapping = (byte[])biff.Clone();
        Array.Copy(biff, sheetRecords[0], overlapping, sheetRecords[1], 4);
        Assert.Throws<InvalidDataException>(() => Read(Cfb.Build([("Workbook", overlapping)])));

        var outside = (byte[])biff.Clone();
        BitConverter.GetBytes(biff.Length + 100).CopyTo(outside, sheetRecords[1]);
        Assert.Throws<InvalidDataException>(() => Read(Cfb.Build([("Workbook", outside)])));
    }

    [Fact]
    public void A_workbook_stream_cut_inside_a_sheet_is_refused()
    {
        var biff = Book([], Biff.Label(0, 0, 0, "x")).BuildBiff();

        Assert.Throws<InvalidDataException>(() => Read(Cfb.Build([("Workbook", biff[..^6])])));
        Assert.Throws<InvalidDataException>(() => Read(Cfb.Build([("Workbook", biff[..20])])));
        Assert.Throws<InvalidDataException>(() => Read(Cfb.Build([("Workbook", [1, 2, 3, 4, 5, 6, 7, 8])])));
    }

    [Fact]
    public void A_null_stream_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => SpreadsheetXlsReader.Read(null!, "x.xls"));
    }
}
