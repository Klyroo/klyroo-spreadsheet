namespace Klyroo.Spreadsheet.Maui;

/// <summary>A file the user chose for the import: its name, which tells CSV from Excel, and a way to read it.</summary>
/// <param name="Name">The file name with its extension, such as <c>prices.xlsx</c>.</param>
/// <param name="OpenRead">Opens the content of the file. The wizard disposes the stream.</param>
/// <param name="Length">The size in bytes when it is known, so a file over <see cref="KlySpreadsheetImport.MaxFileSize"/> is refused before it is read.</param>
public sealed record SpreadsheetFile(string Name, Func<Task<Stream>> OpenRead, long? Length = null);
