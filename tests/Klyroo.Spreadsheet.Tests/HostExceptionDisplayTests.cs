using Bunit;
using Klyroo.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

// The exceptions of the host's parser, submit and checks are never shown to the uploader. A parser that returns Fail, and an exception made for the user,
// keep their own text.
[PinCulture]
public sealed class HostExceptionDisplayTests : BunitContext
{
    private sealed class Capture : ILogger
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }

    private static CancellationToken Token => Xunit.TestContext.Current.CancellationToken;

    public HostExceptionDisplayTests()
    {
        Services.AddKlyrooSpreadsheet();
    }

    private static SpreadsheetImporter<ImportRow> ParserImporter(Capture? log, SpreadsheetParser<int?> parser) => new(SpreadsheetSchema.For<ImportRow>(sb =>
    {
        sb.Column(r => r.Name);
        sb.Column(r => r.Age, c => c.Parser(parser));
    }), log);

    [Fact]
    public void A_parser_that_throws_gives_a_generic_cell_issue_and_a_parser_that_fails_keeps_its_message()
    {
        var log = new Capture();
        var importer = ParserImporter(log, text => text switch
        {
            "boom" => throw new FormatException("C:\\secret\\path"),
            "bad" => SpreadsheetParseResult<int?>.Fail("Age must be a whole number"),
            _ => SpreadsheetParseResult<int?>.Ok(int.Parse(text)),
        });

        importer.LoadText("Name,Age\na,1\nb,boom\nc,bad\nd,boom", "f.csv");

        var thrown = Assert.Single(importer.AllRows[1].Issues);
        Assert.Equal("row.failed", thrown.Code);
        Assert.Equal("Age", thrown.Field);
        Assert.DoesNotContain("secret", thrown.Message, StringComparison.Ordinal);
        Assert.Same(thrown, importer.AllRows[1].Cells["Age"].Issue);
        Assert.Equal("The row could not be checked", SpreadsheetLocales.En.ResolveIssue(thrown));
        Assert.Equal("Cette ligne n’a pas pu être vérifiée", SpreadsheetLocales.Fr.ResolveIssue(thrown));

        var failed = Assert.Single(importer.AllRows[2].Issues);
        Assert.Equal("Age must be a whole number", failed.Message);
        Assert.Null(failed.Args);

        var entry = Assert.Single(log.Entries);
        Assert.Contains("the parser of the column 'Age'", entry.Message, StringComparison.Ordinal);
        Assert.Contains("2 row(s)", entry.Message, StringComparison.Ordinal);
        Assert.Equal("C:\\secret\\path", entry.Exception!.Message);
    }

    [Fact]
    public void A_parser_that_is_cancelled_is_not_a_cell_issue()
    {
        var importer = ParserImporter(null, _ => throw new OperationCanceledException());

        Assert.Throws<OperationCanceledException>(() => importer.LoadText("Name,Age\na,1", "f.csv"));
    }

    private static SpreadsheetImporter<ImportRow> SubmitImporter(Capture? log, Func<IReadOnlyList<SpreadsheetRow<ImportRow>>, Task<IReadOnlyList<SpreadsheetRejectedRow>?>> submit)
    {
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.OnSubmit = submit;
            sb.Column(r => r.Name);
        }), log);
        importer.LoadText("Name\na\nb", "f.csv");
        return importer;
    }

    [Fact]
    public async Task A_submit_that_throws_logs_the_exception_and_leaves_no_text_for_the_uploader()
    {
        var log = new Capture();
        var importer = SubmitImporter(log, _ => throw new InvalidOperationException("Server=db01;Password=hunter2"));

        await importer.SubmitAsync(Token);

        Assert.Equal(SpreadsheetSubmitStatus.Error, importer.SubmitStatus);
        Assert.Null(importer.SubmitError);
        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("submit", entry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hunter2", entry.Message, StringComparison.Ordinal);
        Assert.Equal("Server=db01;Password=hunter2", entry.Exception!.Message);
    }

    [Fact]
    public async Task A_SpreadsheetUserMessageException_shows_its_message_as_it_is()
    {
        var importer = SubmitImporter(null, _ => throw new SpreadsheetUserMessageException("The import window is closed until Monday"));

        await importer.SubmitAsync(Token);

        Assert.Equal(SpreadsheetSubmitStatus.Error, importer.SubmitStatus);
        Assert.Equal("The import window is closed until Monday", importer.SubmitError);
    }

    [Fact]
    public async Task A_failed_check_logs_the_exception_and_a_user_message_is_shown_as_it_is()
    {
        var log = new Capture();
        var hostile = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.ValidateRowsAsync = (rows, token) => throw new InvalidOperationException("Server=db01;Password=hunter2");
            sb.Column(r => r.Name);
        }), log);
        hostile.LoadText("Name\na", "f.csv");

        await hostile.ValidateAsync(Token);

        Assert.Equal(SpreadsheetValidationStatus.Error, hostile.ValidationStatus);
        Assert.Null(hostile.ValidationError);
        Assert.Equal("Server=db01;Password=hunter2", Assert.Single(log.Entries).Exception!.Message);

        var polite = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.ValidateRowsAsync = (rows, token) => throw new SpreadsheetUserMessageException("The checking service is closed");
            sb.Column(r => r.Name);
        }));
        polite.LoadText("Name\na", "f.csv");
        await polite.ValidateAsync(Token);
        Assert.Equal("The checking service is closed", polite.ValidationError);
    }

    [Fact]
    public async Task The_component_shows_generic_localized_texts_for_a_failed_submit_and_a_failed_check()
    {
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.OnSubmit = _ => throw new InvalidOperationException("Server=db01;Password=hunter2");
            sb.ValidateRowsAsync = (rows, token) => throw new InvalidOperationException("Password=hunter2");
            sb.Column(r => r.Name);
        }));
        importer.LoadText("Name\na", "f.csv");
        await importer.ValidateAsync(Token);
        importer.NextStep();

        var cut = Render<CascadingValue<KlyLocale>>(p => p.Add(x => x.Value, KlyLocales.Fr)
            .AddChildContent<KlySpreadsheetImport>(c => c.Add(x => x.Importer, importer)));
        Assert.DoesNotContain("hunter2", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(SpreadsheetLocales.Fr.CheckFailedGeneric, cut.Markup, StringComparison.Ordinal);

        await cut.InvokeAsync(() => importer.SubmitAsync(Token));
        cut.WaitForAssertion(() => Assert.Contains(SpreadsheetLocales.Fr.ImportFailed, cut.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("hunter2", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void The_generic_texts_exist_in_every_locale()
    {
        foreach (var (code, texts) in SpreadsheetLocales.Tables)
        {
            Assert.False(string.IsNullOrWhiteSpace(texts.CheckFailedGeneric), code);
            Assert.False(string.IsNullOrWhiteSpace(texts.FileReadFailed), code);
            Assert.False(string.IsNullOrWhiteSpace(texts.DownloadFailed), code);
            if (code is not "en")
            {
                Assert.NotEqual(SpreadsheetLocales.En.CheckFailedGeneric, texts.CheckFailedGeneric);
                Assert.NotEqual(SpreadsheetLocales.En.FileReadFailed, texts.FileReadFailed);
                Assert.NotEqual(SpreadsheetLocales.En.DownloadFailed, texts.DownloadFailed);
            }
        }
    }
}
