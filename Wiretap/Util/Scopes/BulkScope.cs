using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util.Buzz;

namespace Wiretap.Util.Scopes;

public sealed class BulkScope<TActivity, TItem>(ILogger logger, TActivity activity) : BuzzScope<TActivity>(logger, activity)
    where TActivity : Activity.Bulk<TActivity, TItem>
    where TItem : Activity.Buzz
{
    private BulkMath Math { get; } = new();

    public override void StateItems(PushStateItem push)
    {
        base.StateItems(push);
        Math.StateItems(push);
    }

    public override void MessageParts(ActivityStatus.Context context, PushMessagePart push)
    {
        base.MessageParts(context, push);
        Math.MessageParts(context, push);
    }

    public ItemScope<TItem> BeginItem(TItem item)
    {
        return new ItemScope<TItem>(Logger, item, Math.Count, Activity.StatusLogPolicy).Also(x => x.Push());
    }
}

public sealed class ItemScope<TActivity>(ILogger logger, TActivity activity, Action<ActivityStatus<TActivity>, TimeSpan> count, StatusLogPolicy statusLogPolicy)
    : BuzzScope<TActivity>(logger, activity, statusLogPolicy, count) where TActivity : Activity.Buzz;
