using System.Text;

namespace Wiretap.Util.Buzz;

public sealed class CreateLogEntry
{
    private readonly PropertyName _root;
    private readonly Func<MessageContext, IReadOnlyList<MessagePartMap.Entry>> _arrangeMessageParts;
    private readonly Func<IReadOnlyList<MessagePartMap.Entry>, MessageTemplate> _joinMessageParts;

    public PropertyName Root => _root;

    private CreateLogEntry
    (
        PropertyName root,
        Func<MessageContext, IReadOnlyList<MessagePartMap.Entry>> arrangeMessageParts,
        Func<IReadOnlyList<MessagePartMap.Entry>, MessageTemplate> joinMessageParts
    )
    {
        _root = root;
        _arrangeMessageParts = arrangeMessageParts;
        _joinMessageParts = joinMessageParts;
    }

    public LogEntry From(ActivityScope scope, ActivityStatus status)
    {
        var properties = CollectLogProperties(scope, status);
        var messageParts = CollectMessageParts(properties, scope, status);
        var context = new MessageContext(_root, properties, messageParts);
        var message = _joinMessageParts(_arrangeMessageParts(context));
        return new LogEntry(status.Level, message, properties, status.Exception);
    }

    private Dictionary<string, object?> CollectLogProperties(ActivityScope scope, ActivityStatus status)
    {
        var properties = new Dictionary<string, object?>();
        var push = new PushLogProperty((name, value) =>
        {
            if (value is not null)
            {
                properties[name] = value;
            }
        });

        AnnotatedStateItems.PushFromAncestors(
            _root.Activity.State,
            push,
            scope.Reverse().SkipLast(1).Select(x => x.Activity)
        );

        foreach (var source in new object[] { scope, scope.Activity, status })
        {
            if (source is ILogPropertySource feed)
            {
                feed.LogProperties(_root, push);
            }
        }

        AnnotatedStateItems.PushFromSelf(_root.Activity.State, push, scope.Activity);
        AnnotatedStateItems.PushFromSelf(_root.Activity.State, push, status);
        return properties;
    }

    private MessagePartMap CollectMessageParts(
        IReadOnlyDictionary<string, object?> properties,
        ActivityScope scope,
        ActivityStatus status
    )
    {
        var parts = new MessagePartMap();
        var push = new PushMessagePart(parts.Push);
        GetMessageParts.From(_root, properties, push, scope, scope.Activity, status);
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
        private Func<MessageContext, IReadOnlyList<MessagePartMap.Entry>> _arrangeMessageParts = context =>
        [
            ..new[]
            {
                context.Parts.Pop(context.Root.Activity.Name),
                context.Parts.Pop(context.Root.Activity.DurationMs),
            }.OfType<MessagePartMap.Entry>(),
            ..context.Parts
                .OrderBy(x => x.Key.ToString(), StringComparer.Ordinal)
                .Select(x => x.Value),
        ];

        private Func<IReadOnlyList<MessagePartMap.Entry>, MessageTemplate> _joinMessageParts = JoinByAppending;

        public PropertyName Root { get; set; } = new(Parts: ["wiretap"]);

        public Builder ArrangeMessageParts(Func<MessageContext, IReadOnlyList<MessagePartMap.Entry>> arrange)
        {
            _arrangeMessageParts = arrange;
            return this;
        }

        public Builder JoinMessageParts(Func<IReadOnlyList<MessagePartMap.Entry>, MessageTemplate> join)
        {
            _joinMessageParts = join;
            return this;
        }

        internal CreateLogEntry Build() => new(Root, _arrangeMessageParts, _joinMessageParts);

        private static MessageTemplate JoinByAppending(IReadOnlyList<MessagePartMap.Entry> entries)
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

public sealed record MessageContext
(
    PropertyName Root,
    IReadOnlyDictionary<string, object?> Properties,
    MessagePartMap Parts
);
