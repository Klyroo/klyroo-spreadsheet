using Microsoft.Extensions.Logging;

namespace Klyroo.Spreadsheet.Maui;

/// <summary>
/// The native import wizard for a <see cref="SpreadsheetImporter"/>: a step bar and five steps over the engine of Klyroo.Spreadsheet.Core.
/// <list type="number">
/// <item><description><b>File</b>: the file picker of the device (CSV, TSV, text, <c>.xlsx</c> or <c>.xls</c>), a paste box, the template to download, the columns the schema expects, then the loaded file with its sheet and header row.</description></item>
/// <item><description><b>Columns</b>: the column mapping, with the suggestions of the engine.</description></item>
/// <item><description><b>Values</b>: the unknown values, which the user maps, creates, leaves empty or skips.</description></item>
/// <item><description><b>Review</b>: the validation summary, a preview grid of the rows (tabs, severity, issue and text filters, pages, selection, inline cell edits, a row inspector).</description></item>
/// <item><description><b>Import</b>: the summary, the progress, the rows the server refused.</description></item>
/// </list>
/// </summary>
/// <remarks>
/// The wizard draws the state of <see cref="Importer"/> and calls its methods; the importer can also be driven from code. It listens to the importer for as long as it is the importer of the wizard.
/// The template and the invalid rows are handed to <see cref="DownloadRequested"/>, or saved to the cache of the app and shared with the share sheet of the device when nothing listens.
/// </remarks>
public class KlySpreadsheetImport : Border
{
    /// <summary>The value of <see cref="MaxRows"/> when you do not set it: 10,000 rows.</summary>
    public const int DefaultMaxRows = 10_000;

