namespace Klyroo.Spreadsheet.Maui;

/// <summary>The last step: the summary of what will be imported, then the progress, the result, and the rows the server refused.</summary>
internal sealed class ImportSubmitStep : VerticalStackLayout, IImportStep
{
    private readonly KlySpreadsheetImport _owner;

    public ImportSubmitStep(KlySpreadsheetImport owner)
    {
        _owner = owner;
        Spacing = 16;
        Refresh();
    }

    public string Key => "submit";

    // What the tests look at.
    public IReadOnlyList<Label> SummaryValues { get; private set; } = [];
    public Label? ProgressLabel { get; private set; }
    public Label? ProgressCount { get; private set; }
    public KlyProgress? Progress { get; private set; }
    public Label? ResumeNote { get; private set; }
    public Label? SubmitError { get; private set; }
    public Border? RejectedBox { get; private set; }
    public IReadOnlyList<KlyButton> RejectedButtons { get; private set; } = [];

    public void Refresh()
    {
        Children.Clear();
        SummaryValues = [];
        ProgressLabel = ProgressCount = ResumeNote = SubmitError = null;
        Progress = null;
        RejectedBox = null;
        RejectedButtons = [];
        if (_owner.Importer is not { } importer) return;
        var texts = KlySpreadsheetImport.T;

        if (importer.SubmitStatus == SpreadsheetSubmitStatus.Idle) BuildSummary(importer, texts);
        else BuildProgress(importer, texts);
    }

    private void BuildSummary(SpreadsheetImporter importer, SpreadsheetTexts texts)
    {
        var values = new List<Label>();
        View Item(string caption, int value, KlyThemeColor color)
        {
            var number = SheetUi.Text(value.ToString("N0", System.Globalization.CultureInfo.CurrentCulture), 16, color, FontAttributes.Bold, LineBreakMode.NoWrap);
            values.Add(number);
            var item = new VerticalStackLayout { Spacing = 2, Children = { SheetUi.Text(caption, 12, KlyTheme.TextMuted), number } };
            FlexLayout.SetGrow(item, 1);
            FlexLayout.SetBasis(item, new Microsoft.Maui.Layouts.FlexBasis(120f, false));
            item.Margin = new Thickness(0, 0, 12, 8);
            SemanticProperties.SetDescription(item, $"{caption}: {value}");
            return item;
        }
        var grid = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
        grid.Add(Item(texts.SummaryToImport, importer.ImportableRows.Count, KlyTheme.TextHighlighted));
        grid.Add(Item(texts.SummaryInvalid, importer.InvalidRows.Count, SheetUi.TextOf(KlyColor.Error)));
        grid.Add(Item(texts.SummaryLeftOut, importer.DiscardedRows.Count, KlyTheme.TextMuted));
        grid.Add(Item(texts.SummaryEdited, importer.AllRows.Count(r => r.Edited), KlyTheme.TextHighlighted));
        SummaryValues = values;
        Children.Add(SheetUi.Card(grid, 16));
    }

    private void BuildProgress(SpreadsheetImporter importer, SpreadsheetTexts texts)
    {
        var stack = new VerticalStackLayout { Spacing = 12 };
        var label = importer.SubmitStatus switch
        {
            SpreadsheetSubmitStatus.Running => texts.Importing,
            SpreadsheetSubmitStatus.Done => texts.RowsImported(importer.ImportedCount),
            SpreadsheetSubmitStatus.Cancelled => texts.ImportCancelled,
            _ => texts.ImportFailed,
        };
        var head = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        ProgressLabel = SheetUi.Text(label, 12, KlyTheme.TextHighlighted, FontAttributes.Bold);
        ProgressCount = SheetUi.Text(texts.Progress(importer.SubmitDone, importer.SubmitTotal), 12, KlyTheme.TextMuted, FontAttributes.None, LineBreakMode.NoWrap);
        head.Add(ProgressLabel, 0);
        head.Add(ProgressCount, 1);
        stack.Add(head);

        Progress = new KlyProgress { Max = Math.Max(1, importer.SubmitTotal), Value = Math.Min(importer.SubmitDone, Math.Max(1, importer.SubmitTotal)), AccessibleName = label };
        if (importer.SubmitStatus == SpreadsheetSubmitStatus.Done) Progress.Color = KlyColor.Success;
        else if (importer.SubmitStatus == SpreadsheetSubmitStatus.Error) Progress.Color = KlyColor.Error;
        stack.Add(Progress);

        if (importer.SubmitStatus is SpreadsheetSubmitStatus.Error or SpreadsheetSubmitStatus.Cancelled)
        {
            ResumeNote = SheetUi.Text(texts.AlreadyImported(importer.ImportedCount), 12, KlyTheme.TextMuted);
            stack.Add(ResumeNote);
            if (importer.SubmitStatus == SpreadsheetSubmitStatus.Error)
            {
                SubmitError = SheetUi.Text(importer.SubmitError ?? texts.ImportFailed, 12, SheetUi.TextOf(KlyColor.Error), FontAttributes.Bold);
                stack.Add(SubmitError);
            }
        }

        if (importer.RejectedCount > 0) stack.Add(BuildRejected(importer, texts));
        Children.Add(SheetUi.Card(stack, 16));
    }

    private View BuildRejected(SpreadsheetImporter importer, SpreadsheetTexts texts)
    {
        var again = SheetUi.Link(texts.SendAgain, () => _ = importer.RetryRejectedAsync(), KlyColor.Error);
        var see = SheetUi.Link(texts.SeeThem, () =>
        {
            importer.SetReviewTab(SpreadsheetReviewTab.Invalid);
            importer.GoToStep("review");
        }, KlyColor.Error);
        RejectedButtons = [again, see];
        var row = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center };
        var text = SheetUi.Text(texts.RowsRefused(importer.RejectedCount), 12, SheetUi.TextOf(KlyColor.Error));
        text.Margin = new Thickness(0, 0, 12, 0);
        text.VerticalOptions = LayoutOptions.Center;
        row.Add(text);
        row.Add(again);
        row.Add(see);
        var box = new Border { StrokeThickness = 0, Padding = new Thickness(12, 4), Content = row, HorizontalOptions = LayoutOptions.Fill };
        box.SetRadius(KlyTheme.RadiusOf(1));
        box.SetThemeColor(VisualElement.BackgroundColorProperty, SheetUi.TintOf(KlyColor.Error));
        RejectedBox = box;
        return box;
    }
}
