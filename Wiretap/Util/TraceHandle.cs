using System.Diagnostics;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public interface ITraceHandle : IDisposable, ILogPropertySource
{
    void Stop(bool? ok);
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