namespace Wiretap.Util;

public sealed class DetailCollection : IEnumerable<KeyValuePair<string, object?>>
{
    private readonly OrderedDictionary<string, object?> _details = [];

    public void Put(PropertyName name, object? value)
    {
        Put(name.ToString(), value);
    }

    public void Put(string key, object? value)
    {
        _details[key] = value;
    }

    public bool ContainsKey(string key) => _details.ContainsKey(key);

    public object? GetValueOrDefault(string key)
    {
        return _details.GetValueOrDefault(key);
    }

    public object? GetValueOrDefault(PropertyName name)
    {
        return GetValueOrDefault(name.ToString());
    }

    public bool TryGetValue(string key, out object? value) => _details.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _details.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}