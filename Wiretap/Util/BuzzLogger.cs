using Microsoft.Extensions.Logging;
using Wiretap.Util.Buzz2;
using Wiretap.Util.Data;

namespace Wiretap.Util;

public sealed class BuzzLogger(ILogger logger) : IObserver
{
    public void OnBuzzChange(Buzz buzz)
    {
        switch (buzz.Status)
        {
            case BuzzStatus.First:
            case BuzzStatus.Last:
                LogStatus(buzz);
                break;
        }
    }

    private void LogStatus(Buzz buzz)
    {
        var configuration = Configuration.Resolve(buzz);

        var root = configuration.Root;
        var status = buzz.Status;

        var details = new DetailCollection
        {
            { root.Buzz.Name, buzz.Name },
            { root.Buzz.Status.Code, status.Code },
            { root.Buzz.Status.Role, status.Role },
            { root.Buzz.Depth, buzz.Count() - 1 },
            { root.Buzz.Path, buzz.Path },
            { root.Buzz.Tags, buzz.Tags.Length > 0 ? buzz.Tags : null },
            { root.TraceId, buzz.TraceHandle.TraceId },
            { root.SpanId, buzz.TraceHandle.SpanId },
            { root.ParentSpanId, buzz.TraceHandle.ParentSpanId },
        };

        foreach (var (source, level) in buzz.Select((source, level) => (source, level)))
        {
            var builder = new DetailBuilder(root.Buzz.State, level, details);
            CollectDetails.From(builder, source);
        }

        var remarks = new RemarkCollection();
        foreach (var source in new object[] { buzz, status })
        {
            var builder = new RemarkBuilder(root, details, remarks);
            CollectRemarks.From(builder, source);
        }

        var message = configuration.ComposeMessage.From(root, details, remarks);
        Log(
            status.Level,
            details
                .Where(pair => pair.Value is not null)
                .ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            message,
            status.Exception
        );
    }

    private void Log(LogLevel level, IReadOnlyDictionary<string, object?> details, MessageTemplate message, Exception? exception = null)
    {
        using var scope = details.Count == 0 ? null : logger.BeginScope(details);
        logger.Log(level, exception, message.Template, message.Args);
    }
}
