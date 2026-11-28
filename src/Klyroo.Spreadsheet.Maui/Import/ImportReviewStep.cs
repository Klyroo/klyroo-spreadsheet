using System.Globalization;

namespace Klyroo.Spreadsheet.Maui;

/// <summary>
/// The review step: the validation summary (the rows to import, with errors, left out), the filters, the preview grid of the rows with inline cell edits,
/// the pager, the selection bar and the inspector of a row.
/// </summary>
internal sealed class ImportReviewStep : VerticalStackLayout, IImportStep
{
    private readonly KlySpreadsheetImport _owner;
    private readonly KlyInput _search = new() { ReturnType = ReturnType.Search, Size = KlySize.Sm };
    private bool _syncingSearch;

    public ImportReviewStep(KlySpreadsheetImport owner)
    {
        _owner = owner;
        Spacing = 12;
        _search.TextChanged += (_, e) =>
        {
            if (_syncingSearch || _owner.Importer is not { } importer) return;
            var text = e.NewTextValue ?? "";
            if (text != importer.ReviewSearch) importer.SetReviewSearch(text);
        };
        Refresh();
    }

    public string Key => "review";

    // What the tests look at.
    public Label? ImportableCount { get; private set; }
    public Label? InvalidCount { get; private set; }
    public Label? DiscardedCount { get; private set; }
    public IReadOnlyList<(SpreadsheetReviewTab Tab, Border Chip)> Tabs { get; private set; } = [];
    public KlySelect? LevelSelect { get; private set; }
    public KlySelect? IssueSelect { get; private set; }
    public KlyInput Search => _search;
    public Grid? Table { get; private set; }
    public IReadOnlyList<(SpreadsheetRow Row, CheckBox Select, Label Number, Border Status)> Rows { get; private set; } = [];
    public IReadOnlyList<(SpreadsheetRow Row, string FieldKey, View Cell)> Cells { get; private set; } = [];
    public CheckBox? SelectAll { get; private set; }
    public Label? EmptyNote { get; private set; }
    public Border? SelectionBar { get; private set; }
    public KlyButton? InvalidDownload { get; private set; }
    public Label? PagerRange { get; private set; }
    public Label? PagerStatus { get; private set; }
    public IReadOnlyList<KlyButton> PagerButtons { get; private set; } = [];
    public KlySelect? PageSizeSelect { get; private set; }
    public Border? Inspector { get; private set; }
    public IReadOnlyList<KlyButton> InspectorButtons { get; private set; } = [];
    public Entry? CellEditor { get; private set; }
    public Label? ValidationLabel { get; private set; }

    public void Refresh()
    {
        Children.Clear();
        ImportableCount = InvalidCount = DiscardedCount = null;
        Tabs = [];
        LevelSelect = IssueSelect = null;
        Table = null;
        Rows = [];
        Cells = [];
        SelectAll = null;
        EmptyNote = null;
        SelectionBar = null;
        InvalidDownload = null;
        PagerRange = PagerStatus = null;
        PagerButtons = [];
        PageSizeSelect = null;
        Inspector = null;
        InspectorButtons = [];
        CellEditor = null;
        ValidationLabel = null;
        if (_owner.Importer is not { } importer) return;
        var texts = KlySpreadsheetImport.T;

        foreach (var notice in ImportFileStep.Notices(importer, texts).Where(n => !importer.SavedMappingApplied || !IsSavedMappingNotice(n, texts))) Children.Add(notice);
        BuildValidation(importer, texts);
        BuildStats(importer, texts);
        BuildToolbar(importer, texts);

        if (importer.InvalidRows.Count > 0)
        {
            var download = SheetUi.Button(texts.DownloadInvalidRows(importer.InvalidRows.Count), ButtonVariant.Outline, KlyColor.Neutral, () => _ = _owner.DownloadInvalidRowsAsync());
            download.HorizontalOptions = LayoutOptions.Start;
            InvalidDownload = download;
            Children.Add(download);
            if (_owner.DownloadError is { } error) Children.Add(SheetUi.Notice(error, KlyColor.Error));
        }

        if (importer.SelectedRowIndexes.Count > 0) Children.Add(BuildSelectionBar(importer, texts));

        Children.Add(BuildTable(importer, texts));
        if (importer.InspectedRow is { } inspected) Children.Add(BuildInspector(importer, inspected, texts));
    }

