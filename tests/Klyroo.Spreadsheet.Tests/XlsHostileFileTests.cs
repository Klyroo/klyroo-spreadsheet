using System.Buffers.Binary;
using System.Diagnostics;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

// The reader is fed user uploads: whatever the bytes, the only acceptable outcomes are a workbook or an
// InvalidDataException, quickly and without a large allocation.
public sealed class XlsHostileFileTests
{
    private static SpreadsheetWorkbook Read(byte[] file) => SpreadsheetXlsReader.Read(new MemoryStream(file), "hostile.xls");

    private static XlsBook SmallBook()
    {
        var book = new XlsBook { Sst = SstWriter.Build(SstEntry.Plain("alpha"), SstEntry.Plain("beta")) };
        book.Sheets.Add(new XlsSheet("S", Biff.Cat(Biff.LabelSst(0, 0, 0, 0), Biff.LabelSst(0, 1, 0, 1), Biff.Number(1, 0, 0, 1.5))));
        return book;
    }

    // A workbook too large for the mini stream, so its sectors are chained through the regular FAT.
    private static byte[] LargeFile()
    {
        var book = new XlsBook();
        book.Sheets.Add(new XlsSheet("S", Biff.Cat(Biff.Label(0, 0, 0, "x"), Biff.Unknown(8000))));
        return book.BuildFile();
    }

    private static readonly byte[] Signature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    private static uint U32(byte[] file, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(offset));
    private static void Set32(byte[] file, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(offset), value);

    // Offset of the directory entry of the Workbook stream: the second stream of XlsBook.BuildFile, after the root and
    // the summary information stream.
    private static int WorkbookEntry(byte[] file) => (int)(U32(file, 48) + 1) * 512 + 2 * 128;

    private static int FatEntry(byte[] file, uint sector) => (int)(U32(file, 76) + 1) * 512 + 4 * (int)sector;

    [Fact]
    public void A_FAT_cycle_is_refused_and_does_not_hang()
    {
        var file = LargeFile();
        var start = U32(file, WorkbookEntry(file) + 116);
        Set32(file, FatEntry(file, start), start);

        var error = Assert.Throws<InvalidDataException>(() => Read(file));
        Assert.Contains("loops", error.Message);
    }

    [Fact]
    public void A_two_sector_FAT_cycle_is_refused()
    {
        var file = LargeFile();
        var start = U32(file, WorkbookEntry(file) + 116);
        var second = U32(file, FatEntry(file, start));
        Set32(file, FatEntry(file, second), start);

        Assert.Throws<InvalidDataException>(() => Read(file));
    }

    [Fact]
    public void A_mini_FAT_cycle_is_refused_and_does_not_hang()
    {
        var file = SmallBook().BuildFile();
        var start = U32(file, WorkbookEntry(file) + 116);
        var miniFat = (int)(U32(file, 60) + 1) * 512;
        Set32(file, miniFat + 4 * (int)start, start);

        Assert.Throws<InvalidDataException>(() => Read(file));
    }

    [Fact]
    public void A_chain_that_leaves_the_file_or_hits_a_free_sector_is_refused()
    {
        var file = LargeFile();
        var start = U32(file, WorkbookEntry(file) + 116);
        var outside = (byte[])file.Clone();
        Set32(outside, FatEntry(outside, start), 0x00FFFFFF);
        Assert.Throws<InvalidDataException>(() => Read(outside));

        var free = (byte[])file.Clone();
        Set32(free, FatEntry(free, start), 0xFFFFFFFF);
        Assert.Throws<InvalidDataException>(() => Read(free));
    }

    [Fact]
    public void A_declared_stream_size_that_the_chain_cannot_hold_is_refused_without_allocating_it()
    {
        var file = LargeFile();
        var entry = WorkbookEntry(file);

        var beyondChain = (byte[])file.Clone();
        Set32(beyondChain, entry + 120, 200_000_000);
        var shortChain = Assert.Throws<InvalidDataException>(() => Read(beyondChain));
        Assert.Contains("truncated", shortChain.Message);

        var enormous = (byte[])file.Clone();
        Set32(enormous, entry + 120, 0xFFFFFF00);
        Assert.Throws<InvalidDataException>(() => Read(enormous));

        var small = SmallBook().BuildFile();
        Set32(small, WorkbookEntry(small) + 120, 4000);
        Assert.Throws<InvalidDataException>(() => Read(small));
    }

    [Fact]
    public void A_header_with_impossible_values_is_refused()
    {
        var file = SmallBook().BuildFile();
        foreach (var (offset, value) in new (int, uint)[] { (72, 0x7FFFFFFF), (48, 0x00FFFFFF), (56, 0), (68, 0x00FFFFFF) })
        {
            var broken = (byte[])file.Clone();
            Set32(broken, offset, value);
            if (offset == 68) Set32(broken, 72, 3);
            Assert.Throws<InvalidDataException>(() => Read(broken));
        }
        var shift = (byte[])file.Clone();
        shift[30] = 31;
        Assert.Throws<InvalidDataException>(() => Read(shift));
        var order = (byte[])file.Clone();
        order[28] = 0;
        Assert.Throws<InvalidDataException>(() => Read(order));
    }

