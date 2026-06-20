using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Wiretap.Util.Buzz;

public delegate void PushLogProperty(string key, object? value);

public interface ILogPropertyFeed
{
    void LogProperties(PropertyName name, PushLogProperty push);
}

public static class GetStateItems
{
    private static readonly ConcurrentDictionary<Type, Getter[]> Cache = new();

    public static Dictionary<string, object?> From(params object?[] sources) =>
        From(Configuration.Current.PropertyName, sources);

    public static Dictionary<string, object?> From(PropertyName root, params object?[] sources)
    {
        // note: Using a list rather than Enumerable.Concat for performance reasons.

        var stateItems = new Dictionary<string, object?>();
        var pushStateItem = new PushLogProperty((key, value) => stateItems[key] = value);

        foreach (var source in sources)
        {
            if (source is not null)
            {
                ByInterface(root, source, pushStateItem);
                ByAttribute(root, source, pushStateItem);
            }
        }

        return stateItems;
    }

    private static void ByInterface(PropertyName root, object source, PushLogProperty push)
    {
        if (source is ILogPropertyFeed logPropertyFeed)
        {
            logPropertyFeed.LogProperties(root, push);
        }
    }

    private static void ByAttribute<T>(PropertyName root, T source, PushLogProperty push) where T : notnull
    {
        var getters = Cache.GetOrAdd(source.GetType(), DiscoverStateItems);

        foreach (var getter in getters)
        {
            if (getter.GetValue(source) is { } value)
            {
                push(root.Activity.State.Append(getter.Key), value);
            }
        }
    }

    private static Getter[] DiscoverStateItems(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        var stateItemGetters =
            from property in type.GetProperties(flags)
            let attr = property.GetCustomAttribute<StateItem>()
            where attr is not null
            select new Getter(attr.Name ?? property.Name, Getter.Compile(type, property));

        return [..stateItemGetters];
    }

    private sealed record Getter(string Key, Func<object, object?> GetValue)
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
