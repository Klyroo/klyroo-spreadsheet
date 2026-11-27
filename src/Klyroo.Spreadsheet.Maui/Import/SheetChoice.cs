namespace Klyroo.Spreadsheet.Maui;

/// <summary>An entry of a <see cref="KlySelect"/> of the wizard: a value, and the text the list shows for it.</summary>
internal sealed record SheetChoice(string Value, string Label)
{
    public override string ToString() => Label;
}
