using System.Globalization;

namespace Klyroo.Spreadsheet.Maui;

/// <summary>
/// The built-in texts of <see cref="KlySpreadsheetImport"/>: button labels, accessible names, empty states, counters, the messages of the issues the
/// importer raises, and what the screen reader announces. The wizard reads <see cref="Current"/> when it draws, so set it once at startup
/// (the way <see cref="Klyroo.UI.Maui.KlyTheme.Register"/> is used) to translate it. The Blazor package ships a set per locale; this one holds the English texts.
/// </summary>
/// <remarks>
/// A text that contains a number or a name is a function, so a language can choose its own plural forms and word order.
/// Build a variant with a <c>with</c> expression: <c>SpreadsheetTexts.Current = SpreadsheetTexts.Current with { Back = "Retour" };</c>.
/// Text you pass to the wizard (<c>Title</c>, column labels and descriptions, your own rule messages) is never translated, and neither are the messages of the files the
/// reader refuses (<see cref="System.IO.InvalidDataException"/>), which are English.
/// </remarks>
public sealed record SpreadsheetTexts
{
    private static SpreadsheetTexts _current = new();

    /// <summary>Creates the texts with their English defaults.</summary>
    public SpreadsheetTexts()
    {
    }

    /// <summary>The texts the wizard uses. Replace it at startup to translate the wizard.</summary>
    public static SpreadsheetTexts Current
    {
        get => _current;
        set => _current = value ?? throw new ArgumentNullException(nameof(value));
    }

    // Wizard
    /// <summary>The heading of the wizard when you set no <c>Title</c> (<c>"Import"</c>).</summary>
    public string Import { get; init; } = "Import";

    /// <summary>The title of the file step in the step bar (<c>"File"</c>).</summary>
    public string StepFile { get; init; } = "File";

    /// <summary>The title of the columns step (<c>"Columns"</c>).</summary>
    public string StepColumns { get; init; } = "Columns";

    /// <summary>The title of the values step (<c>"Values"</c>).</summary>
    public string StepValues { get; init; } = "Values";

    /// <summary>The title of the review step (<c>"Review"</c>).</summary>
    public string StepReview { get; init; } = "Review";

    /// <summary>The title of the last step (<c>"Import"</c>).</summary>
    public string StepSubmit { get; init; } = "Import";

    /// <summary>The note on a step that was passed over because it had nothing to resolve (<c>"(nothing to do)"</c>).</summary>
    public string NothingToDo { get; init; } = "(nothing to do)";

    /// <summary>The Back button (<c>"Back"</c>).</summary>
    public string Back { get; init; } = "Back";

    /// <summary>The Continue button (<c>"Continue"</c>).</summary>
    public string Continue { get; init; } = "Continue";

    /// <summary>The button that cancels a running import or check (<c>"Cancel"</c>).</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The button that sends the rest again after a failed import (<c>"Try again"</c>).</summary>
    public string TryAgain { get; init; } = "Try again";

    /// <summary>The button that continues a cancelled import (<c>"Resume the import"</c>).</summary>
    public string ResumeImport { get; init; } = "Resume the import";

    /// <summary>The button shown once the import is done (<c>"Import another file"</c>).</summary>
    public string ImportAnotherFile { get; init; } = "Import another file";

    /// <summary>The import button, given the number of rows it sends (<c>"Import 120 rows"</c>).</summary>
    public Func<int, string> ImportRows { get; init; } = n => $"Import {Grouped(n)} rows";

    /// <summary>The hint next to the disabled Continue button of the file step (<c>"Choose a file to continue"</c>).</summary>
    public string ChooseFileToContinue { get; init; } = "Choose a file to continue";

    /// <summary>The hint of the columns step, given the number of required columns without a file column (<c>"Required columns missing: 2"</c>).</summary>
    public Func<int, string> RequiredColumnsMissing { get; init; } = n => $"Required columns missing: {n}";