    // The review shows the notes of the file step except the saved mapping, which belongs to the first two steps.
    private static bool IsSavedMappingNotice(View notice, SpreadsheetTexts texts) =>
        notice is Border { Content: Label label } && label.Text == texts.SavedMappingApplied;

    private void BuildValidation(SpreadsheetImporter importer, SpreadsheetTexts texts)
    {
        if (!importer.HasAsyncChecks || importer.ValidationStatus == SpreadsheetValidationStatus.Idle) return;
        var text = importer.ValidationStatus switch
        {
            SpreadsheetValidationStatus.Running => texts.CheckingRows,
            SpreadsheetValidationStatus.Done => texts.RowsChecked,
            SpreadsheetValidationStatus.Cancelled => texts.CheckCancelled,
            _ => importer.ValidationError is { Length: > 0 } error ? texts.CheckFailed(error) : texts.CheckFailedGeneric,
        };
        var row = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
        ValidationLabel = SheetUi.Text(text, 12, KlyTheme.TextMuted);
        ValidationLabel.VerticalOptions = LayoutOptions.Center;
        row.Add(ValidationLabel);
        if (importer.ValidationStatus == SpreadsheetValidationStatus.Running) row.Add(SheetUi.Link(texts.Cancel, importer.CancelValidation));
        else if (importer.ValidationStatus is SpreadsheetValidationStatus.Error or SpreadsheetValidationStatus.Cancelled) row.Add(SheetUi.Link(texts.CheckAgain, () => _ = importer.ValidateAsync()));
        Children.Add(row);
    }

    private void BuildStats(SpreadsheetImporter importer, SpreadsheetTexts texts)
    {
        var tiles = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
        View Tile(int value, KlyThemeColor color, string label, out Label valueLabel)
        {
            valueLabel = SheetUi.Text(value.ToString("N0", CultureInfo.CurrentCulture), 18, color, FontAttributes.Bold, LineBreakMode.NoWrap);
            valueLabel.VerticalOptions = LayoutOptions.Center;
            var caption = SheetUi.Text(label, 12, KlyTheme.TextMuted);
            caption.VerticalOptions = LayoutOptions.Center;
            var tile = SheetUi.Card(new HorizontalStackLayout { Spacing = 6, Children = { valueLabel, caption } }, 12, KlyTheme.BackgroundMuted);
            tile.Margin = new Thickness(0, 0, 8, 8);
            FlexLayout.SetGrow(tile, 1);
            FlexLayout.SetBasis(tile, new Microsoft.Maui.Layouts.FlexBasis(130f, false));
            SemanticProperties.SetDescription(tile, $"{value} {label}");
            return tile;
        }
        tiles.Add(Tile(importer.ImportableRows.Count, SheetUi.TextOf(KlyColor.Success), texts.RowsToImport, out var importable));
        tiles.Add(Tile(importer.InvalidRows.Count, importer.InvalidRows.Count > 0 ? SheetUi.TextOf(KlyColor.Error) : KlyTheme.TextHighlighted, texts.RowsWithErrors, out var invalid));
        tiles.Add(Tile(importer.DiscardedRows.Count, KlyTheme.TextHighlighted, texts.RowsLeftOut, out var discarded));
        ImportableCount = importable;
        InvalidCount = invalid;
        DiscardedCount = discarded;
        Children.Add(tiles);
    }

