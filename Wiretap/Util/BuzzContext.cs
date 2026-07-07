namespace Wiretap.Util;

public interface IBuzzStatus<TBuzz> where TBuzz : Buzz { }

public sealed class BuzzContext<TBuzz>(TBuzz buzz) : IDisposable where TBuzz : Buzz
{
    public bool SetStatus(IBuzzStatus<TBuzz> status) => buzz.SetStatus(status);

    public void Dispose() => buzz.Dispose();
}

public sealed class BulkContext<TBulk, TItem>(TBulk bulk) : IDisposable
    where TBulk : Buzz.Bulk<TItem>
    where TItem : Buzz
{
    public BuzzContext<TItem> BeginItem(TItem item) => new(bulk.BeginItem(item));

    public bool SetStatus(BuzzStatus<TBulk> status) => bulk.SetStatus(status);

    public void Dispose() => bulk.Dispose();
}