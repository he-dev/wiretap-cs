using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

[PublicAPI]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
public abstract class ComposeMessage : Attribute
{
    public abstract MessageTemplate From(ActivityStatus.Context context, params object?[] messagePartFeeds);
}

public static class GetComposeMessage
{
    private static readonly ConcurrentDictionary<Type, ComposeMessage> Cache = new();

    public static ComposeMessage FromAttributeOrDefault(Type activityType)
    {
        return Cache.GetOrAdd(activityType, type =>
            type.GetCustomAttribute<ComposeMessage>(inherit: true)
            ?? type.Assembly.GetCustomAttribute<ComposeMessage>()
            ?? Assembly.GetEntryAssembly()?.GetCustomAttribute<ComposeMessage>()
            ?? Default);
    }

    private static readonly ComposeMessage Default = new ComposeMessageByAppending();
}

// core: Implements a message schema where parts are appended in feed order and joined by a separator.
public class ComposeMessageByAppending(string separator = "; ") : ComposeMessage
{
    public override MessageTemplate From(ActivityStatus.Context context, params object?[] messagePartFeeds)
    {
        // note: Does not use LINQ for better performance.

        var temp = new StringBuilder(256);
        var args = new List<object?>(32);

        var push = new PushMessagePart((t, a) =>
        {
            if (!string.IsNullOrEmpty(t))
            {
                temp.Append(temp.Length > 0 ? separator : string.Empty);
                temp.Append(t);
                args.AddRange(a);
            }
        });

        GetMessageParts.From(context, push, messagePartFeeds);

        return new(temp.ToString(), args.ToArray());
    }
}

public delegate void PushMessagePart([StructuredMessageTemplate] string? message, params object?[] args);

public interface IMessagePartFeed
{
    void MessageParts(ActivityStatus.Context context, PushMessagePart push);
}

public class LastStatusMessageFeed([StructuredMessageTemplate] string? message, params object?[] args) : IMessagePartFeed
{
    public void MessageParts(ActivityStatus.Context context, PushMessagePart push)
    {
        push(message, args);
    }
}