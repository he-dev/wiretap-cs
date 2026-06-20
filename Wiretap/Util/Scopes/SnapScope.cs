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

    public override void MessageParts(PropertyName root, GetStateItem get, PushMessagePart push)
    {
        base.MessageParts(root, get, push);
        push(root.Activity.DurationMs, "Duration: N/A");
    }

    private void Log(ActivityStatus<TActivity> status)
    {
        WarnIfCustomStatusName(status);
        var properties = GetStateItems.From(
            this,
            new ActivityDurationFeed.Zero(),
            Activity,
            status
        );

        using (logger.BeginScope(properties))
        {
            var template = ComposeMessage.From(properties, this, Activity, status);
            logger.Log(status.Level, status.Exception, template.Template, template.Args);
        }

        ActivityCast.Stop(isOk: status switch
        {
            ActivityStatus<TActivity>.Okay => true,
            ActivityStatus<TActivity>.Fail => false,
            _ => null
        });
    }
}
