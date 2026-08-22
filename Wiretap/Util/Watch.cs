using System.Collections;
using System.Diagnostics;
using Wiretap.Meta;
using Wiretap.Util.Buzz;
using Wiretap.Util.Data;

namespace Wiretap.Util;

public abstract class Watch : IEnumerable<Watch>, IDisposable
{
    protected Watch(string? name = null)
    {
        Name = name ?? GetActivityName.For(GetType());
        TraceHandle = Configuration.Default.TraceContext.Start(Name);
        Pop = AmbientContext<Watch>.Push(this);
    }

    private IDisposable Pop { get; }

    private Stopwatch Stopwatch { get; } = Stopwatch.StartNew();

    private string Name { get; }

    public ITraceHandle TraceHandle { get; }

    public virtual string[] Tags { get; } = [];

    public TimeSpan Elapsed => Stopwatch.Elapsed;

    public virtual TimeSpan Freeze(WatchEvent watchEvent)
    {
        switch (watchEvent)
        {
            case WatchEvent.Last.Okay:
                TraceHandle.Stop(ok: true);
                break;
            case WatchEvent.Last.Fail:
                TraceHandle.Stop(ok: false);
                break;
        }

        return Elapsed;
    }

    public virtual bool CanLogEvent(WatchEvent watchEvent) => true;

    public override string ToString() => Name;

    public IEnumerator<Watch> GetEnumerator()
    {
        if (AmbientContext<Watch>.Current is IEnumerable<AmbientContext<Watch>> context)
        {
            foreach (var item in context)
            {
                yield return item.Value;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        TraceHandle.Dispose();
        Pop.Dispose();
    }
}

public class Batch(string? name = null) : Watch(name)
{
    private BatchMath Math { get; } = new();

    [StateItem(Name = "bulk.item_count")]
    [MessagePart(Label = "Item Count")]
    public int ItemCount => Math.ItemCount;

    [StateItem(Name = "bulk.duration_s")]
    [MessagePart(Label = "Item Duration", Format = "N3", QuoteMode = QuoteMode.Never)]
    public double DurationS => Math.DurationMs / 1000.0;

    [StateItem(Name = "bulk.throughput_s")]
    [MessagePart(Label = "Throughput", Format = "N1", QuoteMode = QuoteMode.Never)]
    public double ThroughputS => Math.ThroughputMs * 1000.0;

    public class Item(string? name = null) : Watch(name)
    {
        public override bool CanLogEvent(WatchEvent watchEvent) => false;
    }
}