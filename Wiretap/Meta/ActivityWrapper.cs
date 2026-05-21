using System.Diagnostics;

namespace Wiretap.Meta;

internal sealed class ActivityWrapper(string name) : IDisposable
{
    private static readonly ActivitySource Source = new(nameof(Wiretap));

    private System.Diagnostics.Activity? Inner { get; } = Source.StartActivity(name);

    public ActivityWrapper AddTag(string name, object value)
    {
        Inner?.AddTag(name, value);
        return this;
    }

    public void Stop(bool? isOk)
    {
        if (Inner is { IsStopped: false })
        {
            var statusCode = isOk switch
            {
                true => System.Diagnostics.ActivityStatusCode.Ok,
                false => System.Diagnostics.ActivityStatusCode.Error,
                _ => System.Diagnostics.ActivityStatusCode.Unset
            };

            Inner
                .SetStatus(statusCode)
                .Stop();
        }
    }

    public void Dispose() => Inner?.Dispose();
}

public static class CreateActivityListener
{
    public static ActivityListener Default()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == nameof(Wiretap),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => { /* optional */ },
            ActivityStopped = activity => { /* optional */ }
        };

        ActivitySource.AddActivityListener(listener);

        return listener;
    }
}
