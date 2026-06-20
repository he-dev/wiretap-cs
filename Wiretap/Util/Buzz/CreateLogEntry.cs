using System.Text;

namespace Wiretap.Util.Buzz;

public sealed class CreateLogEntry
{
    private readonly PropertyName _root;
    private readonly Func<MessageContext, IReadOnlyList<MessagePartMap.Entry>> _arrangeMessageParts;
    private readonly Func<IReadOnlyList<MessagePartMap.Entry>, MessageTemplate> _joinMessageParts;

    private CreateLogEntry(
        PropertyName root,
        Func<MessageContext, IReadOnlyList<MessagePartMap.Entry>> arrangeMessageParts,
        Func<IReadOnlyList<MessagePartMap.Entry>, MessageTemplate> joinMessageParts
    )
    {
        _root = root;
        _arrangeMessageParts = arrangeMessageParts;
        _joinMessageParts = joinMessageParts;
    }

    public LogEntry From(ActivityStatus status, params object?[] sources)
    {
        // TODO: Replace this temporary source list once scopes expose the factory's exact inputs.
        var allSources = new object?[sources.Length + 1];
        sources.CopyTo(allSources, 0);
        allSources[^1] = status;

        var properties = CollectLogProperties(allSources);
        var messageParts = CollectMessageParts(properties, allSources);
        var context = new MessageContext(_root, properties, messageParts);
        var message = _joinMessageParts(_arrangeMessageParts(context));
        return new LogEntry(status.Level, message, properties, status.Exception);
    }

    private static Dictionary<string, object?> CollectLogProperties(object?[] sources) =>
        GetStateItems.From(sources);

    private MessagePartMap CollectMessageParts(
        IReadOnlyDictionary<string, object?> properties,
        object?[] sources
    )
    {
        var parts = new MessagePartMap();
        var push = new PushMessagePart(parts.Push);
        GetMessageParts.From(properties, push, sources);
        return parts;
    }

    public static CreateLogEntry By(Action<Builder>? configure = null)
    {
        var builder = new Builder();
        configure?.Invoke(builder);
        return builder.Build();
    }

    public sealed class Builder
    {
        private Func<MessageContext, IReadOnlyList<MessagePartMap.Entry>> _arrangeMessageParts =
            context => [.. context.Parts.Values];
        private Func<IReadOnlyList<MessagePartMap.Entry>, MessageTemplate> _joinMessageParts =
            JoinByAppending;

        public PropertyName Root { get; set; } = new(Parts: ["wiretap"]);

        public Builder ArrangeMessageParts(
            Func<MessageContext, IReadOnlyList<MessagePartMap.Entry>> arrange
        )
        {
            _arrangeMessageParts = arrange;
            return this;
        }

        public Builder JoinMessageParts(
            Func<IReadOnlyList<MessagePartMap.Entry>, MessageTemplate> join
        )
        {
            _joinMessageParts = join;
            return this;
        }

        internal CreateLogEntry Build() =>
            new(Root, _arrangeMessageParts, _joinMessageParts);

        private static MessageTemplate JoinByAppending(
            IReadOnlyList<MessagePartMap.Entry> entries
        )
        {
            var template = new StringBuilder(256);
            var args = new List<object?>(32);
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Message.Template))
                {
                    continue;
                }

                template.Append(template.Length == 0 ? string.Empty : "; ");
                template.Append(entry.Message.Template);
                args.AddRange(entry.Message.Args);
            }
            return new MessageTemplate(template.ToString(), [.. args]);
        }
    }
}

public sealed record MessageContext(
    PropertyName Root,
    IReadOnlyDictionary<string, object?> Properties,
    MessagePartMap Parts
);
