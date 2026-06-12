using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util.Buzz;

namespace Wiretap.Util.Scopes;

public class SnapScope<TActivity>(ILogger logger, TActivity activity) : ActivityScope<TActivity>(activity) where TActivity : Activity.Snap
{
    internal static void Log(ILogger logger, TActivity activity, ActivityStatus<TActivity> status)
    {
        using var scope = new SnapScope<TActivity>(logger, activity).Also(x => x.Push());
        scope.Log(status);
    }

    public override void MessageParts(IReadOnlyDictionary<string, object?> properties, PushMessagePart push)
    {
        base.MessageParts(properties, push);
        push("Duration: N/A");
    }

    public override void StateItems(PushStateItem push)
    {
        base.StateItems(push);

        push("wiretap.activity.role", "snap");
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
    }
}
