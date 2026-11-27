using Microsoft.Maui.Controls.Shapes;

namespace Klyroo.Spreadsheet.Maui;

/// <summary>
/// The file step: the file picker with the paste box and the template to download, then, once a file is loaded, its card with the sheet and the header row to choose.
/// </summary>
internal sealed class ImportFileStep : VerticalStackLayout, IImportStep
{
    private readonly KlySpreadsheetImport _owner;

    public ImportFileStep(KlySpreadsheetImport owner)
    {
        _owner = owner;
        Spacing = 16;
        Refresh();
    }

    public string Key => "file";

    /// <summary>The zone that opens the file picker, for the tests.</summary>
    public Border? Zone { get; private set; }

    public KlyButton? ChooseButton { get; private set; }

    public KlyTextarea? PasteBox { get; private set; }

    public KlyButton? UsePasteButton { get; private set; }

    public KlyButton? PasteToggle { get; private set; }

    public KlyButton? TemplateButton { get; private set; }

    public KlyButton? ReplaceButton { get; private set; }

    public KlySelect? SheetSelect { get; private set; }

    public KlySelect? HeaderRowSelect { get; private set; }

    public Border? FileCard { get; private set; }

    public void Refresh()
    {
        Children.Clear();
        Zone = null;
        ChooseButton = null;
        PasteBox = null;
        UsePasteButton = null;
        PasteToggle = null;
        TemplateButton = null;
        ReplaceButton = null;
        SheetSelect = null;
        HeaderRowSelect = null;
        FileCard = null;
        if (_owner.Importer is not { } importer) return;
        if (importer.FileLoaded) BuildLoaded(importer);
        else BuildChooser(importer);
    }

