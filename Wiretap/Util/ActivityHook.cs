using System.Diagnostics;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public interface IActivityHook : IDisposable
{
    public IActivityHandle? Start(string name);
}

public interface IActivityHandle : IDisposable
{
    void Stop(bool? ok);
}

public class ActivityHook(string sourceName = nameof(Wiretap)) : IActivityHook, IStateItemFeed
{
    private ActivitySource Source { get; } = new(sourceName);

    public bool AttachTraceContext { get; init; } = true;

    public IActivityHandle? Start(string name)
    {
        return Source.StartActivity(name) is { } activity ? new ActivityHandle(activity) : null;
    }

    public void StateItems(PropertyName name, PushStateItem push)
    {
        if (!AttachTraceContext || System.Diagnostics.Activity.Current is not { } activity)
        {
            return;
        }

        // core: Does not use the "name" because these properties should be in the root scope.
        push("trace_id", activity.TraceId.ToString());
        push("span_id", activity.SpanId.ToString());

        if (activity.ParentSpanId != default)
        {
            push("parent_span_id", activity.ParentSpanId.ToString());
        }
    }

    public ActivityListener Listen()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == Source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    public void Dispose()
    {
        Source.Dispose();
    }
}

public class ActivityHandle(System.Diagnostics.Activity activity) : IActivityHandle
{
    public void Stop(bool? ok)
    {
        if (activity is { IsStopped: false })
        {
            var statusCode = ok switch
            {
                true => ActivityStatusCode.Ok,
                false => ActivityStatusCode.Error,
                _ => ActivityStatusCode.Unset
            };

            activity
                .SetStatus(statusCode)
                .Stop();
        }
    }

    public void Dispose()
    {
        // note: Auto-stop. We do not know the status.
        Stop(null);
    }
}