    /// <summary>Identifies <see cref="Importer"/>.</summary>
    public static readonly BindableProperty ImporterProperty = BindableProperty.Create(nameof(Importer), typeof(SpreadsheetImporter), typeof(KlySpreadsheetImport), null, propertyChanged: (b, o, n) => ((KlySpreadsheetImport)b).OnImporterSet((SpreadsheetImporter?)o, (SpreadsheetImporter?)n));
    /// <summary>Identifies <see cref="Title"/>.</summary>
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(KlySpreadsheetImport), null, propertyChanged: (b, _, _) => ((KlySpreadsheetImport)b).Redraw());
    /// <summary>Identifies <see cref="MaxFileSize"/>.</summary>
    public static readonly BindableProperty MaxFileSizeProperty = BindableProperty.Create(nameof(MaxFileSize), typeof(long), typeof(KlySpreadsheetImport), 10L * 1024 * 1024);
    /// <summary>Identifies <see cref="PageSize"/>.</summary>
    public static readonly BindableProperty PageSizeProperty = BindableProperty.Create(nameof(PageSize), typeof(int), typeof(KlySpreadsheetImport), SpreadsheetImporter.DefaultReviewPageSize, propertyChanged: (b, _, _) => ((KlySpreadsheetImport)b).ApplySettings());
    /// <summary>Identifies <see cref="MaxRows"/>.</summary>
    public static readonly BindableProperty MaxRowsProperty = BindableProperty.Create(nameof(MaxRows), typeof(int?), typeof(KlySpreadsheetImport), (int?)DefaultMaxRows, propertyChanged: (b, _, _) => ((KlySpreadsheetImport)b).ApplySettings());
    /// <summary>Identifies <see cref="Wizard"/>.</summary>
    public static readonly BindableProperty WizardProperty = BindableProperty.Create(nameof(Wizard), typeof(bool), typeof(KlySpreadsheetImport), true, propertyChanged: (b, _, _) => ((KlySpreadsheetImport)b).Redraw());

    private readonly VerticalStackLayout _root = new() { Spacing = 16 };
    private readonly Label _title = SheetUi.NewLabel(16, KlyTheme.TextHighlighted, FontAttributes.Bold);
    private readonly ContentView _filePillHost = new();
    private readonly FlexLayout _stepper = new() { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center };
    private readonly VerticalStackLayout _header = new() { Spacing = 12 };
    private readonly ContentView _body = new();
    private readonly VerticalStackLayout _footer = new() { Spacing = 8 };

    private SpreadsheetImporter? _subscribed;
    private int _lastPageSize = -1;
    private string? _stepKey;
    private IImportStep? _step;
    private string _announced = "";
    private string _progressAnnouncement = "";
    private DateTimeOffset? _announcedAt;
    private bool _validationRequested;

    /// <summary>Creates a wizard: set <see cref="Importer"/>.</summary>
    public KlySpreadsheetImport()
    {
        this.SetThemeColor(BackgroundColorProperty, KlyTheme.Background);
        this.SetStroke(KlyTheme.Border);
        StrokeThickness = 1;
        this.SetRadius(SheetUi.RadiusOf(0.75));
        Padding = new Thickness(16);
        HorizontalOptions = LayoutOptions.Fill;
        SemanticProperties.SetHeadingLevel(_title, SemanticHeadingLevel.Level2);

        var titleRow = new HorizontalStackLayout { Spacing = 10, Children = { _title, _filePillHost } };
        _header.Add(titleRow);
        _header.Add(_stepper);
        _root.Add(_header);
        _root.Add(_body);
        _root.Add(_footer);
        Content = _root;
        Unloaded += (_, _) => Unsubscribe();
        Loaded += (_, _) => Subscribe(Importer);
        Redraw();
    }

    /// <summary>Raised with the file when the user chooses to download the template or the invalid rows. When nothing listens, the wizard saves the file to the cache of the app and opens the share sheet of the device.</summary>
    public event EventHandler<SpreadsheetDownload>? DownloadRequested;

    /// <summary>Raised with the current <see cref="SpreadsheetMappingProfile"/> when the user changes the column mapping or answers a value question (and when your code does it through the importer), so the app can store it and give it back through <see cref="SpreadsheetImporter.SavedMapping"/>. It is not raised when a file is loaded or when a saved mapping is applied automatically.</summary>
    public event EventHandler<SpreadsheetMappingProfile>? MappingProfileChanged;

    /// <summary>The importer that holds the import state. The wizard draws it and calls its methods. Users load a file with the picker or by pasting rows; your code can also call <see cref="SpreadsheetImporter.LoadText"/> or <see cref="SpreadsheetImporter.LoadWorkbook"/>. Nothing shows while it is <see langword="null"/>.</summary>
    public SpreadsheetImporter? Importer { get => (SpreadsheetImporter?)GetValue(ImporterProperty); set => SetValue(ImporterProperty, value); }

    /// <summary>The heading shown above the wizard. Defaults to the word Import in the language of <see cref="SpreadsheetTexts.Current"/>. Your text is shown as it is.</summary>
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>The largest file users can choose, in bytes. Defaults to 10 MB.</summary>
    public long MaxFileSize { get => (long)GetValue(MaxFileSizeProperty); set => SetValue(MaxFileSizeProperty, value); }

    /// <summary>The number of rows the review grid shows per page, 50 by default. Users can change it with the page size list of the pager.</summary>
    public int PageSize { get => (int)GetValue(PageSizeProperty); set => SetValue(PageSizeProperty, value); }

    /// <summary>The most rows an import takes when <see cref="SpreadsheetSchema.MaxRows"/> is not set, 10,000 by default. Rows past the limit are left out and the wizard says how many. <see langword="null"/> removes the limit. A <see cref="SpreadsheetSchema.MaxRows"/> in the schema wins over this value.</summary>
    public int? MaxRows { get => (int?)GetValue(MaxRowsProperty); set => SetValue(MaxRowsProperty, value); }

    /// <summary>Shows the step bar and the Back, Continue and Import buttons. When <see langword="false"/> only the current step is drawn and your code moves between steps with the step methods of the importer.</summary>
    public bool Wizard { get => (bool)GetValue(WizardProperty); set => SetValue(WizardProperty, value); }

    /// <summary>
    /// Opens the file picker of the device and returns the file the user chose, or <see langword="null"/> when they cancelled. It receives the extensions the schema accepts.
    /// Set it to choose files your own way (a file dialog of the desktop, a document library, a test). The default is the <see cref="FilePicker"/> of MAUI, limited to the accepted extensions.
    /// </summary>
    public Func<IReadOnlyList<string>, Task<SpreadsheetFile?>>? PickFileAsync { get; set; }

    /// <summary>The step that is drawn, for the tests.</summary>
    internal IImportStep? CurrentStepView => _step;

    internal VerticalStackLayout Header => _header;

    internal FlexLayout Stepper => _stepper;

    internal Label TitleLabel => _title;

    internal ContentView FilePillHost => _filePillHost;

    internal ContentView Body => _body;

    internal VerticalStackLayout Footer => _footer;

    /// <summary>The texts in use.</summary>
    internal static SpreadsheetTexts T => SpreadsheetTexts.Current;

    internal string? FileError { get; set; }

    internal string? DownloadError { get; set; }

    internal bool LoadingFile { get; set; }

    internal string PastedText { get; set; } = "";

    internal bool PasteOpen { get; set; }

    internal (int RowIndex, string FieldKey)? EditingCell { get; set; }

    internal string EditingText { get; set; } = "";

    /// <summary>Saves a file instead of the platform, for the tests.</summary>
    internal Func<SpreadsheetDownload, Task>? Saver { get; set; }

    /// <summary>Says a text to a screen reader instead of the platform, for the tests.</summary>
    internal Action<string>? Announcer { get; set; }

    /// <summary>The clock of the progress announcements, for the tests.</summary>
    internal TimeProvider Clock { get; set; } = TimeProvider.System;

    /// <summary>The most often the progress of a running import is announced.</summary>
    internal static readonly TimeSpan ProgressAnnouncementInterval = TimeSpan.FromSeconds(3);

    // --- the importer ------------------------------------------------------------------------------------------------------------------------

    private void OnImporterSet(SpreadsheetImporter? old, SpreadsheetImporter? importer)
    {
        Unsubscribe();
        Subscribe(importer);
        _lastPageSize = -1;
        _stepKey = null;
        ApplySettings();
        Redraw();
    }

    private void Subscribe(SpreadsheetImporter? importer)
    {
        if (importer is null || ReferenceEquals(_subscribed, importer)) return;
        Unsubscribe();
        _subscribed = importer;
        importer.Changed += OnImporterChanged;
        importer.MappingChanged += OnMappingChanged;
        ApplySettings();
        Redraw();
    }

    private void Unsubscribe()
    {
        if (_subscribed is null) return;
        _subscribed.Changed -= OnImporterChanged;
        _subscribed.MappingChanged -= OnMappingChanged;
        _subscribed = null;
    }

    private void ApplySettings()
    {
        if (Importer is not { } importer) return;
        importer.DefaultMaxRows = MaxRows;
        if (_lastPageSize == PageSize) return;
        _lastPageSize = PageSize;
        importer.SetReviewPageSize(PageSize);
    }

    private void OnImporterChanged() => SheetUi.Post(this, Redraw);

    private void OnMappingChanged()
    {
        if (MappingProfileChanged is null || _subscribed is not { } importer) return;
        var profile = importer.GetMappingProfile();
        SheetUi.Post(this, () => MappingProfileChanged?.Invoke(this, profile));
    }

    // --- drawing ------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Draws the wizard again from the state of the importer.</summary>
    internal void Redraw()
    {
        var importer = Importer;
        if (importer is null)
        {
            IsVisible = false;
            return;
        }
        IsVisible = true;
        var texts = T;
        var steps = importer.GetSteps();
        var current = steps.FirstOrDefault(s => s.Key == importer.CurrentStepKey) ?? steps[0];

        _title.Text = Title ?? texts.Import;
        _filePillHost.Content = importer.FileLoaded && !string.IsNullOrEmpty(importer.FileName)
            ? SheetUi.Pill(importer.FileName, KlyTheme.BackgroundMuted, KlyTheme.TextMuted, 12, FontAttributes.None)
            : null;
        _stepper.IsVisible = Wizard;
        BuildStepper(steps, current);

        if (_step is null || _stepKey != current.Key)
        {
            _stepKey = current.Key;
            _step = ImportSteps.Create(current.Key, this);
            _body.Content = (View)_step;
        }
        else
        {
            _step.Refresh();
        }

        BuildFooter(current);
        Announce();

        // The checks that need your data (existing records, batched validation) run once the user reaches the review, and again for rows edited since.
        if (current.Key == "review" && importer.NeedsAsyncValidation && !_validationRequested)
        {
            _validationRequested = true;
            _ = RunValidationAsync(importer);
        }
    }

    private async Task RunValidationAsync(SpreadsheetImporter importer)
    {
        try
        {
            await importer.ValidateAsync();
        }
        finally
        {
            _validationRequested = false;
        }
    }

    private void BuildStepper(IReadOnlyList<SpreadsheetStepStatus> steps, SpreadsheetStepStatus current)
    {
        _stepper.Clear();
        if (!Wizard) return;
        var texts = T;
        foreach (var step in steps)
        {
            var isCurrent = step.State == "current";
            var isDone = step.State == "done";
            var isSkipped = step.State == "skipped";
            var isEarlier = step.Position < current.Position;

            KlyThemeColor fill, text;
            if (isCurrent)
            {
                var primary = KlyTheme.Resolve(KlyColor.Primary);
                (fill, text) = (primary.Fill, primary.Contrast);
            }
            else if (isDone)
            {
                (fill, text) = (SheetUi.TintOf(KlyColor.Success, strong: true), SheetUi.TextOf(KlyColor.Success));
            }
            else
            {
                (fill, text) = (KlyTheme.BackgroundMuted, KlyTheme.TextMuted);
            }

            var parts = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
            parts.Add(SheetUi.Text(isDone || isSkipped ? "✓" : $"{step.Position}.", 12, text, FontAttributes.Bold, LineBreakMode.NoWrap));
            parts.Add(SheetUi.Text(texts.StepTitle(step.Key, step.Label), 12, text, FontAttributes.Bold, LineBreakMode.NoWrap));
            if (isSkipped) parts.Add(SheetUi.Text(texts.NothingToDo, 10, text, FontAttributes.None, LineBreakMode.NoWrap));

            var chip = new Border { StrokeThickness = 0, Padding = new Thickness(12, 0), MinimumHeightRequest = 40, Margin = new Thickness(0, 0, 8, 4), Content = parts };
            chip.SetRadius(999);
            chip.SetThemeColor(BackgroundColorProperty, fill);
            var enabled = isDone || (isSkipped && isEarlier);
            chip.IsEnabled = enabled;
            SemanticProperties.SetDescription(chip, texts.StepTitle(step.Key, step.Label));
            SemanticProperties.SetHint(chip, isCurrent ? $"{step.Position}" : null);
            var key = step.Key;
            SheetUi.Tap(chip, () => Importer?.GoToStep(key));
            _stepper.Add(chip);
        }
    }

    private void BuildFooter(SpreadsheetStepStatus current)
    {
        _footer.Clear();
        _footer.IsVisible = Wizard;
        if (!Wizard || Importer is not { } importer) return;
        var texts = T;

        var left = new HorizontalStackLayout { Spacing = 8 };
        if (importer.CanPreviousStep) left.Add(NavBack(importer, texts));

        var right = new HorizontalStackLayout { Spacing = 12, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center };
        var hint = HintOf(importer, current.Key, texts);
        if (hint is { } h) right.Add(h);

        if (current.Key != "submit")
        {
            var next = SheetUi.Button(texts.Continue, ButtonVariant.Solid, KlyColor.Primary, () => importer.NextStep(), KlySize.Md);
            next.IsEnabled = importer.CanNextStep;
            SemanticProperties.SetHint(next, hint?.Text);
            right.Add(next);
        }
        else if (importer.SubmitStatus == SpreadsheetSubmitStatus.Running)
        {
            right.Add(SheetUi.Button(texts.Cancel, ButtonVariant.Outline, KlyColor.Neutral, importer.CancelSubmit, KlySize.Md));
        }
        else if (importer.SubmitStatus != SpreadsheetSubmitStatus.Done && (importer.PendingRows.Count > 0 || importer.SubmitStatus != SpreadsheetSubmitStatus.Idle))
        {
            var label = importer.SubmitStatus switch
            {
                SpreadsheetSubmitStatus.Error => texts.TryAgain,
                SpreadsheetSubmitStatus.Cancelled => texts.ResumeImport,
                _ => texts.ImportRows(importer.PendingRows.Count),
            };
            var submit = SheetUi.Button(label, ButtonVariant.Solid, KlyColor.Primary, () => _ = importer.SubmitAsync(), KlySize.Md);
            submit.IsEnabled = importer.CanSubmit;
            right.Add(submit);
        }
        else
        {
            right.Add(SheetUi.Button(texts.ImportAnotherFile, ButtonVariant.Outline, KlyColor.Neutral, ReplaceFile, KlySize.Md));
        }

        var row = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) } };
        row.Add(left, 0);
        row.Add(right, 1);
        _footer.Add(SheetUi.Card(row, 12));
    }

    private KlyButton NavBack(SpreadsheetImporter importer, SpreadsheetTexts texts) =>
        SheetUi.Button(texts.Back, ButtonVariant.Outline, KlyColor.Neutral, () => importer.PreviousStep(), KlySize.Md);

    /// <summary>The hint next to the buttons: what stops the user from going on, in the color that says how serious it is.</summary>
    internal static Label? HintOf(SpreadsheetImporter importer, string stepKey, SpreadsheetTexts texts)
    {
        if (stepKey == "file" && !importer.FileLoaded) return SheetUi.Text(texts.ChooseFileToContinue, 12, KlyTheme.TextMuted);
        if (stepKey == "columns" && importer.MissingFields.Count > 0) return SheetUi.Text(texts.RequiredColumnsMissing(importer.MissingFields.Count), 12, SheetUi.TextOf(KlyColor.Error), FontAttributes.Bold);
        if (stepKey == "values" && importer.OpenQuestions.Count > 0) return SheetUi.Text(texts.ValuesToChoose(importer.OpenQuestions.Count), 12, SheetUi.TextOf(KlyColor.Error), FontAttributes.Bold);
        if (stepKey == "review" && importer.InvalidRows.Count > 0) return SheetUi.Text(texts.RowsWithErrorsLeftOut(importer.InvalidRows.Count), 12, SheetUi.TextOf(KlyColor.Warning));
        return null;
    }

    // --- what is said to a screen reader -------------------------------------------------------------------------------------------------------

    /// <summary>What a screen reader is told: the counts of the review (they change after each edit), then the progress and the result of the import.</summary>
    internal string LiveMessage
    {
        get
        {
            var importer = Importer;
            if (importer is null) return "";
            var texts = T;
            if (importer.SubmitStatus != SpreadsheetSubmitStatus.Running) _announcedAt = null;
            switch (importer.SubmitStatus)
            {
                case SpreadsheetSubmitStatus.Running:
                    return RunningAnnouncement(importer, texts);
                case SpreadsheetSubmitStatus.Done:
                    return importer.RejectedCount > 0
                        ? $"{texts.RowsImported(importer.ImportedCount)}. {texts.RowsRefused(importer.RejectedCount)}."
                        : texts.RowsImported(importer.ImportedCount);
                case SpreadsheetSubmitStatus.Cancelled:
                    return texts.ImportCancelled;
                case SpreadsheetSubmitStatus.Error:
                    return importer.SubmitError is { Length: > 0 } error ? $"{texts.ImportFailed}. {error}" : texts.ImportFailed;
            }
            if (importer.CurrentStepKey != "review") return "";
            var counts = texts.LiveReview(importer.ImportableRows.Count, importer.InvalidRows.Count, importer.DiscardedRows.Count);
            return importer.HasAsyncChecks && importer.ValidationStatus == SpreadsheetValidationStatus.Running ? $"{texts.CheckingRows} {counts}" : counts;
        }
    }

    // Progress is announced at most every few seconds: a screen reader would otherwise be told about each batch. Before the first
    // batch is known (nothing to send yet, or the checks still run) the message has no count, so it never says "0 of 0".
    private string RunningAnnouncement(SpreadsheetImporter importer, SpreadsheetTexts texts)
    {
        if (importer.SubmitTotal == 0) return texts.Importing;
        var now = Clock.GetUtcNow();
        if (_announcedAt is { } at && now - at < ProgressAnnouncementInterval) return _progressAnnouncement;
        _announcedAt = now;
        return _progressAnnouncement = texts.LiveImporting(importer.SubmitDone, importer.SubmitTotal);
    }

    private void Announce()
    {
        var message = LiveMessage;
        if (message == _announced) return;
        _announced = message;
        if (message.Length == 0) return;
        if (Announcer is { } announcer)
        {
            announcer(message);
            return;
        }
        try
        {
            SemanticScreenReader.Default.Announce(message);
        }
        catch (Exception)
        {
            // No screen reader service on this platform: the wizard still shows everything it would have said.
        }
    }

    // --- the file ------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Opens the picker and loads the chosen file into the importer. A file that cannot be read is reported on the file step, in words that never carry the text of an exception.</summary>
    internal async Task ChooseFileAsync()
    {
        if (Importer is not { } importer) return;
        var texts = T;
        FileError = null;
        DownloadError = null;
        LoadingFile = true;
        Redraw();
        try
        {
            var file = await (PickFileAsync ?? DefaultPickers.PickAsync)(importer.Schema.Accept);
            if (file is null) return;
            if (file.Length is { } length && length > MaxFileSize)
            {
                FileError = texts.FileTooLarge((int)(MaxFileSize / (1024 * 1024)));
                return;
            }
            await using var stream = await file.OpenRead();
            if (stream.CanSeek && stream.Length > MaxFileSize)
            {
                FileError = texts.FileTooLarge((int)(MaxFileSize / (1024 * 1024)));
                return;
            }
            await importer.LoadFileAsync(stream, file.Name, MaxFileSize);
        }
        catch (InvalidDataException ex)
        {
            // The readers of the library write these messages (not a workbook, a damaged archive), so they are shown.
            FileError = ex.Message;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or NotImplementedException or InvalidOperationException)
        {
            // An I/O failure or a refused permission can name a path: the user gets a generic text, the log the exception.
            if (importer.Logger is { } logger) logger.LogWarning(ex, "KlySpreadsheetImport: the file the user chose could not be read. The user sees a generic message.");
            FileError = texts.FileReadFailed;
        }
        finally
        {
            LoadingFile = false;
            Redraw();
        }
    }

    internal void LoadPastedRows()
    {
        if (string.IsNullOrWhiteSpace(PastedText) || Importer is not { } importer) return;
        FileError = null;
        DownloadError = null;
        var text = PastedText;
        PastedText = "";
        importer.Paste(text);
        Redraw();
    }

    internal void ReplaceFile()
    {
        FileError = null;
        DownloadError = null;
        Importer?.ClearFile();
    }

    // --- editing a cell ------------------------------------------------------------------------------------------------------------------------

    internal void StartCellEdit(int rowIndex, string fieldKey, string currentText)
    {
        EditingCell = (rowIndex, fieldKey);
        EditingText = currentText;
        Redraw();
    }

    internal void CommitCellEdit(int rowIndex, string fieldKey)
    {
        if (EditingCell != (rowIndex, fieldKey)) return;
        EditingCell = null;
        Importer?.EditCell(rowIndex, fieldKey, EditingText);
        Redraw();
    }

    // --- downloads -----------------------------------------------------------------------------------------------------------------------------

    internal Task DownloadTemplateAsync() =>
        Importer is not { } importer
            ? Task.CompletedTask
            : DownloadAsync(new SpreadsheetDownload($"{SpreadsheetDownload.Slug(importer.Schema.Key)}-template.csv", importer.ExportTemplateCsv(), SpreadsheetDownload.CsvMimeType));

    internal Task DownloadInvalidRowsAsync() =>
        Importer is not { } importer
            ? Task.CompletedTask
            : DownloadAsync(new SpreadsheetDownload($"{SpreadsheetDownload.Slug(importer.Schema.Key)}-invalid-rows.csv", importer.ExportInvalidRowsCsv(T.ResolveIssue, T.IssuesColumn), SpreadsheetDownload.CsvMimeType));

    // The host takes the file when it listens (custom saving); otherwise the file goes to the cache of the app and to the share sheet.
    private async Task DownloadAsync(SpreadsheetDownload file)
    {
        DownloadError = null;
        if (DownloadRequested is { } handler)
        {
            handler(this, file);
            return;
        }
        try
        {
            await (Saver ?? DefaultPickers.SaveAsync)(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or NotImplementedException or InvalidOperationException)
        {
            if (Importer?.Logger is { } logger) logger.LogWarning(ex, "KlySpreadsheetImport: the file could not be downloaded. The user sees a generic message.");
            DownloadError = T.DownloadFailed;
        }
        Redraw();
    }
}
