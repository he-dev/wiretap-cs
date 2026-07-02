using System.Collections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

    protected void Log()
    {
        // TODO: Replace the dummy logger when ActivityLogger is passed into ActivityScope.
        var logger = new ActivityLogger(NullLogger.Instance);
        var recipe = Variant.CreateLogEntryBy;
        var root = recipe.Root;
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

        TraceHandle.LogProperties(root, (name, value) => details.Put(name, value));

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

        var message = ComposeMessage2.Default.From(root, details, remarks);
        logger.Log(
            status.Level,
            details
                .Where(pair => pair.Value is not null)
                .ToDictionary(pair => pair.Key, pair => pair.Value),
            message,
            status.Exception
        );
    }

    public virtual void LogProperties(PropertyName name, PushLogProperty push)
    {
        foreach (var ancestor in Ancestors.Reverse())
        {
            GetLogProperties.ByAttribute(name.Activity.State, ancestor.Activity, push, cascadingOnly: true);
        }

        TraceHandle.LogProperties(name, push);
        push(name.Activity.Name, ActivityName);
        push(name.Activity.Role, Activity.Role);
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
