using JetBrains.Annotations;

namespace Wiretap.Util.Buzz;

public delegate void PushMessagePart(
    PropertyName name,
    [StructuredMessageTemplate] string? message,
    params object?[] args
);

public delegate object? GetStateItem(string name);

public interface IMessagePartFeed
{
    void MessageParts(PropertyName root, GetStateItem get, PushMessagePart push);
}
