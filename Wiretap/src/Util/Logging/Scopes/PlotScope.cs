using System;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Wiretap.Core;
using Wiretap.Util.Logging.Events;

namespace Wiretap.Util.Logging.Scopes;

public sealed class PlotScope
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
    internal List<PlotEvent> History { get; } = [];

    private IDisposable? LoggerScope { get; } = loggerScope;
    private HashSet<string> Beeps { get; } = [];

    public void Beep(LogLevel logLevel, string errorCode, [StructuredMessageTemplate] string? message, params object?[] args)
    {
        if (IsEnabled(logLevel) && Beeps.Add(errorCode))
        {
            this
                .With
                (
                    ("plot.about", Activity.OperationName),
                    ("plot.event", PlotEvent.Beep),
                    ("plot.story", Story.Buzz.Value),
                    ("context.error_code", errorCode)
                )
                .Log(logLevel, message, args);
        }
    }

    public static PlotScope About
    (
        ILogger logger,
        string about,
        Story story,
        (string Name, object? Value)[]? properties = null,
        Action<Activity>? configure = null
    )
    {
        var activity = new Activity(about);
        configure?.Invoke(activity);
        activity.Start();

        var scope = logger.BeginScopeWith
        ([
            ("plot.about", about),
            ("plot.story", story.Value),
            // core: Apply tick-event by default; explicit events are validated independently of trace activation.
            ("plot.event", PlotEvent.Tick),
            .. properties ?? []
        ]);
        return new PlotScope(logger, activity, scope);
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);

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
        Last.Once.With(Story.Buzz).LogWarning(null);

        Activity.Dispose();
        LoggerScope?.Dispose();
    }
}