    [Fact]
    public void Garbage_and_tiny_inputs_are_refused()
    {
        Assert.Throws<InvalidDataException>(() => Read([]));
        Assert.Throws<InvalidDataException>(() => Read([1, 2, 3]));
        Assert.Throws<InvalidDataException>(() => Read(Signature));
        var random = new byte[5000];
        new Random(7).NextBytes(random);
        Assert.Throws<InvalidDataException>(() => Read(random));
        Signature.CopyTo(random, 0);
        Assert.Throws<InvalidDataException>(() => Read(random));
        Assert.Throws<InvalidDataException>(() => Read(new byte[4096]));
    }

    [Fact]
    public void Every_truncation_of_a_file_is_a_workbook_or_an_InvalidDataException()
    {
        foreach (var file in new[] { SmallBook().BuildFile(), LargeFile(), SmallBook().BuildFile(4096) })
        {
            for (var length = 0; length < file.Length; length += length < 1200 ? 7 : 211)
            {
                var exception = Record.Exception(() => Read(file[..length]));
                Assert.True(exception is null or InvalidDataException, $"length {length}: {exception}");
            }
        }
    }

    [Fact]
    public void Random_corruption_never_throws_anything_but_InvalidDataException_and_stays_fast()
    {
        var files = new[] { SmallBook().BuildFile(), XlsGoldenFixture.Bytes(), SmallBook().BuildFile(4096), LargeFile() };
        var random = new Random(20260126);
        var watch = Stopwatch.StartNew();
        foreach (var original in files)
        {
            for (var round = 0; round < 1500; round++)
            {
                var file = (byte[])original.Clone();
                var flips = random.Next(1, 9);
                for (var i = 0; i < flips; i++)
                {
                    // Half of the flips hit the header, the directory and the first records, where the structure lives.
                    var position = random.Next(2) == 0 ? random.Next(Math.Min(file.Length, 1536)) : random.Next(file.Length);
                    file[position] = (byte)random.Next(256);
                }
                var exception = Record.Exception(() => Read(file));
                Assert.True(exception is null or InvalidDataException, $"round {round}: {exception}");
            }
        }
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(60), $"took {watch.Elapsed}");
    }

    [Fact]
    public void Random_corruption_of_the_workbook_stream_itself_is_contained()
    {
        var biff = SmallBook().BuildBiff();
        var random = new Random(99);
        for (var round = 0; round < 3000; round++)
        {
            var copy = (byte[])biff.Clone();
            for (var i = random.Next(1, 6); i > 0; i--) copy[random.Next(copy.Length)] = (byte)random.Next(256);
            var exception = Record.Exception(() => Read(Cfb.Build([("Workbook", copy)])));
            Assert.True(exception is null or InvalidDataException, $"round {round}: {exception}");
        }
    }

    [Fact]
    public void Values_in_a_far_column_cannot_hold_much_more_memory_than_the_file()
    {
        // One small RK record per row in the last column: each row would keep 256 cells.
        var cells = Enumerable.Range(0, 8000).Select(row => Biff.Rk(row, 255, 0, Biff.RkInt(1))).ToArray();
        var book = new XlsBook();
        book.Sheets.Add(new XlsSheet("S", Biff.Cat(cells)));

        var error = Assert.Throws<InvalidDataException>(() => Read(book.BuildFile()));
        Assert.Contains("cells", error.Message);
    }

    [Fact]
    public void An_xlsx_with_values_in_a_far_column_is_refused_the_same_way()
    {
        var stream = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string path, string xml)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path).Open());
                writer.Write(xml);
            }
            const string main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            Add("xl/workbook.xml", $"""<workbook xmlns="{main}" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="S" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Add("xl/_rels/workbook.xml.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Target="worksheets/sheet1.xml"/></Relationships>""");
            var rows = string.Concat(Enumerable.Range(1, 130).Select(r => $"""<row r="{r}"><c r="XFD{r}"><v>1</v></c></row>"""));
            Add("xl/worksheets/sheet1.xml", $"""<worksheet xmlns="{main}"><sheetData>{rows}</sheetData></worksheet>""");
        }
        stream.Position = 0;

        var error = Assert.Throws<InvalidDataException>(() => SpreadsheetXlsxReader.Read(stream, "far.xlsx"));
        Assert.Contains("cells", error.Message);
    }

    [Fact]
    public void The_workbook_of_the_file_wins_over_an_embedded_one_listed_first()
    {
        var embedded = new XlsBook();
        embedded.Sheets.Add(new XlsSheet("Embedded", Biff.Label(0, 0, 0, "inner")));
        var real = new XlsBook();
        real.Sheets.Add(new XlsSheet("Real", Biff.Label(0, 0, 0, "outer")));

        var file = Cfb.Build([("MBD0001", []), ("Workbook", embedded.BuildBiffStream()), ("Workbook", real.BuildBiffStream())], firstIsStorage: true);

        Assert.Equal("Real", Assert.Single(Read(file).Sheets).Name);
    }

    [Fact]
    public void A_stream_positioned_past_its_end_is_refused_as_invalid_data()
    {
        var stream = new MemoryStream(SmallBook().BuildFile()) { Position = 1_000_000 };

        Assert.Throws<InvalidDataException>(() => SpreadsheetXlsReader.Read(stream, "moved.xls"));
    }
}
