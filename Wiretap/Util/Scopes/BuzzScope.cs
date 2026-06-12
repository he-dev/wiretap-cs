using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Wiretap.Meta;
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
    private ActivityWrapper ActivityWrapper { get; } = new(activity.Name);
    private Snapshot? _lastStatus;
    private bool _disposed;

    protected ILogger Logger => logger;

    protected virtual string Role => "buzz";

    public void SetStatus(ActivityStatus<TActivity> status, [StructuredMessageTemplate] string? message = null, params object?[] args)
    {
        // note: Makes sure everyone uses the same value.
        var duration = Stopwatch.Elapsed;

        if (_lastStatus is { } lastStatus)
        {
            var state = GetStateItems.From(this, ActivityDurationFeed.Freeze(duration), activity, status);

            using (logger.BeginScope(state))
            {
                logger.LogWarning(
                    "{wiretap.activity.name} status changed from [{wiretap.activity.state.status.code.old}] to [{wiretap.activity.state.status.code.new}] before scope exit.",
                    activity.Name,
                    lastStatus.Status.Code,
                    status.Code
                );
            }
        }

        _lastStatus = new(status, new LastStatusMessageFeed(message, args), duration);
    }

    public override void MessageParts(IReadOnlyDictionary<string, object?> properties, PushMessagePart push)
    {
        base.MessageParts(properties, push);
        push("Duration: {wiretap.activity.duration_ms:N0} ms", properties["wiretap.activity.duration_ms"]);
    }

    public override void StateItems(PushStateItem push)
    {
        base.StateItems(push);

        push("wiretap.activity.role", Role);
    }

    private void LogStatus(ActivityStatus<TActivity> status, IMessagePartFeed? suffix = null, TimeSpan? duration = null)
    {
        duration ??= Stopwatch.Elapsed;
        var state = GetStateItems.From(this, ActivityDurationFeed.Freeze(duration.Value), activity, status);

        using (logger.BeginScope(state))
        {
            var template = ComposeMessage.From(state, this, activity, status, suffix);
            logger.Log(status.Level, status.Exception, template.Template, template.Args);
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
            _lastStatus ??= new(new ActivityStatus<TActivity>.Void(), null, Stopwatch.Elapsed);
            ActivityWrapper.Stop(isOk: _lastStatus.Status switch
            {
                ActivityStatus<TActivity>.Okay => true,
                ActivityStatus<TActivity>.Fail => false,
                _ => null
            });

            if (statusLogPolicy.HasFlag(StatusLogPolicy.Last))
            {
                LogStatus(_lastStatus.Status, _lastStatus.Message, _lastStatus.Duration);
            }

            onLastStatus?.Invoke(_lastStatus.Status, _lastStatus.Duration);
        }
        finally
        {
            ActivityWrapper.Dispose();
            base.Dispose();
            _disposed = true;
        }
    }

    private record Snapshot(ActivityStatus<TActivity> Status, IMessagePartFeed? Message, TimeSpan Duration);
}
