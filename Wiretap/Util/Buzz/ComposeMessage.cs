using JetBrains.Annotations;

namespace Wiretap.Util.Buzz;

public interface IComposeMessage
{
    MessageTemplate From(IReadOnlyDictionary<string, object?> properties, params object?[] messagePartFeeds);
}

// core: Implements a message schema where parts are appended in feed order and joined by a separator.

public delegate void PushMessagePart([StructuredMessageTemplate] string? message, params object?[] args);

public delegate object? GetStateItem(string name);

public interface IMessagePartFeed
{
    void MessageParts(PropertyName root, GetStateItem get, PushMessagePart push);
}
