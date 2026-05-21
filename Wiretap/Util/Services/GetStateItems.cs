using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Wiretap.Util.Services;

public delegate void AddStateItem(string key, object? value);

public interface IWithStateItems
{
    void StateItems(AddStateItem add);
}

public static class GetStateItems
{
    private static readonly ConcurrentDictionary<Type, Getter[]> Cache = new();

    public static IEnumerable<KeyValuePair<string, object?>> From(params object?[] sources)
    {
        // note: Using a list rather than Enumerable.Concat for performance reasons.

        var stateItems = new List<KeyValuePair<string, object?>>(32);
        var addStateItem = new AddStateItem((key, value) => stateItems.Add(new(key, value)));

        foreach (var source in sources)
        {
            if (source is not null)
            {
                ByInterface(source, addStateItem);
                ByAttribute(source, addStateItem);
            }
        }

        return stateItems;
    }

    private static void ByInterface(object source, AddStateItem add)
    {
        if (source is IWithStateItems withStateItems)
        {
            withStateItems.StateItems(add);
        }
    }

    private static void ByAttribute<T>(T source, AddStateItem add) where T : notnull
    {
        var getters = Cache.GetOrAdd(source.GetType(), DiscoverStateItems);

        foreach (var getter in getters)
        {
            if (getter.GetValue(source) is { } value)
            {
                add(getter.Key, value);
            }
        }
    }

    private static Getter[] DiscoverStateItems(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        var stateItemGetters =
            from property in type.GetProperties(flags)
            let attr = property.GetCustomAttribute<ScopeStateItem>()
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
                throw new InvalidOperationException($"The property '{type.Name}.{property.Name}' is marked with '{nameof(ScopeStateItem)}' but does not have a getter.");
            }

            var source = Expression.Parameter(typeof(object), "source");
            var typedSource = Expression.Convert(source, type);
            var value = Expression.Property(typedSource, property);
            var boxedValue = Expression.Convert(value, typeof(object));
            return Expression.Lambda<Func<object, object?>>(boxedValue, source).Compile();
        }
    }
}