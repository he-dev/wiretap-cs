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
            temp.Append(temp.Length > 0 ? separator : string.Empty);
            temp.Append(t);
            args.AddRange(a);
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