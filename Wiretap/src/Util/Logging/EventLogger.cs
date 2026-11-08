using System;
using Microsoft.Extensions.Logging;
using Wiretap.Core;
using Wiretap.Util.Logging.Scopes;

namespace Wiretap.Util.Logging;

public abstract class EventLogger(ActivityScope scope, ActivityEvent activityEvent) : ILogger
{
    protected ActivityScope Scope { get; } = scope;

    // core: Null skips the closing request; an empty array still represents an event to log.
    protected abstract (string Name, object? Value)[]? Log(LogLevel logLevel);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => Scope.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => Scope.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (Log(logLevel) is { } properties)
        {
            // core: Record accepted events even when their log level is filtered out.
            Scope.History.Add(activityEvent);

            if (IsEnabled(logLevel))
            {
                Scope
                    .With([("activity.event", activityEvent), .. properties])
                    .Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}