    /// <summary>The hint of the values step, given the number of open questions (<c>"Values to choose: 3"</c>).</summary>
    public Func<int, string> ValuesToChoose { get; init; } = n => $"Values to choose: {n}";

    /// <summary>The hint of the review step, given the number of invalid rows (<c>"Rows with errors are left out: 4"</c>).</summary>
    public Func<int, string> RowsWithErrorsLeftOut { get; init; } = n => $"Rows with errors are left out: {Grouped(n)}";


    // File step
    /// <summary>The button that opens the file picker of the device (<c>"Choose a file"</c>).</summary>
    public string ChooseFile { get; init; } = "Choose a file";

    /// <summary>The text of the file zone while a file is read (<c>"Reading the file…"</c>).</summary>
    public string ReadingFile { get; init; } = "Reading the file…";

    /// <summary>The title of the file zone, and the accessible name of the button that opens the file picker (<c>"Choose a file to import"</c>).</summary>
    public string ChooseFileLabel { get; init; } = "Choose a file to import";

    /// <summary>The row limit shown under the file zone, given the limit (<c>"up to 10,000 rows"</c>).</summary>
    public Func<int, string> UpToRows { get; init; } = n => $"up to {Grouped(n)} rows";

    /// <summary>The error for a file over <c>MaxFileSize</c>, given the limit in megabytes (<c>"The file is larger than 10 MB."</c>).</summary>
    public Func<int, string> FileTooLarge { get; init; } = n => $"The file is larger than {n} MB.";

    /// <summary>The heading of the paste area (<c>"Or paste rows copied from a spreadsheet"</c>).</summary>
    public string PasteSummary { get; init; } = "Or paste rows copied from a spreadsheet";

    /// <summary>The accessible name of the paste box (<c>"Rows to import"</c>).</summary>
    public string PasteLabel { get; init; } = "Rows to import";

    /// <summary>The placeholder of the paste box (<c>"Paste the rows here, header row first"</c>).</summary>
    public string PastePlaceholder { get; init; } = "Paste the rows here, header row first";

    /// <summary>The button that loads the pasted rows (<c>"Use these rows"</c>).</summary>
    public string UseTheseRows { get; init; } = "Use these rows";

    /// <summary>The label above the columns the schema expects (<c>"Expected columns:"</c>).</summary>
    public string ExpectedColumns { get; init; } = "Expected columns:";

    /// <summary>The mark of an optional expected column (<c>"optional"</c>).</summary>
    public string Optional { get; init; } = "optional";

    /// <summary>The button that downloads the CSV template (<c>"Download a template"</c>).</summary>
    public string DownloadTemplate { get; init; } = "Download a template";

    /// <summary>The summary of the loaded file, given the number of rows, the number of sheets and the row number of the header (<c>"120 rows · 2 sheets · Column names on row 1"</c>).</summary>
    public Func<int, int, int, string> FileSummary { get; init; } = (rows, sheets, headerRow) => $"{Grouped(rows)} {(rows == 1 ? "row" : "rows")} · {sheets} {(sheets == 1 ? "sheet" : "sheets")} · Column names on row {headerRow}";

    /// <summary>The button that unloads the file to choose another (<c>"Replace"</c>).</summary>
    public string Replace { get; init; } = "Replace";

    /// <summary>The label of the sheet list (<c>"Sheet"</c>).</summary>
    public string Sheet { get; init; } = "Sheet";

    /// <summary>The label of the header row list (<c>"Column names on row"</c>).</summary>
    public string ColumnNamesOnRow { get; init; } = "Column names on row";

    /// <summary>The note shown when <see cref="Klyroo.Spreadsheet.SpreadsheetImporter.SavedMapping"/> was applied (<c>"Your saved column mapping was applied."</c>).</summary>
    public string SavedMappingApplied { get; init; } = "Your saved column mapping was applied.";

