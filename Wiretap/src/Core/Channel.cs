namespace Wiretap.Core;

public record Channel(string Key, string Value)
{
    public static readonly Channel Official = new Channel("activity.channel", nameof(Official));
    public static readonly Channel Internal = new Channel("activity.channel", nameof(Internal));

    public static implicit operator (string Key, string Value)(Channel channel) => (channel.Key, channel.Value);
}