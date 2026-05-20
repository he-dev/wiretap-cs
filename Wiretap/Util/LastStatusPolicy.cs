namespace Wiretap.Util;

[AttributeUsage(AttributeTargets.Class)]
public abstract class LastStatusPolicy : Attribute
{
    public class CanBeVoid : LastStatusPolicy;

    // core: Mutes leaks except fails.
    public class MuteLeaks : LastStatusPolicy
    {
        // core: Does not log leaks.
        public bool Silently { get; init; }
    }
}

public readonly struct LastStatusPolicySet
{
    public LastStatusPolicy.CanBeVoid? CanBeVoid { get; init; }
    public LastStatusPolicy.MuteLeaks? MuteLeaks { get; init; }
}