    /// <summary>The note shown when rows are past the row limit, given the limit and the number of rows left out (<c>"Only the first 10,000 rows are imported. Rows left out because of the limit: 2,345."</c>).</summary>
    public Func<int, int, string> RowLimitNotice { get; init; } = (limit, left) => $"Only the first {Grouped(limit)} rows are imported. Rows left out because of the limit: {Grouped(left)}.";

    /// <summary>The note shown when a rule was stopped because its matches kept timing out, given the column label and the rule code (<c>"The \"pattern\" check of Code was stopped after repeated timeouts. Its remaining values were not checked."</c>).</summary>
    public Func<string, string, string> RuleDisabledNotice { get; init; } = (column, rule) => $"The \"{rule}\" check of {column} was stopped after repeated timeouts. Its remaining values were not checked.";

    /// <summary>The note shown when a rule was stopped because its predicate kept throwing, given the column label and the rule code (<c>"The \"min\" check of Qty was stopped after repeated failures. Its remaining values were not checked."</c>).</summary>
    public Func<string, string, string> RuleFailedNotice { get; init; } = (column, rule) => $"The \"{rule}\" check of {column} was stopped after repeated failures. Its remaining values were not checked.";


    // Columns step
    /// <summary>The button that accepts a suggested file column (<c>"Use it"</c>).</summary>
    public string UseSuggestion { get; init; } = "Use it";

    /// <summary>The suggestion, given the header and the sample values (<c>"\"Code\" has values like A-1, B-2"</c>).</summary>
    public Func<string, string, string> SuggestionText { get; init; } = (header, samples) => $"\"{header}\" has values like {samples}";

    /// <summary>The sample values of a mapped column, given the list (<c>"Samples: A-1, B-2"</c>).</summary>
    public Func<string, string> SamplesText { get; init; } = samples => $"Samples: {samples}";

    /// <summary>The accessible name of the file column list, given the field label (<c>"Column for Email"</c>).</summary>
    public Func<string, string> ColumnFor { get; init; } = label => $"Column for {label}";

    /// <summary>The choice that declares a field absent from the file (<c>"Not in the file"</c>).</summary>
    public string NotInFile { get; init; } = "Not in the file";

    /// <summary>The button that undoes a manual choice or an edit (<c>"Undo"</c>).</summary>
    public string Undo { get; init; } = "Undo";

    /// <summary>The list of file columns no field uses, given the list (<c>"Not imported: Notes, Id"</c>).</summary>
    public Func<string, string> NotImported { get; init; } = list => $"Not imported: {list}";


    // Values step
    /// <summary>The text shown when no value is unknown (<c>"Every value was found in the list"</c>).</summary>
    public string AllValuesFound { get; init; } = "Every value was found in the list";

    /// <summary>The number of rows that hold an unknown value, given the count (<c>"3 rows"</c>).</summary>
    public Func<int, string> RowCount { get; init; } = n => n == 1 ? "1 row" : $"{Grouped(n)} rows";

    /// <summary>The accessible name of the answer list, given the value (<c>"Answer for Gold"</c>).</summary>
    public Func<string, string> AnswerFor { get; init; } = value => $"Answer for {value}";

    /// <summary>The first choice of an unanswered question (<c>"Choose…"</c>).</summary>
    public string Choose { get; init; } = "Choose…";

    /// <summary>The choice that creates the value, given the value (<c>"Create \"Gold\""</c>).</summary>
    public Func<string, string> CreateValue { get; init; } = value => $"Create \"{value}\"";

    /// <summary>The choice that imports the rows with an empty cell (<c>"Leave empty"</c>).</summary>
    public string LeaveEmpty { get; init; } = "Leave empty";

    /// <summary>The choice that leaves out the rows that hold the value (<c>"Don't import these rows"</c>).</summary>
    public string SkipRows { get; init; } = "Don't import these rows";


    // Review step
    /// <summary>The label of the statistic that counts the rows to import (<c>"rows to import"</c>).</summary>
    public string RowsToImport { get; init; } = "rows to import";

    /// <summary>The label of the statistic that counts the rows with errors (<c>"rows with errors"</c>).</summary>
    public string RowsWithErrors { get; init; } = "rows with errors";

