namespace Wiretap.Util.Data;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public class Detail(string? name = null) : Attribute
{
    public string? Name { get; } = name;

    public bool Cascade { get; init; }
}


