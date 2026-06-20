using System.Diagnostics;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public interface ITraceContext : IDisposable
{
    public ITraceHandle Start(string name);
    public ActivityListener Listen();
}

public interface ITraceHandle : IDisposable, ILogPropertySource
{
    void Stop(bool? ok);
}

public class TraceContext(string sourceName = nameof(Wiretap)) : ITraceContext
{
    private ActivitySource Source { get; } = new(sourceName);

    public ITraceHandle Start(string name)
    {
        return Source.StartActivity(name) is { } activity ? new TraceHandle(activity) : new TraceHandle.Noop();
    }

    public ActivityListener Listen()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == Source.Name,
            Sample = (ref ActivityCreationOptions<System.Diagnostics.ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
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

public class TraceHandle(System.Diagnostics.Activity activity) : ITraceHandle
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

    public void LogProperties(PropertyName name, PushLogProperty push)
    {
        // core: Does not use the "name" because these properties should be in the root scope.
        push("trace_id", activity.TraceId.ToString());
        push("span_id", activity.SpanId.ToString());

        if (activity.ParentSpanId != default)
        {
            push("parent_span_id", activity.ParentSpanId.ToString());
        }
    }

    public void Dispose()
    {
        // note: Auto-stop without status because we do not know it.
        Stop(null);
    }

    public class Noop : ITraceHandle
    {
        public void Stop(bool? ok) { }
        public void LogProperties(PropertyName name, PushLogProperty push) { }
        public void Dispose() { }
    }
}