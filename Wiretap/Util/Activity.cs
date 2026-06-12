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
    protected Activity()
    {
        Name = GetActivityName.For(GetType());
    }

    public string Name { get; }

    public abstract class Buzz : Activity;

    public abstract class Bulk : Buzz
    {
        public abstract StatusLogPolicy StatusLogPolicy { get; }
    }

    public abstract class Bulk<TActivity, TItem>(StatusLogPolicy statusLogPolicy) : Bulk
        where TActivity : Bulk<TActivity, TItem>
        where TItem : Buzz
    {
        public override StatusLogPolicy StatusLogPolicy => statusLogPolicy;
    }

    public abstract class Snap : Activity;
}