using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Wiretap.Util;
using ActivityEvent = Wiretap.Util.ActivityEvent;

namespace Wiretap.Core.Logging;

public class ActivityLogger
(
    ILogger logger,
    Activity activity,
    IDisposable? loggerScope
) : ActivityScope
{
    protected override ILogger Logger => logger;

    public override ILogger Zero => new ZeroLogger(this);
    public override ILogger Tick => new TickLogger(this);
    public override ILogger Last => new LastLogger(this);

    internal Activity Activity { get; } = activity;
    internal List<ActivityEvent> History { get; } = [];
    internal bool InProgress { get; private set; } = true;

    private IDisposable? LoggerScope { get; } = loggerScope;

    public static ActivityLogger About
    (
        ILogger logger,
        string about,
        (string Name, object? Value)[]? properties = null,
        Action<Activity>? configure = null
    )
    {
        var activity = new Activity(about);
        configure?.Invoke(activity);
        activity.Start();

        var scope = logger.BeginScopeWith
        ([
            ("activity.about", about),
            // core: Apply tick-event by default; explicit events are validated independently of trace activation.
            ("activity.event", ActivityEvent.Tick),
            .. properties ?? []
        ]);
        return new ActivityLogger(logger, activity, scope);
    }

    public override IDisposable? BeginScope<TState>(TState state) => Logger.BeginScope(state);

    public override bool IsEnabled(LogLevel logLevel) => Logger.IsEnabled(logLevel);

    public override void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            var parentId = Activity.ParentSpanId == default ? null : Activity.ParentSpanId.ToHexString();
            using var traceScope = Logger.BeginScopeWith
            (
                ("trace.id", Activity.TraceId.ToHexString()),
                ("trace.span_id", Activity.SpanId.ToHexString()),
                ("trace.parent_id", parentId)
            );

            Logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }

    public override void Dispose()
    {
        // core: Mark scope closure before requesting last; the event skips redundant closure.
        InProgress = false;
        Last.System.LogWarning(null);

        Activity.Dispose();
        LoggerScope?.Dispose();
    }
}
