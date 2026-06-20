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

    public override void LogProperties(PropertyName name, PushLogProperty push)
    {
        base.LogProperties(name, push);
        push(name.Activity.DurationMs, 0L);
    }

    private void Log(ActivityStatus<TActivity> status)
    {
        WarnIfCustomStatusName(status);
        logger.LogEntry(Variant.CreateLogEntryBy.From(this, status));

        TraceHandle.Stop(ok: status switch
        {
            ActivityStatus<TActivity>.Okay => true,
            ActivityStatus<TActivity>.Fail => false,
            _ => null
        });
    }
}
