namespace Klyroo.Spreadsheet.Maui;

/// <summary>Creates the view of a step of the wizard.</summary>
internal static class ImportSteps
{
    public static IImportStep Create(string key, KlySpreadsheetImport owner) => key switch
    {
        "columns" => new ImportColumnsStep(owner),
        "values" => new ImportValuesStep(owner),
        "review" => new ImportReviewStep(owner),
        "submit" => new ImportSubmitStep(owner),
        _ => new ImportFileStep(owner),
    };
}
