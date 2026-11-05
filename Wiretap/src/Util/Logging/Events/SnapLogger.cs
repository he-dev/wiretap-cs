using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Wiretap.Core;

namespace Wiretap.Util.Logging.Events;

public sealed class SnapLogger(ILogger logger, string about, Story story) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            var activity = Activity.Current;
            var parentId = activity is null || activity.ParentSpanId == default ? null : activity.ParentSpanId.ToHexString();

            logger
                .With
                (
                    ("plot.about", about),
                    ("plot.event", PlotEvent.Snap),
                    ("plot.story", story.Value),
                    ("trace.id", activity?.TraceId.ToHexString()),
                    ("trace.span_id", activity?.SpanId.ToHexString()),
                    ("trace.parent_id", parentId)
                )
                .Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
