using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util.Buzz;

namespace Wiretap.Util.Scopes;

public class SnapScope<TActivity>(ILogger logger, TActivity activity) : ActivityScope where TActivity : Activity.Snap
{
    public override string ActivityName => activity.Name;

    internal static void Log(ILogger logger, TActivity activity, ActivityStatus<TActivity> status)
    {
        using var scope = new SnapScope<TActivity>(logger, activity).Also(x => x.Push());
        scope.Log(status);
    }

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
            ActivityDepth = Depth,
            ActivityPath = Path,
            ParentActivity = Parent?.ActivityName,
            MessageRole = nameof(MessageRole.Data),
            Duration = TimeSpan.Zero
        };
    }

    private void Log(ActivityStatus<TActivity> status)
    {
        var context = CreateContext(status);
        var stateItems = GetStateItems.From(this, context, activity, status);

        using (logger.BeginScope(stateItems))
        {
            var template = GetComposeMessage
                .FromAttributeOrDefault(activity.GetType())
                .From(context, this, activity as IMessagePartFeed, status as IMessagePartFeed);
            logger.Log(status.Level, status.Exception, template.Template, template.Args);
        }
    }
}
