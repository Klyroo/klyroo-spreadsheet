using Microsoft.Extensions.Logging;

namespace Klyroo.Spreadsheet;

internal static partial class SpreadsheetComponentLog
{
    [LoggerMessage(EventId = 11, Level = LogLevel.Warning, Message = "KlySpreadsheetImport: the file the user chose could not be read. The user sees a generic message.")]
    public static partial void FileReadFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 12, Level = LogLevel.Warning, Message = "KlySpreadsheetImport: the file could not be downloaded. The user sees a generic message.")]
    public static partial void DownloadFailed(ILogger logger, Exception exception);
}
