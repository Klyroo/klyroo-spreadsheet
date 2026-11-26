namespace Klyroo.Spreadsheet;

/// <summary>
/// Throw it from a callback of the import (the submit, an async check) when the text of the exception is meant for the uploader: <see cref="SpreadsheetImporter.SubmitError"/>
/// or <see cref="SpreadsheetImporter.ValidationError"/> then holds <see cref="Exception.Message"/> as it is, and the component shows it. Any other exception is logged
/// and the uploader sees a generic, localized text, because the message of an exception can hold a path or a connection string. To refuse some rows with a message, prefer
/// the typed way, <see cref="SpreadsheetRejectedRow"/> returned by the submit callback, or <see cref="SpreadsheetIndexedIssue"/> returned by <c>ValidateRowsAsync</c>; this
/// exception is for a failure of the whole call, such as a closed import window.
/// </summary>
public sealed class SpreadsheetUserMessageException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">The text shown to the uploader as it is. Write it for the uploader, in their language.</param>
    public SpreadsheetUserMessageException(string message) : base(message)
    {
    }
}
