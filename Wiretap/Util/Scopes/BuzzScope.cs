using Microsoft.Extensions.Logging;
using Wiretap.Util.Buzz;

namespace Wiretap.Util.Scopes;

public delegate void CountStatus<TActivity>(ActivityStatus<TActivity> status, TimeSpan duration) where TActivity : Activity.Buzz;

public class BuzzScope<TActivity>
(
    ILogger logger,
    TActivity activity,
    StatusLogPolicy statusLogPolicy = StatusLogPolicy.Both,
    CountStatus<TActivity>? onLastStatus = null
) : ActivityScope<TActivity>(activity) where TActivity : Activity.Buzz
{
    private System.Diagnostics.Stopwatch Stopwatch { get; } = System.Diagnostics.Stopwatch.StartNew();
    private Snapshot? _lastStatus;
    private TimeSpan? _logDuration;
    private bool _disposed;

    protected ILogger Logger => logger;

    protected override string Role => "buzz";

    public void SetStatus(ActivityStatus<TActivity> status)
    {
        // note: Makes sure everyone uses the same value.
        var duration = Stopwatch.Elapsed;

        if (_lastStatus is { } lastStatus)
        {
            var name = Variant.CreateLogEntryBy.Root;
            var state = GetLogProperties.From(name, this, ActivityDurationSource.Freeze(duration), Activity, status);

            using (logger.BeginScope(state))
            {
                logger.LogWarning(
                    $"{name.Activity.Name:_} status changed from [{name.Activity.State.Append("status", "code", "old"):_}] to [{name.Activity.State.Append("status", "code", "new"):_}] before scope exit.",
                    Activity.Name,
                    lastStatus.Status.Code,
                    status.Code
                );
            }
        }

        _lastStatus = new(status, duration);
    }

    public override void LogProperties(PropertyName name, PushLogProperty push)
    {
        base.LogProperties(name, push);
        push(name.Activity.DurationMs, (long)(_logDuration ?? Stopwatch.Elapsed).TotalMilliseconds);
    }

    private void LogStatus(ActivityStatus<TActivity> status, TimeSpan? duration = null)
    {
        WarnIfCustomStatusName(status);
        _logDuration = duration ?? Stopwatch.Elapsed;
        try
        {
            logger.LogEntry(Variant.CreateLogEntryBy.From(this, status));
        }
        finally
        {
            _logDuration = null;
        }
    }

    internal override void Push()
    {
        base.Push();

        if (statusLogPolicy.HasFlag(StatusLogPolicy.First))
        {
            LogStatus(new ActivityStatus<TActivity>.Ready());
        }
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _lastStatus ??= new(new ActivityStatus<TActivity>.Void(), Stopwatch.Elapsed);
            TraceHandle.Stop(ok: _lastStatus.Status switch
            {
                ActivityStatus<TActivity>.Okay => true,
                ActivityStatus<TActivity>.Fail => false,
                _ => null
            });

            if (statusLogPolicy.HasFlag(StatusLogPolicy.Last))
            {
                LogStatus(_lastStatus.Status, _lastStatus.Duration);
            }

            onLastStatus?.Invoke(_lastStatus.Status, _lastStatus.Duration);
        }
        finally
        {
            base.Dispose();
            _disposed = true;
        }
    }

    private record Snapshot(ActivityStatus<TActivity> Status, TimeSpan Duration);
}
