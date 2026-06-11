using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util;
using Wiretap.Util.Services;

namespace Wiretap.Core;

public abstract class ActivityScope : IMessagePartFeed
{
    private static readonly AsyncLocal<ActivityScope?> CurrentScope = new();

    protected System.Diagnostics.Stopwatch Stopwatch { get; } = System.Diagnostics.Stopwatch.StartNew();

    protected TimeSpan Elapsed => Stopwatch.Elapsed;

    public static ActivityScope? Current => CurrentScope.Value;

    public ActivityScope? Parent { get; private set; }

    public int Depth => Parent?.Depth + 1 ?? 0;

    public string Path => Parent is null ? ActivityName : $"{Parent.Path}/{ActivityName}";

    public abstract string ActivityName { get; }

    public abstract string ActivityRole { get; }

    public virtual void MessageParts(ActivityStatus.Context context, PushMessagePart push)
    {
        push("{ActivityRole}: {Activity}[{ActivityStatus}]", context.ActivityRole, context.Activity, context.ActivityStatus);
    }

    protected IDisposable EnterScope()
    {
        var parent = CurrentScope.Value;
        Parent = parent;
        CurrentScope.Value = this;
        return new AmbientScope(this, parent);
    }

    private sealed class AmbientScope(ActivityScope scope, ActivityScope? parent) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CurrentScope.Value = parent;
            scope.Parent = null;
            _disposed = true;
        }
    }

    public static KeyValuePair<string, object?>[] CurrentItemTags()
    {
        if (Current is { } current)
        {
            return
            [
                new(nameof(ActivityStatus.Context.Activity), current.ActivityName),
                new(nameof(ActivityStatus.Context.ActivityRole), current.ActivityRole),
                new(nameof(ActivityStatus.Context.ActivityDepth), current.Depth),
                new(nameof(ActivityStatus.Context.ActivityPath), current.Path),
                new(nameof(ActivityStatus.Context.ParentActivity), current.Parent?.ActivityName),
                new(nameof(ActivityStatus.Context.ElapsedMs), (long)current.Elapsed.TotalMilliseconds),
            ];
        }

        return [];
    }
}

public class BuzzScope<TActivity>(ILogger logger, TActivity activity, Action<ActivityStatus<TActivity>, long>? onFinalStatus = null) : ActivityScope, IDisposable where TActivity : Activity.Buzz
{
    private ActivityWrapper ActivityWrapper { get; } = new(activity.Name);
    private BuzzBatch Batch { get; } = new();
    private (ActivityStatus<TActivity> Status, IMessagePartFeed? Suffix, long ElapsedMs)? _lastStatus;
    private IDisposable? _ambientScope;

    public override string ActivityName => activity.Name;

    public override string ActivityRole => activity.Role;

    public static BuzzScope<TActivity> BeginBuzz<T>(ILogger<T> logger, TActivity activity)
    {
        var activityScope = new BuzzScope<TActivity>(logger, activity);
        activityScope.Enter();
        activityScope.InitActivityWrapper();
        activityScope.LogStatus(new ActivityStatus<TActivity>.Ready());
        return activityScope;
    }

    public void SetStatus(ActivityStatus<TActivity> status, [StructuredMessageTemplate] string? message = null, params object?[] args)
    {
        var suffix = new MessageTemplateSuffix(message, args);
        var elapsedMs = (long)Stopwatch.Elapsed.TotalMilliseconds;

        if (_lastStatus is { } lastStatus)
        {
            var context = CreateContext(status, elapsedMs);
            var stateItems = GetStateItems.From(context, activity, status);

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

        _lastStatus = (status, suffix, elapsedMs);
    }

    public BuzzItemScope<TItemActivity> BeginItem<TItemActivity>(TItemActivity activity) where TItemActivity : Activity.Buzz
    {
        return BuzzItemScope<TItemActivity>.BeginItem(logger, activity, Batch);
    }

    public override void MessageParts(ActivityStatus.Context context, PushMessagePart push)
    {
        base.MessageParts(context, push);
        push("Elapsed: {ElapsedMs:N0} ms", context.ElapsedMs);
        Batch.MessageParts(context, push);
    }

    private ActivityStatus.Context CreateContext(ActivityStatus<TActivity> status, long elapsedMs)
    {
        return new()
        {
            Activity = activity.Name,
            ActivityStatus = status.Code,
            ActivityRole = activity.Role,
            ActivityDepth = Depth,
            ActivityPath = Path,
            ParentActivity = Parent?.ActivityName,
            MessageRole = nameof(MessageRole.Data),
            ElapsedMs = elapsedMs
        };
    }

    protected void LogStatus(ActivityStatus<TActivity> status, IMessagePartFeed? suffix = null, long? elapsedMs = null)
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

        var context = CreateContext(status, elapsedMs ?? (long)Stopwatch.Elapsed.TotalMilliseconds);

        var stateItems = GetStateItems.From(context, Batch, activity, status);

        using (logger.BeginScope(stateItems))
        {
            var template = MessageTemplateSchema
                .For(activity.GetType())
                .From(context, this, activity as IMessagePartFeed, status as IMessagePartFeed, suffix);
            logger.Log(status.Level, status.Exception, template.Template, template.Args);
        }
    }

