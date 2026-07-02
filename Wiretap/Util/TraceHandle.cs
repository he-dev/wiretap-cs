using System.Diagnostics;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public interface ITraceHandle : IDisposable
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

    public void Dispose()
    {
        // note: Auto-stop without status because we do not know it.
        Stop(null);
    }

    public class Noop : ITraceHandle
    {
        public void Stop(bool? ok) { }
        public void Dispose() { }
    }
}