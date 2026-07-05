using Wiretap.Meta;

namespace Wiretap.Util.Scopes;

public sealed class BulkScope<TBulk, TItem>(ActivityLogger logger, activity) : BuzzScope<TBulk>(logger, activity)
    where TBulk : Activity.Bulk<TBulk, TItem>
    where TItem : Activity.Item
{
    public ItemScope<TItem> BeginItem(TItem item)
    {
        return new ItemScope<TItem>(logger, item).Also(x => x.Push());
    }
}

public sealed class ItemScope<TActivity>(ActivityLogger logger)
    : BuzzScope(logger) where TActivity : Activity<TActivity>.Item;