using System.Diagnostics;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public sealed class ActivityWrapper(string name) : IStateItemFeed, IDisposable
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
                true => ActivityStatusCode.Ok,
                false => ActivityStatusCode.Error,
                _ => ActivityStatusCode.Unset
            };

            Inner
                .SetStatus(statusCode)
                .Stop();
        }
    }

    public void StateItems(ItemFeed<PushStateItem> feed)
    {
        if (!Configuration.Current.AttachTraceContext || Inner is not { } activity)
        {
            return;
        }

        // core: Does not use the "name" because these properties should be in the root scope.
        feed((name, push) =>
        {
            push("trace_id", activity.TraceId.ToString());
            push("span_id", activity.SpanId.ToString());

            if (activity.ParentSpanId != default)
            {
                push("parent_span_id", activity.ParentSpanId.ToString());
            }
        });
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
