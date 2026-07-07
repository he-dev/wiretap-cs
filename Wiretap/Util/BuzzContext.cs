using Wiretap.Meta;

namespace Wiretap.Util;

public sealed class BuzzContext<TBuzz>(TBuzz buzz) : IDisposable where TBuzz : Buzz
{
    public bool SetStatus<TStatus>(TStatus status)
        where TStatus : Status, IAssociatedWith<TBuzz>
    {
        return buzz.SetStatus(status);
    }

    public void Dispose() => buzz.Dispose();
}

public sealed class BulkContext<TItem>(Buzz.Bulk<TItem> bulk) : IDisposable
    where TItem : Buzz
{
    public BuzzContext<TItem> BeginItem(TItem item)
    {
        item.Subscribe(bulk);
        item.SetStatus(new Status.Ready());
        return new(item);
    }

    public bool SetStatus<TStatus>(TStatus status)
        where TStatus : Status, IAssociatedWith<TItem>
    {
        return bulk.SetStatus(status);
    }

    public void Dispose() => bulk.Dispose();
}