    /// <summary>The label of the statistic that counts the rows left out (<c>"rows left out"</c>).</summary>
    public string RowsLeftOut { get; init; } = "rows left out";

    /// <summary>The tab of all rows, given their count (<c>"All (120)"</c>).</summary>
    public Func<int, string> TabAll { get; init; } = n => $"All ({Grouped(n)})";

    /// <summary>The tab of the rows to import, given their count (<c>"To import (118)"</c>).</summary>
    public Func<int, string> TabImportable { get; init; } = n => $"To import ({Grouped(n)})";

    /// <summary>The tab of the invalid rows, given their count (<c>"Invalid (2)"</c>).</summary>
    public Func<int, string> TabInvalid { get; init; } = n => $"Invalid ({Grouped(n)})";

    /// <summary>The tab of the rows left out, given their count (<c>"Left out (0)"</c>).</summary>
    public Func<int, string> TabDiscarded { get; init; } = n => $"Left out ({Grouped(n)})";

    /// <summary>The accessible name of the severity filter (<c>"Severity"</c>).</summary>
    public string Severity { get; init; } = "Severity";

    /// <summary>The choice of the severity filter that shows every row (<c>"All severities"</c>).</summary>
    public string AllSeverities { get; init; } = "All severities";

    /// <summary>The choice of the severity filter that shows rows with errors (<c>"Errors"</c>).</summary>
    public string Errors { get; init; } = "Errors";

    /// <summary>The choice of the severity filter that shows rows with warnings (<c>"Warnings"</c>).</summary>
    public string Warnings { get; init; } = "Warnings";

    /// <summary>The accessible name of the issue filter (<c>"Issue"</c>).</summary>
    public string Issue { get; init; } = "Issue";

    /// <summary>The choice of the issue filter that shows every row (<c>"All issues"</c>).</summary>
    public string AllIssues { get; init; } = "All issues";

    /// <summary>The accessible name of the search box (<c>"Search rows"</c>).</summary>
    public string SearchRows { get; init; } = "Search rows";

    /// <summary>The placeholder of the search box (<c>"Search rows…"</c>).</summary>
    public string SearchPlaceholder { get; init; } = "Search rows…";

    /// <summary>The size of the selection, given the number of rows (<c>"3 selected"</c>).</summary>
    public Func<int, string> SelectedCount { get; init; } = n => $"{Grouped(n)} selected";

    /// <summary>The button that leaves the selected rows out (<c>"Leave out"</c>).</summary>
    public string LeaveOut { get; init; } = "Leave out";

    /// <summary>The button that brings the selected rows back (<c>"Restore"</c>).</summary>
    public string Restore { get; init; } = "Restore";

    /// <summary>The button that empties the selection (<c>"Clear selection"</c>).</summary>
    public string ClearSelection { get; init; } = "Clear selection";

    /// <summary>The accessible name of the checkbox that selects the rows of the page (<c>"Select the rows of this page"</c>).</summary>
    public string SelectPageRows { get; init; } = "Select the rows of this page";

    /// <summary>The heading of the row number column (<c>"Row"</c>).</summary>
    public string ColumnRow { get; init; } = "Row";

    /// <summary>The heading of the status column (<c>"Status"</c>).</summary>
    public string ColumnStatus { get; init; } = "Status";

    /// <summary>The accessible name of a row checkbox, given the row number (<c>"Select row 12"</c>).</summary>
    public Func<int, string> SelectRow { get; init; } = n => $"Select row {n}";

    /// <summary>The accessible name of the button that opens a row, given the row number (<c>"Inspect row 12"</c>).</summary>
    public Func<int, string> InspectRow { get; init; } = n => $"Inspect row {n}";

    /// <summary>The text of a cell with no value (<c>"empty"</c>).</summary>
    public string EmptyCell { get; init; } = "empty";

    /// <summary>The accessible name of the cell editor, given the field label (<c>"Edit Email"</c>).</summary>
    public Func<string, string> EditCell { get; init; } = label => $"Edit {label}";

