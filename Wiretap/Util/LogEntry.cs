using System.Collections;
using Microsoft.Extensions.Logging;

namespace Wiretap.Util;

public sealed record LogEntry(
    LogLevel Level,
    MessageTemplate Message,
    IReadOnlyDictionary<string, object?> Properties,
    Exception? Exception = null
) : IReadOnlyDictionary<string, object?>
{
    public object? this[string key] => Properties[key];

    public IEnumerable<string> Keys => Properties.Keys;

    public IEnumerable<object?> Values => Properties.Values;

    public int Count => Properties.Count;

    public bool ContainsKey(string key) => Properties.ContainsKey(key);

    public bool TryGetValue(string key, out object? value) =>
        Properties.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() =>
        Properties.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
