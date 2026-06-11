using Wiretap.Util.Services;

namespace Wiretap.Util;

public abstract class Activity
{
    protected Activity()
    {
        Name = GetActivityName.For(GetType());
    }

    public abstract string Role { get; }

    public string Name { get; }

    public abstract class Buzz : Activity
    {
        public override string Role => nameof(Buzz);
    }

    public abstract class Snap : Activity
    {
        public override string Role => nameof(Snap);
    }
}
