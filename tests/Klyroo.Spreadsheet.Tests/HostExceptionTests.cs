using Xunit;

namespace Klyroo.Spreadsheet.Tests;

public sealed class Fragile
{
    private string? _name;

    public string? Name
    {
        get => _name;
        set => _name = value == "boom" ? throw new InvalidOperationException("Name refused") : value;
    }

    public int? Qty { get; set; }
}

// A host setter, factory, rule predicate or ValidateRow that throws is a row issue, never a failed load.
public sealed class HostExceptionTests
{
    private static SpreadsheetImporter<Fragile> Importer(Action<SpreadsheetSchemaBuilder<Fragile>>? extra = null) =>
        new(SpreadsheetSchema.For<Fragile>(sb =>
        {
            sb.Column(r => r.Name);
            sb.Column(r => r.Qty);
            extra?.Invoke(sb);
        }));

    [Fact]
    public void A_throwing_setter_is_a_row_issue_that_does_not_show_the_exception()
    {
        var importer = Importer();

        importer.LoadText("Name,Qty\na,1\nboom,2\nc,3", "f.csv");

        Assert.Equal(3, importer.AllRows.Count);
        Assert.Empty(importer.AllRows[0].Issues);
        var issue = Assert.Single(importer.AllRows[1].Issues);
        Assert.Equal(SpreadsheetIssueLevel.Error, issue.Level);
        Assert.Equal("row.failed", issue.Code);
        Assert.DoesNotContain("Name refused", issue.Message, StringComparison.Ordinal);
        Assert.False(importer.AllRows[1].Importable);
        Assert.Equal(SpreadsheetRowStatus.Blocking, importer.AllRows[1].Status);
        Assert.Equal([0, 2], importer.ImportableRows.Select(r => r.Index));
    }

