using System.Collections;
using System.Diagnostics;
using Wiretap.Meta;
using Wiretap.Util.Buzz2;
using Wiretap.Util.Data;

namespace Wiretap.Util;

public interface IObserver
{
    void OnBuzzChange(Buzz buzz);
}

public interface IObservable
{
    void Subscribe(IObserver observer);
}

internal static class Observer
{
    public class Noop : IObserver
    {
        public void OnBuzzChange(Buzz buzz) { }
    }
}

public interface IBuzzMeta
{
    string Name { get; }
}

public class Buzz : IEnumerable<Buzz>, IObservable, IDisposable
{
    public Buzz(string? name = null)
    {
        Name = name ?? GetActivityName.For(GetType());
        TraceHandle = Configuration.Default.TraceContext.Start(Name);
        Pop = AmbientContext<Buzz>.Push(this);
    }

    private IDisposable Pop { get; }

    private Stopwatch Stopwatch { get; } = new();

    protected IObserver Subscriber { get; set; } = new Observer.Noop();

    public ITraceHandle TraceHandle { get; }

    public string Name { get; }

    public virtual string[] Tags { get; } = [];

    public TimeSpan Elapsed => Stopwatch.Elapsed;

    public string Path => string.Join("/", this.Reverse().Select(x => x.Name));

    public BuzzStatus Status { get; private set; } = new BuzzStatus.Idle.Pending();

    public TimeSpan Duration { get; private set; } = TimeSpan.Zero;

    public void Subscribe(IObserver observer) => Subscriber = observer;

    public void SetStatus(BuzzStatus status)
    {
        // Util.Configuration.Default.DiagnosticLogger.WarnAboutCustomStatusName(
        //     $"{Name}.{status.GetType().Name}",
        //     $"{Name}.{status.Code}"
        // );

        switch (Status)
        {
            case BuzzStatus.First.Ready:
                Stopwatch.Start();
                break;
            case BuzzStatus.Last.Okay:
                TraceHandle.Stop(ok: true);
                break;
            case BuzzStatus.Last.Fail:
                TraceHandle.Stop(ok: false);
                break;
        }

        Status = status;
        Duration = Stopwatch.Elapsed;
        Subscriber.OnBuzzChange(this);
    }

    public IEnumerator<Buzz> GetEnumerator()
    {
        if (AmbientContext<Buzz>.Current is IEnumerable<AmbientContext<Buzz>> context)
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
        SetStatus(new BuzzStatus.Idle.Cold());
        TraceHandle.Dispose();
        Pop.Dispose();
    }

    public class Bulk(string? name = null, bool logItems = false)
        : Buzz(name), IObserver
    {
        private BulkMath Math { get; } = new();

        [Detail(Name = "bulk.item_count")]
        [Remark(Label = "Item Count")]
        public int ItemCount => Math.ItemCount;

        [Detail(Name = "bulk.duration_s")]
        [Remark(Label = "Item Duration", Format = "N3", QuoteMode = QuoteMode.Never)]
        public double DurationS => Math.DurationMs / 1000.0;

        [Detail(Name = "bulk.throughput_s")]
        [Remark(Label = "Throughput", Format = "N1", QuoteMode = QuoteMode.Never)]
        public double ThroughputS => Math.ThroughputMs * 1000.0;

        public void OnBuzzChange(Buzz item)
        {
            if (item.Status is BuzzStatus.Last)
            {
                Math.Count(item.Status.Code, item.Duration);
            }

            if (logItems)
            {
                Subscriber.OnBuzzChange(item);
            }
        }
    }
}