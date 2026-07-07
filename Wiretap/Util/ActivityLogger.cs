using Microsoft.Extensions.Logging;
using Wiretap.Util.Buzz2;
using Wiretap.Util.Data;

namespace Wiretap.Util;

public sealed class ActivityLogger(ILogger logger) : IObserver
{
    public void OnBuzzChange(Buzz buzz)
    {
        var configuration = Util.Configuration.Resolve(buzz);

        switch (buzz.Status)
        {
            case Status.First:
            case Status.Last:
                LogStatus(configuration, buzz, buzz.Duration);
                break;
        }
    }

    private void LogStatus(Configuration configuration, Buzz buzz, TimeSpan duration)
    {
        var root = configuration.Root;
        var status = buzz.Status;


        var details = new DetailCollection();
        details.Put(root.Activity.Name, buzz.Name);
        details.Put(root.Activity.Status.Code, status.Code);
        details.Put(root.Activity.Status.Role, status switch
        {
            Status.First => "first",
            Status.Last => "last",
            _ => null
        });
        //details.Put(root.Activity.Role, Activity.Role);
        details.Put(root.Activity.Depth, buzz.Count() - 1);
        details.Put(root.Activity.Path, buzz.Path);
        details.Put(root.Activity.Tags, buzz.Tags.Length > 0 ? buzz.Tags : null);
        //details.Put(root.Activity.DurationMs, Activity is Activity.Buzz buzz ? buzz.DurationMs : null);

        details.Put(root.TraceId, buzz.TraceHandle.TraceId);
        details.Put(root.SpanId, buzz.TraceHandle.SpanId);
        details.Put(root.ParentSpanId, buzz.TraceHandle.ParentSpanId);

        foreach (var (source, level) in buzz.Select((source, level) => (source, level)))
        {
            var builder = new DetailBuilder(root.Activity.State, level, details);
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