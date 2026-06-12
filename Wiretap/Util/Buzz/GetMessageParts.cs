using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Wiretap.Util.Buzz;

public static class GetMessageParts
{
    private static readonly ConcurrentDictionary<Type, Getter[]> Cache = new();

    public static void From(ActivityStatus.Context context, PushMessagePart push, params object?[] sources)
    {
        foreach (var source in sources)
        {
            if (source is not null)
            {
                ByInterface(context, source, push);
                ByAttribute(source, push);
            }
        }
    }

    private static void ByInterface(ActivityStatus.Context context, object source, PushMessagePart push)
    {
        if (source is IMessagePartFeed messagePartFeed)
        {
            messagePartFeed.MessageParts(context, push);
        }
    }

    private static void ByAttribute<T>(T source, PushMessagePart push) where T : notnull
    {
        var getters = Cache.GetOrAdd(source.GetType(), DiscoverMessageParts);

        foreach (var getter in getters)
        {
            if (getter.GetValue(source) is { } value)
            {
                push(getter.Template, value);
            }
        }
    }

    private static Getter[] DiscoverMessageParts(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        var messagePartGetters =
            from property in type.GetProperties(flags)
            let attr = property.GetCustomAttribute<FeedToMessagePart>()
            where attr is not null
            select new Getter(TemplateFor(property, attr), Getter.Compile(type, property));

        return [..messagePartGetters];
    }

    private static string TemplateFor(PropertyInfo property, FeedToMessagePart attr)
    {
        if (!attr.IncludeLabel)
        {
            return $"{{{property.Name}}}";
        }

        var label = attr.Label ?? property.Name;
        return $"{label}{attr.Separator}{{{property.Name}}}";
    }

    private sealed record Getter(string Template, Func<object, object?> GetValue)
    {
        public static Func<object, object?> Compile(Type type, PropertyInfo property)
        {
            if (!property.CanRead)
            {
                throw new InvalidOperationException($"The property '{type.Name}.{property.Name}' is marked with '{nameof(FeedToMessagePart)}' but does not have a getter.");
            }

            var source = Expression.Parameter(typeof(object), "source");
            var typedSource = Expression.Convert(source, type);
            var value = Expression.Property(typedSource, property);
            var boxedValue = Expression.Convert(value, typeof(object));
            return Expression.Lambda<Func<object, object?>>(boxedValue, source).Compile();
        }
    }
}
