namespace Wiretap.Core;

public record Story(string Key, string Value)
{
    public static readonly Story System = new Story("story", nameof(System));
    public static readonly Story Domain = new Story("story", nameof(Domain));

    public static implicit operator (string Key, string Value)(Story story) => (story.Key, story.Value);
}