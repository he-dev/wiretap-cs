using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util.Buzz;

namespace Wiretap.Util.Scopes;

public sealed class BulkScope<TBulk, TItem>(ILogger logger, TBulk activity) : BuzzScope<TBulk>(logger, activity)
    where TBulk : Activity.Bulk<TBulk, TItem>
    where TItem : Activity.Item
{
    public override void LogProperties(PropertyName name, PushLogProperty push)
    {
        base.LogProperties(name, push);

        // TODO: Move framework property collection out of scopes with the CreateLogEntry migration.
        foreach (var (key, value) in GetLogProperties.From(name, Activity.Math))
        {
            push(key, value);
        }
    }

    public ItemScope<TItem> BeginItem(TItem item)
    {
        return new ItemScope<TItem>(Logger, item, Activity.Math.Count, Activity.OmitStatus).Also(x => x.Push());
    }
}

public sealed class ItemScope<TActivity>
(
    ILogger logger,
    TActivity activity,
    OnLastStatus<TActivity> onLast,
    OmitStatus omitStatus
) : BuzzScope<TActivity>(logger, activity, omitStatus, onLast) where TActivity : Activity.Item;
