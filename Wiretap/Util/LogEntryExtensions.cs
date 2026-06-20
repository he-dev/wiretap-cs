using Microsoft.Extensions.Logging;

namespace Wiretap.Util;

internal static class LogEntryExtensions
{
    public static void LogEntry(this ILogger logger, LogEntry entry)
    {
        using var scope = entry.Properties.Count == 0 ? null : logger.BeginScope(entry.Properties);
        logger.Log(entry.Level, entry.Exception, entry.Message.Template, entry.Message.Args);
    }
}
