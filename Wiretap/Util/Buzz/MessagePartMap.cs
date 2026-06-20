using JetBrains.Annotations;

namespace Wiretap.Util.Buzz;

public sealed class MessagePartMap : Dictionary<PropertyName, MessagePartMap.Entry>
{
    public sealed record Entry(PropertyName Name, MessageTemplate Message);

    public void Push(
        PropertyName name,
        [StructuredMessageTemplate] string? message,
        params object?[] args
    ) =>
        Push(name, new MessageTemplate(message, args));

    public void Push(PropertyName name, MessageTemplate message) =>
        this[name] = new Entry(name, message);

    public Entry? Pop(PropertyName name) =>
        Remove(name, out var entry) ? entry : null;
}
