using System;
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

    public override ILogger Zero => new EventLogger(this, ActivityEvent.Zero);
    public override ILogger Tick => new EventLogger(this, ActivityEvent.Tick);
    public override ILogger Last => new EventLogger(this, ActivityEvent.Last);
    private Activity Activity { get; } = activity;
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

    private ActivityEvent? RecentEvent { get; set; }

    public override IDisposable? BeginScope<TState>(TState state) => Logger.BeginScope(state);

    public override bool IsEnabled(LogLevel logLevel) => Logger.IsEnabled(logLevel);

    public override void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            using var durationScope =
                RecentEvent is ActivityEvent.Last
                    ? Logger.BeginScopeWith(("activity.duration_ms", (long)Activity.Duration.TotalMilliseconds))
                    : null;

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
                        Activity.SetStatus(ActivityStatusCode.Ok);
                        break;
                    case LogLevel.Error:
                    case LogLevel.Critical:
                        Activity.SetStatus(ActivityStatusCode.Error);
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