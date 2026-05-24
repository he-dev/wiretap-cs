using System.Text;
using JetBrains.Annotations;

namespace Wiretap.Util;

[PublicAPI]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
public class MessageTemplateSchema(string separator = "; ") : Attribute
{
    // core: Implements a message-schema where parts are joined by the specified separator.
    public virtual MessageTemplate From(ActivityStatus.Context context, params IWithMessageParts?[] messageParts)
    {
        // note: Does not use LINQ for better performance.

        var temp = new StringBuilder(256);
        var args = new List<object?>(32);

        var append = new AppendMessagePart((t, a) =>
        {
            if (!string.IsNullOrEmpty(t))
            {
                temp.Append(temp.Length > 0 ? separator : string.Empty);
                temp.Append(t);
                args.AddRange(a);
            }
        });

        foreach (var item in messageParts)
        {
            item?.MessageParts(context, append);
        }

        return new(temp.ToString(), args.ToArray());
    }
}

public delegate void AppendMessagePart([StructuredMessageTemplate] string? message, params object?[] args);

public interface IWithMessageParts
{
    void MessageParts(ActivityStatus.Context context, AppendMessagePart append);
}

[PublicAPI]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
public abstract class MessageTemplatePrefix : Attribute, IWithMessageParts
{
    public abstract void MessageParts(ActivityStatus.Context context, AppendMessagePart append);

    public class Full : MessageTemplatePrefix
    {
        public override void MessageParts(ActivityStatus.Context context, AppendMessagePart append)
        {
            append("{ActivityRole}: {Activity}[{ActivityStatus}]", context.ActivityRole, context.Activity, context.ActivityStatus);
            append("Elapsed: {ElapsedMs:N0} ms", context.ElapsedMs);
        }
    }

    public class Compact : MessageTemplatePrefix
    {
        public override void MessageParts(ActivityStatus.Context context, AppendMessagePart append)
        {
            append("{ActivityRole}: {Activity}[{ActivityStatus}] in {ElapsedMs:N0} ms", context.ActivityRole, context.Activity, context.ActivityStatus, context.ElapsedMs);
        }
    }
}

public class MessageTemplateSuffix([StructuredMessageTemplate] string? message, params object?[] args) : IWithMessageParts
{
    public void MessageParts(ActivityStatus.Context context, AppendMessagePart append)
    {
        append(message, args);
    }
}