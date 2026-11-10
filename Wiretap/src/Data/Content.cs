namespace Wiretap.Data;

public record Content(string Value)
{
    public string Key => "content";

    public static readonly Content Baseline = new Content("baseline");
    public static readonly Content Internal = new Content("internal");
    public static readonly Content Metadata = new Content("metadata");

    public static implicit operator (string Key, string Value)(Content content) => (content.Key, content.Value);
}