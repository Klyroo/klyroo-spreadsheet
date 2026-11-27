namespace Klyroo.Spreadsheet.Maui;

/// <summary>A step of the wizard: a view that draws itself again when the importer changes.</summary>
internal interface IImportStep
{
    /// <summary>The key of the step in <see cref="SpreadsheetStepStatus.Key"/>.</summary>
    string Key { get; }

    /// <summary>Draws the step again from the state of the importer.</summary>
    void Refresh();
}
