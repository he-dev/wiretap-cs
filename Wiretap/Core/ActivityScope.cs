using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util;
using Wiretap.Util.Services;

namespace Wiretap.Core;

public abstract class ActivityScope
{
    protected System.Diagnostics.Stopwatch Stopwatch { get; } = System.Diagnostics.Stopwatch.StartNew();

    protected bool ContainsLastStatus { get; set; }

    protected TimeSpan Elapsed => Stopwatch.Elapsed;

    public static KeyValuePair<string, object?>[] CurrentItemTags()
    {
        if (System.Diagnostics.Activity.Current is { } current)
        {
            var activity = current.GetTagItem("Activity") as string;
            var activityRole = current.GetTagItem("ActivityRole") as string;
            var elapsedMs = current.GetTagItem("ElapsedMs") as Func<long>;
            return
            [
                new(nameof(ActivityStatus.Context.Activity), activity),
                new(nameof(ActivityStatus.Context.ActivityRole), activityRole),
                new(nameof(ActivityStatus.Context.ActivityStatus), nameof(ActivityStatus.Auto<>.Busy)),
                new(nameof(ActivityStatus.Context.ElapsedMs), elapsedMs?.Invoke()),
            ];
        }

        return [];
    }
}

public class ActivityScope<TActivity>(ILogger logger, TActivity activity) : ActivityScope, IDisposable where TActivity : Activity
{
    private ActivityWrapper ActivityWrapper { get; } = new(activity.Name);

    public static ActivityScope<TActivity> Begin<T>(ILogger<T> logger, TActivity activity)
    {
        var activityScope = new ActivityScope<TActivity>(logger, activity);
        activityScope.ActivityWrapper.AddTag("Activity", activity.Name);
        activityScope.ActivityWrapper.AddTag("ActivityRole", activity.Role);
        activityScope.ActivityWrapper.AddTag("ElapsedMs", new Func<long>(() => (long)activityScope.Elapsed.TotalMilliseconds));
        activityScope.LogStatus(new ActivityStatus.Auto<TActivity>.Zero((activity as IWithZeroStatus)?.ZeroStatusLevel));
        return activityScope;
    }

    public void LogStatus(ActivityStatus.Core<TActivity> status, [StructuredMessageTemplate] string? message = null, params object?[] args)
    {
        // core: Mute leaks except fails.
        if (status is ActivityStatusRole.ILast && ContainsLastStatus && status is not ActivityStatus.Core<TActivity>.Fail)
        {
            if (activity.LastStatusPolicy.MuteLeaks is { } muteLeaks)
            {
                if (muteLeaks.Silently)
                {
                    // core: Do not log anything.
                    return;
                }

                // core: Log the leak.
                LogStatus(new ActivityStatus.Auto<TActivity>.Leak(status), new MessageTemplateSuffix(message, args));

                return;
            }

            throw new InvalidOperationException($"The code is trying to log '{status.Code}' as another last status for the '{activity.Name}' activity, but activities can have only one last status.");
        }

        // meta: Needs to cast so the right overload is called.
        LogStatus((ActivityStatus<TActivity>)status, new MessageTemplateSuffix(message, args));
    }

    private void LogStatus(ActivityStatus<TActivity> status, IWithMessageParts? suffix = null)
    {
        if (status is ActivityStatusRole.ILast)
        {
            ContainsLastStatus = true;
            ActivityWrapper.Stop(isOk: status switch
            {
                ActivityStatus.Core<TActivity>.Okay => true,
                ActivityStatus.Core<TActivity>.Fail => false,
                _ => null
            });
        }

        var context = new ActivityStatus.Context
        {
            Activity = activity.Name,
            ActivityStatus = status.Code,
            ActivityRole = activity.Role,
            MessageRole = nameof(MessageRole.Data),
            ElapsedMs = (long)Stopwatch.Elapsed.TotalMilliseconds
        };

        var stateItems = GetStateItems.From(context, activity, status);

        using (logger.BeginScope(stateItems))
        {
            var template = activity.MessageTemplateSchema.From(context, activity.MessageTemplatePrefix, activity as IWithMessageParts, status as IWithMessageParts, suffix);
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
        if (!ContainsLastStatus)
        {
            if (activity.LastStatusPolicy.CanBeVoid is null)
            {
                LogStatus(new ActivityStatus.Auto<TActivity>.Void.Warn());
            }
            else
            {
                LogStatus(new ActivityStatus.Auto<TActivity>.Void.Info());
            }
        }

        ActivityWrapper.Dispose();
    }
}