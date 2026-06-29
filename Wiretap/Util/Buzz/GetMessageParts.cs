using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Wiretap.Util.Buzz;

public static class GetMessageParts
{
    private static readonly ConcurrentDictionary<Type, Getter[]> Cache = new();

    public static MessagePartMap From(
        PropertyName root,
        GetLogProperty get,
        params object?[] sources
    )
    {
        var parts = new MessagePartMap();
        var push = new PushMessagePart(get, parts);

        foreach (var source in sources)
        {
            if (source is not null)
            {
                ByInterface(root, get, source, push);
                ByAttribute(root, source, push);
            }
        }

        return parts;
    }

    private static void ByInterface(PropertyName root, GetLogProperty get, object source, PushMessagePart push)
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
                var part = push.Discrete(root.Activity.State.Append(getter.PropertyName), value);
                if (getter.Attribute.Label is { } label)
                {
                    part.Label(label).Separator(getter.Attribute.Separator ?? ": ");
                }
            }
        }
    }

    private static Getter[] DiscoverMessageParts(Type type)
    {
        // todo: Warn through the diagnostic logger when annotated non-public properties are ignored.
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

        var messagePartGetters =
            from property in type.GetProperties(flags)
            let attr = property.GetCustomAttribute<MessagePart>()
            where attr is not null
            select new Getter(property.Name, attr, Getter.Compile(type, property));

        return [..messagePartGetters];
    }

    private sealed record Getter(string PropertyName, MessagePart Attribute, Func<object, object?> GetValue)
    {
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
