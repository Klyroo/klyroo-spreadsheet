namespace Klyroo.Spreadsheet.Maui;

/// <summary>A file <see cref="KlySpreadsheetImport"/> offers to the user: the template or the invalid rows. Passed to <see cref="KlySpreadsheetImport.DownloadRequested"/>.</summary>
/// <param name="FileName">The suggested file name, for example <c>people-template.csv</c>.</param>
/// <param name="Content">The text of the file. CSV starts with a byte order mark so Excel reads it as UTF-8.</param>
/// <param name="MimeType">The media type, <c>text/csv;charset=utf-8</c>.</param>
public sealed record SpreadsheetDownload(string FileName, string Content, string MimeType)
{
    /// <summary>The media type of the CSV files the wizard produces.</summary>
    public const string CsvMimeType = "text/csv;charset=utf-8";

    // A schema key as a file name: letters and digits kept, anything else becomes a dash, so a key such as "people/2026" cannot become a path.
    internal static string Slug(string key)
    {
        var slug = new string(key.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray()).Trim('-');
        return slug.Length == 0 ? "import" : slug;
    }
}
