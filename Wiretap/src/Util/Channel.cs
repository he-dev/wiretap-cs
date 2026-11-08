namespace Wiretap.Util;

public record Channel(string Value)
{
    private string Key => "activity.channel";

    public static readonly Channel Official = new Channel(nameof(Official).ToLower());
    public static readonly Channel Internal = new Channel(nameof(Internal).ToLower());

    public static implicit operator (string Key, string Value)(Channel channel) => (channel.Key, channel.Value);
}