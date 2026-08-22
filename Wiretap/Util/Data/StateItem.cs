namespace Wiretap.Util.Data;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public class StateItem : Attribute
{
    public string? Name { get; init; }

    public bool Cascade { get; init; }
}


