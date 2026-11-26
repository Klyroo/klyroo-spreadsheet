using Microsoft.Extensions.Logging;

namespace Klyroo.Spreadsheet;

internal static partial class SpreadsheetLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Spreadsheet import: {Source} threw on {Count} row(s) in this pass (stopped for the rest of the read: {Stopped}). Each of those rows got a row.failed issue with a generic message. The first exception follows.")]
    public static partial void HostCallbackFailed(ILogger logger, string source, int count, bool stopped, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Spreadsheet import: the submit callback threw. The submit stopped, the rows accepted before stay accepted, and the uploader sees a generic message.")]
    public static partial void SubmitFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Spreadsheet import: an async check callback threw. The checks stopped, and the uploader sees a generic message.")]
    public static partial void CheckFailed(ILogger logger, Exception exception);
}
