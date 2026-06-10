namespace Wiretap.Util;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public class ScopeStateItem(string? name = null) : Attribute
{
    public string? Name { get; } = name;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public class FeedToStateItem(string? name = null) : Attribute
{
    public string? Name { get; } = name;

    public bool Cascade { get; init; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public class FeedToMessagePart(string? label = null) : Attribute
{
    public string? Label { get; } = label;

    public bool IncludeLabel { get; init; } = true;

    public string? Separator { get; init; } = ": ";
}
