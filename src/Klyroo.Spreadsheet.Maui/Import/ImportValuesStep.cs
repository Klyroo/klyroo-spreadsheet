namespace Klyroo.Spreadsheet.Maui;

/// <summary>The values step: each value of a choice column that the schema does not know, and what the user wants done with it.</summary>
internal sealed class ImportValuesStep : VerticalStackLayout, IImportStep
{
    private readonly KlySpreadsheetImport _owner;
    private readonly List<(SpreadsheetValueQuestion Question, Border Card, KlySelect Select)> _rows = [];

    public ImportValuesStep(KlySpreadsheetImport owner)
    {
        _owner = owner;
        Spacing = 12;
        Refresh();
    }

    public string Key => "values";

    /// <summary>A card and its list per question, for the tests.</summary>
    public IReadOnlyList<(SpreadsheetValueQuestion Question, Border Card, KlySelect Select)> Rows => _rows;

    /// <summary>The note shown when there is nothing to answer, for the tests.</summary>
    public Label? EmptyNote { get; private set; }

    public void Refresh()
    {
        Children.Clear();
        _rows.Clear();
        EmptyNote = null;
        if (_owner.Importer is not { } importer) return;
        var texts = KlySpreadsheetImport.T;

        if (importer.Questions.Count == 0)
        {
            EmptyNote = SheetUi.Text(texts.AllValuesFound, 12, KlyTheme.TextMuted);
            Children.Add(EmptyNote);
            return;
        }

        foreach (var question in importer.Questions)
        {
            var info = new VerticalStackLayout { Spacing = 2 };
            info.Add(SheetUi.Text($"{question.FieldLabel}: \"{question.RawValue}\"", 12, KlyTheme.TextHighlighted, FontAttributes.Bold));
            info.Add(SheetUi.Text(texts.RowCount(question.RowIndexes.Count), 11, KlyTheme.TextMuted));

            var choices = Choices(question, texts);
            var select = SheetUi.Select(choices, SelectedChoice(question, choices), texts.AnswerFor(question.RawValue), choice => Answer(importer, question, choice.Value), !importer.MappingLocked);
            var card = SheetUi.Card(new VerticalStackLayout { Spacing = 8, Children = { info, select } }, 12);
            SemanticProperties.SetDescription(card, $"{question.FieldLabel}: {question.RawValue}");
            _rows.Add((question, card, select));
            Children.Add(card);
        }
    }

    /// <summary>The answers a question offers: nothing yet, each known value, creating the value, leaving the cell empty, and leaving the rows out.</summary>
    internal static List<SheetChoice> Choices(SpreadsheetValueQuestion question, SpreadsheetTexts texts)
    {
        var choices = new List<SheetChoice> { new("", texts.Choose) };
        foreach (var option in question.Choices) choices.Add(new SheetChoice($"map:{option.Value}", option.Label));
        if (question.CanCreate) choices.Add(new SheetChoice("action:create", texts.CreateValue(question.RawValue)));
        choices.Add(new SheetChoice("action:leave-empty", texts.LeaveEmpty));
        choices.Add(new SheetChoice("action:skip-rows", texts.SkipRows));
        return choices;
    }

    internal static SheetChoice SelectedChoice(SpreadsheetValueQuestion question, IReadOnlyList<SheetChoice> choices)
    {
        var key = question.Answer switch
        {
            { Action: SpreadsheetValueAnswerAction.Map } answer => $"map:{answer.MappedValue}",
            { Action: SpreadsheetValueAnswerAction.Create } => "action:create",
            { Action: SpreadsheetValueAnswerAction.LeaveEmpty } => "action:leave-empty",
            { Action: SpreadsheetValueAnswerAction.SkipRows } => "action:skip-rows",
            _ => "",
        };
        return choices.FirstOrDefault(c => c.Value == key) ?? choices[0];
    }

    /// <summary>Gives the answer the user chose to the importer. An empty value takes the answer back.</summary>
    internal static void Answer(SpreadsheetImporter importer, SpreadsheetValueQuestion question, string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            importer.ClearAnswer(question.FieldKey, question.RawValue);
        }
        else if (raw.StartsWith("map:", StringComparison.Ordinal))
        {
            var value = raw[4..];
            var option = question.Choices.FirstOrDefault(c => c.Value == value);
            importer.AnswerValue(question.FieldKey, question.RawValue, new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Map, value, option?.Label ?? value));
        }
        else if (raw == "action:create")
        {
            importer.AnswerValue(question.FieldKey, question.RawValue, new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.Create));
        }
        else if (raw == "action:leave-empty")
        {
            importer.AnswerValue(question.FieldKey, question.RawValue, new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.LeaveEmpty));
        }
        else if (raw == "action:skip-rows")
        {
            importer.AnswerValue(question.FieldKey, question.RawValue, new SpreadsheetValueAnswer(SpreadsheetValueAnswerAction.SkipRows));
        }
    }
}
