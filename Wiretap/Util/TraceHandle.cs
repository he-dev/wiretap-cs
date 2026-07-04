using System.Diagnostics;

namespace Wiretap.Util;

public interface ITraceHandle : IDisposable
{
    string TraceId { get; }
    string SpanId { get; }
    string? ParentSpanId { get; }

    void Stop(bool? ok);
}

public class TraceHandle(System.Diagnostics.Activity activity) : ITraceHandle
{
    public string TraceId => activity.TraceId.ToHexString();
    public string SpanId => activity.SpanId.ToHexString();
    public string? ParentSpanId => activity.ParentSpanId == default ? null : activity.ParentSpanId.ToHexString();

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
        // note: Auto-stop without status because we do not know it.
        Stop(null);
    }

    public class Noop : ITraceHandle
    {
        public string TraceId { get; } = string.Empty;
        public string SpanId { get; } =  string.Empty;
        public string? ParentSpanId { get; } = null;

        public void Stop(bool? ok) { }
        public void Dispose() { }
    }
}