    [Fact]
    public void The_importable_typed_rows_leave_the_refused_row_out_instead_of_throwing()
    {
        var importer = Importer(sb => sb.ValidateRow = row => []);

        importer.LoadText("Name,Qty\na,1\nboom,2\nc,3", "f.csv");

        Assert.Equal(["a", "c"], importer.ImportableTypedRows.Select(r => r.Value.Name));
        Assert.DoesNotContain("Name refused", Assert.Single(importer.AllRows[1].Issues).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToTyped_of_a_row_the_setter_refuses_fails_with_the_row_and_the_cause()
    {
        var importer = Importer();
        importer.LoadText("Name,Qty\nboom,2", "f.csv");

        var failure = Assert.Throws<InvalidOperationException>(() => importer.ToTyped(importer.AllRows[0]));

        Assert.Contains("row 2", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Name refused", failure.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(failure.InnerException);
    }

    [Fact]
    public void A_throwing_factory_is_a_row_issue_on_every_row()
    {
        var calls = 0;
        var importer = new SpreadsheetImporter<Fragile>(SpreadsheetSchema.For<Fragile>(
            () => ++calls > 1 ? throw new InvalidOperationException("No instance") : new Fragile(),
            sb => sb.Column(r => r.Name)));

        importer.LoadText("Name\na\nb\nc", "f.csv");

        Assert.Equal(3, importer.AllRows.Count);
        Assert.All(importer.AllRows.Skip(1), r => Assert.DoesNotContain("No instance", Assert.Single(r.Issues).Message, StringComparison.Ordinal));
        Assert.All(importer.AllRows.Skip(1), r => Assert.Equal("row.failed", Assert.Single(r.Issues).Code));
    }

    [Fact]
    public void A_throwing_ValidateRow_is_a_row_issue_too_without_the_exception_text()
    {
        var importer = Importer(sb => sb.ValidateRow = row => row.Name == "bad" ? throw new ArgumentException("Cannot check this row") : []);

        importer.LoadText("Name,Qty\nok,1\nbad,2", "f.csv");

        Assert.Empty(importer.AllRows[0].Issues);
        var validateIssue = Assert.Single(importer.AllRows[1].Issues);
        Assert.Equal("row.failed", validateIssue.Code);
        Assert.DoesNotContain("Cannot check this row", validateIssue.Message, StringComparison.Ordinal);
        Assert.False(importer.AllRows[1].Importable);
    }

    [Fact]
    public void An_edit_that_makes_the_setter_throw_is_reported_and_undoing_it_clears_the_issue()
    {
        var importer = Importer();
        importer.LoadText("Name,Qty\na,1", "f.csv");

        importer.EditCell(0, "Name", "boom");
        Assert.False(importer.AllRows[0].Importable);

        importer.RevertCell(0, "Name");
        Assert.True(importer.AllRows[0].Importable);
    }

    [Fact]
    public void A_throwing_rule_predicate_is_a_row_issue_that_does_not_show_the_exception()
    {
        var importer = new SpreadsheetImporter<Fragile>(SpreadsheetSchema.For<Fragile>(sb =>
        {
            sb.Column(r => r.Name);
            sb.Column(r => r.Qty, c => c.Rule("positive", "Must be positive", v => v > 0 ? true : throw new InvalidOperationException("Cannot check this quantity")));
        }));

        importer.LoadText("Name,Qty\na,1\nb,-5\nc,3", "f.csv");

        Assert.Equal(3, importer.AllRows.Count);
        Assert.Empty(importer.AllRows[0].Issues);
        var issue = Assert.Single(importer.AllRows[1].Issues);
        Assert.Equal("row.failed", issue.Code);
        Assert.Equal("Qty", issue.Field);
        Assert.Same(issue, importer.AllRows[1].Cells["Qty"].Issue);
        Assert.Null(importer.AllRows[1].Cells["Name"].Issue);
        Assert.Equal(SpreadsheetIssueLevel.Error, issue.Level);
        Assert.DoesNotContain("Cannot check this quantity", issue.Message, StringComparison.Ordinal);
        Assert.False(importer.AllRows[1].Importable);
        Assert.Equal([0, 2], importer.ImportableRows.Select(r => r.Index));
    }

    [Fact]
    public void A_throwing_rule_blocks_the_row_whatever_the_level_of_the_rule_and_does_not_hide_the_other_rules()
    {
        var importer = new SpreadsheetImporter<Fragile>(SpreadsheetSchema.For<Fragile>(sb =>
        {
            sb.Column(r => r.Name, c => c
                .Rule("soft", "Soft", v => v == "x" ? throw new InvalidOperationException("Soft rule broke") : true, SpreadsheetIssueLevel.Warning)
                .Rule("long", "Too short", v => v!.Length > 3));
            sb.Column(r => r.Qty);
        }));

        importer.LoadText("Name,Qty\nx,1", "f.csv");

        var issues = importer.AllRows[0].Issues;
        Assert.Contains(issues, i => i.Code == "row.failed" && i.Level == SpreadsheetIssueLevel.Error && !i.Message.Contains("Soft rule broke", StringComparison.Ordinal));
        Assert.Contains(issues, i => i.Code == "long");
        Assert.False(importer.AllRows[0].Importable);
    }

    [Fact]
    public void A_throwing_rule_does_not_fail_the_load_when_a_column_is_suggested_from_the_values()
    {
        var importer = new SpreadsheetImporter<Fragile>(SpreadsheetSchema.For<Fragile>(sb =>
        {
            sb.Column(r => r.Name);
            sb.Column(r => r.Qty, c => c.Rule("boom", "Boom", _ => throw new InvalidOperationException("always")));
        }));

        var load = Record.Exception(() => importer.LoadText("Name,Other\na,1\nb,2", "f.csv"));

        Assert.Null(load);
        Assert.Equal(2, importer.AllRows.Count);
    }

    [Fact]
    public void A_rule_that_is_cancelled_is_not_a_row_issue()
    {
        var importer = new SpreadsheetImporter<Fragile>(SpreadsheetSchema.For<Fragile>(sb =>
        {
            sb.Column(r => r.Name);
            sb.Column(r => r.Qty, c => c.Rule("stop", "Stop", _ => throw new OperationCanceledException()));
        }));

        Assert.Throws<OperationCanceledException>(() => importer.LoadText("Name,Qty\na,1", "f.csv"));
    }
}
