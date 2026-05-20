using Wiretap.Util.Services;

namespace Wiretap.Util;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public class ScopeStateItem(string? name = null) : Attribute
{
    public string? Name { get; } = name;

    public static void From<T>(T source, AddStateItem add) where T : notnull
    {
        GetScopeStatePropertyValues.From(source, add);
    }
}