    /// <summary>The tooltip of an edited cell, given the text in the file (<c>"Original: ada@exmaple.com"</c>).</summary>
    public Func<string, string> OriginalValue { get; init; } = text => $"Original: {text}";

    /// <summary>The status of a row without issues (<c>"Valid"</c>).</summary>
    public string StatusValid { get; init; } = "Valid";

    /// <summary>The status of a row with warnings only (<c>"Warning"</c>).</summary>
    public string StatusWarning { get; init; } = "Warning";

    /// <summary>The status of a row with errors (<c>"Blocking"</c>).</summary>
    public string StatusBlocking { get; init; } = "Blocking";

    /// <summary>The status of a row left out of the import (<c>"Discarded"</c>).</summary>
    public string StatusDiscarded { get; init; } = "Discarded";

    /// <summary>The status of a row the host accepted. It is final: the row can no longer be edited or left out (<c>"Imported"</c>).</summary>
    public string StatusImported { get; init; } = "Imported";

    /// <summary>The text of a review table with no row to show (<c>"No rows to show"</c>).</summary>
    public string NoRows { get; init; } = "No rows to show";

    /// <summary>The button that downloads the invalid rows, given their count (<c>"Download the 2 invalid rows"</c>).</summary>
    public Func<int, string> DownloadInvalidRows { get; init; } = n => n == 1 ? "Download the invalid row" : $"Download the {Grouped(n)} invalid rows";

    /// <summary>The error shown when the file could not be saved or shared (<c>"The file could not be downloaded"</c>). It never carries the text of the exception.</summary>
    public string DownloadFailed { get; init; } = "The file could not be downloaded";

    /// <summary>The error shown when the file the user chose could not be read from the device (<c>"The file could not be read"</c>). A file that is not a valid workbook has its own message, written by the library.</summary>
    public string FileReadFailed { get; init; } = "The file could not be read";

    /// <summary>The heading of the last column of the invalid rows download (<c>"Issues"</c>).</summary>
    public string IssuesColumn { get; init; } = "Issues";


    // Pager
    /// <summary>The accessible name of the pager (<c>"Pagination"</c>).</summary>
    public string Pagination { get; init; } = "Pagination";

    /// <summary>The accessible name of the first page button (<c>"First page"</c>).</summary>
    public string FirstPage { get; init; } = "First page";

    /// <summary>The accessible name of the previous page button (<c>"Previous page"</c>).</summary>
    public string PreviousPage { get; init; } = "Previous page";

    /// <summary>The accessible name of the next page button (<c>"Next page"</c>).</summary>
    public string NextPage { get; init; } = "Next page";

    /// <summary>The accessible name of the last page button (<c>"Last page"</c>).</summary>
    public string LastPage { get; init; } = "Last page";

    /// <summary>The page position, given the 1-based page and the page count (<c>"Page 2 of 3"</c>).</summary>
    public Func<int, int, string> PageOf { get; init; } = (page, count) => $"Page {Grouped(page)} of {Grouped(count)}";

    /// <summary>The rows on the page, given the first row, the last row and the total (<c>"Rows 51 to 100 of 120"</c>).</summary>
    public Func<int, int, int, string> RowsRange { get; init; } = (from, to, total) => $"Rows {Grouped(from)} to {Grouped(to)} of {Grouped(total)}";

    /// <summary>The label of the page size list (<c>"Rows per page"</c>).</summary>
    public string RowsPerPage { get; init; } = "Rows per page";


    // Row inspector
    /// <summary>The title of the inspector, given the row number (<c>"Row 12"</c>).</summary>
    public Func<int, string> InspectorRow { get; init; } = n => $"Row {n}";

    /// <summary>The accessible name of the button that goes to the previous row with issues (<c>"Previous row with issues"</c>).</summary>
    public string PreviousIssueRow { get; init; } = "Previous row with issues";

