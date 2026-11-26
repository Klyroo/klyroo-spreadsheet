namespace Klyroo.Spreadsheet;

// What a workbook reader may keep from an untrusted file, shared by the .xlsx and .xls readers. A row is a list padded with
// empty cells up to its last value, so a value in a far column costs every cell on its left: a few bytes of file could
// otherwise hold gigabytes of memory. Both counts are far above what an import wizard can show.
internal sealed class SpreadsheetReadBudget
{
    // Cells kept, counting the empty cells left of each value: 65,536 rows of 30 columns.
    internal const long MaxCells = 2_000_000;

    // Characters kept in cells and shared strings: 64 MB of text.
    internal const long MaxTextLength = 32L * 1024 * 1024;

    private long _cells;
    private long _text;

    // Sets a cell of a row, padding the row up to its column.
    internal void Place(List<string> cells, int column, string text)
    {
        if (cells.Count <= column)
        {
            _cells += column + 1 - cells.Count;
            if (_cells > MaxCells)
            {
                throw new InvalidDataException($"The workbook has more than {MaxCells} cells, counting the empty cells left of each value. Save the sheet to import as .csv, or split it.");
            }
            while (cells.Count <= column) cells.Add("");
        }
        AddText(text.Length);
        cells[column] = text;
    }

    internal void AddText(int length)
    {
        _text += length;
        if (_text > MaxTextLength) throw new InvalidDataException("The workbook holds too much text to import.");
    }
}
