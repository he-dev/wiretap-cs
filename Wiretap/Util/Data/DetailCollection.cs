namespace Wiretap.Util;

public sealed class DetailCollection : IEnumerable<KeyValuePair<PropertyName, object?>>
{
    private readonly OrderedDictionary<PropertyName, object?> _details = [];

    public void Put(PropertyName name, object? value)
    {
        _details[name] = value;
    }

    public bool ContainsKey(PropertyName key) => _details.ContainsKey(key);

    public object? GetValueOrDefault(PropertyName key)
    {
        return _details.GetValueOrDefault(key);
    }

    public bool TryGetValue(PropertyName key, out object? value) => _details.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<PropertyName, object?>> GetEnumerator() => _details.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}