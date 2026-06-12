namespace Wiretap.Meta;

internal static class AlsoExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
