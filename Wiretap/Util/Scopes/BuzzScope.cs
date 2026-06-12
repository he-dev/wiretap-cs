using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util.Buzz;

namespace Wiretap.Util.Scopes;

public class BuzzScope<TActivity>(
    ILogger logger,
    TActivity activity,
    StatusLogPolicy statusLogPolicy = StatusLogPolicy.Both,
    Action<ActivityStatus<TActivity>, TimeSpan>? onLastStatus = null
) : ActivityScope where TActivity : Activity.Buzz
{
    private ActivityWrapper ActivityWrapper { get; } = new(activity.Name);
    private (ActivityStatus<TActivity> Status, IMessagePartFeed? Suffix, TimeSpan Duration)? _lastStatus;
    private bool _disposed;
    protected TActivity Activity => activity;
    protected ILogger Logger => logger;
    public override string ActivityName => activity.Name;

    public void SetStatus(ActivityStatus<TActivity> status, [StructuredMessageTemplate] string? message = null, params object?[] args)
    {
        var suffix = new LastStatusMessageFeed(message, args);
        var duration = Stopwatch.Elapsed;

        if (_lastStatus is { } lastStatus)
        {
            var context = CreateContext(status, duration);
            var stateItems = GetStateItems.From(this, context, activity, status);

            using (logger.BeginScope(stateItems))
            {
                logger.LogWarning(
                    "{Activity} status changed from [{PreviousStatus}] to [{CurrentStatus}] before scope exit.",
                    activity.Name,
                    lastStatus.Status.Code,
                    status.Code
                );
            }
        }

        _lastStatus = (status, suffix, duration);
    }

    public override void MessageParts(ActivityStatus.Context context, PushMessagePart push)
    {
        base.MessageParts(context, push);
        push("Elapsed: {ElapsedMs:N0} ms", context.Duration.TotalMilliseconds);
    }

    private ActivityStatus.Context CreateContext(ActivityStatus<TActivity> status, TimeSpan duration)
    {
        return new()
        {
            Activity = activity.Name,
            ActivityStatus = status.Code,
            ActivityDepth = Depth,
            ActivityPath = Path,
            ParentActivity = Parent?.ActivityName,
            MessageRole = nameof(MessageRole.Data),
            Duration = duration
        };
    }

    private void LogStatus(ActivityStatus<TActivity> status, IMessagePartFeed? suffix = null, TimeSpan? duration = null)
    {
        if (status is ActivityStatusRole.ILast)
        {
            ActivityWrapper.Stop(isOk: status switch
            {
                ActivityStatus<TActivity>.Okay => true,
                ActivityStatus<TActivity>.Fail => false,
                _ => null
            });
        }

        var context = CreateContext(status, duration ?? Stopwatch.Elapsed);

        var stateItems = GetStateItems.From(this, context, activity, status);

        using (logger.BeginScope(stateItems))
        {
            var template = GetComposeMessage
                .FromAttributeOrDefault(activity.GetType())
                .From(context, this, activity as IMessagePartFeed, status as IMessagePartFeed, suffix);
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
            _lastStatus ??= (new ActivityStatus<TActivity>.Void(), null, Stopwatch.Elapsed);
            var lastStatus = _lastStatus!.Value;
            if (statusLogPolicy.HasFlag(StatusLogPolicy.Last))
            {
                LogStatus(lastStatus.Status, lastStatus.Suffix, lastStatus.Duration);
            }

            onLastStatus?.Invoke(lastStatus.Status, lastStatus.Duration);
        }
        finally
        {
            ActivityWrapper.Dispose();
            base.Dispose();
            _disposed = true;
        }
    }
}
