using System.Collections;
using Wiretap.Meta;
using Wiretap.Util.Data;

namespace Wiretap.Util;

public interface IStatusObserver
{
    void OnStatusChange(Buzz buzz, TimeSpan duration);
}

public interface IObservableStatus
{
    void Subscribe(IStatusObserver observer);
}

internal class StatusObserverNoop : IStatusObserver
{
    public void OnStatusChange(Buzz buzz, TimeSpan duration) { }
}

public class Buzz : IEnumerable<Buzz>, IObservableStatus
{
    protected Buzz()
    {
        Name = GetActivityName.For(GetType());
        TraceHandle = Configuration.Default.TraceContext.Start(Name);
        Pop = AmbientContext<Buzz>.Push(this);
    }

    private IDisposable Pop { get; }

    private System.Diagnostics.Stopwatch Stopwatch { get; } = new();

    private IStatusObserver StatusObserver { get; set; } = new StatusObserverNoop();

    public ITraceHandle TraceHandle { get; }

    public string Name { get; }

    public virtual string[] Tags { get; } = [];

    public TimeSpan Elapsed => Stopwatch.Elapsed;

    public string Path => string.Join("/", this.Reverse().Select(x => x.Name));

    //public abstract string Role { get; }

    public IActivityStatus Status { get; private set; } = new BuzzStatus<TBuzz>.Pending();

    public void Subscribe(IStatusObserver statusObserver)
    {
        StatusObserver = statusObserver;
    }

    public bool SetStatus(BuzzStatus status)
    {
        // Util.Configuration.Default.DiagnosticLogger.WarnAboutCustomStatusName(
        //     $"{Name}.{status.GetType().Name}",
        //     $"{Name}.{status.Code}"
        // );

        if (status is BuzzStatus<TBuzz>.Ready)
        {
            Stopwatch.Start();
        }

        if (Status is ActivityStatusRole.ILast)
        {
            switch (Status)
            {
                case BuzzStatus<TBuzz>.Okay:
                    TraceHandle.Stop(ok: true);
                    break;
                case BuzzStatus<TBuzz>.Fail:
                    TraceHandle.Stop(ok: false);
                    break;
            }
        }

        Status = status;
        StatusObserver.OnStatusChange(this, Stopwatch.Elapsed);
        return true;
    }

    public IEnumerator<Buzz> GetEnumerator()
    {
        if (AmbientContext<Buzz>.Current is IEnumerable < AmbientContext < Buzz > context)
        {
            foreach (var item in context)
            {
                yield return item.Value;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Dispose()
    {
        SetStatus(new BuzzStatus<Buzz>.Cold());
        TraceHandle.Dispose();
        Pop.Dispose();
    }

    public abstract class Bulk<TItem> : Buzz where TItem : Buzz
    {
        internal BulkMath Math { get; } = new();

        //public override string Role => "bulk";

        [Detail("bulk.item_count")]
        [Remark("Item Count")]
        public int ItemCount => Math.ItemCount;

        [Detail("bulk.duration_s")]
        [Remark("Item Duration", Format = "N3", QuoteMode = QuoteMode.Never)]
        public double DurationS => Math.DurationMs / 1000.0;

        [Detail("bulk.throughput_s")]
        [Remark("Throughput", Format = "N1", QuoteMode = QuoteMode.Never)]
        public double ThroughputS => Math.ThroughputMs * 1000.0;

        public TItem BeginItem(TItem item)
        {
            // todo: wire the item status changes with the math.count
            return item;
        }
    }
}