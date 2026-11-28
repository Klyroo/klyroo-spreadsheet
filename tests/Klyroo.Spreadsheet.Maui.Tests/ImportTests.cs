using System.Text;
using Microsoft.Extensions.Logging;

namespace Klyroo.Spreadsheet.Maui.Tests;

public sealed class Person
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Level { get; set; }
    public double? Price { get; set; }
}

public class ImportTests
{
    static ImportTests()
    {
        TestSupport.InstallDispatcher();
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    }

    private const string People = "Name;Email;Level;Price\nAda;ada@x.test;B1;10\nBob;not-an-email;B2;20\nCleo;cleo@x.test;B1;30";

    private static SpreadsheetImporter<Person> NewImporter(
        Func<IReadOnlyList<SpreadsheetRow<Person>>, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>? onSubmit = null,
        int batchSize = 2,
        bool editableEmail = true,
        ILogger? logger = null,
        bool requirePrice = false,
        Func<IReadOnlyList<SpreadsheetRow<Person>>, CancellationToken, Task<IReadOnlyList<SpreadsheetRejectedRow>?>>? onSubmitAsync = null) =>
        new(SpreadsheetSchema.For<Person>(s =>
        {
            s.Key = "people/2026";
            s.BatchSize = batchSize;
            s.OnSubmit = onSubmit;
            s.OnSubmitAsync = onSubmitAsync;
            s.Column(r => r.Name, c => c.Label("Name").Required().Description("Full name").Group("Identity"));
            s.Column(r => r.Email, c => c.Label("Email").Rule(SpreadsheetRule.Email()).Editable(editableEmail));
            s.Column(r => r.Level, c => c.Label("Level").Options([new SpreadsheetOption<string?>("B1", "B1"), new SpreadsheetOption<string?>("B2", "B2")]).UnknownPolicy(SpreadsheetUnknownValuePolicy.Ask));
            s.Column(r => r.Price, c => c.Label("Price").DecimalSeparator('.').Required(requirePrice));
        }), logger);

    private static (KlySpreadsheetImport Wizard, SpreadsheetImporter<Person> Importer, List<string> Said) Start(string? csv = People, Action<KlySpreadsheetImport>? configure = null, SpreadsheetImporter<Person>? importer = null)
    {
        importer ??= NewImporter();
        var said = new List<string>();
        var wizard = new KlySpreadsheetImport { Importer = importer, Announcer = said.Add };
        configure?.Invoke(wizard);
        if (csv is not null) importer.LoadText(csv, "people.csv");
        return (wizard, importer, said);
    }

    private static T Step<T>(KlySpreadsheetImport wizard) where T : class, IImportStep => Assert.IsType<T>(wizard.CurrentStepView);

    private static List<KlyButton> Buttons(View root) => TestSupport.All<KlyButton>(root);

    private static KlyButton Button(View root, string text) => Buttons(root).Single(b => b.Text == text);

    private static void Press(KlyButton button) => TestSupport.Click(button);

    private static void Choose(KlySelect select, string value) => select.SelectedItem = ((IEnumerable<SheetChoice>)select.ItemsSource!).Single(c => c.Value == value);

