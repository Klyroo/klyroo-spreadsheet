namespace Klyroo.Spreadsheet.Maui;

/// <summary>What the wizard does with the platform when the app plugs in nothing: the file picker, and the saving of a download.</summary>
internal static class DefaultPickers
{
    // The kinds of file the schema can accept, by extension: the MIME types of Android, the type identifiers of Apple platforms.
    private static readonly Dictionary<string, (string[] Mime, string[] Uti)> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        [".csv"] = (["text/csv", "text/comma-separated-values", "application/csv"], ["public.comma-separated-values-text"]),
        [".tsv"] = (["text/tab-separated-values"], ["public.tab-separated-values-text"]),
        [".txt"] = (["text/plain"], ["public.plain-text"]),
        [".xlsx"] = (["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"], ["org.openxmlformats.spreadsheetml.sheet"]),
        [".xls"] = (["application/vnd.ms-excel"], ["com.microsoft.excel.xls"]),
    };

    /// <summary>The MIME types, the type identifiers and the extensions of the files a schema accepts. An extension the table does not know still goes to the platforms that filter by extension.</summary>
    public static (IReadOnlyList<string> Mime, IReadOnlyList<string> Uti, IReadOnlyList<string> Extensions) FileTypes(IReadOnlyList<string> accept)
    {
        var mime = new List<string>();
        var uti = new List<string>();
        var extensions = new List<string>();
        foreach (var raw in accept)
        {
            var extension = raw.StartsWith('.') ? raw : "." + raw;
            extensions.Add(extension.ToLowerInvariant());
            if (!Known.TryGetValue(extension, out var known)) continue;
            foreach (var m in known.Mime) if (!mime.Contains(m)) mime.Add(m);
            foreach (var u in known.Uti) if (!uti.Contains(u)) uti.Add(u);
        }
        return (mime, uti, extensions);
    }

    public static async Task<SpreadsheetFile?> PickAsync(IReadOnlyList<string> accept)
    {
        var (mime, uti, extensions) = FileTypes(accept);
        var types = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.Android] = mime,
            [DevicePlatform.iOS] = uti,
            [DevicePlatform.MacCatalyst] = uti,
            [DevicePlatform.WinUI] = extensions,
        });
        var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = SpreadsheetTexts.Current.ChooseFileLabel, FileTypes = types });
        return result is null ? null : new SpreadsheetFile(result.FileName, result.OpenReadAsync);
    }

    /// <summary>Writes the file to the cache of the app and opens the share sheet of the device on it.</summary>
    public static async Task SaveAsync(SpreadsheetDownload file)
    {
        var path = System.IO.Path.Combine(FileSystem.CacheDirectory, System.IO.Path.GetFileName(file.FileName));
        await File.WriteAllTextAsync(path, file.Content, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await Share.Default.RequestAsync(new ShareFileRequest(file.FileName, new ShareFile(path, file.MimeType)));
    }
}
