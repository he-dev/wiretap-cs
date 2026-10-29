using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace Aion.Meta.Logging.Wiretap;

public enum Event
{
    Zero,
    Tick,
    Last,
}

public interface IPropertyLogger : ILogger
{
    public ILogger Outer { get; }
}

public class StoryLogger(ILogger logger, string story) : IPropertyLogger
{
    public ILogger Outer => logger;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            using (logger.BeginScopeWith(("story", story)))
            {
                logger.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}

public class EventLogger(ILogger logger, Event activityEvent) : IPropertyLogger
{
    public ILogger Outer => logger;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // core: Advance the lifecycle even when this log level is filtered out.
        var fixme = this.ActivityLogger?.Push(activityEvent, logLevel);

        if (IsEnabled(logLevel))
        {
            using (fixme is null ? null : logger.BeginScopeWith(("fixme", new[] { fixme })))
            using (logger.BeginScopeWith(("activity.event", activityEvent)))
            {
                logger.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}

public interface IPropertyScope : ILogger, IDisposable;

public class PropertyScope(ILogger logger, KeyValuePair<string, object?>[] properties) : IPropertyScope
{
    private IDisposable? Scope { get; } = logger.BeginScope(properties);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }

    public void Dispose() => Scope?.Dispose();
}

public interface IActivityLogger : ILogger, IDisposable
{
    string? Push(Event next, LogLevel logLevel);
}

public class ActivityLogger(string about, (string Name, object? Value)[] properties, Action<System.Diagnostics.Activity> configure, ILogger outer, ActivityTraceId? traceId = null, ActivitySpanId? parentId = null) : IActivityLogger
{
    private System.Diagnostics.Activity Activity { get; } = CreateActivity(about, configure, traceId, parentId);

    private IDisposable? LoggerScope { get; } = outer.BeginScopeWith
    ([
        ("activity.about", about),
        // core: Apply tick-event by default.
        ("activity.event", Event.Tick),
        .. properties
    ]);

    private Event? RecentEvent { get; set; }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => outer.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => outer.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            var durationScope =
                RecentEvent is Event.Last
                    ? outer.BeginScopeWith(("activity.duration_ms", (long)Activity.Duration.TotalMilliseconds))
                    : null;

            // core: Read IDs after Zero starts the activity, keeping them available to every log provider.
            using (Activity.Id is null ? null : outer.BeginScopeWith(
                ("trace.id", Activity.TraceId.ToHexString()),
                ("trace.span_id", Activity.SpanId.ToHexString()),
                ("trace.parent_id", Activity.ParentSpanId == default ? null : Activity.ParentSpanId.ToHexString())))
            using (durationScope)
            {
                outer.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }

    public string? Push(Event next, LogLevel logLevel)
    {
        switch (next)
        {
            case Event.Zero when RecentEvent is null:
                RecentEvent = next;
                Activity.Start();
                return null;
            case Event.Tick when RecentEvent is Event.Zero or Event.Tick:
                RecentEvent = next;
                return null;
            case Event.Last when RecentEvent is Event.Zero or Event.Tick:
                RecentEvent = next;
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
                        Activity.SetStatus(System.Diagnostics.ActivityStatusCode.Ok);
                        break;
                    case LogLevel.Error:
                    case LogLevel.Critical:
                        Activity.SetStatus(System.Diagnostics.ActivityStatusCode.Error);
                        break;
                }

                return null;
            default:
                // core: Preserve the original log entry and explain the invalid transition without changing state.
                return next switch
                {
                    Event.Zero => "zero-must-occur-only-once",
                    Event.Tick => "tick-must-follow-zero-or-tick",
                    Event.Last when RecentEvent is null => "last-must-follow-zero-or-tick",
                    Event.Last => "last-must-occur-only-once",
                    _ => "event-must-be-zero-tick-or-last",
                };
        }
    }

    public void Dispose()
    {
        try
        {
            // core: Emit a missing Last normally; invalid transitions carry fixme instead of being repaired.
            if (RecentEvent is not Event.Last)
            {
                this.System.Last.LogInformation(null);
            }
        }
        finally
        {
            Activity.Dispose();
            LoggerScope?.Dispose();
        }
    }

    private static System.Diagnostics.Activity CreateActivity(string name, Action<System.Diagnostics.Activity> configure, ActivityTraceId? traceId, ActivitySpanId? parentId)
    {
        if (parentId.HasValue && !traceId.HasValue)
        {
            throw new ArgumentException("An external parent span ID requires its trace ID.", nameof(parentId));
        }

        var activity = new System.Diagnostics.Activity(name);
        if (traceId.HasValue)
        {
            // core: Explicit incoming context takes precedence over Activity.Current.
            activity.SetIdFormat(ActivityIdFormat.W3C);
            activity.SetParentId(traceId.Value, parentId.GetValueOrDefault());
        }
        configure(activity);
        return activity;
    }
}

public static class LoggerExtensions
{
    extension(ILogger logger)
    {
        // util: The built-in BeginScope does not support anonymous objects, thus this helper.
        public IDisposable? BeginScopeFrom<T>(T state) where T : notnull
        {
            var properties =
                from p in state.GetType().GetProperties()
                select new KeyValuePair<string, object?>(p.Name, p.GetValue(state));

            return logger.BeginScope(properties);
        }

        public IPropertyScope BeginScopeWith(params (string Name, object? Value)[] properties)
        {
            return new PropertyScope(logger, [.. properties.Select(p => new KeyValuePair<string, object?>(p.Name, p.Value))]);
        }

        public IActivityLogger BeginScopeAbout
        (
            string about,
            (string Name, object? Value)[]? properties = null,
            Action<System.Diagnostics.Activity>? configure = null,
            ActivityTraceId? traceId = null,
            ActivitySpanId? parentId = null
        )
        {
            return new ActivityLogger(about, properties ?? [], configure ?? (_ => { }), logger, traceId, parentId);
        }

        public IActivityLogger BeginScopeAbout<TAbout>
        (
            (string Name, object? Value)[]? properties = null,
            Action<System.Diagnostics.Activity>? configure = null,
            ActivityTraceId? traceId = null,
            ActivitySpanId? parentId = null
        )
        {
            return logger.BeginScopeAbout(typeof(TAbout).Name, properties, configure, traceId, parentId);
        }

        //public IPropertyLogger With(LoggerProperty property) => new PropertyLogger(logger, property);

        public IPropertyLogger Domain => new StoryLogger(logger, "Domain");
        public IPropertyLogger System => new StoryLogger(logger, "System");
    }

    extension(IPropertyLogger propertyLogger)
    {
        // core: Locates the scene.
        public IActivityLogger? ActivityLogger
        {
            get
            {
                ILogger current = propertyLogger;
                while (current is IPropertyLogger decorator)
                {
                    current = decorator.Outer;
                }

                return current as IActivityLogger;
            }
        }

        public IPropertyLogger Zero => new EventLogger(propertyLogger, Event.Zero);
        public IPropertyLogger Tick => new EventLogger(propertyLogger, Event.Tick);
        public IPropertyLogger Last => new EventLogger(propertyLogger, Event.Last);
    }
}