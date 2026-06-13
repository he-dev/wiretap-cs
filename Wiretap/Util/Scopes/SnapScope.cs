using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util.Buzz;

namespace Wiretap.Util.Scopes;

public class SnapScope<TActivity>(ILogger logger, TActivity activity) : ActivityScope<TActivity>(activity) where TActivity : Activity.Snap
{
    protected override string Role => "snap";

    internal static void Log(ILogger logger, TActivity activity, ActivityStatus<TActivity> status)
    {
        using var scope = new SnapScope<TActivity>(logger, activity).Also(x => x.Push());
        scope.Log(status);
    }

    public override void MessageParts(IReadOnlyDictionary<string, object?> properties, ItemFeed<PushMessagePart> feed)
    {
        base.MessageParts(properties, feed);
        feed((name, next) => next("Duration: N/A"));
    }

    private void Log(ActivityStatus<TActivity> status)
    {
        var properties = GetStateItems.From(
            this,
            new ActivityDurationFeed.Zero(),
            activity,
            status
        );

        using (logger.BeginScope(properties))
        {
            var template = ComposeMessage.From(properties, this, activity, status);
            logger.Log(status.Level, status.Exception, template.Template, template.Args);
        }

        ActivityWrapper.Stop(isOk: status switch
        {
            ActivityStatus<TActivity>.Okay => true,
            ActivityStatus<TActivity>.Fail => false,
            _ => null
        });
    }
}