    private void BuildChooser(SpreadsheetImporter importer)
    {
        var texts = KlySpreadsheetImport.T;

        // The zone: the picker opens on a tap anywhere in it, or on the button.
        var zone = new Border { StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 4, 3 }, Padding = new Thickness(24), HorizontalOptions = LayoutOptions.Fill };
        zone.SetRadius(SheetUi.RadiusOf(0.75));
        zone.SetStroke(KlyTheme.BorderAccented);
        zone.SetThemeColor(VisualElement.BackgroundColorProperty, KlyTheme.BackgroundMuted);
        var accepted = string.Join(", ", importer.Schema.Accept);
        var limit = importer.EffectiveMaxRows is { } rows ? $" · {texts.UpToRows(rows)}" : "";
        var choose = SheetUi.Button(texts.ChooseFile, ButtonVariant.Solid, KlyColor.Primary, () => _ = _owner.ChooseFileAsync(), KlySize.Md);
        choose.IsEnabled = !_owner.LoadingFile;
        SemanticProperties.SetDescription(choose, texts.ChooseFileLabel);
        var title = SheetUi.Text(_owner.LoadingFile ? texts.ReadingFile : texts.ChooseFileLabel, 14, KlyTheme.TextHighlighted, FontAttributes.Bold);
        title.HorizontalTextAlignment = TextAlignment.Center;
        var formats = SheetUi.Text(accepted + limit, 12, KlyTheme.TextMuted);
        formats.HorizontalTextAlignment = TextAlignment.Center;
        zone.Content = new VerticalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center, Children = { title, formats, choose } };
        SheetUi.Tap(zone, () => _ = _owner.ChooseFileAsync());
        Zone = zone;
        ChooseButton = choose;
        Children.Add(zone);

        if (_owner.FileError is { } error) Children.Add(SheetUi.Notice(error, KlyColor.Error));

        BuildPaste(texts);

        var template = SheetUi.Button(texts.DownloadTemplate, ButtonVariant.Outline, KlyColor.Neutral, () => _ = _owner.DownloadTemplateAsync());
        TemplateButton = template;
        Children.Add(template);
        if (_owner.DownloadError is { } downloadError) Children.Add(SheetUi.Notice(downloadError, KlyColor.Error));

        BuildExpectedColumns(importer, texts);
    }

    private void BuildPaste(SpreadsheetTexts texts)
    {
        var toggle = SheetUi.Button(texts.PasteSummary, ButtonVariant.Ghost, KlyColor.Neutral, () =>
        {
            _owner.PasteOpen = !_owner.PasteOpen;
            Refresh();
        });
        toggle.HorizontalOptions = LayoutOptions.Fill;
        PasteToggle = toggle;
        var panel = new VerticalStackLayout { Spacing = 8 };
        panel.Add(toggle);
        if (_owner.PasteOpen)
        {
            var box = new KlyTextarea { Rows = 5, Placeholder = texts.PastePlaceholder, AccessibleName = texts.PasteLabel, Text = _owner.PastedText };
            var use = SheetUi.Button(texts.UseTheseRows, ButtonVariant.Solid, KlyColor.Primary, _owner.LoadPastedRows);
            use.IsEnabled = !string.IsNullOrWhiteSpace(_owner.PastedText);
            use.HorizontalOptions = LayoutOptions.Start;
            box.TextChanged += (_, e) =>
            {
                _owner.PastedText = e.NewTextValue ?? "";
                use.IsEnabled = !string.IsNullOrWhiteSpace(_owner.PastedText);
            };
            PasteBox = box;
            UsePasteButton = use;
            panel.Add(box);
            panel.Add(use);
        }
        Children.Add(SheetUi.Card(panel, 8));
    }

    private void BuildExpectedColumns(SpreadsheetImporter importer, SpreadsheetTexts texts)
    {
        var block = new VerticalStackLayout { Spacing = 6 };
        block.Add(SheetUi.Text(texts.ExpectedColumns, 12, KlyTheme.TextMuted, FontAttributes.Bold));
        foreach (var group in importer.Schema.Columns.Where(c => string.IsNullOrEmpty(c.From)).GroupBy(c => c.Group ?? ""))
        {
            var pills = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center };
            if (group.Key.Length > 0)
            {
                var name = SheetUi.Text(group.Key.ToUpperInvariant(), 11, KlyTheme.TextMuted, FontAttributes.Bold, LineBreakMode.NoWrap);
                name.Margin = new Thickness(0, 0, 8, 6);
                name.VerticalOptions = LayoutOptions.Center;
                pills.Add(name);
            }
            foreach (var column in group)
            {
                var content = new HorizontalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
                content.Add(SheetUi.Text(column.EffectiveLabel, 12, KlyTheme.TextHighlighted, FontAttributes.None, LineBreakMode.NoWrap));
                content.Add(column.Required
                    ? SheetUi.Text("*", 12, SheetUi.TextOf(KlyColor.Error), FontAttributes.None, LineBreakMode.NoWrap)
                    : SheetUi.Text(texts.Optional, 10, KlyTheme.TextMuted, FontAttributes.None, LineBreakMode.NoWrap));
                var pill = new Border { StrokeThickness = 1, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 6), Content = content };
                pill.SetRadius(999);
                pill.SetStroke(KlyTheme.Border);
                pill.SetThemeColor(VisualElement.BackgroundColorProperty, KlyTheme.BackgroundMuted);
                SemanticProperties.SetDescription(pill, column.EffectiveLabel);
                SemanticProperties.SetHint(pill, column.Description);
                pills.Add(pill);
            }
            block.Add(pills);
        }
        Children.Add(block);
    }

    private void BuildLoaded(SpreadsheetImporter importer)
    {
        var texts = KlySpreadsheetImport.T;

        var badge = SheetUi.Pill(SheetUi.ExtensionLabel(importer.FileName), KlyTheme.Background, KlyTheme.TextHighlighted, 12);
        badge.MinimumWidthRequest = 40;
        badge.VerticalOptions = LayoutOptions.Center;
        var name = SheetUi.Text(importer.FileName, 14, KlyTheme.TextHighlighted, FontAttributes.Bold, LineBreakMode.TailTruncation);
        var summary = SheetUi.Text(texts.FileSummary(importer.AllRows.Count, importer.Sheets.Count, importer.HeaderRowNumber), 12, KlyTheme.TextMuted);
        var replace = SheetUi.Button(texts.Replace, ButtonVariant.Outline, KlyColor.Neutral, _owner.ReplaceFile);
        ReplaceButton = replace;

        var grid = new Grid { ColumnSpacing = 10, ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        grid.Add(badge, 0);
        grid.Add(new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Children = { name, summary } }, 1);
        grid.Add(replace, 2);
        var card = SheetUi.Card(grid, 12, KlyTheme.BackgroundMuted);
        FileCard = card;
        Children.Add(card);

        foreach (var notice in Notices(importer, texts)) Children.Add(notice);

        var layout = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center };
        if (importer.Sheets.Count > 1)
        {
            var choices = importer.Sheets.Select(s => new SheetChoice(s.Name, s.Name)).ToList();
            var current = choices.FirstOrDefault(c => c.Value == importer.CurrentSheet?.Name);
            var select = SheetUi.Select(choices, current, texts.Sheet, c => importer.SetSheet(c.Value));
            SheetSelect = select;
            layout.Add(Labeled(texts.Sheet, select));
        }
        if (importer.CurrentSheet is { Rows.Count: > 1 } sheet)
        {
            var choices = Enumerable.Range(0, Math.Min(sheet.Rows.Count, SpreadsheetImporter.HeaderScanRows))
                .Select(i => new SheetChoice(i.ToString(System.Globalization.CultureInfo.InvariantCulture), sheet.RowNumbers[i].ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .ToList();
            var current = choices.FirstOrDefault(c => c.Value == importer.HeaderRowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var select = SheetUi.Select(choices, current, texts.ColumnNamesOnRow, c => importer.SetHeaderRow(int.Parse(c.Value, System.Globalization.CultureInfo.InvariantCulture)));
            HeaderRowSelect = select;
            layout.Add(Labeled(texts.ColumnNamesOnRow, select));
        }
        if (layout.Children.Count > 0) Children.Add(layout);
    }

    private static View Labeled(string label, View control)
    {
        var box = new VerticalStackLayout { Spacing = 4, Margin = new Thickness(0, 0, 16, 8), MinimumWidthRequest = 140 };
        box.Add(SheetUi.Text(label, 12, KlyTheme.TextMuted, FontAttributes.Bold));
        box.Add(control);
        return box;
    }

    /// <summary>The notes under the file card: the row limit, the rules that were stopped, and the saved mapping.</summary>
    internal static IEnumerable<View> Notices(SpreadsheetImporter importer, SpreadsheetTexts texts)
    {
        if (importer.LimitedRowCount > 0 && importer.EffectiveMaxRows is { } limit)
        {
            yield return SheetUi.Notice(texts.RowLimitNotice(limit, importer.LimitedRowCount), KlyColor.Warning);
        }
        foreach (var disabled in importer.DisabledRules)
        {
            var column = importer.Fields.FirstOrDefault(f => f.Column.Key == disabled.ColumnKey)?.Column.EffectiveLabel ?? disabled.ColumnKey;
            yield return SheetUi.Notice(disabled.Reason == SpreadsheetDisabledReason.Failures ? texts.RuleFailedNotice(column, disabled.RuleCode) : texts.RuleDisabledNotice(column, disabled.RuleCode), KlyColor.Warning);
        }
        if (importer.SavedMappingApplied) yield return SheetUi.Notice(texts.SavedMappingApplied, KlyColor.Success);
    }
}
