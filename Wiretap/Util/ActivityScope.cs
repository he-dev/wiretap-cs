using Wiretap.Meta;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public abstract class ActivityScope : IStateItemFeed, IMessagePartFeed, IDisposable
{
    private AmbientContext<ActivityScope>? AmbientScope { get; set; }

    public int Depth => AmbientScope.Depth;

    public string Path => AmbientScope.PathOf(x => x.ActivityName);

    public abstract string ActivityName { get; }

    protected abstract string Role { get; }

    public virtual void MessageParts(IReadOnlyDictionary<string, object?> properties, SchemaFeed<PushMessagePart> push)
    {
        push((name, next) => next(
            $"{name.Activity.Name:_}[{name.Activity.Status.Code:_}]",
            properties[name.Activity.Name],
            properties[name.Activity.Status.Code]
        ));
    }

    public virtual void StateItems(SchemaFeed<PushStateItem> push)
    {
        push((name, next) =>
        {
            next(name.Activity.Name, ActivityName);
            next(name.Activity.Role, Role);
            next(name.Activity.Depth, Depth);
            next(name.Activity.Path, Path);
        });
    }

    internal virtual void Push()
    {
        AmbientScope = AmbientContext<ActivityScope>.Push(this);
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

    protected IComposeMessage ComposeMessage => Configuration.Current.ComposeMessage;
}

internal record ActivityDurationFeed(TimeSpan Duration) : IStateItemFeed
{
    public sealed record Zero() : ActivityDurationFeed(TimeSpan.Zero);

    // core: Freezes the duration because the last activity may be logged later than set.
    public static ActivityDurationFeed Freeze(TimeSpan duration) => new(duration);

    public void StateItems(SchemaFeed<PushStateItem> push)
    {
        push((name, next) => next(name.Activity.DurationMs, (long)Duration.TotalMilliseconds));
    }
}
