using System.Diagnostics;
using Bunit;
using Klyroo.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

// A rule whose matches keep timing out is stopped after a few timeouts, so a hostile file costs a few timeouts, not one per cell.
public sealed class RuleCircuitBreakerTests : BunitContext
{
    private static readonly string Hostile = new string('a', 60) + "!";

    public RuleCircuitBreakerTests()
    {
        Services.AddKlyrooSpreadsheet();
    }

    private static SpreadsheetImporter Importer(int? limit = null, params SpreadsheetRule[] rules) => new(new SpreadsheetSchema
    {
        RuleTimeoutLimit = limit ?? 3,
        Columns = [new SpreadsheetColumnSchema { Key = "code", Label = "Code", Rules = rules }],
    });

    private static string Csv(int rows) => "code\n" + string.Join('\n', Enumerable.Repeat(Hostile, rows));

    [Fact]
    public void The_default_limit_is_three()
    {
        Assert.Equal(3, new SpreadsheetSchema().RuleTimeoutLimit);
    }

    [Fact]
    public void A_hostile_file_costs_a_few_timeouts_and_reports_the_rule_once()
    {
        var importer = Importer(null, SpreadsheetRule.Pattern("^(a+)+$", matchTimeout: TimeSpan.FromMilliseconds(50)));
        var watch = Stopwatch.StartNew();

        importer.LoadText(Csv(300), "codes.csv");

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"Took {watch.Elapsed}");   // 300 x 50 ms would be 15 s
        var disabled = Assert.Single(importer.DisabledRules);
        Assert.Equal(new SpreadsheetDisabledRule("code", "pattern", 3), disabled);
        // The values after the limit are not waved through: they get the same "could not be checked in time" issue, without running the regex.
        Assert.Equal(300, importer.AllRows.Count(r => r.Issues.Any(i => i.Code == "pattern" && i.Args is ["timeout"])));
        Assert.Empty(importer.ImportableRows);
    }

    [Fact]
    public void The_limit_is_an_option_and_zero_never_stops_a_rule()
    {
        var rule = SpreadsheetRule.Pattern("^(a+)+$", matchTimeout: TimeSpan.FromMilliseconds(20));

        var one = Importer(1, rule);
        one.LoadText(Csv(10), "codes.csv");
        Assert.Equal(10, one.AllRows.Count(r => r.Issues.Count > 0));
        Assert.Equal(1, Assert.Single(one.DisabledRules).Timeouts);

        var never = Importer(0, rule);
        never.LoadText(Csv(6), "codes.csv");
        Assert.Equal(6, never.AllRows.Count(r => r.Issues.Count > 0));
        Assert.Empty(never.DisabledRules);
    }

    [Fact]
    public void Each_rule_is_counted_on_its_own_and_ordinary_failures_do_not_count()
    {
        var slow = SpreadsheetRule.Pattern("^(a+)+$", matchTimeout: TimeSpan.FromMilliseconds(20));
        var plain = SpreadsheetRule.Pattern("^b", "Starts with b");
        var importer = Importer(2, slow, plain);

        importer.LoadText(Csv(8), "codes.csv");

        Assert.Equal("pattern", Assert.Single(importer.DisabledRules).RuleCode);
        // The second rule keeps judging every row: all 8 fail it, whatever the first one does.
        Assert.Equal(8, importer.AllRows.Count(r => r.Issues.Any(i => i.Message == "Starts with b")));
    }

    [Fact]
    public void Reading_the_file_again_starts_the_count_again()
    {
        var importer = Importer(null, SpreadsheetRule.Pattern("^(a+)+$", matchTimeout: TimeSpan.FromMilliseconds(20)));
        importer.LoadText(Csv(10), "a.csv");
        Assert.Single(importer.DisabledRules);

        importer.LoadText("code\naaa", "b.csv");
        Assert.Empty(importer.DisabledRules);

        importer.LoadText(Csv(10), "c.csv");
        Assert.Equal(3, Assert.Single(importer.DisabledRules).Timeouts);
        Assert.Equal(10, importer.AllRows.Count(r => r.Issues.Count > 0));
    }

    [Fact]
    public void A_stopped_rule_stays_stopped_for_edits_of_the_same_read()
    {
        var importer = Importer(null, SpreadsheetRule.Pattern("^(a+)+$", matchTimeout: TimeSpan.FromMilliseconds(20)));
        importer.LoadText(Csv(10), "a.csv");

        var watch = Stopwatch.StartNew();
        importer.EditCell(9, "code", Hostile + "?");

        Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(15));                        // no new timeout was paid
        Assert.Equal("pattern", Assert.Single(importer.AllRows[9].Issues).Code);            // and the edited value is still not waved through
        Assert.False(importer.AllRows[9].Importable);
    }

    [Fact]
    public void The_component_names_the_stopped_rule_once_in_the_language_of_the_user()
    {
        var importer = Importer(null, SpreadsheetRule.Pattern("^(a+)+$", matchTimeout: TimeSpan.FromMilliseconds(20)));
        importer.LoadText(Csv(20), "codes.csv");
        importer.NextStep();

        var cut = Render<CascadingValue<KlyLocale>>(p => p.Add(x => x.Value, KlyLocales.Fr)
            .AddChildContent<KlySpreadsheetImport>(c => c.Add(x => x.Importer, importer)));

        var notice = Assert.Single(cut.FindAll("[data-slot=spreadsheet-rule-disabled]"));
        Assert.Equal("pattern", notice.GetAttribute("data-rule"));
        Assert.Contains("Code", notice.TextContent, StringComparison.Ordinal);
        Assert.Contains("a été arrêtée", notice.TextContent, StringComparison.Ordinal);

        var english = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));
        Assert.Contains("was stopped after repeated timeouts", english.Find("[data-slot=spreadsheet-rule-disabled]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void No_notice_without_a_stopped_rule()
    {
        var importer = Importer(null, SpreadsheetRule.Pattern("^a"));
        importer.LoadText("code\nabc", "codes.csv");
        importer.NextStep();

        Assert.Empty(Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer)).FindAll("[data-slot=spreadsheet-rule-disabled]"));
    }
}
