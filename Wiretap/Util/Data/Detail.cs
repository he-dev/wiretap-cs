namespace Wiretap.Util.Data;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public class Detail : Attribute
{
    public string? Name { get; init; }

    public bool Cascade { get; init; }
}


