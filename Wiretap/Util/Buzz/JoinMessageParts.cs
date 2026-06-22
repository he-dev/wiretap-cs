using System.Text;

namespace Wiretap.Util.Buzz;

public interface IJoinMessageParts
{
    MessageTemplate By(IReadOnlyList<MessageTemplate> entries);
}

public class JoinMessagePartsByAppending : IJoinMessageParts
{
    public MessageTemplate By(IReadOnlyList<MessageTemplate> entries)
    {
        var template = new StringBuilder(256);
        var args = new List<object?>(32);
        foreach (var message in entries)
        {
            if (string.IsNullOrEmpty(message.Template))
            {
                continue;
            }

            template.Append(template.Length == 0 ? string.Empty : "; ");
            template.Append(message.Template);
            args.AddRange(message.Args);
        }

        return new MessageTemplate(template.ToString(), [.. args]);
    }
}
