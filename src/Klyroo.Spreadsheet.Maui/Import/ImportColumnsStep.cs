namespace Klyroo.Spreadsheet.Maui;

/// <summary>The columns step: for each field of the schema, the column of the file it reads, with the suggestion and the samples of the engine.</summary>
internal sealed class ImportColumnsStep : VerticalStackLayout, IImportStep
{
    private readonly KlySpreadsheetImport _owner;
    private readonly List<(string FieldKey, Border Card, KlySelect Select)> _rows = [];

    public ImportColumnsStep(KlySpreadsheetImport owner)
    {
        _owner = owner;
        Spacing = 12;
        Refresh();
    }

    public string Key => "columns";

    /// <summary>A card and its list per field, for the tests.</summary>
    public IReadOnlyList<(string FieldKey, Border Card, KlySelect Select)> Rows => _rows;

    public void Refresh()
    {
        Children.Clear();
        _rows.Clear();
        if (_owner.Importer is not { } importer) return;
        var texts = KlySpreadsheetImport.T;

        if (importer.SavedMappingApplied) Children.Add(SheetUi.Notice(texts.SavedMappingApplied, KlyColor.Success));

        foreach (var field in importer.Fields.Where(f => string.IsNullOrEmpty(f.Column.From)))
        {
            var (card, select) = BuildField(importer, field, texts);
            _rows.Add((field.Column.Key, card, select));
            Children.Add(card);
        }

        if (importer.UnusedHeaders.Count > 0) Children.Add(SheetUi.Text(texts.NotImported(string.Join(", ", importer.UnusedHeaders)), 12, KlyTheme.TextMuted));
    }

    private (Border, KlySelect) BuildField(SpreadsheetImporter importer, SpreadsheetFieldState field, SpreadsheetTexts texts)
    {
        var column = field.Column;
        var info = new VerticalStackLayout { Spacing = 2 };

        var label = new HorizontalStackLayout { Spacing = 4 };
        label.Add(SheetUi.Text(column.EffectiveLabel, 12, KlyTheme.TextHighlighted, FontAttributes.Bold));
        if (column.Required) label.Add(SheetUi.Text("*", 12, SheetUi.TextOf(KlyColor.Error), FontAttributes.Bold, LineBreakMode.NoWrap));
        info.Add(label);
        if (!string.IsNullOrEmpty(column.Description)) info.Add(SheetUi.Text(column.Description, 12, KlyTheme.TextMuted));

        if (field.Suggestion is { } suggestion)
        {
            var row = new VerticalStackLayout { Spacing = 0 };
            row.Add(SheetUi.Text(texts.SuggestionText(suggestion.HeaderText, string.Join(", ", suggestion.Samples)), 12, KlyTheme.TextMuted));
            var use = SheetUi.Link(texts.UseSuggestion, () => importer.AssignColumn(column.Key, suggestion.HeaderIndex));
            use.IsEnabled = !importer.MappingLocked;
            use.HorizontalOptions = LayoutOptions.Start;
            row.Add(use);
            info.Add(row);
        }
        else if (field.Samples.Count > 0)
        {
            info.Add(SheetUi.Text(texts.SamplesText(string.Join(", ", field.Samples)), 12, KlyTheme.TextMuted));
        }

        var choices = new List<SheetChoice> { new("", texts.NotInFile) };
        for (var h = 0; h < importer.Headers.Count; h++)
        {
            if (string.IsNullOrWhiteSpace(importer.Headers[h])) continue;
            choices.Add(new SheetChoice(h.ToString(System.Globalization.CultureInfo.InvariantCulture), importer.Headers[h]));
        }
        var selected = choices.FirstOrDefault(c => c.Value == (field.HeaderIndex?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "")) ?? choices[0];
        var select = SheetUi.Select(choices, selected, texts.ColumnFor(column.EffectiveLabel), choice => OnColumnChanged(importer, column.Key, choice.Value), !importer.MappingLocked);

        var controls = new VerticalStackLayout { Spacing = 4, Children = { select } };
        if (field.AssignedBy == "user")
        {
            var undo = SheetUi.Link(texts.Undo, () => importer.ResetColumn(column.Key));
            undo.IsEnabled = !importer.MappingLocked;
            undo.HorizontalOptions = LayoutOptions.Start;
            controls.Add(undo);
        }

        var body = new VerticalStackLayout { Spacing = 8, Children = { info, controls } };
        var card = SheetUi.Card(body, 12);
        SemanticProperties.SetDescription(card, column.EffectiveLabel);
        return (card, select);
    }

    /// <summary>Maps a field to the column the user chose, or to none.</summary>
    internal static void OnColumnChanged(SpreadsheetImporter importer, string fieldKey, string? value)
    {
        if (string.IsNullOrEmpty(value)) importer.IgnoreColumn(fieldKey);
        else if (int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var index)) importer.AssignColumn(fieldKey, index);
    }
}
