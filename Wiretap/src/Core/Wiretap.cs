using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace Aion.Meta.Logging.Wiretap;

public sealed record Fixme(string Constraint);

public enum ActivityEvent
{
    Zero,
    Tick,
    Last,
}

public enum EnvelopeMode
{
    Auto,
    ZeroOnly,
    LastOnly,
}


public class StoryLogger(ILogger logger, string story) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // core: Forward filtered calls too so an underlying event logger can advance its lifecycle.
        using var storyScope = IsEnabled(logLevel) ? logger.BeginScopeWith(("story", story)) : null;
        logger.Log(logLevel, eventId, state, exception, formatter);
    }
}

public class EventLogger(ActivityLogger logger, ActivityEvent activityEvent) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // core: Advance the lifecycle even when this log level is lower than the current one.
        var fixme = logger.MoveNext(activityEvent, logLevel);

        if (IsEnabled(logLevel))
        {
            using (fixme is null ? null : logger.BeginScopeWith(("fixme", new[] { fixme.Constraint })))
            using (logger.BeginScopeWith(("activity.event", activityEvent)))
            {
                logger.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}

public class PropertyScope(ILogger logger, KeyValuePair<string, object?>[] properties) : ILogger, IDisposable
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

public abstract class ActivityScope : ILogger, IDisposable
{
    protected abstract ILogger Outer { get; }
    public abstract ILogger Zero { get; }
    public abstract ILogger Tick { get; }
    public abstract ILogger Last { get; }

    public virtual IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => Outer.BeginScope(state);

    public virtual bool IsEnabled(LogLevel logLevel)
        => Outer.IsEnabled(logLevel);

    public virtual void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Outer.Log(logLevel, eventId, state, exception, formatter);

    public abstract void Dispose();
}
public class ActivityLogger
(
    ILogger outer,
    System.Diagnostics.Activity activity,
    IDisposable? loggerScope
) : ActivityScope
{
    protected override ILogger Outer => outer;
    public override ILogger Zero => new EventLogger(this, ActivityEvent.Zero);
    public override ILogger Tick => new EventLogger(this, ActivityEvent.Tick);
    public override ILogger Last => new EventLogger(this, ActivityEvent.Last);
    private System.Diagnostics.Activity Activity { get; } = activity;
    private IDisposable? LoggerScope { get; } = loggerScope;

    public static ActivityLogger About
    (
        ILogger logger,
        string about,
        (string Name, object? Value)[]? properties = null,
        Action<System.Diagnostics.Activity>? configure = null
    )
    {
        var activity = new System.Diagnostics.Activity(about);
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

    private ActivityEvent? RecentEvent { get; set; }

    public override IDisposable? BeginScope<TState>(TState state) => outer.BeginScope(state);

    public override bool IsEnabled(LogLevel logLevel) => outer.IsEnabled(logLevel);

    public override void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            using var durationScope =
                RecentEvent is ActivityEvent.Last
                    ? outer.BeginScopeWith(("activity.duration_ms", (long)Activity.Duration.TotalMilliseconds))
                    : null;

            var parentId = Activity.ParentSpanId == default ? null : Activity.ParentSpanId.ToHexString();
            using var traceScope = outer.BeginScopeWith
            (
                ("trace.id", Activity.TraceId.ToHexString()),
                ("trace.span_id", Activity.SpanId.ToHexString()),
                ("trace.parent_id", parentId)
            );

            outer.Log(logLevel, eventId, state, exception, formatter);
        }
    }

    internal Fixme? MoveNext(ActivityEvent next, LogLevel logLevel)
    {
        switch (next)
        {
            case ActivityEvent.Zero when RecentEvent is null:
                RecentEvent = next;
                return null;
            case ActivityEvent.Tick when RecentEvent is ActivityEvent.Zero or ActivityEvent.Tick:
                RecentEvent = next;
                return null;
            case ActivityEvent.Last when RecentEvent is ActivityEvent.Zero or ActivityEvent.Tick:
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
                    ActivityEvent.Zero => new Fixme("zero-must-occur-only-once"),
                    ActivityEvent.Tick => new Fixme("tick-must-follow-zero-or-tick"),
                    ActivityEvent.Last when RecentEvent is null => new Fixme("last-must-follow-zero-or-tick"),
                    ActivityEvent.Last => new Fixme("last-must-occur-only-once"),
                    // note: This is to make the compiler happy.
                    _ => throw new ArgumentOutOfRangeException(nameof(next), next, null)
                };
        }
    }

    public override void Dispose()
    {
        // core: Log a warning-level Last if the activity was not explicitly finalized.
        if (RecentEvent is not ActivityEvent.Last)
        {
            Last.System.LogWarning(null);
        }

        Activity.Dispose();
        LoggerScope?.Dispose();
    }
}

