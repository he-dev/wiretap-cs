using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Wiretap.Util.Buzz;

public static class AnnotatedProperties
{
    private static readonly ConcurrentDictionary<(Type Source, Type Annotation), Getter[]> Cache = new();

    public static void For<TAnnotation>(
        object source,
        Action<string, TAnnotation, object?> report
    ) where TAnnotation : Attribute
    {
        var getters = Cache.GetOrAdd((source.GetType(), typeof(TAnnotation)), Discover<TAnnotation>);

        foreach (var getter in getters)
        {
            report(getter.PropertyName, (TAnnotation)getter.Annotation, getter.GetValue(source));
        }
    }

    private static Getter[] Discover<TAnnotation>((Type Source, Type Annotation) key)
        where TAnnotation : Attribute
    {
        // todo: Warn through the diagnostic logger when annotated non-public properties are ignored.
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

        var getters =
            from property in key.Source.GetProperties(flags)
            let annotation = property.GetCustomAttribute<TAnnotation>()
            where annotation is not null
            select new Getter(property.Name, annotation, Compile(key.Source, property));

        return [.. getters];
    }

    private static Func<object, object?> Compile(Type type, PropertyInfo property)
    {
        if (!property.CanRead)
        {
            throw new InvalidOperationException($"The property '{type.Name}.{property.Name}' is marked with an annotation but does not have a getter.");
        }

        var source = Expression.Parameter(typeof(object), "source");
        var typedSource = Expression.Convert(source, type);
        var value = Expression.Property(typedSource, property);
        var boxedValue = Expression.Convert(value, typeof(object));
        return Expression.Lambda<Func<object, object?>>(boxedValue, source).Compile();
    }

    private sealed record Getter(string PropertyName, Attribute Annotation, Func<object, object?> GetValue);
}
