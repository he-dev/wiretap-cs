namespace Wiretap.Util;

[AttributeUsage(AttributeTargets.Class)]
public abstract class LastStatusPolicy : Attribute
{
    public class CanBeVoid : LastStatusPolicy;
}

public readonly struct LastStatusPolicySet
{
    public LastStatusPolicy.CanBeVoid? CanBeVoid { get; init; }
}
