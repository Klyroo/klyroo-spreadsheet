using Bunit;
using Klyroo.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

// What a callback of the host throws goes to the log, never to the uploader, once per rule with a count, and a rule that keeps throwing is stopped.
[PinCulture]
public sealed class HostFailureReportingTests : BunitContext
{
    private sealed class Capture : ILogger
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }

    public HostFailureReportingTests()
    {
        Services.AddKlyrooSpreadsheet();
    }

    private static string Csv(int rows) => "Name,Qty\n" + string.Join('\n', Enumerable.Range(0, rows).Select(i => $"n{i},{i + 1}"));

    private static SpreadsheetImporter<Fragile> Importer(Capture? log, Func<int, bool> predicate, int limit = 3) => new(SpreadsheetSchema.For<Fragile>(sb =>
    {
        sb.RuleTimeoutLimit = limit;
        sb.Column(r => r.Name);
        sb.Column(r => r.Qty, c => c.Rule("limit", "Too high", v => predicate(v ?? 0)));
    }), log);

    [Fact]
    public void The_issue_is_generic_in_the_language_of_the_user_and_never_the_text_of_the_exception()
    {
        var importer = Importer(null, v => v == 2 ? throw new InvalidOperationException("Server=db01;Password=hunter2") : true);
        importer.LoadText(Csv(3), "f.csv");

        var issue = Assert.Single(importer.AllRows[1].Issues);
        Assert.Equal("row.failed", issue.Code);
        Assert.Equal("The row could not be checked", SpreadsheetLocales.En.ResolveIssue(issue));
        Assert.Equal("Cette ligne n’a pas pu être vérifiée", SpreadsheetLocales.Fr.ResolveIssue(issue));
        Assert.DoesNotContain("hunter2", issue.Message, StringComparison.Ordinal);
        foreach (var (code, texts) in SpreadsheetLocales.Tables)
        {
            var resolved = texts.ResolveIssue(issue);
            Assert.DoesNotContain("hunter2", resolved, StringComparison.Ordinal);
            if (code is not "en") Assert.NotEqual(SpreadsheetLocales.En.IssueRowFailed, resolved);
        }

        var cut = Render<CascadingValue<KlyLocale>>(p => p.Add(x => x.Value, KlyLocales.Fr)
            .AddChildContent<KlySpreadsheetImport>(c => c.Add(x => x.Importer, importer)));
        importer.NextStep();
        importer.NextStep();
        Assert.DoesNotContain("hunter2", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void The_exception_is_logged_once_per_rule_with_the_count_of_rows()
    {
        var log = new Capture();
        var importer = Importer(log, v => v % 2 == 0 ? throw new InvalidOperationException("boom " + v) : true, limit: 0);

        importer.LoadText(Csv(10), "f.csv");

        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("the rule 'limit' of the column 'Qty'", entry.Message, StringComparison.Ordinal);
        Assert.Contains("5 row(s)", entry.Message, StringComparison.Ordinal);
        Assert.Equal("boom 2", entry.Exception!.Message);
        Assert.DoesNotContain("boom", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failure_of_the_factory_setter_or_ValidateRow_is_logged_once_for_all_the_rows()
    {
        var log = new Capture();
        var importer = new SpreadsheetImporter<Fragile>(SpreadsheetSchema.For<Fragile>(sb =>
        {
            sb.Column(r => r.Name);
            sb.Column(r => r.Qty);
        }), log);

        importer.LoadText("Name,Qty\nboom,1\nboom,2\nfine,3", "f.csv");

        var entry = Assert.Single(log.Entries);
        Assert.Contains("2 row(s)", entry.Message, StringComparison.Ordinal);
        Assert.Equal("Name refused", entry.Exception!.Message);
    }

    [Fact]
    public void Without_a_logger_nothing_is_reported_and_the_load_still_succeeds()
    {
        var importer = Importer(null, v => throw new InvalidOperationException("x"));

        importer.LoadText(Csv(4), "f.csv");

        Assert.All(importer.AllRows, r => Assert.False(r.Importable));
    }

    [Fact]
    public void A_rule_that_keeps_throwing_is_stopped_and_reported_once()
    {
        var log = new Capture();
        var calls = 0;
        var importer = Importer(log, _ => throw new InvalidOperationException("always " + ++calls), limit: 3);

        importer.LoadText(Csv(20), "f.csv");

        Assert.Equal(3, calls);
        var stopped = Assert.Single(importer.DisabledRules);
        Assert.Equal(("Qty", "limit", 3, SpreadsheetDisabledReason.Failures), (stopped.ColumnKey, stopped.RuleCode, stopped.Timeouts, stopped.Reason));
        Assert.All(importer.AllRows, r =>
        {
            var issue = Assert.Single(r.Issues);
            Assert.Equal("row.failed", issue.Code);
            Assert.Equal("Qty", issue.Field);
            Assert.False(r.Importable);
        });
        var entry = Assert.Single(log.Entries);
        Assert.Contains("3 row(s)", entry.Message, StringComparison.Ordinal);
        Assert.Contains("stopped for the rest of the read: True", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_component_says_a_stopped_rule_failed_not_that_it_timed_out()
    {
        var importer = Importer(null, _ => throw new InvalidOperationException("always"), limit: 3);
        importer.LoadText(Csv(10), "f.csv");
        importer.NextStep();

        var english = Render<KlySpreadsheetImport>(p => p.Add(x => x.Importer, importer));
        var notice = Assert.Single(english.FindAll("[data-slot=spreadsheet-rule-disabled]"));
        Assert.Contains("was stopped after repeated failures", notice.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("timeouts", notice.TextContent, StringComparison.Ordinal);

        var french = Render<CascadingValue<KlyLocale>>(p => p.Add(x => x.Value, KlyLocales.Fr)
            .AddChildContent<KlySpreadsheetImport>(c => c.Add(x => x.Importer, importer)));
        Assert.Contains("plusieurs échecs", french.Find("[data-slot=spreadsheet-rule-disabled]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void An_edit_that_makes_a_rule_throw_is_logged_for_that_edit()
    {
        var log = new Capture();
        var importer = Importer(log, v => v == 99 ? throw new InvalidOperationException("edit") : true);
        importer.LoadText(Csv(3), "f.csv");
        Assert.Empty(log.Entries);

        importer.EditCell(1, "Qty", "99");

        var entry = Assert.Single(log.Entries);
        Assert.Contains("1 row(s)", entry.Message, StringComparison.Ordinal);
        Assert.Equal("row.failed", Assert.Single(importer.AllRows[1].Issues).Code);
    }
}
