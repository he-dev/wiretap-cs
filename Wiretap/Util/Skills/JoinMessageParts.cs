using System.Text;
using JetBrains.Annotations;

namespace Wiretap.Util.Skills;

public delegate void AppendMessagePart([StructuredMessageTemplate] string? message, params object?[] args);

public interface IWithMessageParts
{
    void MessageParts(ActivityStatus.Context context, AppendMessagePart append);
}

// core: Implements a message-schema where parts are joined by the specified separator.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
public class JoinMessageParts(string separator = "; ") : Attribute
{
    public virtual MessageTemplate From(ActivityStatus.Context context, params IWithMessageParts?[] messageParts)
    {
        // note: Does not use LINQ for better performance.

        var msgs = new StringBuilder(256);
        var args = new List<object?>(32);

        var append = new AppendMessagePart((t, a) =>
        {
            if (msgs.Length > 0)
            {
                msgs.Append(separator);
            }

            msgs.Append(t);
            args.AddRange(a);
        });

        foreach (var item in messageParts)
        {
            item?.MessageParts(context, append);
        }

        return new(msgs.ToString(), args.ToArray());
    }
}