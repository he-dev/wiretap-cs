using JetBrains.Annotations;

namespace Wiretap.Util.Buzz;

public delegate void PushMessagePart(
    PropertyName name,
    [StructuredMessageTemplate] string? message,
    params object?[] args
);

public delegate object? GetLogProperty(string name);

public interface IMessagePartFeed
{
    void MessageParts(PropertyName root, GetLogProperty get, PushMessagePart push);
}
