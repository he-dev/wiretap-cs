using System.Collections;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public abstract class ActivityScope : ILogPropertyFeed, IMessagePartFeed, IDisposable, IEnumerable<ActivityScope>
{
    private AmbientContext<ActivityScope>? AmbientScope { get; set; }

    protected ActivityScope(Activity activity)
    {
        Activity = activity;
        ActivityCast = ActivityCast.Start(activity.Name);
    }

    protected ActivityCast ActivityCast { get; }

    public Activity Activity { get; }

    public ActivityScope? Parent { get; private set; }

    public IEnumerable<ActivityScope> Ancestors => this.Skip(1);

    public int Depth => Ancestors.Count();

    public string Path => string.Join("/", this.Reverse().Select(x => x.Activity.Name));

    public string ActivityName => Activity.Name;

    public static ActivityScope? Current => AmbientContext<ActivityScope>.Current;

    protected abstract string Role { get; }

    public virtual void MessageParts(PropertyName root, GetStateItem get, PushMessagePart push)
    {
        push(
            root.Activity.Name,
            $"{root.Activity.Name:_}[{root.Activity.Status.Code:_}]",
            get(root.Activity.Name),
            get(root.Activity.Status.Code)
        );
    }

    public virtual void LogProperties(PropertyName name, PushLogProperty push)
    {
        ActivityCast.LogProperties(name, push);
        push(name.Activity.Name, ActivityName);
        push(name.Activity.Role, Role);
        push(name.Activity.Depth, Depth);
        push(name.Activity.Path, Path);
    }

    internal virtual void Push()
    {
        Parent = Current;
        AmbientScope = AmbientContext<ActivityScope>.Push(this);
    }

    public virtual void Dispose()
    {
        ActivityCast.Dispose();
        AmbientScope?.Dispose();
        GC.SuppressFinalize(this);
    }

    // core: Scope traversal starts with the current scope and proceeds toward the root.
    public IEnumerator<ActivityScope> GetEnumerator()
    {
        for (ActivityScope? current = this; current is not null; current = current.Parent)
        {
            yield return current;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public abstract class ActivityScope<TActivity>(TActivity activity) : ActivityScope(activity) where TActivity : Activity
{
    private static ConcurrentDictionary<Type, byte> CustomStatusWarnings { get; } = new();

    public new TActivity Activity => (TActivity)base.Activity;

    protected IComposeMessage ComposeMessage => Configuration.Current.ComposeMessage;

    protected void WarnIfCustomStatusName(ActivityStatus<TActivity> status)
    {
        if (status.GetType().Name == status.Code)
        {
            return;
        }

        if (!CustomStatusWarnings.TryAdd(status.GetType(), 0))
        {
            return;
        }

        var statusName = $"{ActivityName}.{status.GetType().Name}";
        var canonicalName = $"{ActivityName}.{status.Code}";
        Configuration.Current.Logger?.LogWarning(
            "{StatusName} will be logged as {CanonicalName} because only canonical status names are allowed. Rename {StatusNameToRename} to {CanonicalNameToUse} to get rid of this warning.",
            statusName,
            canonicalName,
            statusName,
            canonicalName
        );
    }

    public override void LogProperties(PropertyName name, PushLogProperty push)
    {
        base.LogProperties(name, push);
        if (Activity.Tags.Length > 0)
        {
            push(name.Activity.Tags, Activity.Tags);
        }
    }
}

internal record ActivityDurationFeed(TimeSpan Duration) : ILogPropertyFeed
{
    public sealed record Zero() : ActivityDurationFeed(TimeSpan.Zero);

    // core: Freezes the duration because the last activity may be logged later than set.
    public static ActivityDurationFeed Freeze(TimeSpan duration) => new(duration);

    public void LogProperties(PropertyName name, PushLogProperty push)
    {
        push(name.Activity.DurationMs, (long)Duration.TotalMilliseconds);
    }
}
