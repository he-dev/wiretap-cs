using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using Wiretap.Util.Services;

namespace Wiretap.Util;

[PublicAPI]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
public class MessageTemplateSchema(string separator = "; ") : Attribute
{
    private static readonly ConcurrentDictionary<Type, MessageTemplateSchema> Cache = new();

    public static MessageTemplateSchema For(Type activityType)
    {
        return Cache.GetOrAdd(activityType, type =>
            type.GetCustomAttribute<MessageTemplateSchema>(inherit: true)
            ?? type.Assembly.GetCustomAttribute<MessageTemplateSchema>()
            ?? Assembly.GetEntryAssembly()?.GetCustomAttribute<MessageTemplateSchema>()
            ?? new MessageTemplateSchema());
    }

    // core: Implements a message-schema where parts are joined by the specified separator.
    public virtual MessageTemplate From(ActivityStatus.Context context, params object?[] messagePartFeeds)
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

public class MessageTemplateSuffix([StructuredMessageTemplate] string? message, params object?[] args) : IMessagePartFeed
{
    public void MessageParts(ActivityStatus.Context context, PushMessagePart push)
    {
        push(message, args);
    }
}
