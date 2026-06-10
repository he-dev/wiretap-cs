using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util;
using Wiretap.Util.Services;

namespace Wiretap.Core;

public abstract class ActivityScope
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

    protected void Push()
    {
        (Parent, CurrentScope.Value) = (CurrentScope.Value, this);
    }

    protected void Pop()
    {
        (CurrentScope.Value, Parent) = (Parent, null);
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
                new(nameof(ActivityStatus.Context.ActivityStatus), nameof(ActivityStatus.Auto<>.Busy)),
                new(nameof(ActivityStatus.Context.ElapsedMs), (long)current.Elapsed.TotalMilliseconds),
            ];
        }

        return [];
    }
}

public class ActivityScope<TActivity>(ILogger logger, TActivity activity) : ActivityScope, IDisposable where TActivity : Activity
{
    private ActivityWrapper ActivityWrapper { get; } = new(activity.Name);
    private (ActivityStatus.Core<TActivity> Status, IMessagePartFeed? Suffix, long ElapsedMs)? _lastStatus;

    public override string ActivityName => activity.Name;

    public override string ActivityRole => activity.Role;

    public static ActivityScope<TActivity> Begin<T>(ILogger<T> logger, TActivity activity)
    {
        var activityScope = new ActivityScope<TActivity>(logger, activity);
        activityScope.Push();
        activityScope.ActivityWrapper.AddTag("Activity", activity.Name);
        activityScope.ActivityWrapper.AddTag("ActivityRole", activity.Role);
        activityScope.ActivityWrapper.AddTag("ElapsedMs", new Func<long>(() => (long)activityScope.Elapsed.TotalMilliseconds));
        activityScope.LogStatus(new ActivityStatus.Auto<TActivity>.Ready((activity as IWithReadyStatus)?.ReadyStatusLevel));
        return activityScope;
    }

    public void SetStatus(ActivityStatus.Core<TActivity> status, [StructuredMessageTemplate] string? message = null, params object?[] args)
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

    private void LogStatus(ActivityStatus<TActivity> status, IMessagePartFeed? suffix = null, long? elapsedMs = null)
    {
        if (status is ActivityStatusRole.ILast)
        {
            ActivityWrapper.Stop(isOk: status switch
            {
                ActivityStatus.Core<TActivity>.Okay => true,
                ActivityStatus.Core<TActivity>.Fail => false,
                _ => null
            });
        }

        var context = CreateContext(status, elapsedMs ?? (long)Stopwatch.Elapsed.TotalMilliseconds);

        var stateItems = GetStateItems.From(context, activity, status);

        using (logger.BeginScope(stateItems))
        {
            var template = activity.MessageTemplateSchema.From(context, activity.MessageTemplatePrefix, activity as IMessagePartFeed, status as IMessagePartFeed, suffix);
            logger.Log(status.Level, status.Exception, template.Template, template.Args);
        }
    }

    public void LogDebug([StructuredMessageTemplate] string? message, params object?[] args)
    {
        LogStatus(new ActivityStatus.Auto<TActivity>.Busy.Debug(message, args));
    }

    public void LogTrace([StructuredMessageTemplate] string? message, params object?[] args)
    {
        LogStatus(new ActivityStatus.Auto<TActivity>.Busy.Trace(message, args));
    }

    public void Dispose()
    {
        try
        {
            if (_lastStatus is { } lastStatus)
            {
                LogStatus(lastStatus.Status, lastStatus.Suffix, lastStatus.ElapsedMs);
            }
            else
            {
                LogStatus(new ActivityStatus.Auto<TActivity>.Void.Warn());
            }
        }
        finally
        {
            ActivityWrapper.Dispose();
            Pop();
        }
    }
}