    /// <summary>The accessible name of the button that goes to the next row with issues (<c>"Next row with issues"</c>).</summary>
    public string NextIssueRow { get; init; } = "Next row with issues";

    /// <summary>The accessible name of the button that closes the inspector (<c>"Close inspector"</c>).</summary>
    public string CloseInspector { get; init; } = "Close inspector";

    /// <summary>The label of an issue that belongs to the whole row (<c>"Row"</c>).</summary>
    public string WholeRow { get; init; } = "Row";

    /// <summary>The button that brings the inspected row back (<c>"Restore this row"</c>).</summary>
    public string RestoreRow { get; init; } = "Restore this row";

    /// <summary>The button that leaves the inspected row out (<c>"Leave out this row"</c>).</summary>
    public string LeaveOutRow { get; init; } = "Leave out this row";


    // Import step
    /// <summary>The summary label of the rows to import (<c>"Rows to import"</c>).</summary>
    public string SummaryToImport { get; init; } = "Rows to import";

    /// <summary>The summary label of the invalid rows (<c>"Invalid, not imported"</c>).</summary>
    public string SummaryInvalid { get; init; } = "Invalid, not imported";

    /// <summary>The summary label of the rows left out (<c>"Left out"</c>).</summary>
    public string SummaryLeftOut { get; init; } = "Left out";

    /// <summary>The summary label of the rows with an edited cell (<c>"Cells edited"</c>).</summary>
    public string SummaryEdited { get; init; } = "Cells edited";

    /// <summary>The progress label of a running import (<c>"Importing…"</c>).</summary>
    public string Importing { get; init; } = "Importing…";

    /// <summary>The progress label of a finished import, given the number of rows accepted (<c>"120 rows imported"</c>).</summary>
    public Func<int, string> RowsImported { get; init; } = n => n == 1 ? "1 row imported" : $"{Grouped(n)} rows imported";

    /// <summary>The progress label of a cancelled import (<c>"The import was cancelled"</c>).</summary>
    public string ImportCancelled { get; init; } = "The import was cancelled";

    /// <summary>The progress label of a failed import (<c>"The import failed"</c>).</summary>
    public string ImportFailed { get; init; } = "The import failed";

    /// <summary>The progress count, given the rows sent and the rows to send (<c>"40 of 120"</c>).</summary>
    public Func<int, int, string> Progress { get; init; } = (done, total) => $"{Grouped(done)} of {Grouped(total)}";

    /// <summary>The note of a failed or cancelled import, given the number of rows already accepted (<c>"40 rows are already imported and will not be sent again."</c>).</summary>
    public Func<int, string> AlreadyImported { get; init; } = n => n == 1 ? "1 row is already imported and will not be sent again." : $"{Grouped(n)} rows are already imported and will not be sent again.";

    /// <summary>The count of rows the server refused, given the count (<c>"Rows refused: 2"</c>).</summary>
    public Func<int, string> RowsRefused { get; init; } = n => $"Rows refused: {Grouped(n)}";

    /// <summary>The button that sends the refused rows again (<c>"Send them again"</c>).</summary>
    public string SendAgain { get; init; } = "Send them again";

    /// <summary>The button that shows the refused rows (<c>"See them"</c>).</summary>
    public string SeeThem { get; init; } = "See them";


    // Checks that call your server
    /// <summary>The state of the checks while they run (<c>"Checking the rows…"</c>).</summary>
    public string CheckingRows { get; init; } = "Checking the rows…";

    /// <summary>The state of the checks once they finished (<c>"The rows were checked"</c>).</summary>
    public string RowsChecked { get; init; } = "The rows were checked";

    /// <summary>The state of the checks after a cancellation (<c>"The check was cancelled"</c>).</summary>
    public string CheckCancelled { get; init; } = "The check was cancelled";

    /// <summary>The state of the checks after a failure whose text is meant for the user (<see cref="Klyroo.Spreadsheet.SpreadsheetUserMessageException"/>), given that text (<c>"The check failed: ..."</c>).</summary>
    public Func<string, string> CheckFailed { get; init; } = message => $"The check failed: {message}";

