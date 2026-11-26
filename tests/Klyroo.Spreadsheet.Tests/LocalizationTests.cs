using System.Reflection;
using Bunit;
using Klyroo.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Klyroo.Spreadsheet.Tests;

// The texts of the component and the messages of the issues follow the locale of Klyroo.UI, like the other Klyroo packages.
public sealed class LocalizationTests : BunitContext
{
    public LocalizationTests()
    {
        Services.AddKlyrooSpreadsheet();
    }

    private static CancellationToken Token => Xunit.TestContext.Current.CancellationToken;

    private static SpreadsheetSchema<ImportRow> Schema(Func<IReadOnlyList<SpreadsheetRow<ImportRow>>, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>? onSubmit = null) =>
        SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Key = "people";
            sb.OnSubmit = onSubmit;
            sb.RowKey(sb.Column(r => r.Email, col => col.Label("Email").Required().Rule(SpreadsheetRule.Email())));
            sb.Column(r => r.Age, col => col.Label("Age").Rule(SpreadsheetRule.Between(0, 120)));
        });

    private IRenderedComponent<CascadingValue<KlyLocale>> Render(KlyLocale locale, SpreadsheetImporter<ImportRow> importer, Action<ComponentParameterCollectionBuilder<KlySpreadsheetImport>>? parameters = null) =>
        Render<CascadingValue<KlyLocale>>(p => p
            .Add(x => x.Value, locale)
            .AddChildContent<KlySpreadsheetImport>(c =>
            {
                c.Add(x => x.Importer, importer);
                parameters?.Invoke(c);
            }));

    // The sets

    [Fact]
    public void Every_locale_of_Klyroo_UI_has_a_complete_set()
    {
        Assert.Equal(KlyLocales.All.Select(l => l.Code).Order(StringComparer.OrdinalIgnoreCase), SpreadsheetLocales.Tables.Keys.Order(StringComparer.OrdinalIgnoreCase));
        Assert.Equal(["en", "fr"], SpreadsheetLocales.ReviewedCodes);
        var properties = typeof(SpreadsheetTexts).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.True(properties.Length > 100);
        foreach (var (code, texts) in SpreadsheetLocales.Tables)
        {
            foreach (var property in properties)
            {
                var value = property.GetValue(texts);
                var shown = value switch
                {
                    string s => new[] { s },
                    Func<int, string> f => new[] { f(0), f(1), f(2), f(5), f(21), f(1500) },
                    Func<string, string> f => new[] { f("Office") },
                    Func<string, string, string> f => new[] { f("Office", "A, B") },
                    Func<int, int, string> f => new[] { f(1, 1), f(2, 12), f(1500, 20000) },
                    Func<int, int, int, string> f => new[] { f(1, 1, 1), f(2, 12, 1500) },
                    _ => throw new InvalidOperationException($"{property.Name} has an unexpected type."),
                };
                Assert.All(shown, s => Assert.False(string.IsNullOrWhiteSpace(s), $"{code}.{property.Name}"));
            }
            if (code != "en")
            {
                // A translated set does not fall back to the English text of a label.
                Assert.NotEqual(SpreadsheetLocales.En.Continue, texts.Continue);
                Assert.NotEqual(SpreadsheetLocales.En.DropFileHere, texts.DropFileHere);
                Assert.NotEqual(SpreadsheetLocales.En.IssueRowExists, texts.IssueRowExists);
                Assert.NotEqual(SpreadsheetLocales.En.LiveReview(1, 2, 3), texts.LiveReview(1, 2, 3));
            }
        }
    }

    [Fact]
    public void A_function_text_carries_its_values_in_every_locale()
    {
        foreach (var (code, texts) in SpreadsheetLocales.Tables)
        {
            Assert.Contains("Office", texts.CreateValue("Office"), StringComparison.Ordinal);
            Assert.Contains("Office", texts.IssueNumber("Office"), StringComparison.Ordinal);
            Assert.Contains("7", texts.IssueRowDuplicate("7"), StringComparison.Ordinal);
            Assert.Contains("12", texts.SelectRow(12), StringComparison.Ordinal);
            var range = texts.RowsRange(51, 100, 120);
            Assert.True(range.Contains("51", StringComparison.Ordinal) && range.Contains("100", StringComparison.Ordinal) && range.Contains("120", StringComparison.Ordinal), $"{code}: {range}");
            var live = texts.LiveReview(3, 4, 5);
            Assert.True(live.Contains('3') && live.Contains('4') && live.Contains('5'), $"{code}: {live}");
            Assert.Contains("0", texts.IssueBetween("0", "100"), StringComparison.Ordinal);
            Assert.Contains("100", texts.IssueBetween("0", "100"), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Counts_group_thousands_the_way_the_language_writes_them_and_take_their_plural_form()
    {
        Assert.Equal("Rows 1 to 50 of 12,345", SpreadsheetLocales.En.RowsRange(1, 50, 12345));
        Assert.Equal("Lignes 1 à 50 sur 12 345", SpreadsheetLocales.Fr.RowsRange(1, 50, 12345));
        Assert.Equal("Zeilen 1 bis 50 von 12.345", SpreadsheetLocales.De.RowsRange(1, 50, 12345));
        Assert.Equal("1 ligne", SpreadsheetLocales.Fr.RowCount(1));
        Assert.Equal("0 ligne", SpreadsheetLocales.Fr.RowCount(0));
        Assert.Equal("2 lignes", SpreadsheetLocales.Fr.RowCount(2));
        Assert.Equal("1 row", SpreadsheetLocales.En.RowCount(1));
        Assert.Equal("Importer 1 ligne", SpreadsheetLocales.Fr.ImportRows(1));
        Assert.Equal("Importer 1 200 lignes", SpreadsheetLocales.Fr.ImportRows(1200));
        Assert.Equal("1 Zeile", SpreadsheetLocales.De.RowCount(1));
        Assert.Equal("5 строк", SpreadsheetLocales.Ru.RowCount(5));
        Assert.Equal("21 строка", SpreadsheetLocales.Ru.RowCount(21));
        Assert.Equal("22 строки", SpreadsheetLocales.Ru.RowCount(22));
        Assert.Equal("11 строк", SpreadsheetLocales.Ru.RowCount(11));
        Assert.Equal("3 صفوف", SpreadsheetLocales.Ar.RowCount(3));
        Assert.Equal("12 صف", SpreadsheetLocales.Ar.RowCount(12));
        Assert.Equal("5行", SpreadsheetLocales.Ja.RowCount(5));
    }

    [Fact]
    public void The_texts_of_a_locale_resolve_like_the_other_packages_do()
    {
        var options = Services.GetRequiredService<KlyrooOptions>().Extension<KlyrooSpreadsheetOptions>();

        var texts = typeof(KlyrooSpreadsheetOptions).GetMethod("ResolveTexts", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Equal("Continuer", ((SpreadsheetTexts)texts.Invoke(options, ["fr"])!).Continue);
        Assert.Equal("Continuer", ((SpreadsheetTexts)texts.Invoke(options, ["fr-CA"])!).Continue);       // a region resolves to its language
        Assert.Equal("Weiter", ((SpreadsheetTexts)texts.Invoke(options, ["de"])!).Continue);
        Assert.Equal("Continue", ((SpreadsheetTexts)texts.Invoke(options, ["tlh"])!).Continue);            // an unknown language gets English
        Assert.Equal("继续", ((SpreadsheetTexts)texts.Invoke(options, ["zh-Hans"])!).Continue);
    }

    // The component

    [Fact]
    public void The_component_shows_the_texts_of_its_locale()
    {
        var importer = new SpreadsheetImporter<ImportRow>(Schema());

        var fr = Render(KlyLocales.Fr, importer);
        Assert.Equal("Importer", fr.Find("[data-slot=spreadsheet-title]").TextContent.Trim());
        Assert.Contains("Déposez votre fichier ici", fr.Find("[data-slot=spreadsheet-dropzone]").TextContent, StringComparison.Ordinal);
        Assert.Equal("Choisir un fichier à importer", fr.Find("[data-slot=spreadsheet-file-input]").GetAttribute("aria-label"));
        Assert.Equal("Télécharger un modèle", fr.Find("[data-slot=spreadsheet-download-template]").TextContent.Trim());
        Assert.Equal("Choisissez un fichier pour continuer", fr.Find("[data-slot=spreadsheet-nav-hint]").TextContent.Trim());
        Assert.Equal("Continuer", fr.Find("[data-slot=spreadsheet-nav-continue]").TextContent.Trim());
        Assert.Equal("Fichier", fr.Find("[data-slot=spreadsheet-step-item][data-key=file]").TextContent.Replace("1.", "").Trim());

        var en = Render(KlyLocales.En, importer);
        Assert.Equal("Import", en.Find("[data-slot=spreadsheet-title]").TextContent.Trim());
        Assert.Equal("Continue", en.Find("[data-slot=spreadsheet-nav-continue]").TextContent.Trim());

        var de = Render(KlyLocales.De, importer);
        Assert.Equal("Weiter", de.Find("[data-slot=spreadsheet-nav-continue]").TextContent.Trim());
    }

    [Fact]
    public void A_title_you_set_is_shown_as_it_is()
    {
        var cut = Render(KlyLocales.Fr, new SpreadsheetImporter<ImportRow>(Schema()), p => p.Add(x => x.Title, "Import prices"));

        Assert.Equal("Import prices", cut.Find("[data-slot=spreadsheet-title]").TextContent.Trim());
    }

    [Fact]
    public void The_review_pager_and_stats_are_translated()
    {
        var importer = new SpreadsheetImporter<ImportRow>(Schema());
        importer.LoadText("Email;Age\na@example.com;1\nb@example.com;2\nc@example.com;x", "people.csv");
        importer.NextStep();

        var cut = Render(KlyLocales.Fr, importer);

        Assert.Equal("Pagination", cut.Find("[data-slot=spreadsheet-pager]").GetAttribute("aria-label"));
        Assert.Equal("Page 1 sur 1", cut.Find("[data-slot=spreadsheet-pager-status]").TextContent.Trim());
        Assert.Equal("Lignes 1 à 3 sur 3", cut.Find("[data-slot=spreadsheet-pager-range]").TextContent.Trim());
        Assert.Equal("Page suivante", cut.Find("[data-slot=spreadsheet-pager-next]").GetAttribute("aria-label"));
        Assert.Contains("lignes en erreur", cut.Find("[data-slot=spreadsheet-stat-invalid]").TextContent, StringComparison.Ordinal);
        Assert.Equal("À importer (2)", cut.Find("[data-slot=spreadsheet-review-tab][data-tab=importable]").TextContent.Trim());
        Assert.Equal("Bloquante", cut.FindAll("[data-slot=spreadsheet-row-status]")[2].TextContent.Trim());
        Assert.Equal("Télécharger la ligne en erreur", cut.Find("[data-slot=spreadsheet-download-invalid]").TextContent.Trim());
    }

    [Fact]
    public void Issue_messages_are_resolved_by_code_in_the_locale_of_the_user()
    {
        var importer = new SpreadsheetImporter<ImportRow>(Schema());
        importer.LoadText("Email;Age\nnot-an-email;1\nb@example.com;x\nc@example.com;500\nb@example.com;5", "people.csv");
        importer.NextStep();
        importer.InspectRow(0);

        var cut = Render(KlyLocales.Fr, importer);

        Assert.Equal("Ce n’est pas une adresse e-mail", cut.Find("[data-slot=inspector-issue]").TextContent.Split(':', 2)[1].Trim());
        importer.InspectRow(1);
        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("[data-slot=inspector-issue]"), e => e.TextContent.Contains("« x » n’est pas un nombre", StringComparison.Ordinal)));
        Assert.Contains(cut.FindAll("[data-slot=inspector-issue]"), e => e.TextContent.Contains("Même clé que la ligne 5", StringComparison.Ordinal));
        importer.InspectRow(2);
        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("[data-slot=inspector-issue]"), e => e.TextContent.Contains("La valeur n’est pas comprise entre 0 et 120", StringComparison.Ordinal)));
        // The title of the cell carries the same message.
        var titles = cut.FindAll("[data-slot=spreadsheet-cell-trigger]").Select(e => e.GetAttribute("title")).ToList();
        Assert.Contains("La valeur n’est pas comprise entre 0 et 120", titles);
    }

    [Fact]
    public void A_message_you_wrote_is_never_replaced()
    {
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.ValidateRow = row => [new SpreadsheetRowIssue(null, "custom", SpreadsheetIssueLevel.Error, "Total does not add up")];
            sb.Column(r => r.Email, col => col
                .Rule(SpreadsheetRule.Email("Use a company address"))
                .Rule(SpreadsheetRule.Pattern("^a", "Starts with a")));
            sb.Column(r => r.Age, col => col.Parser(text => SpreadsheetParseResult<int?>.Fail("Age is not valid here")));
        }));
        importer.LoadText("Email;Age\nnope;1", "people.csv");
        importer.NextStep();
        importer.InspectRow(0);

        var cut = Render(KlyLocales.Fr, importer);
        var shown = string.Join('\n', cut.FindAll("[data-slot=inspector-issue]").Select(e => e.TextContent));

        Assert.Contains("Use a company address", shown, StringComparison.Ordinal);
        Assert.Contains("Starts with a", shown, StringComparison.Ordinal);
        Assert.Contains("Total does not add up", shown, StringComparison.Ordinal);
        Assert.Contains("Age is not valid here", shown, StringComparison.Ordinal);
    }

    [Fact]
    public void The_library_marks_the_messages_it_wrote_and_the_English_set_gives_back_the_same_text()
    {
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.ExistingRecords = new Dictionary<string, ImportRow> { ["old"] = new ImportRow() };
            sb.ExistingAction = SpreadsheetExistingAction.Error;
            var colKey = sb.Column(r => r.Key, col => col.Required());
            sb.Column(r => r.Mail, col => col.Rule(SpreadsheetRule.Email()).Rule(SpreadsheetRule.Pattern("^zzz")));
            sb.Column(r => r.Num, col => col
                .Rule(SpreadsheetRule.Min(10d))
                .Rule(SpreadsheetRule.Max(20d))
                .Rule(SpreadsheetRule.Between(12d, 18d)));
            sb.Column(r => r.Flag);
            sb.Column(r => r.Day);
            sb.Column(r => r.Lvl, col => col
                .Options([new SpreadsheetOption<string?>("a", "A")])
                .UnknownPolicy(SpreadsheetUnknownValuePolicy.Error));
            sb.Column(r => r.Lvl2, col => col.Options([new SpreadsheetOption<string?>("a", "A")]));
            sb.Column(r => r.Lvl3, col => col
                .Options([new SpreadsheetOption<string?>("a", "A")])
                .UnknownPolicy(SpreadsheetUnknownValuePolicy.SkipRows));
            sb.RowKey(colKey);
        }));
        importer.LoadText("key;mail;num;flag;day;lvl;lvl2;lvl3\n;bad;5;maybe;31/02/2026;zz;zz;zz\nold;a@example.com;25;yes;01/01/2026;A;A;A\nold;a@example.com;15;yes;01/01/2026;A;A;A", "all.csv");

        var issues = importer.AllRows.SelectMany(r => r.Issues).ToList();
        var codes = issues.Select(i => i.Code).Distinct().Order().ToList();
        string[] expected = ["between", "boolean", "date", "email", "max", "min", "pattern", "required", "row.duplicate", "row.exists", "value.not-found", "value.skipped", "value.unknown"];
        Assert.Equal(expected, codes.Where(c => c != "number"));
        Assert.All(issues, i => Assert.NotNull(i.Args));
        foreach (var issue in issues)
        {
            Assert.Equal(issue.Message, SpreadsheetLocales.En.ResolveIssue(issue));         // the English set says exactly what the importer wrote
            if (issue.Code is not ("required" or "row.exists" or "email" or "pattern"))
            {
                Assert.NotEmpty(issue.Args!);
            }
            Assert.NotEqual(issue.Message, SpreadsheetLocales.Fr.ResolveIssue(issue));        // and every other set says something else
            Assert.NotEqual(issue.Message, SpreadsheetLocales.De.ResolveIssue(issue));
        }
    }

    [Fact]
    public void ResolveIssue_returns_the_message_as_it_is_without_arguments_or_for_an_unknown_code()
    {
        var texts = SpreadsheetLocales.Fr;

        Assert.Equal("As written", texts.ResolveIssue(new SpreadsheetRowIssue(null, "required", SpreadsheetIssueLevel.Error, "As written")));
        Assert.Equal("As written", texts.ResolveIssue(new SpreadsheetRowIssue(null, "mine", SpreadsheetIssueLevel.Error, "As written", [])));
        Assert.Equal("Valeur obligatoire manquante", texts.ResolveIssue(new SpreadsheetRowIssue(null, "required", SpreadsheetIssueLevel.Error, "As written", [])));
        Assert.Equal("Le format n’a pas pu être vérifié dans le temps imparti", texts.ResolveIssue(new SpreadsheetRowIssue("a", "pattern", SpreadsheetIssueLevel.Error, "x", ["timeout"])));
        Assert.Equal("Format inattendu", texts.ResolveIssue(new SpreadsheetRowIssue("a", "pattern", SpreadsheetIssueLevel.Error, "x", [])));
        Assert.Throws<ArgumentNullException>(() => texts.ResolveIssue(null!));
    }

    [Fact]
    public void A_regex_timeout_is_announced_in_the_locale()
    {
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.Column(r => r.Code, col => col.Rule(SpreadsheetRule.Pattern("^(a+)+$", matchTimeout: TimeSpan.FromMilliseconds(20))));
        }));
        importer.LoadText($"code\n{new string('a', 60)}!", "codes.csv");

        var issue = Assert.Single(importer.AllRows[0].Issues);

        Assert.Equal("Le format n’a pas pu être vérifié dans le temps imparti", SpreadsheetLocales.Fr.ResolveIssue(issue));
        Assert.Equal("The format could not be checked in time", SpreadsheetLocales.En.ResolveIssue(issue));
    }

    // Configuration

    [Fact]
    public void A_change_applies_to_its_locale_and_to_the_locales_that_resolve_to_it()
    {
        Services.AddKlyrooSpreadsheet(s => s.Texts("fr", t => t with { Continue = "Poursuivre", IssueRowExists = "Déjà en base" }));
        var canadian = KlyLocales.Extend(KlyLocales.Fr, code: "fr-CA", name: "Français (Canada)");
        var importer = new SpreadsheetImporter<ImportRow>(Schema());

        Assert.Equal("Poursuivre", Render(KlyLocales.Fr, importer).Find("[data-slot=spreadsheet-nav-continue]").TextContent.Trim());
        Assert.Equal("Poursuivre", Render(canadian, importer).Find("[data-slot=spreadsheet-nav-continue]").TextContent.Trim());
        Assert.Equal("Continue", Render(KlyLocales.En, importer).Find("[data-slot=spreadsheet-nav-continue]").TextContent.Trim());
    }

    [Fact]
    public void Texts_cannot_change_once_a_component_resolved_one()
    {
        Render(KlyLocales.En, new SpreadsheetImporter<ImportRow>(Schema()));
        var options = Services.GetRequiredService<KlyrooOptions>().Extension<KlyrooSpreadsheetOptions>();

        Assert.Throws<InvalidOperationException>(() => options.Texts("en", t => t));
    }

    [Fact]
    public void The_download_of_the_invalid_rows_is_written_in_the_locale()
    {
        var importer = new SpreadsheetImporter<ImportRow>(Schema());
        importer.LoadText("Email;Age\nnope;1", "people.csv");
        importer.NextStep();
        SpreadsheetDownload? received = null;
        var cut = Render(KlyLocales.Fr, importer, p => p.Add(x => x.OnDownload, file => received = file));

        cut.Find("[data-slot=spreadsheet-download-invalid]").Click();

        Assert.Contains("\"Problèmes\"", received?.Content, StringComparison.Ordinal);
        Assert.Contains("Ce n’est pas une adresse e-mail", received?.Content, StringComparison.Ordinal);
        Assert.Equal("people-invalid-rows.csv", received?.FileName);
        Assert.Contains("\"Issues\"", importer.ExportInvalidRowsCsv(), StringComparison.Ordinal);                 // the headless export is unchanged
        Assert.Contains("Not an email address", importer.ExportInvalidRowsCsv(), StringComparison.Ordinal);
    }

    // The live region

    [Fact]
    public void A_polite_live_region_announces_the_counts_of_the_review_as_they_change()
    {
        var importer = new SpreadsheetImporter<ImportRow>(Schema());
        importer.LoadText("Email;Age\na@example.com;1\nb@example.com;2\nc@example.com;x", "people.csv");
        importer.NextStep();

        var cut = Render(KlyLocales.En, importer);
        var live = cut.Find("[data-slot=spreadsheet-live]");

        Assert.Equal("polite", live.GetAttribute("aria-live"));
        Assert.Equal("true", live.GetAttribute("aria-atomic"));
        Assert.Equal("status", live.GetAttribute("role"));
        Assert.Contains("sr-only", live.ClassName, StringComparison.Ordinal);
        Assert.Equal("To import: 2. With errors: 1. Left out: 0.", live.TextContent.Trim());

        importer.EditCell(2, "Age", "3");
        cut.WaitForAssertion(() => Assert.Equal("To import: 3. With errors: 0. Left out: 0.", cut.Find("[data-slot=spreadsheet-live]").TextContent.Trim()));

        importer.DiscardRows([0]);
        cut.WaitForAssertion(() => Assert.Equal("To import: 2. With errors: 0. Left out: 1.", cut.Find("[data-slot=spreadsheet-live]").TextContent.Trim()));
    }

    [Fact]
    public void The_live_region_speaks_the_locale_and_is_silent_outside_the_review_and_the_import()
    {
        var importer = new SpreadsheetImporter<ImportRow>(Schema());
        importer.LoadText("Email;Age\na@example.com;1", "people.csv");
        var cut = Render(KlyLocales.Fr, importer);
        Assert.Equal("", cut.Find("[data-slot=spreadsheet-live]").TextContent.Trim());     // the file step

        importer.NextStep();

        cut.WaitForAssertion(() => Assert.Equal("À importer : 1. En erreur : 0. Exclues : 0.", cut.Find("[data-slot=spreadsheet-live]").TextContent.Trim()));
    }

    [Fact]
    public async Task The_live_region_announces_the_progress_and_the_end_of_the_import()
    {
        var gate = new TaskCompletionSource();
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.BatchSize = 1;
            sb.OnSubmitAsync = async (rows, token) =>
            {
                if (rows[0].Index == 1) await gate.Task.WaitAsync(token);
                return null;
            };
            sb.Column(r => r.Name);
        }));
        importer.LoadText("Name\na\nb\nc", "names.csv");
        importer.NextStep();
        importer.NextStep();
        var cut = Render(KlyLocales.En, importer);
        var clock = new StepClock();
        cut.FindComponent<KlySpreadsheetImport>().Instance.Clock = clock;

        var submit = cut.InvokeAsync(() => importer.SubmitAsync(Token));
        cut.WaitForAssertion(() => Assert.Equal("Importing: 0 of 3 rows", cut.Find("[data-slot=spreadsheet-live]").TextContent.Trim()));

        clock.Advance(TimeSpan.FromSeconds(4));
        importer.SetReviewSearch("");                             // any change renders again
        cut.WaitForAssertion(() => Assert.Equal("Importing: 1 of 3 rows", cut.Find("[data-slot=spreadsheet-live]").TextContent.Trim()));

        gate.SetResult();
        await submit;
        cut.WaitForAssertion(() => Assert.Equal("3 rows imported", cut.Find("[data-slot=spreadsheet-live]").TextContent.Trim()));
    }

    [Fact]
    public async Task The_live_region_announces_a_cancelled_and_a_failed_import()
    {
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.BatchSize = 1;
            sb.OnSubmitAsync = (rows, token) => throw new InvalidOperationException("Database unavailable");
            sb.Column(r => r.Name);
        }));
        importer.LoadText("Name\na\nb", "names.csv");
        importer.NextStep();
        importer.NextStep();
        var cut = Render(KlyLocales.En, importer);

        await cut.InvokeAsync(() => importer.SubmitAsync(Token));

        cut.WaitForAssertion(() => Assert.Equal("The import failed", cut.Find("[data-slot=spreadsheet-live]").TextContent.Trim()));
    }

    [Fact]
    public async Task The_live_region_mentions_the_refused_rows_when_an_import_ends_with_some()
    {
        var importer = new SpreadsheetImporter<ImportRow>(SpreadsheetSchema.For<ImportRow>(sb =>
        {
            sb.OnSubmit = rows => Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>([new SpreadsheetRejectedRow(0, "Refused")]);
            sb.Column(r => r.Name);
        }));
        importer.LoadText("Name\na\nb", "names.csv");
        importer.NextStep();
        importer.NextStep();
        var cut = Render(KlyLocales.En, importer);

        await cut.InvokeAsync(() => importer.SubmitAsync(Token));

        cut.WaitForAssertion(() => Assert.Equal("1 row imported. Rows refused: 1.", cut.Find("[data-slot=spreadsheet-live]").TextContent.Trim()));
    }

    [Fact]
    public void The_stylesheet_hides_the_live_region_visually()
    {
        var css = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Klyroo.Spreadsheet", "wwwroot", "klyroo-spreadsheet.css"));

        Assert.Contains("[data-slot=\"spreadsheet-live\"].sr-only", css, StringComparison.Ordinal);
    }

    private sealed class StepClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public void Advance(TimeSpan by) => _now += by;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
