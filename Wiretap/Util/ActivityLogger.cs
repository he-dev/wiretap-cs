using Microsoft.Extensions.Logging;
using Wiretap.Util.Buzz;
using Wiretap.Util.Data;

namespace Wiretap.Util;

public sealed class ActivityLogger(ILogger logger) : IStatusObserver
{
    public void OnStatusChange(IBuzz buzz, TimeSpan duration)
    {
        var configuration = Util.Configuration.Resolve(buzz);

        if (buzz.Status is ActivityStatusRole.ILast) { }

        switch (buzz.Status.LogStatusPolicy())
        {
            case LogStatusPolicy.Auto:
                break;
            case LogStatusPolicy.Sure:
                break;
            case LogStatusPolicy.Nope:
                break;
            default:
                break;
        }
    }

    private void LogStatus(IBuzz buzz, TimeSpan duration)
    {
        var root = Configuration.Root;
        var status = buzz.Status;


        var details = new DetailCollection();
        details.Put(root.Activity.Name, buzz.Name);
        details.Put(root.Activity.Status.Code, status.Code);
        details.Put(root.Activity.Status.Role, status switch
        {
            ActivityStatusRole.IFirst => "first",
            ActivityStatusRole.ILast => "last",
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

        var message = Configuration.ComposeMessage.From(root, details, remarks);
        Logger.Log(
            status.Level,
            details
                .Where(pair => pair.Value is not null)
                .ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            message,
            status.Exception
        );
    }

    public void Log(LogLevel level, IReadOnlyDictionary<string, object?> details, MessageTemplate message, Exception? exception = null)
    {
        using var scope = details.Count == 0 ? null : logger.BeginScope(details);
        logger.Log(level, exception, message.Template, message.Args);
    }

}