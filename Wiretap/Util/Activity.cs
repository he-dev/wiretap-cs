using Wiretap.Util.Buzz;

namespace Wiretap.Util;

[Flags]
public enum StatusLogPolicy
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

    internal virtual bool SetStatus(ActivityStatus status)
    {
        if (_status is ActivityStatusRole.ILast)
        {
            return false;
        }

        _status = status;
        return true;
    }

    public abstract class Buzz : Activity
    {
        private readonly System.Diagnostics.Stopwatch _stopwatch = new();

        public override string Role => "buzz";

        public TimeSpan Duration { get; private set; }

        public long DurationMs => (long)Duration.TotalMilliseconds;

        internal void Start() => _stopwatch.Start();

        internal override bool SetStatus(ActivityStatus status)
        {
            if (!base.SetStatus(status))
            {
                return false;
            }

            Duration = _stopwatch.Elapsed;
            return true;
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

        public abstract StatusLogPolicy StatusLogPolicy { get; init; }
    }

    public abstract class Bulk<TBulk, TItem>(StatusLogPolicy statusLogPolicy) : Bulk
        where TBulk : Bulk<TBulk, TItem>
        where TItem : Item
    {
        public override StatusLogPolicy StatusLogPolicy { get; init; } = statusLogPolicy;
    }

    public abstract class Snap : Activity
    {
        public override string Role => "snap";
    }
}
