using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Wiretap.Util.Buzz;

internal static class AnnotatedStateItems
{
    private static readonly ConcurrentDictionary<Type, Getter[]> Cache = new();

    public static void PushFromSelf(PropertyName root, PushLogProperty push, object source)
    {
        Push(root, push, source, cascadingOnly: false);
    }

    public static void PushFromAncestors(PropertyName root, PushLogProperty push, IEnumerable<object> sources)
    {
        foreach (var source in sources)
        {
            Push(root, push, source, cascadingOnly: true);
        }
    }

    private static void Push(PropertyName root, PushLogProperty push, object source, bool cascadingOnly)
    {
        foreach (var getter in Cache.GetOrAdd(source.GetType(), Discover))
        {
            if ((!cascadingOnly || getter.Cascade) && getter.GetValue(source) is { } value)
            {
                push(root.Append(getter.Name), value);
            }
        }
    }

    private static Getter[] Discover(Type type)
    {
        // TODO: Warn through the future diagnostic logger when non-public properties are annotated.
        return
        [
            ..from property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
              let annotation = property.GetCustomAttribute<StateItem>()
              where annotation is not null
              select new Getter(annotation.Name ?? property.Name, annotation.Cascade, Compile(type, property))
        ];
    }

    private static Func<object, object?> Compile(Type type, PropertyInfo property)
    {
        var source = Expression.Parameter(typeof(object), "source");
        var value = Expression.Property(Expression.Convert(source, type), property);
        return Expression.Lambda<Func<object, object?>>(Expression.Convert(value, typeof(object)), source).Compile();
    }

    private sealed record Getter(string Name, bool Cascade, Func<object, object?> GetValue);
}
