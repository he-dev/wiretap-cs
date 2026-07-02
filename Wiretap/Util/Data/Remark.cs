namespace Wiretap.Util.Data;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public class Remark(string? label = null) : Attribute
{
    public string? Label { get; } = label;

    public string? Separator { get; init; } = ": ";

    public string? Format { get; init; }

    public QuoteStyle QuoteStyle { get; init; } = QuoteStyle.Double;

    public QuoteMode QuoteMode { get; init; } = QuoteMode.Never;
}

public enum QuoteStyle
{
    Double,
    Single,
}

public enum QuoteMode
{
    Never,
    Auto,
    Always,
}