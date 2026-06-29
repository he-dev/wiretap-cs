using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public sealed class RemarkOptions
{
    public string? Label { get; set; }

    public string Separator { get; set; } = ": ";

    public string? Format { get; set; }

    public QuoteStyle QuoteStyle { get; set; } = QuoteStyle.Double;

    public QuoteMode QuoteMode { get; set; } = QuoteMode.Never;
}

public interface IRemarkSource
{
    void Remarks(RemarkBuilder remarks);
}

public sealed class RemarkBuilder(
    PropertyName root,
    IReadOnlyDictionary<string, object?> details,
    MessagePartMap remarks
)
{
    public void Add(PropertyName name, object? value, Action<RemarkOptions>? configure = null)
    {
        var options = new RemarkOptions();
        configure?.Invoke(options);

        var part = new PushMessagePart(_ => null, remarks).Discrete(root.Activity.State + name, value);
        if (options.Label is { } label)
        {
            part.Label(label).Separator(options.Separator);
        }
        if (options.Format is { } format)
        {
            part.Format(format);
        }
        part.Quote(options.QuoteMode, options.QuoteStyle);
    }

    public void Add(PropertyName name, Action<RemarkOptions>? configure = null)
    {
        Add(name, details.GetValueOrDefault((root.Activity.State + name).ToString()), configure);
    }
}
