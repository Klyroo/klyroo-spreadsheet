using Microsoft.Extensions.Logging;

namespace Klyroo.Spreadsheet;

internal static partial class SpreadsheetLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Spreadsheet import: {Source} threw on {Count} row(s) in this pass (stopped for the rest of the read: {Stopped}). Each of those rows got a row.failed issue with a generic message. The first exception follows.")]
    public static partial void HostCallbackFailed(ILogger logger, string source, int count, bool stopped, Exception exception);
}