    private static SpreadsheetFile Csv(string text, string name = "people.csv") => new(name, () => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(text))));

    private static void GoTo(SpreadsheetImporter importer, string key)
    {
        for (var i = 0; i < 6 && importer.CurrentStepKey != key; i++)
        {
            importer.NextStep();
        }
        Assert.Equal(key, importer.CurrentStepKey);
    }

    private sealed class ListLogger : ILogger
    {
        public List<(LogLevel Level, Exception? Error)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, exception));
    }

    // --- the frame --------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Wizard_without_an_importer_is_not_shown_and_with_one_it_is()
    {
        var wizard = new KlySpreadsheetImport();
        Assert.False(wizard.IsVisible);

        wizard.Importer = NewImporter();
        Assert.True(wizard.IsVisible);

        wizard.Importer = null;
        Assert.False(wizard.IsVisible);
    }

    [Fact]
    public void Wizard_defaults_match_the_blazor_wizard()
    {
        var wizard = new KlySpreadsheetImport();

        Assert.Null(wizard.Title);
        Assert.Equal(10 * 1024 * 1024, wizard.MaxFileSize);
        Assert.Equal(50, wizard.PageSize);
        Assert.Equal(10_000, wizard.MaxRows);
        Assert.Equal(KlySpreadsheetImport.DefaultMaxRows, wizard.MaxRows);
        Assert.True(wizard.Wizard);
        Assert.Null(wizard.Importer);
        Assert.Null(wizard.PickFileAsync);
    }

    [Fact]
    public void Wizard_title_defaults_to_Import_and_a_given_title_is_shown_as_it_is()
    {
        var (wizard, _, _) = Start(null);
        Assert.Equal("Import", wizard.TitleLabel.Text);
        Assert.Equal(SemanticHeadingLevel.Level2, SemanticProperties.GetHeadingLevel(wizard.TitleLabel));

        wizard.Title = "Import prices";
        Assert.Equal("Import prices", wizard.TitleLabel.Text);
    }

    [Fact]
    public void Wizard_is_a_bordered_panel()
    {
        var (wizard, _, _) = Start(null);

        Assert.Equal(1, wizard.StrokeThickness);
        Assert.Equal(KlyTheme.Radius * 0.75, ((Microsoft.Maui.Controls.Shapes.RoundRectangle)wizard.StrokeShape!).CornerRadius.TopLeft);
        Assert.Equal(new Thickness(16), wizard.Padding);
        Assert.Equal(LayoutOptions.Fill, wizard.HorizontalOptions);
    }

    [Fact]
    public void Wizard_applies_the_row_limit_and_the_page_size_to_the_importer()
    {
        var (wizard, importer, _) = Start(null, w => { w.MaxRows = 2; w.PageSize = 25; });

        Assert.Equal(2, importer.DefaultMaxRows);
        Assert.Equal(25, importer.ReviewPageSize);

        wizard.MaxRows = null;
        wizard.PageSize = 100;
        Assert.Null(importer.DefaultMaxRows);
        Assert.Equal(100, importer.ReviewPageSize);
    }

    [Fact]
    public void The_step_bar_lists_the_steps_marks_the_current_one_and_lets_the_user_go_back_to_a_done_one()
    {
        var (wizard, importer, _) = Start();
        GoTo(importer, "review");

        var chips = wizard.Stepper.Children.OfType<Border>().ToList();
        Assert.Equal(5, chips.Count);
        Assert.Equal(["File", "Columns", "Values", "Review", "Import"], chips.Select(c => SemanticProperties.GetDescription(c)).ToArray());
        Assert.Equal("4", SemanticProperties.GetHint(chips[3]));
        Assert.True(chips[0].IsEnabled);
        Assert.False(chips[3].IsEnabled);
        Assert.False(chips[4].IsEnabled);

        TestSupport.Tap(chips[0]);

        Assert.Equal("file", importer.CurrentStepKey);
        Assert.IsType<ImportFileStep>(wizard.CurrentStepView);
    }

    [Fact]
    public void The_step_bar_says_when_a_step_had_nothing_to_do()
    {
        var (wizard, importer, _) = Start();
        GoTo(importer, "review");

        var texts = TestSupport.Texts(wizard.Stepper);
        Assert.Contains("(nothing to do)", texts);
        Assert.Contains("✓", texts);
    }

    [Fact]
    public void Without_the_wizard_flag_only_the_current_step_shows()
    {
        var (wizard, importer, _) = Start(configure: w => w.Wizard = false);

        Assert.False(wizard.Stepper.IsVisible);
        Assert.False(wizard.Footer.IsVisible);
        Assert.Empty(wizard.Footer.Children);
        Assert.IsType<ImportFileStep>(wizard.CurrentStepView);
        Assert.True(importer.CanNextStep);
    }

    [Fact]
    public void The_file_name_shows_as_a_pill_once_a_file_is_loaded()
    {
        var (wizard, importer, _) = Start(null);
        Assert.Null(wizard.FilePillHost.Content);

        importer.LoadText(People, "people.csv");
        Assert.Equal("people.csv", TestSupport.Texts(wizard.FilePillHost).Single());
    }

    [Fact]
    public void The_wizard_follows_a_new_importer_and_lets_go_of_the_old_one()
    {
        var (wizard, first, _) = Start();
        var second = NewImporter();

        wizard.Importer = second;
        first.LoadText(People, "ignored.csv");
        Assert.Null(wizard.FilePillHost.Content);

        second.LoadText(People, "second.csv");
        Assert.Equal("second.csv", TestSupport.Texts(wizard.FilePillHost).Single());
    }

    [Fact]
    public void The_footer_holds_back_and_continue_and_dims_continue_until_the_step_is_ready()
    {
        var (wizard, importer, _) = Start(null);

        var next = Button(wizard.Footer, "Continue");
        Assert.False(next.IsEnabled);
        Assert.DoesNotContain(Buttons(wizard.Footer), b => b.Text == "Back");
        Assert.Contains("Choose a file to continue", TestSupport.Texts(wizard.Footer));

        importer.LoadText(People, "people.csv");
        Assert.True(Button(wizard.Footer, "Continue").IsEnabled);

        Press(Button(wizard.Footer, "Continue"));
        Assert.NotEqual("file", importer.CurrentStepKey);
        Assert.Contains(Buttons(wizard.Footer), b => b.Text == "Back");
        Assert.True(Buttons(wizard.Footer).All(b => b.MinimumHeightRequest >= 40));

        Press(Button(wizard.Footer, "Back"));
        Assert.Equal("file", importer.CurrentStepKey);
    }

    [Fact]
    public void The_wizard_texts_follow_SpreadsheetTexts()
    {
        TestSupport.WithTexts(new SpreadsheetTexts { Import = "Importer", Continue = "Continuer", Back = "Retour", ChooseFile = "Choisir un fichier" }, () =>
        {
            var (wizard, importer, _) = Start(null);
            Assert.Equal("Importer", wizard.TitleLabel.Text);
            Assert.Contains(Buttons(wizard.Footer), b => b.Text == "Continuer");
            Assert.Equal("Choisir un fichier", Step<ImportFileStep>(wizard).ChooseButton!.Text);

            importer.LoadText(People, "p.csv");
            importer.NextStep();
            Assert.Contains(Buttons(wizard.Footer), b => b.Text == "Retour");
        });
    }

    // --- the file step ----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_file_step_offers_the_picker_the_paste_box_the_template_and_the_expected_columns()
    {
        var (wizard, _, _) = Start(null);
        var step = Step<ImportFileStep>(wizard);

        Assert.NotNull(step.Zone);
        Assert.Equal("Choose a file", step.ChooseButton!.Text);
        Assert.Equal("Choose a file to import", SemanticProperties.GetDescription(step.ChooseButton));
        Assert.True(step.ChooseButton.IsEnabled);
        var texts = TestSupport.Texts(step);
        Assert.Contains("Choose a file to import", texts);
        Assert.Contains(texts, t => t.Contains(".csv", StringComparison.Ordinal) && t.Contains(".xlsx", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("up to 10,000 rows"));
        Assert.Contains("Download a template", Buttons(step).Select(b => b.Text));
        Assert.Contains("Or paste rows copied from a spreadsheet", Buttons(step).Select(b => b.Text));
        Assert.Contains("Expected columns:", texts);
        Assert.Contains("IDENTITY", texts);
        Assert.Contains("Name", texts);
        Assert.Contains("*", texts);
        Assert.Contains("optional", texts);
    }

    [Fact]
    public void The_expected_columns_describe_themselves_to_a_screen_reader()
    {
        var (wizard, _, _) = Start(null);

        var pills = TestSupport.All<Border>(Step<ImportFileStep>(wizard)).Where(b => SemanticProperties.GetDescription(b) == "Name").ToList();
        var pill = Assert.Single(pills);
        Assert.Equal("Full name", SemanticProperties.GetHint(pill));
    }

    [Fact]
    public async Task Choosing_a_file_loads_it_into_the_importer()
    {
        var (wizard, importer, _) = Start(null);
        IReadOnlyList<string>? accepted = null;
        wizard.PickFileAsync = accept =>
        {
            accepted = accept;
            return Task.FromResult<SpreadsheetFile?>(Csv(People, "from-device.csv"));
        };

        await wizard.ChooseFileAsync();

        Assert.True(importer.FileLoaded);
        Assert.Equal("from-device.csv", importer.FileName);
        Assert.Equal(importer.Schema.Accept, accepted);
        Assert.False(wizard.LoadingFile);
        Assert.Equal(3, importer.AllRows.Count);
        var card = Step<ImportFileStep>(wizard).FileCard!;
        Assert.Contains("3 rows · 1 sheet · Column names on row 1", TestSupport.Texts(card));
        Assert.Contains("CSV", TestSupport.Texts(card));
        Assert.Contains("from-device.csv", TestSupport.Texts(card));
    }

    [Fact]
    public async Task The_zone_and_the_button_open_the_picker()
    {
        var (wizard, _, _) = Start(null);
        var opened = 0;
        wizard.PickFileAsync = _ =>
        {
            opened++;
            return Task.FromResult<SpreadsheetFile?>(null);
        };
        var step = Step<ImportFileStep>(wizard);

        TestSupport.Click(step.ChooseButton!);
        await Task.Yield();
        TestSupport.Tap(Step<ImportFileStep>(wizard).Zone!);
        await Task.Yield();

        Assert.Equal(2, opened);
    }

    [Fact]
    public async Task A_cancelled_picker_changes_nothing()
    {
        var (wizard, importer, _) = Start(null);
        wizard.PickFileAsync = _ => Task.FromResult<SpreadsheetFile?>(null);

        await wizard.ChooseFileAsync();

        Assert.False(importer.FileLoaded);
        Assert.Null(wizard.FileError);
        Assert.False(wizard.LoadingFile);
    }

    [Fact]
    public async Task The_file_zone_says_the_file_is_being_read_and_dims_the_button_meanwhile()
    {
        var (wizard, _, _) = Start(null);
        var gate = new TaskCompletionSource<SpreadsheetFile?>();
        wizard.PickFileAsync = _ => gate.Task;

        var running = wizard.ChooseFileAsync();

        var step = Step<ImportFileStep>(wizard);
        Assert.Contains("Reading the file…", TestSupport.Texts(step));
        Assert.False(step.ChooseButton!.IsEnabled);

        gate.SetResult(null);
        await running;
        Assert.DoesNotContain("Reading the file…", TestSupport.Texts(Step<ImportFileStep>(wizard)));
    }

    [Fact]
    public async Task A_file_over_the_limit_is_refused_before_it_is_read()
    {
        var (wizard, importer, _) = Start(null, w => w.MaxFileSize = 2 * 1024 * 1024);
        var read = false;
        wizard.PickFileAsync = _ => Task.FromResult<SpreadsheetFile?>(new SpreadsheetFile("big.csv", () =>
        {
            read = true;
            return Task.FromResult<Stream>(new MemoryStream());
        }, Length: 5 * 1024 * 1024));

        await wizard.ChooseFileAsync();

        Assert.False(read);
        Assert.False(importer.FileLoaded);
        Assert.Equal("The file is larger than 2 MB.", wizard.FileError);
        Assert.Contains("The file is larger than 2 MB.", TestSupport.Texts(Step<ImportFileStep>(wizard)));
    }

    [Fact]
    public async Task A_seekable_stream_over_the_limit_is_refused_when_the_length_was_not_known()
    {
        var (wizard, importer, _) = Start(null, w => w.MaxFileSize = 1024);
        wizard.PickFileAsync = _ => Task.FromResult<SpreadsheetFile?>(new SpreadsheetFile("big.csv", () => Task.FromResult<Stream>(new MemoryStream(new byte[4096]))));

        await wizard.ChooseFileAsync();

        Assert.False(importer.FileLoaded);
        Assert.Equal("The file is larger than 0 MB.", wizard.FileError);
    }

    [Fact]
    public async Task A_file_the_reader_refuses_shows_the_message_of_the_library()
    {
        var (wizard, importer, _) = Start(null);
        wizard.PickFileAsync = _ => Task.FromResult<SpreadsheetFile?>(new SpreadsheetFile("old.xls", () => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]))));

        await wizard.ChooseFileAsync();

        Assert.False(importer.FileLoaded);
        Assert.False(string.IsNullOrEmpty(wizard.FileError));
        Assert.Contains(wizard.FileError!, TestSupport.Texts(Step<ImportFileStep>(wizard)));
    }

    [Fact]
    public async Task A_file_that_cannot_be_read_shows_a_generic_text_and_logs_the_exception()
    {
        var logger = new ListLogger();
        var (wizard, importer, _) = Start(null, importer: NewImporter(logger: logger));
        wizard.PickFileAsync = _ => Task.FromResult<SpreadsheetFile?>(new SpreadsheetFile("a.csv", () => throw new IOException("/secret/path/a.csv is locked")));

        await wizard.ChooseFileAsync();

        Assert.False(importer.FileLoaded);
        Assert.Equal("The file could not be read", wizard.FileError);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<IOException>(entry.Error);
        Assert.DoesNotContain("secret", string.Join(" ", TestSupport.Texts(Step<ImportFileStep>(wizard))));
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(NotSupportedException))]
    [InlineData(typeof(NotImplementedException))]
    public async Task A_picker_the_platform_refuses_shows_the_generic_text(Type exception)
    {
        var (wizard, importer, _) = Start(null);
        wizard.PickFileAsync = _ => throw (Exception)Activator.CreateInstance(exception)!;

        await wizard.ChooseFileAsync();

        Assert.False(importer.FileLoaded);
        Assert.Equal("The file could not be read", wizard.FileError);
        Assert.False(wizard.LoadingFile);
    }

    [Fact]
    public void The_file_types_of_the_picker_follow_the_extensions_the_schema_accepts()
    {
        var (mime, uti, extensions) = DefaultPickers.FileTypes([".csv", "xlsx", ".XLS", ".odd"]);

        Assert.Contains("text/csv", mime);
        Assert.Contains("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", mime);
        Assert.Contains("application/vnd.ms-excel", mime);
        Assert.Contains("public.comma-separated-values-text", uti);
        Assert.Contains("org.openxmlformats.spreadsheetml.sheet", uti);
        Assert.Equal([".csv", ".xlsx", ".xls", ".odd"], extensions);
        Assert.Equal(mime.Distinct().Count(), mime.Count);
    }

    [Fact]
    public void Pasting_rows_opens_a_box_that_loads_them_as_a_file()
    {
        var (wizard, importer, _) = Start(null);
        var step = Step<ImportFileStep>(wizard);
        Assert.Null(step.PasteBox);

        Press(step.PasteToggle!);
        step = Step<ImportFileStep>(wizard);
        Assert.NotNull(step.PasteBox);
        Assert.Equal(5, step.PasteBox!.Rows);
        Assert.Equal("Paste the rows here, header row first", step.PasteBox.Placeholder);
        Assert.Equal("Rows to import", step.PasteBox.AccessibleName);
        Assert.False(step.UsePasteButton!.IsEnabled);

        step.PasteBox.Text = "Name\tEmail\nAda\tada@x.test";
        Assert.True(step.UsePasteButton.IsEnabled);
        Press(step.UsePasteButton);

        Assert.True(importer.FileLoaded);
        Assert.Equal("clipboard.tsv", importer.FileName);
        Assert.Equal("", wizard.PastedText);
        Assert.Single(importer.AllRows);
    }

    [Fact]
    public void Pasted_text_survives_the_wizard_drawing_again()
    {
        var (wizard, _, _) = Start(null);
        Press(Step<ImportFileStep>(wizard).PasteToggle!);
        Step<ImportFileStep>(wizard).PasteBox!.Text = "a\tb";

        wizard.Redraw();

        Assert.Equal("a\tb", Step<ImportFileStep>(wizard).PasteBox!.Text);
    }

    [Fact]
    public void Blank_pasted_text_is_not_loaded()
    {
        var (wizard, importer, _) = Start(null);

        wizard.PastedText = "   ";
        wizard.LoadPastedRows();

        Assert.False(importer.FileLoaded);
    }

    // --- downloads ---------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_template_goes_to_the_listener_with_a_safe_file_name()
    {
        var (wizard, importer, _) = Start(null);
        var received = new List<SpreadsheetDownload>();
        wizard.DownloadRequested += (_, file) => received.Add(file);

        await wizard.DownloadTemplateAsync();

        var file = Assert.Single(received);
        Assert.Equal("people-2026-template.csv", file.FileName);
        Assert.Equal(importer.ExportTemplateCsv(), file.Content);
        Assert.Equal("text/csv;charset=utf-8", file.MimeType);
        Assert.Equal(SpreadsheetDownload.CsvMimeType, file.MimeType);
        Assert.StartsWith("﻿", file.Content);
    }

    [Fact]
    public void The_template_button_downloads_the_template()
    {
        var (wizard, _, _) = Start(null);
        var received = new List<SpreadsheetDownload>();
        wizard.DownloadRequested += (_, file) => received.Add(file);

        Press(Step<ImportFileStep>(wizard).TemplateButton!);

        Assert.Single(received);
    }

    [Theory]
    [InlineData("people/2026", "people-2026")]
    [InlineData("a b", "a-b")]
    [InlineData("../etc", "etc")]
    [InlineData("///", "import")]
    [InlineData("ok_name-1", "ok_name-1")]
    public void A_schema_key_becomes_a_file_name_that_cannot_be_a_path(string key, string slug)
    {
        Assert.Equal(slug, SpreadsheetDownload.Slug(key));
    }

    [Fact]
    public async Task Without_a_listener_the_file_goes_to_the_saver_of_the_platform()
    {
        var (wizard, _, _) = Start(null);
        var saved = new List<SpreadsheetDownload>();
        wizard.Saver = file =>
        {
            saved.Add(file);
            return Task.CompletedTask;
        };

        await wizard.DownloadTemplateAsync();

        Assert.Equal("people-2026-template.csv", Assert.Single(saved).FileName);
        Assert.Null(wizard.DownloadError);
    }

    [Fact]
    public async Task A_saver_that_fails_shows_a_generic_error_and_logs_the_exception()
    {
        var logger = new ListLogger();
        var (wizard, _, _) = Start(null, importer: NewImporter(logger: logger));
        wizard.Saver = _ => throw new IOException("/secret/cache is full");

        await wizard.DownloadTemplateAsync();

        Assert.Equal("The file could not be downloaded", wizard.DownloadError);
        Assert.Contains("The file could not be downloaded", TestSupport.Texts(Step<ImportFileStep>(wizard)));
        Assert.DoesNotContain("secret", string.Join(" ", TestSupport.Texts(Step<ImportFileStep>(wizard))));
        Assert.IsType<IOException>(Assert.Single(logger.Entries).Error);
    }

    // --- the loaded file ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_loaded_file_shows_its_card_and_replace_goes_back_to_the_chooser()
    {
        var (wizard, importer, _) = Start();
        var step = Step<ImportFileStep>(wizard);

        Assert.Null(step.Zone);
        Assert.NotNull(step.FileCard);
        Assert.NotNull(step.ReplaceButton);
        Assert.Equal("Replace", step.ReplaceButton!.Text);

        Press(step.ReplaceButton);

        Assert.False(importer.FileLoaded);
        Assert.NotNull(Step<ImportFileStep>(wizard).Zone);
    }

    [Fact]
    public void An_excel_file_shows_its_extension_in_the_badge()
    {
        var (wizard, importer, _) = Start(null);
        importer.LoadWorkbook(new SpreadsheetWorkbook("prices.xlsx", [new SpreadsheetSheet("Prices", [["Name"], ["Ada"]], [1, 2])]));

        Assert.Contains("XLSX", TestSupport.Texts(Step<ImportFileStep>(wizard).FileCard!));
        Assert.Equal("CSV", SheetUi.ExtensionLabel("noextension"));
        Assert.Equal("XLS", SheetUi.ExtensionLabel("a.b.xls"));
    }

    [Fact]
    public void The_sheet_list_shows_only_for_several_sheets_and_changes_the_sheet()
    {
        var (wizard, importer, _) = Start(null);
        importer.LoadWorkbook(new SpreadsheetWorkbook("book.xlsx",
        [
            new SpreadsheetSheet("Prices", [["Name"], ["Ada"]], [1, 2]),
            new SpreadsheetSheet("Other", [["Name"], ["Bob"], ["Cleo"]], [1, 2, 3]),
        ]));
        var step = Step<ImportFileStep>(wizard);
        var sheets = step.SheetSelect!;

        Assert.Equal(["Prices", "Other"], ((IEnumerable<SheetChoice>)sheets.ItemsSource!).Select(c => c.Label).ToArray());
        Assert.Equal("Sheet", sheets.AccessibleName);
        Assert.Equal(importer.CurrentSheet!.Name, ((SheetChoice)sheets.SelectedItem!).Value);

        var other = importer.Sheets.First(s => s.Name != importer.CurrentSheet!.Name).Name;
        Choose(sheets, other);

        Assert.Equal(other, importer.CurrentSheet!.Name);
    }

    [Fact]
    public void A_single_sheet_has_no_sheet_list()
    {
        var (wizard, _, _) = Start();

        Assert.Null(Step<ImportFileStep>(wizard).SheetSelect);
    }

    [Fact]
    public void The_header_row_list_offers_the_first_rows_and_moves_the_header()
    {
        var (wizard, importer, _) = Start("Report;;;\nName;Email;Level;Price\nAda;ada@x.test;B1;10");
        var step = Step<ImportFileStep>(wizard);
        var rows = step.HeaderRowSelect!;

        Assert.Equal("Column names on row", rows.AccessibleName);
        Assert.Equal(["1", "2", "3"], ((IEnumerable<SheetChoice>)rows.ItemsSource!).Select(c => c.Label).ToArray());
        Assert.Equal(importer.HeaderRowNumber.ToString(), ((SheetChoice)rows.SelectedItem!).Label);

        Choose(rows, "0");

        Assert.Equal(1, importer.HeaderRowNumber);
    }

    [Fact]
    public void The_notes_of_a_loaded_file_show_the_row_limit()
    {
        var (wizard, _, _) = Start(People, w => w.MaxRows = 1);

        var texts = TestSupport.Texts(Step<ImportFileStep>(wizard));
        Assert.Contains("Only the first 1 rows are imported. Rows left out because of the limit: 2.", texts);
    }

    // --- the columns step ---------------------------------------------------------------------------------------------------------------------

    private const string Misnamed = "Nom;Email;Level;Price\nAda;ada@x.test;B1;10\nBob;bob@x.test;B2;20";

    [Fact]
    public void The_columns_step_lists_each_field_with_the_file_column_it_reads()
    {
        var (wizard, importer, _) = Start("Nom;Email;Level;Price\nAda;ada@x.test;B1;10");
        importer.NextStep();
        var step = Step<ImportColumnsStep>(wizard);

        Assert.Equal(["Name", "Email", "Level", "Price"], step.Rows.Select(r => r.FieldKey).ToArray());
        var name = step.Rows[0];
        Assert.Equal("Column for Name", name.Select.AccessibleName);
        var choices = ((IEnumerable<SheetChoice>)name.Select.ItemsSource!).Select(c => c.Label).ToArray();
        Assert.Equal(["Not in the file", "Nom", "Email", "Level", "Price"], choices);
        Assert.Equal("Not in the file", ((SheetChoice)name.Select.SelectedItem!).Label);
        var email = step.Rows[1];
        Assert.Equal("Email", ((SheetChoice)email.Select.SelectedItem!).Label);
        Assert.Contains("Full name", TestSupport.Texts(name.Card));
        Assert.Contains("*", TestSupport.Texts(name.Card));
    }

    [Fact]
    public void A_required_number_column_without_a_match_gets_a_suggestion_that_the_user_accepts()
    {
        var (wizard, importer, _) = Start("Name;Email;Level;Cost\nAda;ada@x.test;B1;10\nBob;bob@x.test;B2;20", importer: NewImporter(requirePrice: true));
        Assert.Single(importer.MissingFields);
        importer.NextStep();
        var step = Step<ImportColumnsStep>(wizard);

        var price = step.Rows.Single(r => r.FieldKey == "Price");
        Assert.Contains(TestSupport.Texts(price.Card), t => t.StartsWith("\"Cost\" has values like", StringComparison.Ordinal));
        Assert.Equal("Not in the file", ((SheetChoice)price.Select.SelectedItem!).Label);
        Assert.Contains("Required columns missing: 1", TestSupport.Texts(wizard.Footer));
        Assert.False(Button(wizard.Footer, "Continue").IsEnabled);

        Press(Button(price.Card, "Use it"));

        Assert.Empty(importer.MissingFields);
        Assert.True(Button(wizard.Footer, "Continue").IsEnabled);
        Assert.Equal("Cost", ((SheetChoice)Step<ImportColumnsStep>(wizard).Rows.Single(r => r.FieldKey == "Price").Select.SelectedItem!).Label);
    }

    [Fact]
    public void Choosing_a_column_assigns_it_and_undo_gives_it_back()
    {
        var (wizard, importer, _) = Start(Misnamed);
        importer.NextStep();
        var nameRow = Step<ImportColumnsStep>(wizard).Rows[0];

        Choose(nameRow.Select, "0");

        var field = importer.Fields.First(f => f.Column.Key == "Name");
        Assert.Equal(0, field.HeaderIndex);
        var rows = Step<ImportColumnsStep>(wizard).Rows;
        Assert.Contains("Undo", Buttons(rows[0].Card).Select(b => b.Text));

        Press(Button(rows[0].Card, "Undo"));
        Assert.Null(importer.Fields.First(f => f.Column.Key == "Name").HeaderIndex);
    }

    [Fact]
    public void Choosing_not_in_the_file_ignores_the_column()
    {
        var (wizard, importer, _) = Start("Nom;Email;Level;Price\nAda;ada@x.test;B1;10");
        importer.NextStep();
        var price = Step<ImportColumnsStep>(wizard).Rows.Single(r => r.FieldKey == "Price");

        Choose(price.Select, "");

        Assert.Null(importer.Fields.First(f => f.Column.Key == "Price").HeaderIndex);
    }

    [Fact]
    public void The_columns_step_names_the_file_columns_no_field_uses()
    {
        var (wizard, importer, _) = Start("Nom;Notes\nAda;hi");
        importer.NextStep();

        Assert.Contains(TestSupport.Texts(Step<ImportColumnsStep>(wizard)), t => t.StartsWith("Not imported:", StringComparison.Ordinal) && t.Contains("Notes", StringComparison.Ordinal));
    }

    [Fact]
    public void The_column_lists_are_locked_while_rows_are_accepted()
    {
        var (wizard, importer, _) = Start(Misnamed.Replace("Nom", "Name"), importer: NewImporter(rows => Accept(rows)));
        importer.GoToStep("file");
        importer.AssignColumn("Name", 0);
        GoTo(importer, "submit");
        TestSupport.RunSync(() => importer.SubmitAsync(TestSupport.Ct));
        importer.GoToStep("columns");

        Assert.True(importer.MappingLocked);
        Assert.IsType<ImportColumnsStep>(wizard.CurrentStepView);
        Assert.False(Step<ImportColumnsStep>(wizard).Rows[0].Select.IsEnabled);
    }

    [Fact]
    public void Changing_the_mapping_raises_the_profile_event_once_and_loading_a_file_does_not()
    {
        var (wizard, importer, _) = Start(null);
        var profiles = new List<SpreadsheetMappingProfile>();
        wizard.MappingProfileChanged += (_, profile) => profiles.Add(profile);

        importer.LoadText(Misnamed, "people.csv");
        Assert.Empty(profiles);

        importer.AssignColumn("Name", 0);

        Assert.Single(profiles);
    }

    // --- the values step ----------------------------------------------------------------------------------------------------------------------

    private const string UnknownLevel = "Name;Email;Level;Price\nAda;ada@x.test;C1;10\nBob;bob@x.test;C1;20\nCleo;cleo@x.test;B1;30";

    private static (KlySpreadsheetImport Wizard, SpreadsheetImporter<Person> Importer, ImportValuesStep Step) ValuesStep()
    {
        var (wizard, importer, _) = Start(UnknownLevel);
        GoTo(importer, "values");
        return (wizard, importer, Step<ImportValuesStep>(wizard));
    }

    [Fact]
    public void The_values_step_asks_about_each_unknown_value_with_the_answers_it_allows()
    {
        var (wizard, importer, step) = ValuesStep();

        var row = Assert.Single(step.Rows);
        Assert.Equal("C1", row.Question.RawValue);
        Assert.Contains("Level: \"C1\"", TestSupport.Texts(row.Card));
        Assert.Contains("2 rows", TestSupport.Texts(row.Card));
        Assert.Equal("Answer for C1", row.Select.AccessibleName);
        var labels = ((IEnumerable<SheetChoice>)row.Select.ItemsSource!).Select(c => c.Label).ToArray();
        Assert.Equal("Choose…", labels[0]);
        Assert.Contains("B1", labels);
        Assert.Contains("B2", labels);
        Assert.Contains("Leave empty", labels);
        Assert.Contains("Don't import these rows", labels);
        Assert.Single(importer.OpenQuestions);
        Assert.Contains("Values to choose: 1", TestSupport.Texts(wizard.Footer));
        Assert.False(Button(wizard.Footer, "Continue").IsEnabled);
    }

    [Fact]
    public void Answering_a_value_question_gives_the_answer_to_the_importer_and_clears_the_hint()
    {
        var (wizard, importer, step) = ValuesStep();

        Choose(step.Rows[0].Select, "map:B2");

        Assert.Empty(importer.OpenQuestions);
        Assert.Equal(SpreadsheetValueAnswerAction.Map, importer.Questions[0].Answer!.Action);
        Assert.Equal("B2", importer.Questions[0].Answer!.MappedValue);
        Assert.Equal("map:B2", ((SheetChoice)Step<ImportValuesStep>(wizard).Rows[0].Select.SelectedItem!).Value);
        Assert.True(Button(wizard.Footer, "Continue").IsEnabled);
    }

    [Theory]
    [InlineData("action:leave-empty", SpreadsheetValueAnswerAction.LeaveEmpty)]
    [InlineData("action:skip-rows", SpreadsheetValueAnswerAction.SkipRows)]
    public void The_other_answers_reach_the_importer(string choice, SpreadsheetValueAnswerAction action)
    {
        var (_, importer, step) = ValuesStep();

        Choose(step.Rows[0].Select, choice);

        Assert.Equal(action, importer.Questions[0].Answer!.Action);
    }

    [Fact]
    public void An_answer_can_be_taken_back_with_the_first_choice()
    {
        var (wizard, importer, step) = ValuesStep();
        Choose(step.Rows[0].Select, "action:leave-empty");
        Assert.Empty(importer.OpenQuestions);

        Choose(Step<ImportValuesStep>(wizard).Rows[0].Select, "");

        Assert.Single(importer.OpenQuestions);
    }

    [Fact]
    public void Creating_the_value_is_offered_only_when_the_schema_allows_it()
    {
        var importer = new SpreadsheetImporter<Person>(SpreadsheetSchema.For<Person>(s =>
        {
            s.Column(r => r.Name);
            s.Column(r => r.Level, c => c.Options([new SpreadsheetOption<string?>("B1", "B1")]).UnknownPolicy(SpreadsheetUnknownValuePolicy.Ask).OnCreateOption(raw => new SpreadsheetOption<string?>(raw, raw)));
        }));
        var (wizard, _, _) = Start("Name;Level\nAda;Z9", importer: importer);
        GoTo(importer, "values");

        var row = Assert.Single(Step<ImportValuesStep>(wizard).Rows);
        Assert.True(row.Question.CanCreate);
        var create = ((IEnumerable<SheetChoice>)row.Select.ItemsSource!).Single(c => c.Value == "action:create");
        Assert.Equal("Create \"Z9\"", create.Label);

        Choose(row.Select, "action:create");
        Assert.Equal(SpreadsheetValueAnswerAction.Create, importer.Questions[0].Answer!.Action);
    }

    [Fact]
    public void The_values_step_says_when_there_is_nothing_to_answer()
    {
        var (wizard, _, _) = Start();

        var step = new ImportValuesStep(wizard);

        Assert.Equal("Every value was found in the list", step.EmptyNote!.Text);
        Assert.Empty(step.Rows);
    }

    // --- the review step ----------------------------------------------------------------------------------------------------------------------

    private static (KlySpreadsheetImport Wizard, SpreadsheetImporter<Person> Importer, ImportReviewStep Step) ReviewStep(string csv = People, Action<KlySpreadsheetImport>? configure = null, SpreadsheetImporter<Person>? importer = null)
    {
        var (wizard, imp, _) = Start(csv, configure, importer);
        GoTo(imp, "review");
        return (wizard, imp, Step<ImportReviewStep>(wizard));
    }

    [Fact]
    public void The_review_shows_the_counts_of_the_rows_to_import_with_errors_and_left_out()
    {
        var (_, importer, step) = ReviewStep();

        Assert.Equal(2, importer.ImportableRows.Count);
        Assert.Equal("2", step.ImportableCount!.Text);
        Assert.Equal("1", step.InvalidCount!.Text);
        Assert.Equal("0", step.DiscardedCount!.Text);
        Assert.Contains("rows to import", TestSupport.Texts(step));
        Assert.Contains("rows with errors", TestSupport.Texts(step));
        Assert.Contains("rows left out", TestSupport.Texts(step));
        Assert.Equal("2 rows to import", SemanticProperties.GetDescription((View)step.ImportableCount.Parent.Parent));
    }

    [Fact]
    public void The_review_hint_tells_that_rows_with_errors_are_left_out()
    {
        var (wizard, _, _) = ReviewStep();

        Assert.Contains("Rows with errors are left out: 1", TestSupport.Texts(wizard.Footer));
        Assert.True(Button(wizard.Footer, "Continue").IsEnabled);
    }

    [Fact]
    public void The_tabs_count_the_rows_and_switch_what_the_grid_shows()
    {
        var (wizard, importer, step) = ReviewStep();

        Assert.Equal(["All (3)", "To import (2)", "Invalid (1)", "Left out (0)"], step.Tabs.Select(t => SemanticProperties.GetDescription(t.Chip)).ToArray());
        Assert.Equal(3, step.Rows.Count);
        Assert.Equal("selected", SemanticProperties.GetHint(step.Tabs.Single(t => t.Tab == SpreadsheetReviewTab.All).Chip));

        TestSupport.Tap(step.Tabs.Single(t => t.Tab == SpreadsheetReviewTab.Invalid).Chip);

        Assert.Equal(SpreadsheetReviewTab.Invalid, importer.ReviewTab);
        step = Step<ImportReviewStep>(wizard);
        Assert.Single(step.Rows);
        Assert.Equal(3, step.Rows[0].Row.RowNumber);
        Assert.Equal("selected", SemanticProperties.GetHint(step.Tabs.Single(t => t.Tab == SpreadsheetReviewTab.Invalid).Chip));
    }

    [Fact]
    public void The_grid_has_a_header_with_the_columns_and_a_row_per_row_with_its_number_and_status()
    {
        var (_, importer, step) = ReviewStep();

        var header = step.Table!.Children.OfType<Label>().Where(l => Grid.GetRow(l) == 0).Select(l => l.Text).ToArray();
        Assert.Equal(["Row", "Status", "Name", "Email", "Level", "Price"], header);
        Assert.Equal(["#2", "#3", "#4"], step.Rows.Select(r => r.Number.Text).ToArray());
        Assert.Equal(["Valid", "Blocking", "Valid"], step.Rows.Select(r => ((Label)r.Status.Content!).Text).ToArray());
        Assert.Equal("Inspect row 3", SemanticProperties.GetDescription(step.Rows[1].Number));
        Assert.Equal("Select row 3", SemanticProperties.GetDescription(step.Rows[1].Select));
        Assert.Equal(importer.Fields.Count * 3, step.Cells.Count);
        Assert.Contains(step.Cells, c => c.Row.RowNumber == 2 && c.FieldKey == "Name" && TestSupport.Texts(c.Cell).Contains("Ada"));
    }

    [Fact]
    public void A_cell_with_an_issue_is_red_and_says_why_to_a_screen_reader()
    {
        var (_, _, step) = ReviewStep();

        var cell = step.Cells.Single(c => c.Row.RowNumber == 3 && c.FieldKey == "Email").Cell;
        Assert.Equal("Email: not-an-email", SemanticProperties.GetDescription(cell));
        Assert.Equal("Not an email address", SemanticProperties.GetHint(cell));
        Assert.Equal(FontAttributes.Bold, TestSupport.All<Label>(cell).Single().FontAttributes);
    }

    [Fact]
    public void An_empty_cell_shows_the_word_empty()
    {
        var (_, _, step) = ReviewStep("Name;Email\nAda;");

        var cell = step.Cells.Single(c => c.FieldKey == "Email").Cell;
        Assert.Equal(["empty"], TestSupport.Texts(cell));
    }

    [Fact]
    public void The_severity_list_filters_the_rows()
    {
        var (wizard, importer, step) = ReviewStep();
        var levels = step.LevelSelect!;

        Assert.Equal("Severity", levels.AccessibleName);
        Assert.Equal(["All severities", "Errors", "Warnings"], ((IEnumerable<SheetChoice>)levels.ItemsSource!).Select(c => c.Label).ToArray());

        Choose(levels, nameof(SpreadsheetReviewLevel.Blocking));

        Assert.Equal(SpreadsheetReviewLevel.Blocking, importer.ReviewLevel);
        Assert.Single(Step<ImportReviewStep>(wizard).Rows);
    }

    [Fact]
    public void The_issue_list_shows_only_when_rows_have_issues_and_filters_by_issue()
    {
        var (wizard, importer, step) = ReviewStep();
        var issues = step.IssueSelect!;

        Assert.Equal("Issue", issues.AccessibleName);
        var labels = ((IEnumerable<SheetChoice>)issues.ItemsSource!).Select(c => c.Label).ToArray();
        Assert.Equal(["All issues", "Email: email (1)"], labels);

        Choose(issues, "Email|email");

        Assert.Equal("Email|email", importer.ReviewIssueFilter);
        Assert.Single(Step<ImportReviewStep>(wizard).Rows);

        var (_, _, clean) = ReviewStep("Name;Email\nAda;ada@x.test");
        Assert.Null(clean.IssueSelect);
    }

    [Fact]
    public void Issue_kinds_group_by_field_and_code_and_count_rows_most_first()
    {
        var (_, importer, _) = ReviewStep("Name;Email\n;ada@x.test\n;bob@x.test\nCleo;nope");

        var kinds = ImportReviewStep.IssueKinds(importer);

        Assert.Equal("Name|required", kinds[0].Filter);
        Assert.Equal("Name: required", kinds[0].Label);
        Assert.Equal(2, kinds[0].Count);
        Assert.Equal(1, kinds[1].Count);
        Assert.Equal("Whole row", ImportReviewStep.IssueFieldLabel(importer, SpreadsheetTexts.Current, null) is "Row" ? "Whole row" : "?");
        Assert.Equal("Row", ImportReviewStep.IssueFieldLabel(importer, SpreadsheetTexts.Current, null));
        Assert.Equal("Name", ImportReviewStep.IssueFieldLabel(importer, SpreadsheetTexts.Current, "Name"));
        Assert.Equal("unknown", ImportReviewStep.IssueFieldLabel(importer, SpreadsheetTexts.Current, "unknown"));
    }

    [Fact]
    public void The_search_box_filters_the_rows_and_keeps_the_same_field_while_the_wizard_draws_again()
    {
        var (wizard, importer, step) = ReviewStep();
        var search = step.Search;
        Assert.Equal("Search rows…", search.Placeholder);
        Assert.Equal("Search rows", search.AccessibleName);
        Assert.Equal(ReturnType.Search, search.ReturnType);

        search.Text = "bob";

        Assert.Equal("bob", importer.ReviewSearch);
        var next = Step<ImportReviewStep>(wizard);
        Assert.Same(search, next.Search);
        Assert.Equal("bob", next.Search.Text);
        Assert.Single(next.Rows);
        Assert.Equal("bob", search.Text);

        search.Text = "";
        Assert.Equal(3, Step<ImportReviewStep>(wizard).Rows.Count);
    }

    [Fact]
    public void A_search_with_no_match_says_there_are_no_rows()
    {
        var (wizard, _, step) = ReviewStep();

        step.Search.Text = "zzz";

        var next = Step<ImportReviewStep>(wizard);
        Assert.Equal("No rows to show", next.EmptyNote!.Text);
        Assert.Empty(next.Rows);
    }

    [Fact]
    public void The_pager_shows_the_range_the_page_and_moves_through_the_pages()
    {
        var (wizard, importer, step) = ReviewStep("Name\na\nb\nc\nd\ne", w => w.PageSize = 2);

        Assert.Equal("Rows 1 to 2 of 5", step.PagerRange!.Text);
        Assert.Equal("Page 1 of 3", step.PagerStatus!.Text);
        Assert.Equal(2, step.Rows.Count);
        Assert.Equal(["First page", "Previous page", "Next page", "Last page"], step.PagerButtons.Select(b => SemanticProperties.GetDescription(b)).ToArray());
        Assert.Equal([false, false, true, true], step.PagerButtons.Select(b => b.IsEnabled).ToArray());

        Press(step.PagerButtons[2]);
        step = Step<ImportReviewStep>(wizard);
        Assert.Equal("Rows 3 to 4 of 5", step.PagerRange!.Text);
        Assert.Equal("Page 2 of 3", step.PagerStatus!.Text);
        Assert.Equal(["#4", "#5"], step.Rows.Select(r => r.Number.Text).ToArray());
        Assert.Equal([true, true, true, true], step.PagerButtons.Select(b => b.IsEnabled).ToArray());

        Press(step.PagerButtons[3]);
        step = Step<ImportReviewStep>(wizard);
        Assert.Equal(2, importer.ReviewPage);
        Assert.Equal("Rows 5 to 5 of 5", step.PagerRange!.Text);
        Assert.Equal([true, true, false, false], step.PagerButtons.Select(b => b.IsEnabled).ToArray());

        Press(step.PagerButtons[0]);
        Assert.Equal(0, importer.ReviewPage);

        Press(Step<ImportReviewStep>(wizard).PagerButtons[2]);
        Press(Step<ImportReviewStep>(wizard).PagerButtons[1]);
        Assert.Equal(0, importer.ReviewPage);
    }

    [Fact]
    public void The_page_size_list_offers_the_usual_sizes_and_the_one_in_use()
    {
        var (_, importer, step) = ReviewStep(configure: w => w.PageSize = 30);

        var sizes = ((IEnumerable<SheetChoice>)step.PageSizeSelect!.ItemsSource!).Select(c => c.Label).ToArray();
        Assert.Equal(["25", "30", "50", "100", "200"], sizes);
        Assert.Equal("Rows per page", step.PageSizeSelect.AccessibleName);

        Choose(step.PageSizeSelect, "100");

        Assert.Equal(100, importer.ReviewPageSize);
        Assert.Equal([25, 50, 100, 200], ImportReviewStep.PageSizeChoices(importer).ToArray().Where(s => s != 30));
    }

    [Fact]
    public void Row_checkboxes_select_rows_and_the_header_one_selects_the_page()
    {
        var (wizard, importer, step) = ReviewStep();
        step.Rows[1].Select.IsChecked = true;

        Assert.Equal([1], importer.SelectedRowIndexes.ToArray());
        step = Step<ImportReviewStep>(wizard);
        Assert.True(step.Rows[1].Select.IsChecked);
        Assert.Equal("Select the rows of this page", SemanticProperties.GetDescription(step.SelectAll!));
        Assert.False(step.SelectAll!.IsChecked);

        step.SelectAll.IsChecked = true;
        Assert.Equal(3, importer.SelectedRowIndexes.Count);
        Assert.True(Step<ImportReviewStep>(wizard).SelectAll!.IsChecked);

        Step<ImportReviewStep>(wizard).SelectAll!.IsChecked = false;
        Assert.Empty(importer.SelectedRowIndexes);
    }

    [Fact]
    public void The_selection_bar_leaves_rows_out_restores_them_and_clears_the_selection()
    {
        var (wizard, importer, step) = ReviewStep();
        Assert.Null(step.SelectionBar);
        step.Rows[0].Select.IsChecked = true;
        step.Rows[2].Select.IsChecked = true;
        step = Step<ImportReviewStep>(wizard);

        Assert.Contains("2 selected", TestSupport.Texts(step.SelectionBar!));
        Press(Button(step.SelectionBar!, "Leave out"));

        Assert.Equal(2, importer.DiscardedRows.Count);
        Assert.Empty(importer.SelectedRowIndexes);
        step = Step<ImportReviewStep>(wizard);
        Assert.Equal("2", step.DiscardedCount!.Text);
        Assert.Null(step.SelectionBar);

        step.Rows[0].Select.IsChecked = true;
        Press(Button(Step<ImportReviewStep>(wizard).SelectionBar!, "Restore"));
        Assert.Single(importer.DiscardedRows);

        Step<ImportReviewStep>(wizard).Rows[1].Select.IsChecked = true;
        Press(Button(Step<ImportReviewStep>(wizard).SelectionBar!, "Clear selection"));
        Assert.Empty(importer.SelectedRowIndexes);
    }

    [Fact]
    public void Left_out_rows_are_marked_in_the_grid()
    {
        var (wizard, importer, _) = ReviewStep();

        importer.DiscardRows([0]);

        var step = Step<ImportReviewStep>(wizard);
        var row = step.Rows[0];
        Assert.Equal("Discarded", ((Label)row.Status.Content!).Text);
        var cell = step.Cells.First(c => c.Row.Index == 0).Cell;
        Assert.Equal(0.6, TestSupport.All<View>(step.Table!).First(v => Grid.GetRow(v) == 1 && v is CheckBox).Opacity);
        Assert.Equal(TextDecorations.Strikethrough, TestSupport.All<Label>(cell).Single().TextDecorations);
        Assert.False(cell.IsEnabled);
    }

    [Fact]
    public void Tapping_a_cell_edits_it_and_completing_commits_the_edit()
    {
        var (wizard, importer, step) = ReviewStep();

        TestSupport.Tap(step.Cells.Single(c => c.Row.RowNumber == 3 && c.FieldKey == "Email").Cell);

        step = Step<ImportReviewStep>(wizard);
        var editor = step.CellEditor!;
        Assert.Equal("not-an-email", editor.Text);
        Assert.Equal("Edit Email", SemanticProperties.GetDescription(editor));
        Assert.Equal((1, "Email"), wizard.EditingCell);

        editor.Text = "bob@x.test";
        editor.SendCompleted();

        Assert.Null(wizard.EditingCell);
        Assert.True(importer.AllRows[1].Edited);
        Assert.Equal("bob@x.test", importer.AllRows[1].Cells["Email"].Text);
        Assert.Equal(3, importer.ImportableRows.Count);
        step = Step<ImportReviewStep>(wizard);
        Assert.Null(step.CellEditor);
        var cell = step.Cells.Single(c => c.Row.RowNumber == 3 && c.FieldKey == "Email").Cell;
        Assert.Contains("✎", TestSupport.Texts(cell));
    }

    [Fact]
    public void Leaving_the_editor_commits_it_once()
    {
        var (wizard, importer, step) = ReviewStep();
        TestSupport.Tap(step.Cells.Single(c => c.Row.RowNumber == 2 && c.FieldKey == "Name").Cell);
        var editor = Step<ImportReviewStep>(wizard).CellEditor!;
        editor.Text = "Ada L.";

        TestSupport.Unfocus(editor);
        editor.SendCompleted();

        Assert.Equal("Ada L.", importer.AllRows[0].Cells["Name"].Text);
        Assert.Null(wizard.EditingCell);
    }

    [Fact]
    public void A_column_that_is_not_editable_has_no_editor()
    {
        var (_, _, step) = ReviewStep(importer: NewImporter(editableEmail: false));

        var cell = step.Cells.First(c => c.FieldKey == "Email").Cell;
        Assert.False(cell.IsEnabled);
        Assert.Empty(cell.GestureRecognizers);
        var editable = step.Cells.First(c => c.FieldKey == "Name").Cell;
        Assert.True(editable.IsEnabled);
        Assert.NotEmpty(editable.GestureRecognizers);
    }

    [Fact]
    public void The_invalid_rows_can_be_downloaded()
    {
        var (wizard, importer, step) = ReviewStep();
        var received = new List<SpreadsheetDownload>();
        wizard.DownloadRequested += (_, file) => received.Add(file);

        Assert.Equal("Download the invalid row", step.InvalidDownload!.Text);
        Press(step.InvalidDownload);

        var file = Assert.Single(received);
        Assert.Equal("people-2026-invalid-rows.csv", file.FileName);
        Assert.Contains("Not an email address", file.Content);
        Assert.Contains("Issues", file.Content);

        var (_, _, clean) = ReviewStep("Name;Email\nAda;ada@x.test");
        Assert.Null(clean.InvalidDownload);
        Assert.NotNull(importer);
    }

    [Fact]
    public void A_warning_row_has_its_own_status_and_stays_importable()
    {
        var importer = new SpreadsheetImporter<Person>(SpreadsheetSchema.For<Person>(s =>
        {
            s.Column(r => r.Name, c => c.Rule("short", "A short name", v => v is { Length: > 2 }, SpreadsheetIssueLevel.Warning));
        }));
        var (_, _, step) = ReviewStep("Name\nAd\nBob", importer: importer);

        Assert.Equal(["Warning", "Valid"], step.Rows.Select(r => ((Label)r.Status.Content!).Text).ToArray());
        Assert.Equal(2, importer.ImportableRows.Count);
    }

    [Fact]
    public async Task Reaching_the_review_runs_the_checks_that_need_the_host_and_shows_their_status()
    {
        var importer = new SpreadsheetImporter<Person>(SpreadsheetSchema.For<Person>(s =>
        {
            s.ValidateRowsAsync = (rows, token) => Task.FromResult<IReadOnlyList<SpreadsheetIndexedIssue>?>(
                rows.Where(r => r.Value.Name == "Bob").Select(r => new SpreadsheetIndexedIssue(r.Index, new SpreadsheetRowIssue("Name", "name.taken", SpreadsheetIssueLevel.Error, "Name is taken"))).ToList());
            s.Column(r => r.Name);
        }));
        var (wizard, _, _) = Start("Name\nAda\nBob", importer: importer);

        importer.NextStep();
        for (var i = 0; i < 100 && importer.ValidationStatus != SpreadsheetValidationStatus.Done; i++) await Task.Delay(20, TestSupport.Ct);

        Assert.Equal(SpreadsheetValidationStatus.Done, importer.ValidationStatus);
        var step = Step<ImportReviewStep>(wizard);
        Assert.Equal("The rows were checked", step.ValidationLabel!.Text);
        Assert.Equal("1", step.InvalidCount!.Text);
        Assert.Equal("1", step.ImportableCount!.Text);
    }

    // --- the inspector ------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Tapping_a_row_number_inspects_the_row_with_its_issues()
    {
        var (wizard, importer, step) = ReviewStep();
        Assert.Null(step.Inspector);

        TestSupport.Tap(step.Rows[1].Number);

        Assert.Equal(1, importer.InspectedRowIndex);
        step = Step<ImportReviewStep>(wizard);
        var inspector = step.Inspector!;
        var texts = TestSupport.Texts(inspector);
        Assert.Contains("Row 3", texts);
        Assert.Equal("Email: Not an email address", string.Concat(TestSupport.All<Label>(inspector).Where(l => l.FormattedText is not null).SelectMany(l => l.FormattedText!.Spans).Select(s => s.Text)));
        Assert.Contains("Leave out this row", texts.Concat(step.InspectorButtons.Select(b => b.Text!)));
        Assert.Equal(["Previous row with issues", "Next row with issues", "Close inspector"], step.InspectorButtons.Take(3).Select(b => SemanticProperties.GetDescription(b)).ToArray());
    }

    [Fact]
    public void The_inspector_moves_between_rows_with_issues_and_closes()
    {
        var (wizard, importer, step) = ReviewStep("Name;Email\nAda;bad\nBob;ok@x.test\nCleo;worse");
        TestSupport.Tap(step.Rows[0].Number);
        step = Step<ImportReviewStep>(wizard);

        Press(step.InspectorButtons[1]);
        Assert.Equal(2, importer.InspectedRowIndex);

        Press(Step<ImportReviewStep>(wizard).InspectorButtons[0]);
        Assert.Equal(0, importer.InspectedRowIndex);

        Press(Step<ImportReviewStep>(wizard).InspectorButtons[2]);
        Assert.Null(importer.InspectedRow);
        Assert.Null(Step<ImportReviewStep>(wizard).Inspector);
    }

    [Fact]
    public void The_inspector_leaves_a_row_out_and_brings_it_back()
    {
        var (wizard, importer, step) = ReviewStep();
        TestSupport.Tap(step.Rows[0].Number);

        Press(Button(Step<ImportReviewStep>(wizard).Inspector!, "Leave out this row"));
        Assert.Single(importer.DiscardedRows);

        Press(Button(Step<ImportReviewStep>(wizard).Inspector!, "Restore this row"));
        Assert.Empty(importer.DiscardedRows);
    }

    [Fact]
    public void The_inspector_lists_the_edits_of_a_row_and_undoes_them()
    {
        var (wizard, importer, _) = ReviewStep();
        importer.EditCell(1, "Email", "bob@x.test");
        importer.InspectRow(1);

        var inspector = Step<ImportReviewStep>(wizard).Inspector!;
        Assert.Contains("Email: not-an-email → bob@x.test", TestSupport.Texts(inspector));

        Press(Button(inspector, "Undo"));

        Assert.False(importer.AllRows[1].Edited);
        Assert.Equal("not-an-email", importer.AllRows[1].Cells["Email"].Text);
    }

    // --- the import step ----------------------------------------------------------------------------------------------------------------------

    private static Task<IReadOnlyList<SpreadsheetRejectedRow>?> Accept(IReadOnlyList<SpreadsheetRow<Person>> rows) => Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>(null);

    [Fact]
    public void The_import_step_summarizes_what_will_be_imported()
    {
        var (wizard, importer, _) = Start();
        importer.EditCell(1, "Email", "bob@x.test");
        importer.DiscardRows([2]);
        GoTo(importer, "submit");

        var step = Step<ImportSubmitStep>(wizard);
        Assert.Equal(["2", "0", "1", "1"], step.SummaryValues.Select(v => v.Text).ToArray());
        var texts = TestSupport.Texts(step);
        Assert.Contains("Rows to import", texts);
        Assert.Contains("Invalid, not imported", texts);
        Assert.Contains("Left out", texts);
        Assert.Contains("Cells edited", texts);
        Assert.Contains(Buttons(wizard.Footer), b => b.Text == "Import 2 rows");
        Assert.True(Button(wizard.Footer, "Import 2 rows").IsEnabled);
        Assert.DoesNotContain(Buttons(wizard.Footer), b => b.Text == "Continue");
    }

    [Fact]
    public async Task Importing_sends_the_rows_and_shows_the_result()
    {
        var sent = new List<int>();
        var importer = NewImporter(rows =>
        {
            sent.AddRange(rows.Select(r => r.Index));
            return Accept(rows);
        }, batchSize: 1);
        var (wizard, _, _) = Start(importer: importer);
        GoTo(importer, "submit");

        await importer.SubmitAsync(TestSupport.Ct);

        Assert.Equal([0, 2], sent);
        var step = Step<ImportSubmitStep>(wizard);
        Assert.Equal("2 rows imported", step.ProgressLabel!.Text);
        Assert.Equal("2 of 2", step.ProgressCount!.Text);
        Assert.Equal(2, step.Progress!.Value);
        Assert.Equal(2, step.Progress.Max);
        Assert.Equal("2 rows imported", step.Progress.AccessibleName);
        Assert.Equal(KlyColor.Success, step.Progress.Color);
        Assert.Contains(Buttons(wizard.Footer), b => b.Text == "Import another file");

        Press(Button(wizard.Footer, "Import another file"));
        Assert.False(importer.FileLoaded);
        Assert.IsType<ImportFileStep>(wizard.CurrentStepView);
    }

    [Fact]
    public void The_import_button_runs_the_import()
    {
        var sent = 0;
        var importer = NewImporter(rows =>
        {
            sent += rows.Count;
            return Accept(rows);
        });
        var (wizard, _, _) = Start(importer: importer);
        GoTo(importer, "submit");

        Press(Button(wizard.Footer, "Import 2 rows"));

        Assert.Equal(2, sent);
        Assert.Equal(SpreadsheetSubmitStatus.Done, importer.SubmitStatus);
    }

    [Fact]
    public async Task A_failed_import_keeps_what_was_accepted_and_offers_to_try_again()
    {
        var fail = true;
        var importer = NewImporter(rows =>
        {
            if (fail && rows[0].Index == 2) throw new InvalidOperationException("Database unavailable");
            return Accept(rows);
        }, batchSize: 1);
        var (wizard, _, _) = Start(importer: importer);
        GoTo(importer, "submit");

        await importer.SubmitAsync(TestSupport.Ct);

        var step = Step<ImportSubmitStep>(wizard);
        Assert.Equal("The import failed", step.ProgressLabel!.Text);
        Assert.Equal("1 row is already imported and will not be sent again.", step.ResumeNote!.Text);
        Assert.NotNull(step.SubmitError);
        Assert.Equal(KlyColor.Error, step.Progress!.Color);
        Assert.Contains(Buttons(wizard.Footer), b => b.Text == "Try again");

        fail = false;
        Press(Button(wizard.Footer, "Try again"));
        Assert.Equal(SpreadsheetSubmitStatus.Done, importer.SubmitStatus);
        Assert.Equal(2, importer.ImportedCount);
    }

    [Fact]
    public async Task A_cancelled_import_offers_to_resume()
    {
        var importer = NewImporter(batchSize: 1, onSubmitAsync: async (rows, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return null;
        });
        var (wizard, _, _) = Start(importer: importer);
        GoTo(importer, "submit");
        using var cts = new CancellationTokenSource();

        var run = importer.SubmitAsync(cts.Token);
        Assert.Equal(SpreadsheetSubmitStatus.Running, importer.SubmitStatus);
        Assert.Contains(Buttons(wizard.Footer), b => b.Text == "Cancel");
        Assert.Equal("Importing…", Step<ImportSubmitStep>(wizard).ProgressLabel!.Text);
        Assert.DoesNotContain(Buttons(wizard.Footer), b => b.Text == "Back");

        Press(Button(wizard.Footer, "Cancel"));
        await run;

        Assert.Equal(SpreadsheetSubmitStatus.Cancelled, importer.SubmitStatus);
        Assert.Equal("The import was cancelled", Step<ImportSubmitStep>(wizard).ProgressLabel!.Text);
        Assert.Contains(Buttons(wizard.Footer), b => b.Text == "Resume the import");
    }

    [Fact]
    public async Task Rows_the_server_refused_can_be_sent_again_or_seen_in_the_review()
    {
        var calls = 0;
        var importer = NewImporter(rows =>
        {
            calls++;
            return Task.FromResult<IReadOnlyList<SpreadsheetRejectedRow>?>(calls == 1 ? [new SpreadsheetRejectedRow(rows[0].Index, "Duplicate", "Name")] : null);
        }, batchSize: 10);
        var (wizard, _, _) = Start(importer: importer);
        GoTo(importer, "submit");

        await importer.SubmitAsync(TestSupport.Ct);

        var step = Step<ImportSubmitStep>(wizard);
        Assert.NotNull(step.RejectedBox);
        Assert.Contains("Rows refused: 1", TestSupport.Texts(step.RejectedBox!));
        Assert.Equal(["Send them again", "See them"], step.RejectedButtons.Select(b => b.Text!).ToArray());

        Press(step.RejectedButtons[1]);
        Assert.Equal("review", importer.CurrentStepKey);
        Assert.Equal(SpreadsheetReviewTab.Invalid, importer.ReviewTab);
        Assert.IsType<ImportReviewStep>(wizard.CurrentStepView);

        Assert.True(importer.NextStep());
        Press(Step<ImportSubmitStep>(wizard).RejectedButtons[0]);
        await Task.Delay(50, TestSupport.Ct);
        Assert.Equal(0, importer.RejectedCount);
        Assert.Equal(2, importer.ImportedCount);
    }

    // --- what is said to a screen reader ------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_review_counts_are_announced_once_and_again_when_they_change()
    {
        var (wizard, importer, said) = Start();
        Assert.Empty(said);

        importer.NextStep();
        Assert.Equal("review", importer.CurrentStepKey);
        Assert.Equal(["To import: 2. With errors: 1. Left out: 0."], said);

        wizard.Redraw();
        Assert.Single(said);

        importer.DiscardRows([0]);
        Assert.Equal("To import: 1. With errors: 1. Left out: 1.", said[^1]);
        Assert.Equal(2, said.Count);
    }

    [Fact]
    public async Task The_result_of_an_import_is_announced()
    {
        var importer = NewImporter(rows => Accept(rows));
        var (wizard, _, said) = Start(importer: importer);
        GoTo(importer, "submit");

        await importer.SubmitAsync(TestSupport.Ct);

        Assert.Equal("2 rows imported", said[^1]);
        Assert.Equal("2 rows imported", wizard.LiveMessage);
    }

    [Fact]
    public async Task A_failed_import_announces_the_failure_with_the_reason()
    {
        var importer = NewImporter(rows => throw new SpreadsheetUserMessageException("Quota exceeded"));
        var (wizard, _, said) = Start(importer: importer);
        GoTo(importer, "submit");

        await importer.SubmitAsync(TestSupport.Ct);

        Assert.StartsWith("The import failed", said[^1]);
        Assert.Contains("Quota exceeded", wizard.LiveMessage);
    }

    [Fact]
    public void A_screen_reader_that_cannot_be_reached_does_not_break_the_wizard()
    {
        var importer = NewImporter();
        var wizard = new KlySpreadsheetImport { Importer = importer };

        importer.LoadText(People, "people.csv");
        importer.NextStep();

        Assert.Equal("review", importer.CurrentStepKey);
        Assert.IsType<ImportReviewStep>(wizard.CurrentStepView);
    }

    [Fact]
    public void The_live_message_is_empty_off_the_review()
    {
        var (wizard, importer, _) = Start();

        Assert.Equal("", wizard.LiveMessage);
        GoTo(importer, "submit");
        Assert.Equal("", wizard.LiveMessage);
        Assert.Equal("", new KlySpreadsheetImport().LiveMessage);
    }

    [Fact]
    public async Task Progress_is_announced_at_most_every_few_seconds()
    {
        var gate = new TaskCompletionSource();
        var importer = NewImporter(async rows =>
        {
            await gate.Task;
            return null;
        }, batchSize: 1);
        var (wizard, _, said) = Start(importer: importer);
        var clock = new ManualClock();
        wizard.Clock = clock;
        GoTo(importer, "submit");

        var run = importer.SubmitAsync(TestSupport.Ct);

        Assert.Contains("Importing…", said);
        var first = wizard.LiveMessage;
        Assert.Equal("Importing: 0 of 2 rows", first);

        clock.Advance(TimeSpan.FromSeconds(1));
        importer.SetReviewTab(SpreadsheetReviewTab.All);
        Assert.Equal(first, wizard.LiveMessage);

        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal("Importing: 0 of 2 rows", wizard.LiveMessage);

        gate.SetResult();
        await run;
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
