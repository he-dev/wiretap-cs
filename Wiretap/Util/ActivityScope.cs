using Wiretap.Meta;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public abstract class ActivityScope : IStateItemFeed, IMessagePartFeed, IDisposable
{
    private ActivityScopeStack<ActivityScope>? AmbientScope { get; set; }

    public int Depth => AmbientScope.Depth;

    public string Path => AmbientScope.PathOf(x => x.ActivityName);

    public abstract string ActivityName { get; }

    public virtual void MessageParts(IReadOnlyDictionary<string, object?> properties, PushMessagePart push)
    {
        push(
            "{wiretap.activity.name}[{wiretap.activity.status.code}]",
            properties["wiretap.activity.name"],
            properties["wiretap.activity.status.code"]
        );
    }

    public virtual void StateItems(PushStateItem push)
    {
        push("wiretap.activity.name", ActivityName);
        push("wiretap.activity.depth", Depth);
        push("wiretap.activity.path", Path);
    }

    internal virtual void Push()
    {
        AmbientScope = ActivityScopeStack<ActivityScope>.Push(this);
    }

    public virtual void Dispose()
    {
        AmbientScope?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public abstract class ActivityScope<TActivity>(TActivity activity) : ActivityScope where TActivity : Activity
{
    protected TActivity Activity => activity;

    public override string ActivityName => activity.Name;

    protected ComposeMessage ComposeMessage => GetComposeMessage.FromAttributeOrDefault(activity.GetType());
}

internal record ActivityDurationFeed(TimeSpan Duration) : IStateItemFeed
{
    public sealed record Zero() : ActivityDurationFeed(TimeSpan.Zero);

    // core: Freezes the duration because the last activity may be logged later than set.
    public static ActivityDurationFeed Freeze(TimeSpan duration) => new(duration);

    public void StateItems(PushStateItem push)
    {
        push("wiretap.activity.duration_ms", (long)Duration.TotalMilliseconds);
    }
}
