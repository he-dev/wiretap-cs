using System.Diagnostics;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util;
using Wiretap.Util.Skills;
using Activity = Wiretap.Util.Activity;

namespace Wiretap.Core;

public class ActivityScope<TActivity>(ILogger logger, TActivity activity) : IDisposable where TActivity : Activity
{
    private ActivityWrapper ActivityWrapper { get; } = new(activity.Name);

    private Stopwatch Stopwatch { get; } = Stopwatch.StartNew();

    private bool ContainsLastStatus { get; set; }

    public TimeSpan Elapsed => Stopwatch.Elapsed;

    public static ActivityScope<TActivity> Begin<T>(ILogger<T> logger, TActivity activity)
    {
        var activityScope = new ActivityScope<TActivity>(logger, activity);
        activityScope.LogStatus(new ActivityStatus.Auto<TActivity>.Zero());
        return activityScope;
    }

    public void LogStatus(ActivityStatus.Core<TActivity> status)
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
                LogStatus(new ActivityStatus.Auto<TActivity>.Leak(status));

                return;
            }

            throw new InvalidOperationException($"The code is trying to log '{status.Code}' as another last status for the '{activity.Name}' activity, but activities can have only one last status.");
        }

        // meta: Needs to cast so the right overload is called.
        LogStatus((ActivityStatus<TActivity>)status);
    }

    private void LogStatus(ActivityStatus<TActivity> status)
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

        // meta: Using a list rather than Enumerable.Concat for performance reasons.
        var stateItems = new List<KeyValuePair<string, object?>>(32);
        var addStateItem = new AddStateItem((key, value) => stateItems.Add(new(key, value)));

        ScopeStateItem.From(activity, addStateItem);
        ScopeStateItem.From(status, addStateItem);

        (context as IWithStateItems)?.StateItems(addStateItem);
        (activity as IWithStateItems)?.StateItems(addStateItem);
        (status as IWithStateItems)?.StateItems(addStateItem);

        using (logger.BeginScope(stateItems))
        {
            var template = activity.JoinMessageParts.From(context, activity.MessageTemplatePrefix, status as IWithMessageParts);
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