public sealed class EnvelopeLogger : ActivityScope
{
    protected override ActivityScope Outer { get; }
    public override ILogger Zero => Outer.Zero;
    public override ILogger Tick => Outer.Tick;
    public override ILogger Last => Outer.Last;

    private EnvelopeMode Mode { get; }

    public EnvelopeLogger(ActivityScope outer, EnvelopeMode mode = EnvelopeMode.Auto)
    {
        Outer = outer;
        Mode = mode;

        if (mode is EnvelopeMode.Auto or EnvelopeMode.ZeroOnly)
        {
            outer.Zero.System.LogDebug(null);
        }
    }

    public override void Dispose()
    {
        try
        {
            if (Mode is EnvelopeMode.Auto or EnvelopeMode.LastOnly)
            {
                Outer.Last.System.LogInformation(null);
            }
        }
        finally
        {
            // core: The envelope owns the activity scope supplied to its constructor.
            Outer.Dispose();
        }
    }
}

/// <summary>Establishes incoming trace context independently of activity event logging.</summary>
internal sealed class ContinuationScope(System.Diagnostics.Activity activity, IDisposable? scope) : IDisposable
{
    private System.Diagnostics.Activity Activity { get; } = activity;
    private IDisposable? Scope { get; } = scope;

    public void Dispose()
    {
        Scope?.Dispose();
        Activity.Dispose();
    }

    public static ContinuationScope About
    (
        ILogger logger,
        string about,
        string traceId,
        string? parentId = null,
        Action<System.Diagnostics.Activity>? configure = null
    )
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(traceId);

        var activity = CreateActivity(about, traceId, parentId, configure);

        // core: Ordinary activity scopes inherit this context through Activity.Current.
        var scope = logger.BeginScopeWith
        (
            ("activity.about", about),
            ("activity.event", ActivityEvent.Tick),
            ("trace.id", activity.TraceId.ToHexString()),
            ("trace.span_id", activity.SpanId.ToHexString()),
            ("trace.parent_id", activity.ParentSpanId == default ? null : activity.ParentSpanId.ToHexString())
        );
        return new ContinuationScope(activity, scope);
    }

    private static System.Diagnostics.Activity CreateActivity
    (
        string name,
        string traceId,
        string? parentId,
        Action<System.Diagnostics.Activity>? configure = null
    )
    {
        var activity = new System.Diagnostics.Activity(name);
        var parsedTraceId = ActivityTraceId.CreateFromString(traceId.AsSpan());
        var parsedParentId =
            parentId is null
                ? default
                : ActivitySpanId.CreateFromString(parentId.AsSpan());

        // core: Explicit incoming context takes precedence over Activity.Current.
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.SetParentId(parsedTraceId, parsedParentId);

        configure?.Invoke(activity);
        activity.Start();
        return activity;
    }
}

public static class LoggerExtensions
{
    extension(ILogger logger)
    {
        public PropertyScope BeginScopeWith(params (string Name, object? Value)[] properties)
        {
            return new PropertyScope(logger, [.. properties.Select(p => new KeyValuePair<string, object?>(p.Name, p.Value))]);
        }

        public ActivityScope BeginScopeAbout
        (
            string about,
            (string Name, object? Value)[]? properties = null,
            Action<System.Diagnostics.Activity>? configure = null
        )
        {
            return ActivityLogger.About(logger, about, properties, configure);
        }

        public ActivityScope BeginScopeAbout<TAbout>
        (
            (string Name, object? Value)[]? properties = null,
            Action<System.Diagnostics.Activity>? configure = null
        )
        {
            return logger.BeginScopeAbout(typeof(TAbout).Name, properties, configure);
        }

        public IDisposable ContinueScopeAbout
        (
            string about,
            string traceId,
            string? parentId = null,
            Action<System.Diagnostics.Activity>? configure = null
        )
        {
            return ContinuationScope.About(logger, about, traceId, parentId, configure);
        }

        public ILogger Domain => new StoryLogger(logger, "Domain");
        public ILogger System => new StoryLogger(logger, "System");
    }

    extension(ActivityScope logger)
    {
        public ActivityScope LogEnvelope(EnvelopeMode mode = EnvelopeMode.Auto) => new EnvelopeLogger(logger, mode);
    }
}