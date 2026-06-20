using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util.Buzz;

namespace Wiretap.Util.Scopes;

public sealed class BulkScope<TBulk, TItem>(ILogger logger, TBulk activity) : BuzzScope<TBulk>(logger, activity)
    where TBulk : Activity.Bulk<TBulk, TItem>
    where TItem : Activity.Buzz
{
    private BulkMath Math { get; } = new();

    protected override string Role => "bulk";

    public override void LogProperties(PropertyName name, PushLogProperty push)
    {
        base.LogProperties(name, push);

        foreach (var (key, value) in GetLogProperties.From(name, Math))
        {
            push(key, value);
        }
    }

    public ItemScope<TItem> BeginItem(TItem item)
    {
        return new ItemScope<TItem>(Logger, item, Math.Count, Activity.StatusLogPolicy).Also(x => x.Push());
    }
}

public sealed class ItemScope<TActivity>
(
    ILogger logger,
    TActivity activity,
    CountStatus<TActivity> count,
    StatusLogPolicy statusLogPolicy
) : BuzzScope<TActivity>(logger, activity, statusLogPolicy, count) where TActivity : Activity.Buzz
{
    protected override string Role => "item";
}
