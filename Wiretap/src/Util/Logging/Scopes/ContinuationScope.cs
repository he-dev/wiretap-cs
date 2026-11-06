using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Wiretap.Core;

namespace Wiretap.Util.Logging.Scopes;

/// <summary>Establishes incoming trace context independently of plot event logging.</summary>
internal sealed class ContinuationScope(Activity activity, IDisposable? scope) : IDisposable
{
    private Activity Activity { get; } = activity;
    private IDisposable? Scope { get; } = scope;

    public void Dispose()
    {
        Scope?.Dispose();
        Activity.Dispose();
    }

    public static ContinuationScope About
    (
        ILogger logger,
        string about,
        string traceId,
        string? parentId = null,
        Action<Activity>? configure = null
    )
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(traceId);

        var activity = CreateActivity(about, traceId, parentId, configure);

        // core: Ordinary plot scopes inherit this context through Activity.Current.
        var scope = logger.BeginScopeWith
        (
            ("plot.about", about),
            ("trace.id", activity.TraceId.ToHexString()),
            ("trace.span_id", activity.SpanId.ToHexString()),
            ("trace.parent_id", activity.ParentSpanId == default ? null : activity.ParentSpanId.ToHexString())
        );
        return new ContinuationScope(activity, scope);
    }

    private static Activity CreateActivity
    (
        string name,
        string traceId,
        string? parentId,
        Action<Activity>? configure = null
    )
    {
        var activity = new Activity(name);
        var parsedTraceId = ActivityTraceId.CreateFromString(traceId.AsSpan());
        var parsedParentId =
            parentId is null
                ? default
                : ActivitySpanId.CreateFromString(parentId.AsSpan());

        // core: Explicit incoming context takes precedence over Activity.Current.
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.SetParentId(parsedTraceId, parsedParentId);

        configure?.Invoke(activity);
        activity.Start();
        return activity;
    }
}