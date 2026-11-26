using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

public sealed class PatternTimeoutTests
{
    // (a+)+$ backtracks exponentially on a long run of "a" followed by a character that cannot match.
    private static readonly string Hostile = new string('a', 60) + "!";

    private static SpreadsheetImporter NewImporter(SpreadsheetRule rule) => new(new SpreadsheetSchema
    {
        Columns = [new SpreadsheetColumnSchema { Key = "code", Rules = [rule] }]
    });

    [Fact]
    public void A_hostile_cell_gets_a_pattern_issue_instead_of_hanging_the_import()
    {
        var importer = NewImporter(SpreadsheetRule.Pattern("^(a+)+$", "Letters only", matchTimeout: TimeSpan.FromMilliseconds(50)));
        var watch = Stopwatch.StartNew();

        importer.LoadText($"code\nok\n{Hostile}\naaa", "codes.csv");

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"Import took {watch.Elapsed}");
        var hostile = importer.AllRows[1];
        var issue = Assert.Single(hostile.Issues);
        Assert.Equal("pattern", issue.Code);
        Assert.Equal("code", issue.Field);
        Assert.Equal(SpreadsheetIssueLevel.Error, issue.Level);
        Assert.False(hostile.Importable);
        Assert.Contains("in time", issue.Message, StringComparison.Ordinal);
        // The other rows are judged normally: "ok" fails the pattern, "aaa" passes.
        Assert.Equal("Letters only", Assert.Single(importer.AllRows[0].Issues).Message);
        Assert.True(importer.AllRows[2].Importable);
    }

    [Fact]
    public void A_timed_out_check_keeps_the_severity_of_the_rule()
    {
        var importer = NewImporter(SpreadsheetRule.Pattern("^(a+)+$", level: SpreadsheetIssueLevel.Warning, matchTimeout: TimeSpan.FromMilliseconds(50)));

        importer.LoadText($"code\n{Hostile}", "codes.csv");

        Assert.Equal(SpreadsheetIssueLevel.Warning, Assert.Single(importer.AllRows[0].Issues).Level);
        Assert.True(importer.AllRows[0].Importable);
    }

    [Fact]
    public void The_default_timeout_applies_and_can_be_replaced()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(100), SpreadsheetRule.DefaultPatternTimeout);
        var importer = NewImporter(SpreadsheetRule.Pattern("^(a+)+$"));

        importer.LoadText($"code\n{Hostile}", "codes.csv");

        Assert.Equal("pattern", Assert.Single(importer.AllRows[0].Issues).Code);
        Assert.Throws<ArgumentOutOfRangeException>(() => SpreadsheetRule.Pattern("a", matchTimeout: TimeSpan.Zero));
    }

    [Fact]
    public void A_custom_rule_that_throws_a_regex_timeout_is_handled_too()
    {
        var rule = new SpreadsheetRule("custom", "Custom", _ => throw new RegexMatchTimeoutException());
        var importer = NewImporter(rule);

        importer.LoadText("code\nabc", "codes.csv");

        Assert.Equal("pattern", Assert.Single(importer.AllRows[0].Issues).Code);
    }

    [Fact]
    public void A_timed_out_rule_does_not_break_column_suggestions()
    {
        var importer = new SpreadsheetImporter(new SpreadsheetSchema
        {
            Columns = [new SpreadsheetColumnSchema { Key = "sku", Required = true, Rules = [SpreadsheetRule.Pattern("^(a+)+$", matchTimeout: TimeSpan.FromMilliseconds(20))] }]
        });

        importer.LoadText($"other\n{Hostile}\naaa\naaaa\naa\naaaaa", "codes.csv");

        Assert.Equal("other", importer.Fields[0].Suggestion?.HeaderText);
    }
}
