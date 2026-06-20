using JetBrains.Annotations;

namespace Wiretap.Util;

public sealed class MessagePartMap : IEnumerable<KeyValuePair<PropertyName, MessageTemplate>>
{
    private readonly OrderedDictionary<PropertyName, MessageTemplate> _entries = [];

    public void Push(
        PropertyName name,
        [StructuredMessageTemplate] string? message,
        params object?[] args
    ) =>
        Push(name, new MessageTemplate(message, args));

    public void Push(PropertyName name, MessageTemplate message)
    {
        if (message.Template is null || message.Args.Any(value => value is null))
        {
            return;
        }

        _entries[name] = message;
    }

    public MessageTemplate? Pop(PropertyName name)
    {
        return _entries.Remove(name, out var entry) ? entry : null;
    }

    public IEnumerator<KeyValuePair<PropertyName, MessageTemplate>> GetEnumerator() => _entries.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