    private void BuildToolbar(SpreadsheetImporter importer, SpreadsheetTexts texts)
    {
        var chips = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center };
        var tabs = new List<(SpreadsheetReviewTab, Border)>();
        foreach (var tab in Enum.GetValues<SpreadsheetReviewTab>())
        {
            var active = importer.ReviewTab == tab;
            var label = tab switch
            {
                SpreadsheetReviewTab.Importable => texts.TabImportable(importer.ImportableRows.Count),
                SpreadsheetReviewTab.Invalid => texts.TabInvalid(importer.InvalidRows.Count),
                SpreadsheetReviewTab.Discarded => texts.TabDiscarded(importer.DiscardedRows.Count),
                _ => texts.TabAll(importer.AllRows.Count),
            };
            var chip = new Border { StrokeThickness = 0, Padding = new Thickness(10, 0), MinimumHeightRequest = 40, Margin = new Thickness(0, 0, 4, 4), Content = SheetUi.Text(label, 12, active ? KlyTheme.TextHighlighted : KlyTheme.TextMuted, FontAttributes.Bold, LineBreakMode.NoWrap) };
            chip.SetRadius(KlyTheme.RadiusOf(1));
            ((Label)chip.Content).VerticalOptions = LayoutOptions.Center;
            if (active) chip.SetThemeColor(VisualElement.BackgroundColorProperty, KlyTheme.BackgroundMuted);
            SemanticProperties.SetDescription(chip, label);
            SemanticProperties.SetHint(chip, active ? "selected" : null);
            var current = tab;
            SheetUi.Tap(chip, () => importer.SetReviewTab(current));
            tabs.Add((tab, chip));
            chips.Add(chip);
        }
        Tabs = tabs;
        Children.Add(chips);

        var levels = new List<SheetChoice>
        {
            new(nameof(SpreadsheetReviewLevel.All), texts.AllSeverities),
            new(nameof(SpreadsheetReviewLevel.Blocking), texts.Errors),
            new(nameof(SpreadsheetReviewLevel.Warnings), texts.Warnings),
        };
        var level = SheetUi.Select(levels, levels.First(l => l.Value == importer.ReviewLevel.ToString()), texts.Severity, c => importer.SetReviewLevel(Enum.Parse<SpreadsheetReviewLevel>(c.Value)));
        LevelSelect = level;

