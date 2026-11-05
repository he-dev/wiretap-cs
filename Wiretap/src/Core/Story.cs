namespace Wiretap.Core;

public record Story(string Key, string Value)
{
    public static readonly Story Buzz = new Story("plot.story", nameof(Buzz));
    public static readonly Story Core = new Story("plot.story", nameof(Core));

    public static implicit operator (string Key, string Value)(Story story) => (story.Key, story.Value);
}