using Wiretap.Meta;

namespace Wiretap.Util;

public sealed class BuzzContext<TBuzz>(TBuzz buzz) : IDisposable where TBuzz : Buzz
{
    public void SetStatus<TStatus>(TStatus status)
        where TStatus : BuzzStatus, IStatusOf<TBuzz>
    {
        buzz.SetStatus(status);
    }

    public void Dispose() => buzz.Dispose();
}

public sealed class BulkContext<TBulk>(TBulk bulk) : IDisposable
    where TBulk : Buzz.Bulk
{
    public BuzzContext<TItem> BeginItem<TItem>(TItem item)
        where TItem : Buzz, IItemOf<TBulk>
    {
        item.Subscribe(bulk);
        item.SetStatus(new BuzzStatus.First.Ready());
        return new(item);
    }

    public void SetStatus<TStatus>(TStatus status)
        where TStatus : BuzzStatus, IStatusOf<TBulk>
    {
        bulk.SetStatus(status);
    }

    public void Dispose() => bulk.Dispose();
}
