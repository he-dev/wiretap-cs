using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Wiretap.Util.Buzz;

public delegate void PushLogProperty(string key, object? value);

public interface ILogPropertySource
{
    void LogProperties(PropertyName name, PushLogProperty push);
}

public static class GetLogProperties
{
    private static readonly ConcurrentDictionary<Type, Getter[]> Cache = new();

    public static Dictionary<string, object?> From(PropertyName root, params object?[] sources)
    {
        var properties = new Dictionary<string, object?>();
        var push = PushTo(properties);

        foreach (var source in sources)
        {
            if (source is not null)
            {
                ByInterface(root, source, push);
                ByAttribute(root.Activity.State, source, push);
            }
        }

        return properties;
    }

    private static void ByInterface(PropertyName root, object source, PushLogProperty push)
    {
        if (source is ILogPropertySource logPropertyFeed)
        {
            logPropertyFeed.LogProperties(root, push);
        }
    }

    private static PushLogProperty PushTo(Dictionary<string, object?> properties)
    {
        return (name, value) =>
        {
            if (value is not null)
            {
                properties[name] = value;
            }
        };
    }

    internal static void ByAttribute
    (
        PropertyName root,
        object source,
        PushLogProperty push,
        bool cascadingOnly = false
    )
    {
        var getters = Cache.GetOrAdd(source.GetType(), DiscoverStateItems);

        foreach (var getter in getters)
        {
            if ((!cascadingOnly || getter.Cascade) && getter.GetValue(source) is { } value)
            {
                push(root.Append(getter.Name), value);
            }
        }
    }

    private static Getter[] DiscoverStateItems(Type type)
    {
        // todo: Warn through the diagnostic logger when annotated non-public properties are ignored.
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

        var stateItemGetters =
            from property in type.GetProperties(flags)
            let attr = property.GetCustomAttribute<StateItem>()
            where attr is not null
            select new Getter(attr.Name ?? property.Name, attr.Cascade, Getter.Compile(type, property));

        return [..stateItemGetters];
    }

    private sealed record Getter(string Name, bool Cascade, Func<object, object?> GetValue)
    {
        public static Func<object, object?> Compile(Type type, PropertyInfo property)
        {
            if (!property.CanRead)
            {
                throw new InvalidOperationException($"The property '{type.Name}.{property.Name}' is marked with '{nameof(StateItem)}' but does not have a getter.");
            }

            var source = Expression.Parameter(typeof(object), "source");
            var typedSource = Expression.Convert(source, type);
            var value = Expression.Property(typedSource, property);
            var boxedValue = Expression.Convert(value, typeof(object));
            return Expression.Lambda<Func<object, object?>>(boxedValue, source).Compile();
        }
    }
}
