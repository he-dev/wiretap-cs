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
    private ILogger LastFallback => new EventLogger(this, ActivityEvent.Last, ActivityEventMode.Fallback);

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

    internal Fixme? MoveNext(ActivityEvent next, LogLevel logLevel, ActivityEventMode mode)
    {
        switch (next, RecentEvent, mode)
        {
            // core: This is the very first zero, in either mode.
            case (ActivityEvent.Zero, null, ActivityEventMode.Regular or ActivityEventMode.Fallback):
                RecentEvent = next;
                return new Fixme([]);

            // core: This zero follows an existing event instead of starting the activity.
            case (ActivityEvent.Zero, ActivityEvent.Zero or ActivityEvent.Tick or ActivityEvent.Last, ActivityEventMode.Regular or ActivityEventMode.Fallback):
                RecentEvent = next;
                return new Fixme(["zero-must-occur-only-once"]);

            // core: This tick follows its sequence, in either mode.
            case (ActivityEvent.Tick, ActivityEvent.Zero or ActivityEvent.Tick, ActivityEventMode.Regular or ActivityEventMode.Fallback):
                RecentEvent = next;
                return new Fixme([]);

            // core: This tick has no preceding event or follows last, so it does not follow its sequence.
            case (ActivityEvent.Tick, null or ActivityEvent.Last, ActivityEventMode.Regular or ActivityEventMode.Fallback):
                RecentEvent = next;
                return new Fixme(["tick-must-follow-zero-or-tick"]);

            // core: This is the fallback-last, but there is one already. Ignore.
            case (ActivityEvent.Last, ActivityEvent.Last, ActivityEventMode.Fallback):
                return null;

            // core: No last was logged explicitly. Emit the fallback-last with a fixme.
            case (ActivityEvent.Last, null or ActivityEvent.Zero or ActivityEvent.Tick, ActivityEventMode.Fallback):
                Complete(logLevel);
                RecentEvent = next;
                return new Fixme(["last-must-be-explicit"]);

            // core: This explicit last follows its sequence.
            case (ActivityEvent.Last, ActivityEvent.Zero or ActivityEvent.Tick, ActivityEventMode.Regular):
                Complete(logLevel);
                RecentEvent = next;
                return new Fixme([]);

            // core: This is another explicit last.
            case (ActivityEvent.Last, ActivityEvent.Last, ActivityEventMode.Regular):
                Complete(logLevel);
                RecentEvent = next;
                return new Fixme(["last-must-occur-only-once"]);

            // core: This explicit last has no preceding zero or tick.
            case (ActivityEvent.Last, null, ActivityEventMode.Regular):
                Complete(logLevel);
                RecentEvent = next;
                return new Fixme(["last-must-follow-zero-or-tick"]);

            // core: An event, state, or mode combination is missing from the cases above.
            case var _:
                throw new InvalidOperationException($"Unhandled activity transition: next={next}, recent={RecentEvent}, mode={mode}");
        }
    }

    private void Complete(LogLevel logLevel)
    {
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
    }

    public override void Dispose()
    {
        // core: Request a fallback-last; the lifecycle skips it if last already occurred.
        LastFallback.System.LogWarning(null);

        Activity.Dispose();
        LoggerScope?.Dispose();
    }
}
