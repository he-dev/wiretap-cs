using Wiretap.Meta;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public abstract class ActivityScope : IStateItemFeed, IMessagePartFeed, IDisposable
{
    public static ActivityScope? Current => ActivityScopeStack<ActivityScope>.Current;

    protected System.Diagnostics.Stopwatch Stopwatch { get; } = System.Diagnostics.Stopwatch.StartNew();

    protected TimeSpan Elapsed => Stopwatch.Elapsed;

    private ActivityScopeStack<ActivityScope>? AmbientScope { get; set; }

    public ActivityScope? Parent => AmbientScope?.Parent?.Value;

    public int Depth => AmbientScope.Depth;

    public string Path => AmbientScope.PathOf(x => x.ActivityName);

    public abstract string ActivityName { get; }

    public virtual void MessageParts(ActivityStatus.Context context, PushMessagePart push)
    {
        push("{Activity}[{ActivityStatus}]", context.Activity, context.ActivityStatus);
    }

    public virtual void StateItems(PushStateItem push)
    {
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

    public static KeyValuePair<string, object?>[] CurrentItemTags()
    {
        if (Current is { } current)
        {
            return
            [
                new(nameof(ActivityStatus.Context.Activity), current.ActivityName),
                new(nameof(ActivityStatus.Context.ActivityDepth), current.Depth),
                new(nameof(ActivityStatus.Context.ActivityPath), current.Path),
                new(nameof(ActivityStatus.Context.ParentActivity), current.Parent?.ActivityName),
                new(nameof(ActivityStatus.Context.Duration), (long)current.Elapsed.TotalMilliseconds),
            ];
        }

        return [];
    }
}