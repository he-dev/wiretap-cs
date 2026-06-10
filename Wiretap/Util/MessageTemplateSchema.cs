using System.Text;
using JetBrains.Annotations;
using Wiretap.Util.Services;

namespace Wiretap.Util;

[PublicAPI]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
public class MessageTemplateSchema(string separator = "; ") : Attribute
{
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

[PublicAPI]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
public abstract class MessageTemplatePrefix : Attribute, IMessagePartFeed
{
    public abstract void MessageParts(ActivityStatus.Context context, PushMessagePart push);

    public class Full : MessageTemplatePrefix
    {
        public override void MessageParts(ActivityStatus.Context context, PushMessagePart push)
        {
            push("{ActivityRole}: {Activity}[{ActivityStatus}]", context.ActivityRole, context.Activity, context.ActivityStatus);
            push("Elapsed: {ElapsedMs:N0} ms", context.ElapsedMs);
        }
    }

    public class Compact : MessageTemplatePrefix
    {
        public override void MessageParts(ActivityStatus.Context context, PushMessagePart push)
        {
            push("{ActivityRole}: {Activity}[{ActivityStatus}] in {ElapsedMs:N0} ms", context.ActivityRole, context.Activity, context.ActivityStatus, context.ElapsedMs);
        }
    }
}

public class MessageTemplateSuffix([StructuredMessageTemplate] string? message, params object?[] args) : IMessagePartFeed
{
    public void MessageParts(ActivityStatus.Context context, PushMessagePart push)
    {
        push(message, args);
    }
}
