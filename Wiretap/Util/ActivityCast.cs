using System.Diagnostics;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public sealed class ActivityCast(string name) : IStateItemFeed, IDisposable
{
    private static readonly ActivitySource Source = new(nameof(Wiretap));

    private System.Diagnostics.Activity? Inner { get; } = Source.StartActivity(name);

    public static ActivityCast Start(string name) => new(name);

    public ActivityCast Configure(Action<System.Diagnostics.Activity> configure)
    {
        if (Inner is not null)
        {
            configure(Inner);
        }

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

    public static ActivityListener Listen()
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

    public void Dispose() => Inner?.Dispose();
}
