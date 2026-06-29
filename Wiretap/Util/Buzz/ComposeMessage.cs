using System.Collections.Immutable;
using JetBrains.Annotations;

namespace Wiretap.Util.Buzz;

public delegate object? GetLogProperty(string name);

public delegate void MessagePartRegistration(PropertyName root, GetLogProperty get, PushMessagePart push);

public interface IMessagePartFeed
{
    void MessageParts(PropertyName root, GetLogProperty get, PushMessagePart push);
}

// TODO: Replace the overlapping message pipeline in CreateLogEntry after configuration owns the composition recipe.
public sealed record ComposeMessage
{
    private ComposeMessage
    (
        ImmutableArray<MessagePartRegistration> include,
        IArrangeMessageParts arrange,
        IJoinMessageParts join
    )
    {
        Include = include;
        Arrange = arrange;
        Join = join;
    }

    private ImmutableArray<MessagePartRegistration> Include { get; }

    private IArrangeMessageParts Arrange { get; }

    private IJoinMessageParts Join { get; }

    public static ComposeMessage Build(Action<ComposeMessageBuilder> configure)
    {
        var builder = new ComposeMessageBuilder();
        configure(builder);
        return builder.Build();
    }

    public MessageTemplate From
    (
        PropertyName root,
        IReadOnlyDictionary<string, object?> properties,
        Activity activity
    )
    {
        var get = new GetLogProperty(properties.GetValueOrDefault);
        var parts = GetMessageParts.From(root, get, activity, activity.Status);
        var push = new PushMessagePart(get, parts);

        foreach (var include in Include)
        {
            include(root, get, push);
        }

        return Join.By(Arrange.By(root, parts));
    }

    public sealed class ComposeMessageBuilder
    {
        private readonly ImmutableArray<MessagePartRegistration>.Builder _include = ImmutableArray.CreateBuilder<MessagePartRegistration>();
        private IArrangeMessageParts? _arrange;
        private IJoinMessageParts? _join;

        public ComposeMessageBuilder Include(params MessagePartRegistration[] registrations)
        {
            _include.AddRange(registrations);
            return this;
        }

        public ComposeMessageBuilder Arrange(IArrangeMessageParts arrange)
        {
            _arrange = arrange;
            return this;
        }

        public ComposeMessageBuilder Join(IJoinMessageParts join)
        {
            _join = join;
            return this;
        }

        internal ComposeMessage Build()
        {
            return new ComposeMessage(
                _include.ToImmutable(),
                _arrange ?? throw new InvalidOperationException("Message arrangement is not configured."),
                _join ?? throw new InvalidOperationException("Message joining is not configured.")
            );
        }
    }
}

public sealed class PushMessagePart(GetLogProperty get, MessagePartMap parts)
{
    public MessagePartBuilder Property(PropertyName name)
    {
        return new MessagePartBuilder(name, get(name), parts.Push);
    }

    public MessagePartBuilder Discrete(PropertyName name, object? value)
    {
        return new MessagePartBuilder(name, value, parts.Push);
    }

    public MessagePartBuilder Discrete
    (
        PropertyName name,
        [StructuredMessageTemplate] string? message,
        params object?[] args
    )
    {
        if (args.Length == 0)
        {
            return Discrete(name, (object?)message);
        }

        return new MessagePartBuilder(name, args.Length == 1 ? args[0] : null, parts.Push)
            .Template(message, args);
    }
}

public sealed class MessagePartBuilder
{
    private readonly Action<PropertyName, MessageTemplate> _push;
    private readonly object? _value;
    private string? _format;
    private string? _label;
    private QuoteStyle _quoteStyle = QuoteStyle.Double;
    private QuoteMode _quoteMode = QuoteMode.Never;
    private string _separator = ": ";

    internal MessagePartBuilder(PropertyName name, object? value, Action<PropertyName, MessageTemplate> push)
    {
        Name = name;
        _value = value;
        _push = push;
        Template(value?.ToString());
    }

    private PropertyName Name { get; }

    public MessagePartBuilder Label(string label)
    {
        _label = label == string.Empty ? Name.Parts.LastOrDefault() ?? Name.ToString() : label;
        Render();
        return this;
    }

    public MessagePartBuilder Separator(string separator)
    {
        _separator = separator;
        Render();
        return this;
    }

    public MessagePartBuilder Format(string format)
    {
        _format = format;
        Render();
        return this;
    }

    public MessagePartBuilder Quote(QuoteMode mode, QuoteStyle style = QuoteStyle.Double)
    {
        _quoteMode = mode;
        _quoteStyle = style;
        Render();
        return this;
    }

    public MessagePartBuilder Template([StructuredMessageTemplate] string? message, params object?[] args)
    {
        _push(Name, new MessageTemplate(message, args));
        return this;
    }

    private void Render()
    {
        var placeholder = _format is null ? $"{Name:_}" : Name.ToString(_format, null);
        var quote = _quoteStyle switch
        {
            QuoteStyle.Double => '"',
            QuoteStyle.Single => '\'',
            _ => '"'
        };
        var shouldQuote = _quoteMode switch
        {
            QuoteMode.Never => false,
            QuoteMode.Auto => _value?.ToString()?.Any(char.IsWhiteSpace) == true,
            QuoteMode.Always => true,
            _ => false
        };

        placeholder = shouldQuote ? $"{quote}{placeholder}{quote}" : placeholder;

        Template(
            _label is null ? placeholder : $"{_label}{_separator}{placeholder}",
            _value
        );
    }
}
