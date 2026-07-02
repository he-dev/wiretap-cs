using JetBrains.Annotations;
using Wiretap.Util.Data;

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

public sealed class RemarkBuilder(PropertyName root, DetailCollection details, RemarkCollection remarks)
{
    public PropertyName Root { get; } = root;

    public DetailCollection Details { get; } = details;

    public void Add(PropertyName name, object? value, Action<RemarkOptions>? configure = null)
    {
        var options = new RemarkOptions();
        configure?.Invoke(options);

        Add(Root.Activity.State + name, Render(Root.Activity.State + name, value, options), value);
    }

    public void Add(PropertyName name, Action<RemarkOptions>? configure = null)
    {
        var key = Root.Activity.State + name;
        Add(name, Details.GetValueOrDefault(key), configure);
    }

    public void Add(PropertyName name, [StructuredMessageTemplate] string? message, params object?[] args)
    {
        remarks.Put(name, new MessageTemplate(message, args));
    }

    private static string? Render(PropertyName name, object? value, RemarkOptions options)
    {
        if (value is null)
        {
            return null;
        }

        var quote = options.QuoteStyle switch
        {
            QuoteStyle.Double => '"',
            QuoteStyle.Single => '\'',
            _ => '"'
        };
        var shouldQuote = options.QuoteMode switch
        {
            QuoteMode.Never => false,
            QuoteMode.Auto => value.ToString()?.Any(char.IsWhiteSpace) == true,
            QuoteMode.Always => true,
            _ => false
        };

        value = options.Format switch { null => $"{value}", _ => $"{{{value}:{options.Format}}}" };
        value = shouldQuote ? $"{quote}{value}{quote}" : value;
        var label = options.Label ?? name.Parts.LastOrDefault() ?? name.ToString();
        return $"{label}{options.Separator}{value}";
    }
}
