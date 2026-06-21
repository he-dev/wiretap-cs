using System.Collections;
using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public abstract class ActivityScope(Activity activity) : ILogPropertySource, IDisposable, IEnumerable<ActivityScope>
{
    private AmbientContext<ActivityScope>? AmbientScope { get; set; }

    protected ITraceHandle TraceHandle { get; } = Configuration.TraceContext.Start(activity.Name);

    protected Configuration.Variant Variant { get; } = Configuration.Resolve(activity);

    public Activity Activity { get; } = activity;

    public ActivityScope? Parent { get; private set; }

    public IEnumerable<ActivityScope> Ancestors => this.Skip(1);

    public int Depth => Ancestors.Count();

    public string Path => string.Join("/", this.Reverse().Select(x => x.Activity.Name));

    public string ActivityName => Activity.Name;

    public static ActivityScope? Current => AmbientContext<ActivityScope>.Current;

    protected abstract string Role { get; }

    public virtual void LogProperties(PropertyName name, PushLogProperty push)
    {
        foreach (var ancestor in Ancestors.Reverse())
        {
            GetLogProperties.ByAttribute(name.Activity.State, ancestor.Activity, push, cascadingOnly: true);
        }

        TraceHandle.LogProperties(name, push);
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
        TraceHandle.Dispose();
        AmbientScope?.Dispose();
        GC.SuppressFinalize(this);
    }

    // core: Scope traversal starts with the current scope and proceeds toward the root.
    public IEnumerator<ActivityScope> GetEnumerator()
    {
        for (var current = this; current is not null; current = current.Parent)
        {
            yield return current;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public abstract class ActivityScope<TActivity>(TActivity activity) : ActivityScope(activity) where TActivity : Activity
{
    public new TActivity Activity => (TActivity)base.Activity;

    public override void LogProperties(PropertyName name, PushLogProperty push)
    {
        base.LogProperties(name, push);
        if (Activity.Tags.Length > 0)
        {
            push(name.Activity.Tags, Activity.Tags);
        }
    }
}

internal record ActivityDurationSource(TimeSpan Duration) : ILogPropertySource
{
    public sealed record Zero() : ActivityDurationSource(TimeSpan.Zero);

    // core: Freezes the duration because the last activity may be logged later than set.
    public static ActivityDurationSource Freeze(TimeSpan duration) => new(duration);

    public void LogProperties(PropertyName name, PushLogProperty push)
    {
        push(name.Activity.DurationMs, (long)Duration.TotalMilliseconds);
    }
}
