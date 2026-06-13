using Wiretap.Meta;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public abstract class ActivityScope(string activityName) : IStateItemFeed, IMessagePartFeed, IDisposable
{
    private AmbientContext<ActivityScope>? AmbientScope { get; set; }
    protected ActivityWrapper ActivityWrapper { get; } = new(activityName);

    public int Depth => AmbientScope.Depth;

    public string Path => AmbientScope.PathOf(x => x.ActivityName);

    public string ActivityName { get; } = activityName;

    protected abstract string Role { get; }

    public virtual void MessageParts(IReadOnlyDictionary<string, object?> properties, ItemFeed<PushMessagePart> feed)
    {
        feed((name, push) => push(
            $"{name.Activity.Name:_}[{name.Activity.Status.Code:_}]",
            properties[name.Activity.Name],
            properties[name.Activity.Status.Code]
        ));
    }

    public virtual void StateItems(ItemFeed<PushStateItem> feed)
    {
        ActivityWrapper.StateItems(feed);

        feed((name, push) =>
        {
            push(name.Activity.Name, ActivityName);
            push(name.Activity.Role, Role);
            push(name.Activity.Depth, Depth);
            push(name.Activity.Path, Path);
        });
    }

    internal virtual void Push()
    {
        AmbientScope = AmbientContext<ActivityScope>.Push(this);
    }

    public virtual void Dispose()
    {
        ActivityWrapper.Dispose();
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

    public void StateItems(ItemFeed<PushStateItem> feed)
    {
        feed((name, next) => next(name.Activity.DurationMs, (long)Duration.TotalMilliseconds));
    }
}
