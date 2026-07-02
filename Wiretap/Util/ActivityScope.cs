using System.Collections;
using Wiretap.Meta;
using Wiretap.Util.Buzz;
using Wiretap.Util.Data;

namespace Wiretap.Util;

public abstract class ActivityScope(ActivityLogger logger, Activity activity) : IDisposable, IEnumerable<ActivityScope>
{
    private AmbientContext<ActivityScope>? AmbientScope { get; set; }

    protected ITraceHandle TraceHandle { get; } = Util.Configuration.TraceContext.Start(activity.Name);

    protected Configuration Configuration { get; } = Util.Configuration.Resolve(activity);

    public Activity Activity { get; } = activity;

    public ActivityScope? Parent { get; private set; }

    public IEnumerable<ActivityScope> Ancestors => this.Skip(1);

    public int Depth => Ancestors.Count();

    public string Path => string.Join("/", this.Reverse().Select(x => x.Activity.Name));

    public string ActivityName => Activity.Name;

    public static ActivityScope? Current => AmbientContext<ActivityScope>.Current;

    protected void LogStatus()
    {
        var root = Configuration.Root;
        var status = Activity.Status;
        var activities = this.Select(scope => scope.Activity).ToList();

        var details = new DetailCollection();
        details.Put(root.Activity.Name, Activity.Name);
        details.Put(root.Activity.Status.Code, status.Code);
        details.Put(root.Activity.Status.Role, status switch
        {
            ActivityStatusRole.IFirst => "first",
            ActivityStatusRole.ILast => "last",
            _ => null
        });
        details.Put(root.Activity.Role, Activity.Role);
        details.Put(root.Activity.Depth, activities.Count - 1);
        details.Put(root.Activity.Path, string.Join("/", activities.AsEnumerable().Reverse().Select(activity => activity.Name)));
        details.Put(root.Activity.Tags, Activity.Tags.Length > 0 ? Activity.Tags : null);
        details.Put(root.Activity.DurationMs, Activity is Activity.Buzz buzz ? buzz.DurationMs : null);

        details.Put(root.TraceId, TraceHandle.TraceId);
        details.Put(root.SpanId, TraceHandle.SpanId);
        details.Put(root.ParentSpanId, TraceHandle.ParentSpanId);

        foreach (var (source, level) in activities.Select((source, level) => (source, level)))
        {
            var builder = new DetailBuilder(root.Activity.State, level, details);
            CollectDetails.From(builder, source);
        }

        var remarks = new RemarkCollection();
        foreach (var source in new object[] { Activity, status })
        {
            var builder = new RemarkBuilder(root, details, remarks);
            CollectRemarks.From(builder, source);
        }

        var message = Configuration.ComposeMessage.From(root, details, remarks);
        logger.Log(
            status.Level,
            details
                .Where(pair => pair.Value is not null)
                .ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            message,
            status.Exception
        );
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

public abstract class ActivityScope<TActivity>(ActivityLogger logger, TActivity activity) : ActivityScope(logger, activity) where TActivity : Activity
{
    protected new TActivity Activity => (TActivity)base.Activity;
}