    protected void InitActivityWrapper()
    {
        ActivityWrapper.AddTag("Activity", activity.Name);
        ActivityWrapper.AddTag("ActivityRole", activity.Role);
        ActivityWrapper.AddTag("ElapsedMs", new Func<long>(() => (long)Elapsed.TotalMilliseconds));
    }

    protected void Enter()
    {
        _ambientScope = EnterScope();
    }

    public void LogDebug([StructuredMessageTemplate] string? message, params object?[] args)
    {
        using (logger.BeginScope(CurrentItemTags()))
        {
            logger.LogDebug(message, args);
        }
    }

    public void LogTrace([StructuredMessageTemplate] string? message, params object?[] args)
    {
        using (logger.BeginScope(CurrentItemTags()))
        {
            logger.LogTrace(message, args);
        }
    }

    public void Dispose()
    {
        try
        {
            if (_lastStatus is { } lastStatus)
            {
                LogStatus(lastStatus.Status, lastStatus.Suffix, lastStatus.ElapsedMs);
                onFinalStatus?.Invoke(lastStatus.Status, lastStatus.ElapsedMs);
            }
            else
            {
                var status = new ActivityStatus<TActivity>.Void();
                var durationMs = (long)Stopwatch.Elapsed.TotalMilliseconds;
                LogStatus(status, elapsedMs: durationMs);
                onFinalStatus?.Invoke(status, durationMs);
            }
        }
        finally
        {
            ActivityWrapper.Dispose();
            _ambientScope?.Dispose();
        }
    }
}

public sealed class BuzzItemScope<TActivity> : BuzzScope<TActivity> where TActivity : Activity.Buzz
{
    private BuzzItemScope(ILogger logger, TActivity activity, BuzzBatch parentBatch) : base(logger, activity, parentBatch.Count) { }

    internal static BuzzItemScope<TActivity> BeginItem(ILogger logger, TActivity activity, BuzzBatch batch)
    {
        var activityScope = new BuzzItemScope<TActivity>(logger, activity, batch);
        activityScope.Enter();
        activityScope.InitActivityWrapper();
        activityScope.LogStatus(new ActivityStatus<TActivity>.Ready());
        return activityScope;
    }

}

public class SnapScope<TActivity>(ILogger logger, TActivity activity) : ActivityScope, IDisposable where TActivity : Activity.Snap
{
    private IDisposable? _ambientScope;

    public override string ActivityName => activity.Name;

    public override string ActivityRole => activity.Role;

    public override void MessageParts(ActivityStatus.Context context, PushMessagePart push)
    {
        base.MessageParts(context, push);
        push("Elapsed: N/A");
    }

    private ActivityStatus.Context CreateContext(ActivityStatus<TActivity> status)
    {
        return new()
        {
            Activity = activity.Name,
            ActivityStatus = status.Code,
            ActivityRole = activity.Role,
            ActivityDepth = Depth,
            ActivityPath = Path,
            ParentActivity = Parent?.ActivityName,
            MessageRole = nameof(MessageRole.Data),
            ElapsedMs = 0
        };
    }

    internal void Log(ActivityStatus<TActivity> status)
    {
        _ambientScope = EnterScope();
        var context = CreateContext(status);
        var stateItems = GetStateItems.From(context, activity, status);

        using (logger.BeginScope(stateItems))
        {
            var template = MessageTemplateSchema
                .For(activity.GetType())
                .From(context, this, activity as IMessagePartFeed, status as IMessagePartFeed);
            logger.Log(status.Level, status.Exception, template.Template, template.Args);
        }
    }

    public void Dispose()
    {
        _ambientScope?.Dispose();
    }
}
