using Microsoft.Extensions.Logging;
using Wiretap.Meta;

namespace Wiretap.Util.Scopes;

public sealed class BulkScope<TBulk, TItem>(ActivityLogger logger, TBulk activity) : BuzzScope<TBulk>(logger, activity)
    where TBulk : Activity.Bulk<TBulk, TItem>
    where TItem : Activity.Item
{
    public ItemScope<TItem> BeginItem(TItem item)
    {
        return new ItemScope<TItem>(logger, item, Activity.Math.Count, Activity.OmitStatus).Also(x => x.Push());
    }
}

public sealed class ItemScope<TActivity>
(
    ActivityLogger logger,
    TActivity activity,
    OnLastStatus<TActivity> onLast,
    OmitStatus omitStatus
) : BuzzScope<TActivity>(logger, activity, omitStatus, onLast) where TActivity : Activity.Item;
