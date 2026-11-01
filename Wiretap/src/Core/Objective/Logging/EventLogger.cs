using System;
using Microsoft.Extensions.Logging;
using Wiretap.Util;

namespace Wiretap.Core.Logging;

public class EventLogger(ActivityLogger logger, ActivityEvent activityEvent, ActivityEventMode mode = ActivityEventMode.Regular) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // core: Advance the lifecycle even when this log level is lower than the current one.
        var fixme = logger.MoveNext(activityEvent, logLevel, mode);

        if (fixme is null)
        {
            return;
        }

        if (IsEnabled(logLevel))
        {
            using (fixme.Constraints.Length == 0 ? null : logger.BeginScopeWith(("fixme", fixme.Constraints)))
            using (logger.BeginScopeWith(("activity.event", activityEvent)))
            {
                logger.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}
