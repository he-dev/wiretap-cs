using System;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Wiretap.Core;
using Wiretap.Util.Logging.Events;

namespace Wiretap.Util.Logging.Scopes;

public sealed class ActivityScope
(
    ILogger logger,
    Activity activity,
    IDisposable? loggerScope
) : ILogger, IDisposable
{
    public ZeroLogger Zero => new ZeroLogger(this);
    public TickLogger Tick => new TickLogger(this);
    public LastLogger Last => new LastLogger(this);

    internal Activity Activity { get; } = activity;
    internal List<ActivityEvent> History { get; } = [];

    private IDisposable? LoggerScope { get; } = loggerScope;
    private HashSet<string> Beeps { get; } = [];

    public void Beep(LogLevel logLevel, string beepCode, [StructuredMessageTemplate] string? message, params object?[] args)
    {
        if (IsEnabled(logLevel) && Beeps.Add(beepCode))
        {
            this
                .With
                (
                    ("activity.about", Activity.OperationName),
                    ("activity.event", ActivityEvent.Beep),
                    ("beep_code", beepCode)
                )
                .Internal
                .Log(logLevel, message, args);
        }
    }

    public static ActivityScope About
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
        return new ActivityScope(logger, activity, scope);
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return logger.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            var parentId = Activity.ParentSpanId == default ? null : Activity.ParentSpanId.ToHexString();
            using var traceScope = logger.BeginScopeWith
            (
                ("trace.id", Activity.TraceId.ToHexString()),
                ("trace.span_id", Activity.SpanId.ToHexString()),
                ("trace.parent_id", parentId)
            );

            logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }

    public void Dispose()
    {
        Last.WhenFirstOnly.Internal.LogWarning(null);

        Activity.Dispose();
        LoggerScope?.Dispose();
    }
}
