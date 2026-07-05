using Wiretap.Util.Buzz;
using Wiretap.Util.Data;

namespace Wiretap.Util;

public interface IActivity : IDisposable
{
    string Name { get; }

    IActivityStatus Status { get; }
}

public interface IActivityStatusObserver : IDisposable
{
    void OnStatusChange(IActivity activity, TimeSpan duration);
}

public interface IObservableActivity
{
    void Subscribe(IActivityStatusObserver observer);
}

internal class ActivityStatusObserverNoop : IActivityStatusObserver
{
    public void OnStatusChange(IActivity activity, TimeSpan duration) { }
    public void Dispose() { }
}

public abstract class Activity<TActivity> : IActivity, IObservableActivity where TActivity : Activity<TActivity>
{
    protected Activity()
    {
        Name = GetActivityName.For(GetType());
    }

    private System.Diagnostics.Stopwatch Stopwatch { get; } = new();

    private IActivityStatusObserver StatusObserver { get; set; } = new ActivityStatusObserverNoop();

    public virtual string Name { get; }

    public virtual string[] Tags { get; init; } = [];

    public TimeSpan Duration => Stopwatch.Elapsed;

    public abstract string Role { get; }

    public IActivityStatus Status { get; private set; } = new ActivityStatus<TActivity>.Pending();

    public void Subscribe(IActivityStatusObserver statusObserver) => StatusObserver = statusObserver;

    public bool SetStatus(ActivityStatus<TActivity> status)
    {
        // Util.Configuration.Default.DiagnosticLogger.WarnAboutCustomStatusName(
        //     $"{Name}.{status.GetType().Name}",
        //     $"{Name}.{status.Code}"
        // );

        if (status is ActivityStatus<TActivity>.Ready)
        {
            Stopwatch.Start();
        }

        if (Status is ActivityStatusRole.ILast)
        {
            return false;
        }

        Status = status;
        StatusObserver.OnStatusChange(this, Stopwatch.Elapsed);
        return true;
    }

    public void Dispose()
    {
        SetStatus(new ActivityStatus<TActivity>.Cold());
        StatusObserver.Dispose();
    }


    public abstract class Bulk : Buzz
    {
        internal BulkMath Math { get; } = new();

        public override string Role => "bulk";

        [Detail("bulk.item_count")]
        [Remark("Item Count")]
        public int ItemCount => Math.ItemCount;

        [Detail("bulk.duration_s")]
        [Remark("Item Duration", Format = "N3", QuoteMode = QuoteMode.Never)]
        public double DurationS => Math.DurationMs / 1000.0;

        [Detail("bulk.throughput_s")]
        [Remark("Throughput", Format = "N1", QuoteMode = QuoteMode.Never)]
        public double ThroughputS => Math.ThroughputMs * 1000.0;
    }

    public abstract class Bulk<TBulk, TItem> : Bulk
        where TBulk : Bulk<TBulk, TItem>
        where TItem : Item;
}