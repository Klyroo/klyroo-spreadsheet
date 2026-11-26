using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

// The template and the invalid rows are offered as downloads: through the browser module, or through OnDownload.
public sealed class DownloadTests : BunitContext
{
    private const string Module = "./_content/Klyroo.Spreadsheet/klyroo-spreadsheet.js";

    public DownloadTests()
    {
        Services.AddKlyrooSpreadsheet();
    }

    private static SpreadsheetImporter NewImporter(string key = "people")
    {
        var importer = new SpreadsheetImporter(new SpreadsheetSchema
        {
            Key = key,
            Columns =
            [
                new SpreadsheetColumnSchema { Key = "name", Label = "Name", Required = true, Example = "Ada Lovelace" },
                new SpreadsheetColumnSchema { Key = "age", Label = "Age", Kind = SpreadsheetColumnKind.Number, Example = "36" },
            ],
        });
        return importer;
    }

    [Fact]
    public void The_template_button_downloads_the_template_through_the_browser_module()
    {
        var importer = NewImporter();
        var module = JSInterop.SetupModule(Module);
        module.SetupVoid("downloadText", _ => true);
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        cut.Find("[data-slot=spreadsheet-download-template]").Click();

        var call = Assert.Single(module.Invocations["downloadText"]);
        Assert.Equal("people-template.csv", call.Arguments[0]);
        Assert.Equal(importer.ExportTemplateCsv(), call.Arguments[1]);
        Assert.Equal("text/csv;charset=utf-8", call.Arguments[2]);
        Assert.Contains("Ada Lovelace", (string)call.Arguments[1]!, StringComparison.Ordinal);
    }

    [Fact]
    public void The_invalid_rows_button_shows_with_the_count_and_downloads_the_rows_with_their_issues()
    {
        var importer = NewImporter();
        importer.LoadText("Name;Age\nAda;36\nGrace;x\n;12", "people.csv");
        importer.NextStep();
        var module = JSInterop.SetupModule(Module);
        module.SetupVoid("downloadText", _ => true);
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        Assert.Contains("2", cut.Find("[data-slot=spreadsheet-download-invalid]").TextContent, StringComparison.Ordinal);
        cut.Find("[data-slot=spreadsheet-download-invalid]").Click();

        var call = Assert.Single(module.Invocations["downloadText"]);
        Assert.Equal("people-invalid-rows.csv", call.Arguments[0]);
        var csv = (string)call.Arguments[1]!;
        Assert.Equal(importer.ExportInvalidRowsCsv(), csv);
        Assert.Contains("Grace", csv, StringComparison.Ordinal);
        Assert.Contains("is not a number", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Ada", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void The_invalid_rows_button_is_hidden_when_every_row_is_valid()
    {
        var importer = NewImporter();
        importer.LoadText("Name;Age\nAda;36", "people.csv");
        importer.NextStep();
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        Assert.Empty(cut.FindAll("[data-slot=spreadsheet-download-invalid]"));
    }

    [Fact]
    public void OnDownload_receives_the_file_and_the_browser_module_is_not_used()
    {
        var importer = NewImporter();
        importer.LoadText("Name;Age\nGrace;x", "people.csv");
        importer.NextStep();
        var received = new List<SpreadsheetDownload>();
        // Strict JS interop (the default) throws on any call that was not set up: the module must stay untouched.
        var cut = Render<KlySpreadsheetImport>(p => p
            .Add(x => x.Importer, importer)
            .Add(x => x.OnDownload, file => received.Add(file)));

        cut.Find("[data-slot=spreadsheet-download-invalid]").Click();

        var file = Assert.Single(received);
        Assert.Equal("people-invalid-rows.csv", file.FileName);
        Assert.Equal(importer.ExportInvalidRowsCsv(), file.Content);
        Assert.Equal(SpreadsheetDownload.CsvMimeType, file.MimeType);
        Assert.Empty(JSInterop.Invocations);
    }

    [Fact]
    public void OnDownload_also_takes_the_template()
    {
        var importer = NewImporter();
        SpreadsheetDownload? received = null;
        var cut = Render<KlySpreadsheetImport>(p => p
            .Add(x => x.Importer, importer)
            .Add(x => x.OnDownload, file => received = file));

        cut.Find("[data-slot=spreadsheet-download-template]").Click();

        Assert.Equal("people-template.csv", received?.FileName);
        Assert.Equal(importer.ExportTemplateCsv(), received?.Content);
    }

    [Fact]
    public void A_failing_browser_download_shows_an_error_instead_of_throwing()
    {
        var importer = NewImporter();
        var module = JSInterop.SetupModule(Module);
        module.SetupVoid("downloadText", _ => true).SetException(new JSException("Downloads are blocked"));
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        cut.Find("[data-slot=spreadsheet-download-template]").Click();

        var alert = cut.Find("[data-slot=spreadsheet-download-error]");
        Assert.Equal("alert", alert.GetAttribute("role"));
        Assert.Contains("Downloads are blocked", alert.TextContent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("people", "people")]
    [InlineData("people/2026 Q1", "people-2026-Q1")]
    [InlineData("../../etc", "etc")]
    [InlineData("///", "import")]
    [InlineData("", "import")]
    public void The_file_name_is_built_from_a_safe_form_of_the_schema_key(string key, string slug)
    {
        var importer = NewImporter(key);
        SpreadsheetDownload? received = null;
        var cut = Render<KlySpreadsheetImport>(p => p
            .Add(x => x.Importer, importer)
            .Add(x => x.OnDownload, file => received = file));

        cut.Find("[data-slot=spreadsheet-download-template]").Click();

        Assert.Equal($"{slug}-template.csv", received?.FileName);
    }

    [Fact]
    public void The_module_script_is_shipped_with_the_package_and_exports_downloadText()
    {
        var script = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Klyroo.Spreadsheet", "wwwroot", "klyroo-spreadsheet.js"));

        Assert.Contains("export function downloadText(fileName, text, mimeType)", script, StringComparison.Ordinal);
        Assert.Contains("URL.createObjectURL", script, StringComparison.Ordinal);
        Assert.Contains("URL.revokeObjectURL", script, StringComparison.Ordinal);
    }
}
