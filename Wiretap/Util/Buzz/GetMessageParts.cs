using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Wiretap.Util.Buzz;

public static class GetMessageParts
{
    private static readonly ConcurrentDictionary<Type, Getter[]> Cache = new();

    public static void From(
        IReadOnlyDictionary<string, object?> properties,
        PushMessagePart push,
        params object?[] sources
    ) =>
        From(Configuration.Current.PropertyName, properties, push, sources);

    public static void From(
        PropertyName root,
        IReadOnlyDictionary<string, object?> properties,
        PushMessagePart push,
        params object?[] sources
    )
    {
        var get = new GetStateItem(name => properties.TryGetValue(name, out var value) ? value : null);

        foreach (var source in sources)
        {
            if (source is not null)
            {
                ByInterface(root, get, source, push);
                ByAttribute(root, source, push);
            }
        }
    }

    private static void ByInterface(PropertyName root, GetStateItem get, object source, PushMessagePart push)
    {
        if (source is IMessagePartFeed messagePartFeed)
        {
            messagePartFeed.MessageParts(root, get, push);
        }
    }

    private static void ByAttribute<T>(PropertyName root, T source, PushMessagePart push) where T : notnull
    {
        var getters = Cache.GetOrAdd(source.GetType(), DiscoverMessageParts);

        foreach (var getter in getters)
        {
            if (getter.GetValue(source) is { } value)
            {
                push(
                    root.Activity.State.Append(getter.PropertyName),
                    getter.Template(root.Activity.State),
                    value
                );
            }
        }
    }

    private static Getter[] DiscoverMessageParts(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        var messagePartGetters =
            from property in type.GetProperties(flags)
            let attr = property.GetCustomAttribute<MessagePart>()
            where attr is not null
            select new Getter(property.Name, attr, Getter.Compile(type, property));

        return [..messagePartGetters];
    }

    private static string TemplateFor(PropertyName prefix, string propertyName, MessagePart attr)
    {
        var key = prefix.Append(propertyName);

        if (attr.Label is null)
        {
            return $"{key:_}";
        }

        var label = attr.Label == string.Empty ? propertyName : attr.Label;
        return $"{label}{attr.Separator}{key:_}";
    }

    private sealed record Getter(string PropertyName, MessagePart Attribute, Func<object, object?> GetValue)
    {
        public string Template(PropertyName prefix)
        {
            return TemplateFor(prefix, PropertyName, Attribute);
        }

        public static Func<object, object?> Compile(Type type, PropertyInfo property)
        {
            if (!property.CanRead)
            {
                throw new InvalidOperationException($"The property '{type.Name}.{property.Name}' is marked with '{nameof(MessagePart)}' but does not have a getter.");
            }

            var source = Expression.Parameter(typeof(object), "source");
            var typedSource = Expression.Convert(source, type);
            var value = Expression.Property(typedSource, property);
            var boxedValue = Expression.Convert(value, typeof(object));
            return Expression.Lambda<Func<object, object?>>(boxedValue, source).Compile();
        }
    }
}
