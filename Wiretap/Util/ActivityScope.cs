using Wiretap.Meta;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public abstract class ActivityScope(string activityName) : IStateItemFeed, IMessagePartFeed, IDisposable
{
    private AmbientContext<ActivityScope>? AmbientScope { get; set; }

    protected ActivityCast ActivityCast { get; } = ActivityCast.Start(activityName);

    public int Depth => AmbientScope.Depth;

    public string Path => AmbientScope.PathOf(x => x.ActivityName);

    public string ActivityName { get; } = activityName;

    protected abstract string Role { get; }

    public virtual void MessageParts(PropertyName root, GetStateItem get, PushMessagePart push)
    {
        push(
            $"{root.Activity.Name:_}[{root.Activity.Status.Code:_}]",
            get(root.Activity.Name),
            get(root.Activity.Status.Code)
        );
    }

    public virtual void StateItems(PropertyName name, PushStateItem push)
    {
        ActivityCast.StateItems(name, push);
        push(name.Activity.Name, ActivityName);
        push(name.Activity.Role, Role);
        push(name.Activity.Depth, Depth);
        push(name.Activity.Path, Path);
    }

    internal virtual void Push()
    {
        AmbientScope = AmbientContext<ActivityScope>.Push(this);
    }

    public virtual void Dispose()
    {
        ActivityCast.Dispose();
        AmbientScope?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public abstract class ActivityScope<TActivity>(TActivity activity) : ActivityScope(activity.Name) where TActivity : Activity
{
    protected TActivity Activity => activity;

    protected IComposeMessage ComposeMessage => Configuration.Current.ComposeMessage;
}

internal record ActivityDurationFeed(TimeSpan Duration) : IStateItemFeed
{
    public sealed record Zero() : ActivityDurationFeed(TimeSpan.Zero);

    // core: Freezes the duration because the last activity may be logged later than set.
    public static ActivityDurationFeed Freeze(TimeSpan duration) => new(duration);

    public void StateItems(PropertyName name, PushStateItem push)
    {
        push(name.Activity.DurationMs, (long)Duration.TotalMilliseconds);
    }
}
