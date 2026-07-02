namespace Wiretap.Util.Data;

public sealed class RemarkCollection : IEnumerable<KeyValuePair<PropertyName, MessageTemplate>>
{
    private readonly OrderedDictionary<PropertyName, MessageTemplate> _remarks = [];

    public void Put(PropertyName name, MessageTemplate remark)
    {
        if (remark.Template is null || remark.Args.Any(value => value is null))
        {
            return;
        }

        _remarks[name] = remark;
    }

    public MessageTemplate? Pop(PropertyName name)
    {
        return _remarks.Remove(name, out var remark) ? remark : null;
    }

    public IEnumerator<KeyValuePair<PropertyName, MessageTemplate>> GetEnumerator() => _remarks.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}