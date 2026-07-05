using System.Collections;
using Wiretap.Meta;
using Wiretap.Util.Buzz;
using Wiretap.Util.Data;

namespace Wiretap.Util;

public class BuzzScope<TBuzz>
    : IEnumerable<BuzzScope<TBuzz>>, IActivityStatusObserver
    where TBuzz : Buzz<TBuzz>
{
    public BuzzScope(ActivityLogger logger, TBuzz activity)
    {
        Logger = logger;
        Activity = activity;
        Activity.Subscribe(this);
        AmbientContext<BuzzScope<TBuzz>>.Push(this);
        TraceHandle = Util.Configuration.Default.TraceContext.Start(Activity.Name);
        Configuration = Util.Configuration.Resolve(activity);
    }

    private ActivityLogger Logger { get; }
    private ITraceHandle TraceHandle { get; }
    private Configuration Configuration { get; }
    private TBuzz Activity { get; }
    public string Path => string.Join("/", this.Reverse().Select(x => x.Activity.Name));

    public void OnStatusChange(IActivity activity, TimeSpan duration)
    {
        if (activity.Status is ActivityStatusRole.ILast) { }

        switch (activity.Status.LogStatusPolicy())
        {
            case LogStatusPolicy.Auto:
                break;
            case LogStatusPolicy.Sure:
                break;
            case LogStatusPolicy.Nope:
                break;
            default:
                break;
        }
    }

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
        //details.Put(root.Activity.Role, Activity.Role);
        details.Put(root.Activity.Depth, activities.Count - 1);
        details.Put(root.Activity.Path, string.Join("/", activities.AsEnumerable().Reverse().Select(activity => activity.Name)));
        details.Put(root.Activity.Tags, Activity.Tags.Length > 0 ? Activity.Tags : null);
        //details.Put(root.Activity.DurationMs, Activity is Activity.Buzz buzz ? buzz.DurationMs : null);

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
        Logger.Log(
            status.Level,
            details
                .Where(pair => pair.Value is not null)
                .ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            message,
            status.Exception
        );
    }

    public virtual void Dispose()
    {
        TraceHandle.Dispose();

        GC.SuppressFinalize(this);
    }


    // core: Scope traversal starts with the current scope and proceeds toward the root.
    public IEnumerator<BuzzScope<TBuzz>> GetEnumerator()
    {
        if (AmbientContext<BuzzScope<TBuzz>>.Current is IEnumerable<AmbientContext<BuzzScope<TBuzz>>> context)
        {
            foreach (var item in context)
            {
                yield return item.Value;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}