        var filters = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center };
        level.Margin = new Thickness(0, 0, 8, 8);
        level.MinimumWidthRequest = 150;
        filters.Add(level);

        var kinds = IssueKinds(importer);
        if (kinds.Count > 0)
        {
            var choices = new List<SheetChoice> { new("", texts.AllIssues) };
            choices.AddRange(kinds.Select(k => new SheetChoice(k.Filter, $"{k.Label} ({k.Count})")));
            var selected = choices.FirstOrDefault(c => c.Value == importer.ReviewIssueFilter) ?? choices[0];
            var issue = SheetUi.Select(choices, selected, texts.Issue, c => importer.SetReviewIssueFilter(c.Value));
            issue.Margin = new Thickness(0, 0, 8, 8);
            issue.MinimumWidthRequest = 180;
            IssueSelect = issue;
            filters.Add(issue);
        }
        Children.Add(filters);

        _search.Placeholder = texts.SearchPlaceholder;
        _search.AccessibleName = texts.SearchRows;
        _syncingSearch = true;
        try
        {
            if (_search.Text != importer.ReviewSearch) _search.Text = importer.ReviewSearch;
        }
        finally
        {
            _syncingSearch = false;
        }
        DisplayDetach(_search);
        Children.Add(_search);
    }

    private static void DisplayDetach(View view)
    {
        if (view.Parent is Layout layout) layout.Remove(view);
    }

    private Border BuildSelectionBar(SpreadsheetImporter importer, SpreadsheetTexts texts)
    {
        var row = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center };
        var count = SheetUi.Text(texts.SelectedCount(importer.SelectedRowIndexes.Count), 12, KlyTheme.Text, FontAttributes.Bold, LineBreakMode.NoWrap);
        count.Margin = new Thickness(0, 0, 12, 0);
        count.VerticalOptions = LayoutOptions.Center;
        row.Add(count);
        row.Add(SheetUi.Link(texts.LeaveOut, () =>
        {
            importer.DiscardRows(importer.SelectedRowIndexes.ToList());
            importer.ClearSelection();
        }, KlyColor.Error));
        row.Add(SheetUi.Link(texts.Restore, () =>
        {
            importer.RestoreRows(importer.SelectedRowIndexes.ToList());
            importer.ClearSelection();
        }));
        row.Add(SheetUi.Button(texts.ClearSelection, ButtonVariant.Link, KlyColor.Neutral, importer.ClearSelection));
        var bar = new Border { StrokeThickness = 0, Padding = new Thickness(12, 4), Content = row, HorizontalOptions = LayoutOptions.Fill };
        bar.SetRadius(KlyTheme.RadiusOf(1));
        bar.SetThemeColor(VisualElement.BackgroundColorProperty, KlyTheme.BackgroundMuted);
        SelectionBar = bar;
        return bar;
    }

    // --- the grid ----------------------------------------------------------------------------------------------------------------------------

    private View BuildTable(SpreadsheetImporter importer, SpreadsheetTexts texts)
    {
        var grid = new Grid { ColumnSpacing = 0, RowSpacing = 0 };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(44)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(72)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        foreach (var _ in importer.Fields) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        Table = grid;
        var columns = 3 + importer.Fields.Count;

        // The header.
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var selectAll = new CheckBox { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        selectAll.SetThemeColor(CheckBox.ColorProperty, DisplayPrimary());
        selectAll.IsChecked = importer.ReviewPageRows.Count > 0 && importer.ReviewPageRows.All(r => importer.SelectedRowIndexes.Contains(r.Index));
        selectAll.CheckedChanged += (_, e) =>
        {
            foreach (var row in importer.ReviewPageRows) importer.ToggleRowSelection(row.Index, e.Value);
        };
        SemanticProperties.SetDescription(selectAll, texts.SelectPageRows);
        SelectAll = selectAll;
        grid.Add(selectAll, 0, 0);
        grid.Add(HeaderCell(texts.ColumnRow), 1, 0);
        grid.Add(HeaderCell(texts.ColumnStatus), 2, 0);
        for (var f = 0; f < importer.Fields.Count; f++) grid.Add(HeaderCell(importer.Fields[f].Column.EffectiveLabel), 3 + f, 0);
        AddHeaderBackground(grid, columns);

        var rows = new List<(SpreadsheetRow, CheckBox, Label, Border)>();
        var cells = new List<(SpreadsheetRow, string, View)>();
        var line = 1;
        foreach (var row in importer.ReviewPageRows)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var discarded = row.DiscardReason is not null;
            var accepted = importer.IsRowAccepted(row.Index);

            var select = new CheckBox { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, IsEnabled = !accepted };
            select.SetThemeColor(CheckBox.ColorProperty, DisplayPrimary());
            select.IsChecked = importer.SelectedRowIndexes.Contains(row.Index);
            var index = row.Index;
            select.CheckedChanged += (_, e) => importer.ToggleRowSelection(index, e.Value);
            SemanticProperties.SetDescription(select, texts.SelectRow(row.RowNumber));
            grid.Add(select, 0, line);

            var number = SheetUi.Text($"#{row.RowNumber}", 12, SheetUi.TextOf(KlyColor.Primary), FontAttributes.Bold, LineBreakMode.NoWrap);
            number.FontFamily = "monospace";
            number.VerticalOptions = LayoutOptions.Center;
            number.Padding = new Thickness(10, 10);
            SemanticProperties.SetDescription(number, texts.InspectRow(row.RowNumber));
            SheetUi.Tap(number, () => importer.InspectRow(index));
            grid.Add(number, 1, line);

            var status = StatusPill(row, accepted, texts);
            status.Margin = new Thickness(8, 0);
            grid.Add(status, 2, line);

            for (var f = 0; f < importer.Fields.Count; f++)
            {
                var field = importer.Fields[f];
                row.Cells.TryGetValue(field.Column.Key, out var cell);
                var view = BuildCell(importer, row, field, cell, discarded, accepted, texts);
                cells.Add((row, field.Column.Key, view));
                grid.Add(view, 3 + f, line);
            }

            if (discarded)
            {
                foreach (var child in grid.Children.OfType<View>().Where(c => Grid.GetRow(c) == line)) child.Opacity = 0.6;
            }
            rows.Add((row, select, number, status));

            var divider = new BoxView { HeightRequest = 1, VerticalOptions = LayoutOptions.End };
            divider.SetThemeColor(BoxView.ColorProperty, KlyTheme.Border);
            grid.Add(divider, 0, line);
            Grid.SetColumnSpan(divider, columns);
            line++;
        }
        Rows = rows;
        Cells = cells;

        var stack = new VerticalStackLayout { Spacing = 0 };
        stack.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Content = grid });
        if (importer.VisibleReviewRows.Count == 0)
        {
            var empty = SheetUi.Text(texts.NoRows, 12, KlyTheme.TextMuted);
            empty.HorizontalTextAlignment = TextAlignment.Center;
            empty.Margin = new Thickness(12, 24);
            EmptyNote = empty;
            stack.Add(empty);
        }
        stack.Add(BuildPager(importer, texts));

        var card = new Border { StrokeThickness = 1, Padding = 0, Content = stack, HorizontalOptions = LayoutOptions.Fill };
        card.SetRadius(KlyTheme.RadiusOf(1));
        card.SetStroke(KlyTheme.Border);
        return card;
    }

    private static KlyThemeColor DisplayPrimary() => KlyTheme.Resolve(KlyColor.Primary).Fill;

    private static Label HeaderCell(string text)
    {
        var label = SheetUi.Text(text, 12, KlyTheme.TextMuted, FontAttributes.Bold, LineBreakMode.NoWrap);
        label.Padding = new Thickness(10, 10);
        label.MinimumWidthRequest = 56;
        label.VerticalOptions = LayoutOptions.Center;
        return label;
    }

    // The header sits on a muted band that spans the columns.
    private static void AddHeaderBackground(Grid grid, int columns)
    {
        var band = new BoxView { VerticalOptions = LayoutOptions.Fill };
        band.SetThemeColor(BoxView.ColorProperty, KlyTheme.BackgroundMuted);
        grid.Add(band, 0, 0);
        Grid.SetColumnSpan(band, columns);
        band.ZIndex = -1;
    }

    private static Border StatusPill(SpreadsheetRow row, bool accepted, SpreadsheetTexts texts)
    {
        KlyThemeColor fill, text;
        string label;
        if (accepted)
        {
            (fill, text, label) = (KlyTheme.BackgroundMuted, KlyTheme.TextMuted, texts.StatusImported);
        }
        else if (row.Status == SpreadsheetRowStatus.Blocking)
        {
            (fill, text, label) = (SheetUi.TintOf(KlyColor.Error, strong: true), SheetUi.TextOf(KlyColor.Error), texts.StatusText(row.Status));
        }
        else if (row.Status == SpreadsheetRowStatus.Warning)
        {
            (fill, text, label) = (SheetUi.TintOf(KlyColor.Warning, strong: true), SheetUi.TextOf(KlyColor.Warning), texts.StatusText(row.Status));
        }
        else
        {
            (fill, text, label) = (SheetUi.TintOf(KlyColor.Success, strong: true), SheetUi.TextOf(KlyColor.Success), texts.StatusText(row.Status));
        }
        var pill = SheetUi.Pill(label, fill, text, 11);
        pill.VerticalOptions = LayoutOptions.Center;
        return pill;
    }

    private View BuildCell(SpreadsheetImporter importer, SpreadsheetRow row, SpreadsheetFieldState field, SpreadsheetCellView? cell, bool discarded, bool accepted, SpreadsheetTexts texts)
    {
        var key = field.Column.Key;
        if (_owner.EditingCell == (row.Index, key)) return BuildEditor(row, key, field, texts);

        var hasIssue = cell?.Issue is not null;
        var text = SheetUi.Text(string.IsNullOrEmpty(cell?.DisplayText) ? texts.EmptyCell : cell.DisplayText, 12, hasIssue ? SheetUi.TextOf(KlyColor.Error) : KlyTheme.Text, hasIssue ? FontAttributes.Bold : FontAttributes.None, LineBreakMode.NoWrap);
        text.VerticalOptions = LayoutOptions.Center;
        if (discarded) text.TextDecorations = TextDecorations.Strikethrough;

        var box = new HorizontalStackLayout { Spacing = 4, Padding = new Thickness(10, 0), MinimumHeightRequest = 40, VerticalOptions = LayoutOptions.Center };
        box.Add(text);
        if (cell?.Edited == true)
        {
            var mark = SheetUi.Text("✎", 10, SheetUi.TextOf(KlyColor.Primary), FontAttributes.None, LineBreakMode.NoWrap);
            mark.VerticalOptions = LayoutOptions.Center;
            SemanticProperties.SetDescription(mark, texts.OriginalValue(cell.OriginalText));
            box.Add(mark);
        }
        SemanticProperties.SetDescription(box, $"{field.Column.EffectiveLabel}: {text.Text}");
        if (cell?.Issue is { } issue) SemanticProperties.SetHint(box, texts.ResolveIssue(issue));

        var editable = !(discarded || accepted || importer.SubmitStatus == SpreadsheetSubmitStatus.Running || !field.Column.Editable);
        if (editable)
        {
            var current = cell?.Text ?? "";
            SheetUi.Tap(box, () => _owner.StartCellEdit(row.Index, key, current));
        }
        else
        {
            box.IsEnabled = false;
        }
        return box;
    }

    private View BuildEditor(SpreadsheetRow row, string key, SpreadsheetFieldState field, SpreadsheetTexts texts)
    {
        var entry = new Entry { Text = _owner.EditingText, FontSize = 12, MinimumHeightRequest = 40, MinimumWidthRequest = 120, VerticalOptions = LayoutOptions.Center, ReturnType = ReturnType.Done };
        entry.SetThemeColor(Entry.TextColorProperty, KlyTheme.Text);
        SemanticProperties.SetDescription(entry, texts.EditCell(field.Column.EffectiveLabel));
        entry.TextChanged += (_, e) => _owner.EditingText = e.NewTextValue ?? "";
        entry.Completed += (_, _) => _owner.CommitCellEdit(row.Index, key);
        entry.Unfocused += (_, _) => _owner.CommitCellEdit(row.Index, key);
        entry.Loaded += (_, _) => entry.Focus();
        CellEditor = entry;
        return entry;
    }

    // --- the pager ---------------------------------------------------------------------------------------------------------------------------

    private View BuildPager(SpreadsheetImporter importer, SpreadsheetTexts texts)
    {
        var total = importer.VisibleReviewRows.Count;
        var page = importer.ReviewPage;
        var pageCount = importer.ReviewPageCount;
        var from = total == 0 ? 0 : page * importer.ReviewPageSize + 1;
        var to = Math.Min(total, (page + 1) * importer.ReviewPageSize);

        var range = SheetUi.Text(texts.RowsRange(from, to, total), 12, KlyTheme.TextMuted);
        PagerRange = range;

        KlyButton Nav(string glyph, string description, bool enabled, Action go)
        {
            var button = SheetUi.Button(glyph, ButtonVariant.Outline, KlyColor.Neutral, go);
            button.IsEnabled = enabled;
            button.MinimumWidthRequest = 40;
            SemanticProperties.SetDescription(button, description);
            return button;
        }
        var first = Nav("«", texts.FirstPage, page > 0, () => importer.SetReviewPage(0));
        var previous = Nav("‹", texts.PreviousPage, page > 0, () => importer.SetReviewPage(page - 1));
        var next = Nav("›", texts.NextPage, page < pageCount - 1, () => importer.SetReviewPage(page + 1));
        var last = Nav("»", texts.LastPage, page < pageCount - 1, () => importer.SetReviewPage(pageCount - 1));
        PagerButtons = [first, previous, next, last];
        var status = SheetUi.Text(texts.PageOf(page + 1, pageCount), 12, KlyTheme.TextMuted, FontAttributes.None, LineBreakMode.NoWrap);
        status.VerticalOptions = LayoutOptions.Center;
        PagerStatus = status;

        var sizes = PageSizeChoices(importer).Select(s => new SheetChoice(s.ToString(CultureInfo.InvariantCulture), s.ToString(CultureInfo.InvariantCulture))).ToList();
        var size = SheetUi.Select(sizes, sizes.First(s => s.Value == importer.ReviewPageSize.ToString(CultureInfo.InvariantCulture)), texts.RowsPerPage, c =>
        {
            if (int.TryParse(c.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) importer.SetReviewPageSize(value);
        });
        size.MinimumWidthRequest = 90;
        PageSizeSelect = size;

        var buttons = new HorizontalStackLayout { Spacing = 6, Children = { first, previous, status, next, last } };
        var sizeRow = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center, Children = { SheetUi.Text(texts.RowsPerPage, 12, KlyTheme.TextMuted, FontAttributes.None, LineBreakMode.NoWrap), size } };
        ((Label)sizeRow.Children[0]).VerticalOptions = LayoutOptions.Center;
        var pager = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(12, 8), Children = { range, buttons, sizeRow } };
        SemanticProperties.SetDescription(pager, texts.Pagination);

        var frame = new Border { StrokeThickness = 0, Padding = 0, Content = pager };
        var topLine = new BoxView { HeightRequest = 1 };
        topLine.SetThemeColor(BoxView.ColorProperty, KlyTheme.Border);
        return new VerticalStackLayout { Spacing = 0, Children = { topLine, frame } };
    }

    /// <summary>The page sizes of the list: the usual ones and the one in use.</summary>
    internal static IEnumerable<int> PageSizeChoices(SpreadsheetImporter importer) =>
        new[] { 25, 50, 100, 200, importer.ReviewPageSize }.Distinct().Order();

    // --- the issues --------------------------------------------------------------------------------------------------------------------------

    /// <summary>The issues present in the rows, as filters for <see cref="SpreadsheetImporter.SetReviewIssueFilter"/> (<c>fieldKey|code</c>, or <c>|code</c> for the issues of a whole row).</summary>
    internal static List<(string Filter, string Label, int Count)> IssueKinds(SpreadsheetImporter importer)
    {
        var labels = importer.Fields.ToDictionary(f => f.Column.Key, f => f.Column.EffectiveLabel, StringComparer.Ordinal);
        return importer.AllRows
            .SelectMany(r => r.Issues.Select(i => (r.Index, i.Field, i.Code)))
            .GroupBy(x => (x.Field, x.Code))
            .Select(g => (
                Filter: $"{g.Key.Field}|{g.Key.Code}",
                Label: $"{(g.Key.Field is { } f && labels.TryGetValue(f, out var l) ? l : g.Key.Field ?? "Row")}: {g.Key.Code}",
                Count: g.Select(x => x.Index).Distinct().Count()))
            .OrderByDescending(x => x.Count)
            .ToList();
    }

    /// <summary>The label of the column an issue belongs to, or the text for an issue of the whole row.</summary>
    internal static string IssueFieldLabel(SpreadsheetImporter importer, SpreadsheetTexts texts, string? field) =>
        field is null ? texts.WholeRow : importer.Fields.FirstOrDefault(f => f.Column.Key == field)?.Column.EffectiveLabel ?? field;

    // --- the inspector -------------------------------------------------------------------------------------------------------------------------

    private View BuildInspector(SpreadsheetImporter importer, SpreadsheetRow inspected, SpreadsheetTexts texts)
    {
        var stack = new VerticalStackLayout { Spacing = 12 };

        var title = SheetUi.Text(texts.InspectorRow(inspected.RowNumber), 14, KlyTheme.TextHighlighted, FontAttributes.Bold);
        title.VerticalOptions = LayoutOptions.Center;
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level3);
        KlyButton Nav(string glyph, string description, Action go)
        {
            var button = SheetUi.Button(glyph, ButtonVariant.Ghost, KlyColor.Neutral, go);
            button.MinimumWidthRequest = 40;
            SemanticProperties.SetDescription(button, description);
            return button;
        }
        var previous = Nav("‹", texts.PreviousIssueRow, importer.InspectPreviousIssueRow);
        var next = Nav("›", texts.NextIssueRow, importer.InspectNextIssueRow);
        var close = Nav("✕", texts.CloseInspector, () => importer.InspectRow(null));
        var header = new Grid { ColumnSpacing = 4, ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) } };
        header.Add(title, 0);
        header.Add(previous, 1);
        header.Add(next, 2);
        header.Add(close, 3);
        stack.Add(header);
        var buttons = new List<KlyButton> { previous, next, close };

        if (inspected.Issues.Count > 0)
        {
            var issues = new VerticalStackLayout { Spacing = 6 };
            foreach (var issue in inspected.Issues)
            {
                var line = new FormattedString();
                line.Spans.Add(new Span { Text = IssueFieldLabel(importer, texts, issue.Field) + ": ", FontAttributes = FontAttributes.Bold });
                line.Spans.Add(new Span { Text = texts.ResolveIssue(issue) });
                var label = SheetUi.NewLabel(12, SheetUi.TextOf(KlyColor.Error));
                label.FormattedText = line;
                var box = new Border { StrokeThickness = 0, Padding = new Thickness(10, 6), Content = label, HorizontalOptions = LayoutOptions.Fill };
                box.SetRadius(KlyTheme.RadiusOf(0.5));
                box.SetThemeColor(VisualElement.BackgroundColorProperty, SheetUi.TintOf(KlyColor.Error));
                issues.Add(box);
            }
            stack.Add(issues);
        }

        var edited = inspected.Cells.Where(c => c.Value.Edited).ToList();
        if (edited.Count > 0)
        {
            var edits = new VerticalStackLayout { Spacing = 4 };
            foreach (var (fieldKey, cell) in edited)
            {
                var label = importer.Fields.FirstOrDefault(f => f.Column.Key == fieldKey)?.Column.EffectiveLabel ?? fieldKey;
                var row = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
                row.Add(SheetUi.Text($"{label}: {cell.OriginalText} → {cell.Text}", 12, KlyTheme.Text), 0);
                var undo = SheetUi.Link(texts.Undo, () => importer.RevertCell(inspected.Index, fieldKey));
                undo.IsEnabled = !importer.IsRowAccepted(inspected.Index);
                row.Add(undo, 1);
                buttons.Add(undo);
                edits.Add(row);
            }
            stack.Add(edits);
        }

        var actions = new HorizontalStackLayout { HorizontalOptions = LayoutOptions.End, Spacing = 8 };
        if (importer.IsRowAccepted(inspected.Index))
        {
            actions.Add(SheetUi.Text(texts.StatusImported, 12, KlyTheme.TextMuted));
        }
        else if (inspected.DiscardReason == SpreadsheetDiscardReason.Manual)
        {
            var restore = SheetUi.Link(texts.RestoreRow, () => importer.RestoreRows([inspected.Index]));
            buttons.Add(restore);
            actions.Add(restore);
        }
        else if (inspected.DiscardReason is null)
        {
            var leave = SheetUi.Link(texts.LeaveOutRow, () => importer.DiscardRows([inspected.Index]), KlyColor.Error);
            buttons.Add(leave);
            actions.Add(leave);
        }
        stack.Add(actions);
        InspectorButtons = buttons;

        var card = SheetUi.Card(stack, 14, KlyTheme.BackgroundElevated);
        Inspector = card;
        return card;
    }
}
