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
    private Snapshot? _lastStatus;
    private bool _disposed;

    protected ILogger Logger => logger;

    protected override string Role => "buzz";

    public void SetStatus(ActivityStatus<TActivity> status, [StructuredMessageTemplate] string? message = null, params object?[] args)
    {
        // note: Makes sure everyone uses the same value.
        var duration = Stopwatch.Elapsed;

        if (_lastStatus is { } lastStatus)
        {
            var state = GetStateItems.From(this, ActivityDurationFeed.Freeze(duration), activity, status);
            var name = Configuration.Current.PropertyName;

            using (logger.BeginScope(state))
            {
                logger.LogWarning(
                    $"{name.Activity.Name:_} status changed from [{name.Activity.State.Append("status", "code", "old"):_}] to [{name.Activity.State.Append("status", "code", "new"):_}] before scope exit.",
                    activity.Name,
                    lastStatus.Status.Code,
                    status.Code
                );
            }
        }

        _lastStatus = new(status, new LastStatusMessageFeed(message, args), duration);
    }

    public override void MessageParts(PropertyName root, GetStateItem get, PushMessagePart push)
    {
        base.MessageParts(root, get, push);
        push($"Duration: {root.Activity.DurationMs:N0} ms", get(root.Activity.DurationMs));
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
            ActivityCast.Stop(isOk: _lastStatus.Status switch
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
            base.Dispose();
            _disposed = true;
        }
    }

    private record Snapshot(ActivityStatus<TActivity> Status, IMessagePartFeed? Message, TimeSpan Duration);
}

public class LastStatusMessageFeed([StructuredMessageTemplate] string? message, params object?[] args) : IMessagePartFeed
{
    public void MessageParts(PropertyName root, GetStateItem get, PushMessagePart push)
    {
        push(message, args);
    }
}
