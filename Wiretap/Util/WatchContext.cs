using Microsoft.Extensions.Logging;
using Wiretap.Meta;

namespace Wiretap.Util;

public class WatchContext<TWatch>(ILogger logger, TWatch watch) : IDisposable where TWatch : Watch
{
    protected WatchLogger Logger { get; } = new(logger, watch);

    public virtual TimeSpan LogEvent<TWatchEvent>(TWatchEvent watchEvent) where TWatchEvent : WatchEvent, IEventOf<TWatch>
    {
        return Logger.LogEvent(watchEvent);
    }

    public void Dispose()
    {
        Logger.Dispose();
        watch.Dispose();
    }

    public static class Builder
    {
        public static WatchContext<TWatch> Open(ILogger logger, TWatch watch)
        {
            return new WatchContext<TWatch>(logger, watch).Also(ctx => ctx.Logger.LogEvent(new WatchEvent.First.Open()));
        }
    }
}

public class BatchContext<TBatch>(ILogger logger, TBatch batch) : WatchContext<TBatch>(logger, batch) where TBatch : Batch
{
    private BatchMath Math { get; } = new();

    public CountContext<TCount> BeginWatch<TCount>(TCount watch) where TCount : Batch.Item, IItemOf<TBatch>
    {
        return CountContext<TCount>.Builder.Open(logger, watch, Math.Count);
    }

    public new static class Builder
    {
        public static BatchContext<TBatch> Open(ILogger logger, TBatch watch)
        {
            return new BatchContext<TBatch>(logger, watch).Also(ctx => ctx.Logger.LogEvent(new WatchEvent.First.Open()));
        }
    }
}

public class CountContext<TCount>(ILogger logger, TCount watch, Action<string, TimeSpan> count) : WatchContext<TCount>(logger, watch) where TCount : Watch
{
    public override TimeSpan LogEvent<TWatchEvent>(TWatchEvent watchEvent)
    {
        return Logger.LogEvent(watchEvent).Also(duration => count(watchEvent.ToString(), duration));
    }

    public new static class Builder
    {
        public static CountContext<TCount> Open(ILogger logger, TCount watch, Action<string, TimeSpan> count)
        {
            return new CountContext<TCount>(logger, watch, count).Also(ctx => ctx.Logger.LogEvent(new WatchEvent.First.Open()));
        }
    }
}