    /// <summary>The state of the checks after any other failure (<c>"The check failed"</c>); the exception goes to <see cref="Klyroo.Spreadsheet.SpreadsheetImporter.Logger"/>.</summary>
    public string CheckFailedGeneric { get; init; } = "The check failed";

    /// <summary>The button that runs the checks again (<c>"Check again"</c>).</summary>
    public string CheckAgain { get; init; } = "Check again";


    // Live region
    /// <summary>What the polite live region announces on the review step, given the rows to import, the rows with errors and the rows left out (<c>"To import: 118. With errors: 2. Left out: 0."</c>).</summary>
    public Func<int, int, int, string> LiveReview { get; init; } = (importable, invalid, discarded) => $"To import: {Grouped(importable)}. With errors: {Grouped(invalid)}. Left out: {Grouped(discarded)}.";

    /// <summary>What the live region announces while importing, given the rows sent and the rows to send (<c>"Importing: 40 of 120 rows"</c>).</summary>
    public Func<int, int, string> LiveImporting { get; init; } = (done, total) => $"Importing: {Grouped(done)} of {Grouped(total)} rows";


    // Issue messages
    /// <summary>The message of the <c>required</c> issue (<c>"Required value is missing"</c>).</summary>
    public string IssueRequired { get; init; } = "Required value is missing";

    /// <summary>The message of the <c>number</c> issue, given the cell text (<c>"\"abc\" is not a number"</c>).</summary>
    public Func<string, string> IssueNumber { get; init; } = text => $"\"{text}\" is not a number";

    /// <summary>The message of the <c>boolean</c> issue, given the cell text (<c>"\"maybe\" is not yes or no"</c>).</summary>
    public Func<string, string> IssueBoolean { get; init; } = text => $"\"{text}\" is not yes or no";

    /// <summary>The message of the <c>date</c> issue, given the cell text (<c>"\"31/02/2026\" is not a date this column reads"</c>).</summary>
    public Func<string, string> IssueDate { get; init; } = text => $"\"{text}\" is not a date this column reads";

    /// <summary>The message of the <c>value.unknown</c> issue, given the cell text (<c>"\"Gold\" is not a known value · choose it in Values"</c>).</summary>
    public Func<string, string> IssueValueUnknown { get; init; } = text => $"\"{text}\" is not a known value · choose it in Values";

    /// <summary>The message of the <c>value.not-found</c> issue, given the cell text (<c>"\"Gold\" is not a known value"</c>).</summary>
    public Func<string, string> IssueValueNotFound { get; init; } = text => $"\"{text}\" is not a known value";

    /// <summary>The message of the <c>value.skipped</c> issue, given the cell text (<c>"Left out: \"Gold\" is set to skip its rows"</c>).</summary>
    public Func<string, string> IssueValueSkipped { get; init; } = text => $"Left out: \"{text}\" is set to skip its rows";

    /// <summary>The message of the <c>row.duplicate</c> issue, given the number of the other row (<c>"Same key as row 4"</c>).</summary>
    public Func<string, string> IssueRowDuplicate { get; init; } = number => $"Same key as row {number}";

    /// <summary>The message of the <c>row.exists</c> issue (<c>"Already stored"</c>).</summary>
    public string IssueRowExists { get; init; } = "Already stored";

    /// <summary>The message of the <c>row.failed</c> issue, which the library writes when a callback of yours (the row factory, a property setter, a rule predicate or <c>ValidateRow</c>) threw. It never contains the text of the exception, which goes to <see cref="Klyroo.Spreadsheet.SpreadsheetImporter.Logger"/> (<c>"The row could not be checked"</c>).</summary>
    public string IssueRowFailed { get; init; } = "The row could not be checked";

    /// <summary>The message of a <c>pattern</c> issue raised because the check timed out (<c>"The format could not be checked in time"</c>).</summary>
    public string IssuePatternTimeout { get; init; } = "The format could not be checked in time";

