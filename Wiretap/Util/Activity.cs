using Wiretap.Util.Buzz;

namespace Wiretap.Util;

[Flags]
public enum OmitStatus
{
    None = 0x0,
    First = 0x1,
    Last = 0x2,
    Both = First | Last,
}

public abstract class Activity
{
    private ActivityStatus? _status;

    protected Activity()
    {
        Name = GetActivityName.For(GetType());
    }

    public virtual string Name { get; }

    public virtual string[] Tags { get; init; } = [];

    public abstract string Role { get; }

    internal ActivityStatus Status => _status ?? throw new InvalidOperationException("The activity has not started.");

    internal bool SetStatus(ActivityStatus status)
    {
        if (_status is ActivityStatusRole.ILast)
        {
            return false;
        }

        _status = status;
        if (status is ActivityStatusRole.ILast)
        {
            OnLastStatusChange();
        }

        return true;
    }

    protected virtual void OnLastStatusChange() { }

    public abstract class Buzz : Activity
    {
        private readonly System.Diagnostics.Stopwatch _stopwatch = new();

        public override string Role => "buzz";

        public TimeSpan Duration { get; private set; }

        public long DurationMs => (long)Duration.TotalMilliseconds;

        internal void Start() => _stopwatch.Start();

        protected override void OnLastStatusChange()
        {
            Duration = _stopwatch.Elapsed;
        }
    }

    public abstract class Item : Buzz
    {
        public override string Role => "item";
    }

    public abstract class Bulk : Buzz
    {
        internal BulkMath Math { get; } = new();

        public override string Role => "bulk";

        public abstract OmitStatus OmitStatus { get; init; }

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

    public abstract class Bulk<TBulk, TItem>(OmitStatus omitStatus) : Bulk
        where TBulk : Bulk<TBulk, TItem>
        where TItem : Item
    {
        public override OmitStatus OmitStatus { get; init; } = omitStatus;
    }

    public abstract class Snap : Activity
    {
        public override string Role => "snap";
    }
}
