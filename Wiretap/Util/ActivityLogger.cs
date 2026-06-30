using Microsoft.Extensions.Logging;

namespace Wiretap.Util;

public sealed class ActivityLogger(ILogger logger)
{
    public void Log(
        LogLevel level,
        IReadOnlyDictionary<string, object?> details,
        MessageTemplate message,
        Exception? exception = null
    )
    {
        using var scope = details.Count == 0 ? null : logger.BeginScope(details);
        logger.Log(level, exception, message.Template, message.Args);
    }
}
