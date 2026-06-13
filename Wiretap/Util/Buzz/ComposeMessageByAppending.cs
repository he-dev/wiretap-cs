using System.Text;

namespace Wiretap.Util.Buzz;

public class ComposeMessageByAppending(string separator = "; ") : IComposeMessage
{
    public MessageTemplate From(IReadOnlyDictionary<string, object?> properties, params object?[] messagePartFeeds)
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

        GetMessageParts.From(properties, push, messagePartFeeds);

        return new(temp.ToString(), args.ToArray());
    }
}
