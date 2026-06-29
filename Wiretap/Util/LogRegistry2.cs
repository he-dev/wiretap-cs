namespace Wiretap.Util;

// TODO: Draft only. If this survives, MessagePartMap and the current push delegates can move behind it.
public class LogRegistry2<TOptions> : IEnumerable<LogRegistry2<TOptions>.Item> where TOptions : class, new()
{
    // core: Linked storage keeps insertion order while still allowing named pop/duplicate checks.
    private readonly OrderedDictionary<PropertyName, Item> _entries = [];

    public bool Put(PropertyName name, object? value, Action<TOptions>? configure = null)
    {
        if (_entries.ContainsKey(name))
        {
            return false;
        }

        var options = new TOptions();
        configure?.Invoke(options);
        _entries[name] = Item.From(name, value, options);
        return true;
    }

    public Item? Pop(PropertyName name)
    {
        if (!_entries.Remove(name, out var item))
        {
            return null;
        }

        return item is Item.Null ? null : item;
    }

    public void Clear()
    {
        _entries.Clear();
    }

    public IEnumerator<Item> GetEnumerator()
    {
        return _entries.Values.Where(item => item is not Item.Null).GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public class Item(PropertyName name, object value, TOptions options)
    {
        public PropertyName Name { get; } = name;

        public object Value { get; } = value;

        public TOptions Options { get; } = options;

        public sealed class Null(PropertyName name, TOptions options) : Item(name, new ValueTuple(), options);

        public static Item From(PropertyName name, object? value, TOptions options)
        {
            return value is null ? new Null(name, options) : new Item(name, value, options);
        }
    }
}

public sealed class MessagePartOptions2
{
    public string? Label { get; set; }

    public string Separator { get; set; } = ": ";

    public string? Format { get; set; }
}

public sealed class LogPropertyOptions2
{
    public bool Cascade { get; set; }
}

public sealed class MessagePartRegistry2(Func<PropertyName, object?>? read = null) : LogRegistry2<MessagePartOptions2>
{
    private readonly Func<PropertyName, object?> _read = read ?? (_ => null);

    public bool Property(PropertyName name, Action<MessagePartOptions2>? configure = null)
    {
        return Put(name, _read(name), configure);
    }

    public bool Discrete(PropertyName name, object? value, Action<MessagePartOptions2>? configure = null)
    {
        return Put(name, value, configure);
    }
}

public sealed class LogPropertyRegistry2 : LogRegistry2<LogPropertyOptions2>
{
    public bool Register(PropertyName name, object? value, Action<LogPropertyOptions2>? configure = null)
    {
        return Put(name, value, configure);
    }
}

public interface ILogEntryBuilder
{
    void OnBuild(LogEntryBuilder log);
}

public sealed class LogEntryBuilder
(
    PropertyName root,
    LogPropertyRegistry2 logProperties,
    MessagePartRegistry2 messageParts
)
{
    public PropertyName Root { get; } = root;

    public void LogProperties(Action<LogPropertyRegistry2> configure)
    {
        configure(logProperties);
    }

    public void MessageParts(Action<MessagePartRegistry2> configure)
    {
        configure(messageParts);
    }
}

public static class LogRegistry2MessagePartExtensions
{
    public static MessageTemplate Text(this LogRegistry2<MessagePartOptions2>.Item item)
    {
        var options = item.Options;
        var label = options.Label == string.Empty ? item.Name.ToString() : options.Label;
        var placeholder = options.Format is null ? $"{item.Name:_}" : item.Name.ToString(options.Format, null);
        var template = label is null ? placeholder : $"{label}{options.Separator}{placeholder}";
        return new MessageTemplate(template, item.Value);
    }
}

file sealed class DraftImportDocument
{
    public sealed class Okay(int recordsParsed) : ILogEntryBuilder
    {
        public void OnBuild(LogEntryBuilder log)
        {
            log.LogProperties(properties => { properties.Register(log.Root.Activity.State.Append("records_parsed"), recordsParsed); });

            log.MessageParts(parts =>
            {
                parts.Property(log.Root.Activity.State.Append("records_parsed"), options => options.Label = "Records");
                parts.Discrete(log.Root.Activity.Name, "Import completed");
            });
        }
    }
}