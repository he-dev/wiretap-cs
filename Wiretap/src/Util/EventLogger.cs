using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Wiretap.Core;
using Wiretap.Core.Logging;

namespace Wiretap.Util;

public abstract class EventLogger(ActivityLogger logger) : ILogger
{
    protected ActivityLogger Logger { get; } = logger;
    protected Activity Activity => Logger.Activity;
    protected List<ActivityEvent> History => Logger.History;
    protected abstract ActivityEvent Event { get; }

    // core: Null skips the closing request; an empty array still represents an event to log.
    protected abstract (string Name, object? Value)[]? Log(LogLevel logLevel);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => Logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => Logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (Log(logLevel) is not { } properties)
        {
            return;
        }

        // core: Record accepted events even when their log level is filtered out.
        History.Add(Event);

        if (!IsEnabled(logLevel))
        {
            return;
        }

        using var scope = Logger.BeginScopeWith
        ([
            ("activity.event", Event),
            .. properties
        ]);

        Logger.Log(logLevel, eventId, state, exception, formatter);
    }
}

public sealed class ZeroLogger(ActivityLogger logger) : EventLogger(logger)
{
    protected override ActivityEvent Event => ActivityEvent.Zero;

    protected override (string Name, object? Value)[]? Log(LogLevel logLevel)
    {
        // core: After last, report only that the activity has already ended.
        if (History.Contains(ActivityEvent.Last))
        {
            return [("fixme", new[] { "no-events-must-follow-last" })];
        }

        // core: Otherwise, attach the duplicate-zero diagnostic to this zero.
        if (History.Contains(ActivityEvent.Zero))
        {
            return [("fixme", new[] { "zero-occurs-more-than-once" })];
        }

        return [];
    }
}

public sealed class TickLogger(ActivityLogger logger) : EventLogger(logger)
{
    protected override ActivityEvent Event => ActivityEvent.Tick;

    protected override (string Name, object? Value)[]? Log(LogLevel logLevel)
    {
        // core: Ticks are unrestricted until the activity has ended.
        if (History.Contains(ActivityEvent.Last))
        {
            return [("fixme", new[] { "no-events-must-follow-last" })];
        }

        return [];
    }
}

public sealed class LastLogger(ActivityLogger logger) : EventLogger(logger)
{
    protected override ActivityEvent Event => ActivityEvent.Last;

    protected override (string Name, object? Value)[]? Log(LogLevel logLevel)
    {
        if (History.Contains(ActivityEvent.Last))
        {
            // core: Scope closure does not add another last if one already occurred.
            if (!Logger.InProgress)
            {
                return null;
            }

            // core: A repeated explicit last preserves the first last's duration and status.
            return [
                ("activity.duration_ms", (long)Activity.Duration.TotalMilliseconds),
                ("fixme", new[] { "no-events-must-follow-last" })
            ];
        }

        // core: Only the first last reports omissions and captures the activity's duration.
        var aboutMe = new List<string>();
        if (!History.Contains(ActivityEvent.Zero))
        {
            aboutMe.Add("does-not-log-zero");
        }
        if (!Logger.InProgress)
        {
            aboutMe.Add("does-not-log-last");
        }

        Activity.SetEndTime(DateTime.UtcNow);
        switch (logLevel)
        {
            case LogLevel.None:
                // core: Leave the status as-is.
                break;
            case LogLevel.Trace:
            case LogLevel.Debug:
            case LogLevel.Information:
            case LogLevel.Warning:
                Activity.SetStatus(ActivityStatusCode.Ok);
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                Activity.SetStatus(ActivityStatusCode.Error);
                break;
        }

        return [
            ("activity.duration_ms", (long)Activity.Duration.TotalMilliseconds),
            ("aboutme", aboutMe.ToArray())
        ];
    }
}
