using Microsoft.Extensions.Logging;
using Wiretap.Util.Buzz;
using Wiretap.Util.Data;

namespace Wiretap.Util;

public sealed class WatchLogger(ILogger logger, Watch watch) : IDisposable
{
    //public ILogger Inner { get; } = logger;

    // todo: initialize with watch-state fields.
    private readonly IDisposable? _scope = logger.BeginScope(new Dictionary<string, object>());

    public TimeSpan LogEvent(WatchEvent watchEvent)
    {
        var duration = watch.Freeze(watchEvent);

        if (!watch.CanLogEvent(watchEvent)) return duration;

        var configuration = Configuration.Resolve(watch);

        var root = configuration.Root;

        var details = new DetailCollection
        {
            { root.Buzz.Name, watch.ToString() },
            { root.Buzz.Status.Code, watchEvent.ToString() },
            //{ root.Buzz.Status.Role, status.Role },
            { root.Buzz.Depth, watch.Count() - 1 },
            { root.Buzz.Path, string.Join("/", watch.Reverse().Select(x => x.ToString())) },
            { root.Buzz.Tags, watch.Tags.Length > 0 ? watch.Tags : null },
            { root.TraceId, watch.TraceHandle.TraceId },
            { root.SpanId, watch.TraceHandle.SpanId },
            { root.ParentSpanId, watch.TraceHandle.ParentSpanId },
        };

        foreach (var (source, level) in watch.Select((source, level) => (source, level)))
        {
            var builder = new DetailBuilder(root.Buzz.State, level, details);
            CollectDetails.From(builder, source);
        }

        var remarks = new RemarkCollection();
        foreach (var source in new object[] { watch, watchEvent })
        {
            var builder = new RemarkBuilder(root, details, remarks);
            CollectRemarks.From(builder, source);
        }

        var message = configuration.ComposeMessage.From(root, details, remarks);
        Log(
            watchEvent.Level,
            details
                .Where(pair => pair.Value is not null)
                .ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            message,
            watchEvent.Exception
        );

        return duration;
    }

    private void Log(LogLevel level, IReadOnlyDictionary<string, object?> details, MessageTemplate message, Exception? exception = null)
    {
        using var scope = details.Count == 0 ? null : logger.BeginScope(details);
        logger.Log(level, exception, message.Template, message.Args);
    }

    public void Dispose()
    {
        _scope?.Dispose();
    }
}