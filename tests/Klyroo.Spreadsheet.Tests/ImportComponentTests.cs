using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

public sealed class ImportComponentTests : BunitContext
{
    public ImportComponentTests()
    {
        Services.AddKlyrooSpreadsheet();
    }

    private static SpreadsheetImporter<ImportRow> NewImporter() => new(SpreadsheetSchema.For<ImportRow>(sb =>
    {
        sb.Key = "people";
        sb.Column(r => r.Name, col => col.Label("Name").Required().Description("Full name").Group("Identity"));
        sb.Column(r => r.Age, col => col.Label("Age"));
    }));

    [Fact]
    public void Choosing_a_file_loads_it()
    {
        var importer = NewImporter();
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("Name;Age\nAda;36\nGrace;45", "people.csv"));

        cut.WaitForAssertion(() => Assert.True(importer.FileLoaded));
        Assert.Equal(2, importer.AllRows.Count);
        Assert.Equal("people.csv", importer.FileName);
    }

    [Fact]
    public void An_xls_file_shows_an_error()
    {
        var importer = NewImporter();
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary([1, 2, 3], "old.xls"));

        cut.WaitForAssertion(() => Assert.Contains(".xls", cut.Find("[data-slot=spreadsheet-file-error]").TextContent));
        Assert.False(importer.FileLoaded);
    }

    [Fact]
    public void Pasted_rows_are_loaded_and_column_groups_and_descriptions_show()
    {
        var importer = NewImporter();
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));
        Assert.Equal("Identity", cut.Find("[data-slot=spreadsheet-expected-group]").GetAttribute("data-group"));
        Assert.Equal("Full name", cut.Find("[data-slot=spreadsheet-expected-pill]").GetAttribute("title"));

        cut.Find("[data-slot=spreadsheet-paste-input]").Input("Name\tAge\nAda\t36");
        cut.Find("[data-slot=spreadsheet-paste-load]").Click();

        Assert.True(importer.FileLoaded);
        Assert.Equal("Ada", importer.Value(0).Name);
    }

    [Fact]
    public void Selected_rows_can_be_left_out_together()
    {
        var importer = NewImporter();
        importer.LoadText("Name;Age\nAda;36\nGrace;45\nLin;29", "people.csv");
        importer.NextStep();
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        var boxes = cut.FindAll("[data-slot=spreadsheet-row-select]");
        boxes[0].Change(true);
        cut.FindAll("[data-slot=spreadsheet-row-select]")[1].Change(true);
        cut.Find("[data-slot=spreadsheet-discard-selected]").Click();

        Assert.Equal(2, importer.DiscardedRows.Count);
        Assert.Empty(importer.SelectedRowIndexes);
    }

    [Fact]
    public void A_skipped_columns_step_is_clickable_from_the_review_step()
    {
        var importer = NewImporter();
        importer.LoadText("Name;Age\nAda;36", "people.csv");
        importer.NextStep();
        var cut = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));

        var columns = cut.Find("[data-slot=spreadsheet-step-item][data-key=columns]");
        Assert.False(columns.HasAttribute("disabled"));
        columns.Click();

        Assert.Equal("columns", importer.CurrentStepKey);
    }
}
