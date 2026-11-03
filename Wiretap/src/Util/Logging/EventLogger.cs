using System;
using System.Collections.Generic;
using System.Diagnostics;
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

public sealed class ZeroLogger(ActivityScope scope) : EventLogger(scope, ActivityEvent.Zero)
{
    protected override (string Name, object? Value)[]? Log(LogLevel logLevel)
    {
        // core: After last, report only that the activity has already ended.
        if (Scope.History.Contains(ActivityEvent.Last))
        {
            return [("fixme", new[] { "no-events-must-follow-last" })];
        }

        // core: Otherwise, attach the duplicate-zero diagnostic to this zero.
        if (Scope.History.Contains(ActivityEvent.Zero))
        {
            return [("fixme", new[] { "zero-occurs-more-than-once" })];
        }

        return [];
    }
}

public sealed class TickLogger(ActivityScope scope) : EventLogger(scope, ActivityEvent.Tick)
{
    protected override (string Name, object? Value)[]? Log(LogLevel logLevel)
    {
        // core: Ticks are unrestricted until the activity has ended.
        if (Scope.History.Contains(ActivityEvent.Last))
        {
            return [("fixme", new[] { "no-events-must-follow-last" })];
        }

        return [];
    }
}

public sealed class LastLogger(ActivityScope scope, bool once = false) : EventLogger(scope, ActivityEvent.Last)
{
    public LastLogger Once => once ? this : new LastLogger(Scope, once: true);

    protected override (string Name, object? Value)[]? Log(LogLevel logLevel)
    {
        if (Scope.History.Contains(ActivityEvent.Last))
        {
            // core: Scope closure does not add another last if one already occurred.
            if (once)
            {
                return null;
            }

            // core: A repeated explicit last preserves the first last's duration and status.
            return
            [
                ("activity.duration_ms", (long)Scope.Activity.Duration.TotalMilliseconds),
                ("fixme", new[] { "no-events-must-follow-last" })
            ];
        }

        // core: Only the first last reports omissions and captures the activity's duration.
        var aboutMe = new List<string>();
        if (!Scope.History.Contains(ActivityEvent.Zero))
        {
            aboutMe.Add("does-not-log-zero");
        }

        if (once)
        {
            aboutMe.Add("logs-last-silently");
        }

        Scope.Activity.SetEndTime(DateTime.UtcNow);
        switch (logLevel)
        {
            case LogLevel.None:
                // core: Leave the status as-is.
                break;
            case LogLevel.Trace:
            case LogLevel.Debug:
            case LogLevel.Information:
            case LogLevel.Warning:
                Scope.Activity.SetStatus(ActivityStatusCode.Ok);
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                Scope.Activity.SetStatus(ActivityStatusCode.Error);
                break;
        }

        return
        [
            ("activity.duration_ms", (long)Scope.Activity.Duration.TotalMilliseconds),
            ("aboutme", aboutMe.ToArray())
        ];
    }
}