    /// <summary>The default message of <see cref="Klyroo.Spreadsheet.SpreadsheetRule.Email"/> (<c>"Not an email address"</c>).</summary>
    public string IssueEmail { get; init; } = "Not an email address";

    /// <summary>The default message of <see cref="Klyroo.Spreadsheet.SpreadsheetRule.Pattern"/> (<c>"Unexpected format"</c>).</summary>
    public string IssuePattern { get; init; } = "Unexpected format";

    /// <summary>The default message of <see cref="Klyroo.Spreadsheet.SpreadsheetRule.Min"/>, given the minimum (<c>"Value is below 0"</c>).</summary>
    public Func<string, string> IssueMin { get; init; } = min => $"Value is below {min}";

    /// <summary>The default message of <see cref="Klyroo.Spreadsheet.SpreadsheetRule.Max"/>, given the maximum (<c>"Value is above 100"</c>).</summary>
    public Func<string, string> IssueMax { get; init; } = max => $"Value is above {max}";

    /// <summary>The default message of <see cref="Klyroo.Spreadsheet.SpreadsheetRule.Between"/>, given the minimum and the maximum (<c>"Value is not between 0 and 100"</c>).</summary>
    public Func<string, string, string> IssueBetween { get; init; } = (min, max) => $"Value is not between {min} and {max}";

    /// <summary>The message of an issue in the language of this set: the text for its <see cref="SpreadsheetRowIssue.Code"/> when the library wrote the message, or <see cref="SpreadsheetRowIssue.Message"/> as it is when you did.</summary>
    /// <param name="issue">The issue of a row.</param>
    /// <returns>The text to show.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="issue"/> is <see langword="null"/>.</exception>
    public string ResolveIssue(SpreadsheetRowIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        if (issue.Args is not { } args) return issue.Message;
        string Arg(int index) => index < args.Count ? args[index] : "";
        return issue.Code switch
        {
            "required" => IssueRequired,
            "number" => IssueNumber(Arg(0)),
            "boolean" => IssueBoolean(Arg(0)),
            "date" => IssueDate(Arg(0)),
            "value.unknown" => IssueValueUnknown(Arg(0)),
            "value.not-found" => IssueValueNotFound(Arg(0)),
            "value.skipped" => IssueValueSkipped(Arg(0)),
            "row.duplicate" => IssueRowDuplicate(Arg(0)),
            "row.exists" => IssueRowExists,
            "row.failed" => IssueRowFailed,
            "pattern" => Arg(0) == "timeout" ? IssuePatternTimeout : IssuePattern,
            "email" => IssueEmail,
            "min" => IssueMin(Arg(0)),
            "max" => IssueMax(Arg(0)),
            "between" => IssueBetween(Arg(0), Arg(1)),
            _ => issue.Message,
        };
    }

    /// <summary>The title of a wizard step in the step bar.</summary>
    /// <param name="stepKey">One of <c>file</c>, <c>columns</c>, <c>values</c>, <c>review</c> or <c>submit</c> (<see cref="SpreadsheetStepStatus.Key"/>).</param>
    /// <param name="fallback">The text to return for another key.</param>
    /// <returns>The title.</returns>
    public string StepTitle(string stepKey, string fallback = "") => stepKey switch
    {
        "file" => StepFile,
        "columns" => StepColumns,
        "values" => StepValues,
        "review" => StepReview,
        "submit" => StepSubmit,
        _ => fallback,
    };

    /// <summary>The text of a row status.</summary>
    /// <param name="status">The status of a row.</param>
    /// <returns>The text.</returns>
    public string StatusText(SpreadsheetRowStatus status) => status switch
    {
        SpreadsheetRowStatus.Valid => StatusValid,
        SpreadsheetRowStatus.Warning => StatusWarning,
        SpreadsheetRowStatus.Blocking => StatusBlocking,
        _ => StatusDiscarded,
    };

    private static string Grouped(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
