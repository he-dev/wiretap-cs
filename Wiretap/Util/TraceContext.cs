using System.Diagnostics;

namespace Wiretap.Util;

public interface ITraceContext : IDisposable
{
    public ITraceHandle Start(string name);
    public ActivityListener Listen();
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