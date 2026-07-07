using System.Collections.Concurrent;

namespace Wiretap.Util.Buzz2;

public static class GetActivityName
{
    private static readonly ConcurrentDictionary<Type, string> Cache = new();

    public static string For(Type type) => Cache.GetOrAdd(type, Discover);

    private static string Discover(Type type)
    {
        var names = new Stack<string>();

        for (var current = type; current is not null; current = current.DeclaringType)
        {
            names.Push(current.Name);
        }

        return string.Join(".", names);
    }
}