namespace Klyroo.Spreadsheet.Tests;

// The row type most tests import into: a few text, number, flag and date properties that schemas pick from.
public sealed class ImportRow
{
    public string? Sku { get; set; }
    public string? Key { get; set; }
    public string? Name { get; set; }
    public string? Level { get; set; }
    public string? Code { get; set; }
    public string? Email { get; set; }
    public string? Note { get; set; }
    public string? Other { get; set; }
    public string? A { get; set; }
    public string? B { get; set; }
    public string? C { get; set; }
    public string? T { get; set; }
    public string? Updated { get; set; }
    public string? Created { get; set; }
    public string? Initial { get; set; }
    public string? Tier { get; set; }
    public string? City { get; set; }
    public string? Product { get; set; }
    public string? Mail { get; set; }
    public string? Lvl { get; set; }
    public string? Lvl2 { get; set; }
    public string? Lvl3 { get; set; }
    public string? Currency { get; set; }
    public double? Price { get; set; }
    public double? Amount { get; set; }
    public double? Num { get; set; }
    public double? Score { get; set; }
    public double? N { get; set; }
    public int? Age { get; set; }
    public int? Qty { get; set; }
    public bool? Flag { get; set; }
    public DateOnly? Since { get; set; }
    public DateOnly? Day { get; set; }
}

internal static class ImportRowExtensions
{
    // The parsed values of one row of the importer, as the typed row the schema callbacks receive.
    public static ImportRow Value(this SpreadsheetImporter<ImportRow> importer, int index) => importer.ToTyped(importer.AllRows[index]).Value;
}
