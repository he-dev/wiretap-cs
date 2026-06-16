namespace Wiretap.Util;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public class StateItem(string? name = null) : Attribute
{
    public string? Name { get; } = name;

    public bool Cascade { get; init; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public class MessagePart(string? label = null) : Attribute
{
    public string? Label { get; } = label;

    public string? Separator { get; init; } = ": ";
}
