using System.Text;

namespace Wiretap.Util.Buzz;

public interface IArrangeMessageParts
{
    IReadOnlyList<MessageTemplate> By(MessageContext context);
}

public class ArrangeMessageParts : IArrangeMessageParts
{
    public IReadOnlyList<MessageTemplate> By(MessageContext context)
    {
        return
        [
            ..new[]
            {
                context.Parts.Pop(context.Root.Activity.Name),
                context.Parts.Pop(context.Root.Activity.DurationMs),
            }.OfType<MessageTemplate>(),
            ..context.Parts.Select(x => x.Value),
        ];
    }
}

public sealed class CreateLogEntry
{
    private readonly PropertyName _root;
    private readonly Func<MessageContext, IReadOnlyList<MessageTemplate>> _arrangeMessageParts;

    private readonly Func<IReadOnlyList<MessageTemplate>, MessageTemplate> _joinMessageParts;

    private readonly IReadOnlyList<Action<PropertyName, GetLogProperty, PushMessagePart>> _messagePartRegistrations;

    public PropertyName Root => _root;

    private CreateLogEntry
    (
        PropertyName root,
        Func<MessageContext, IReadOnlyList<MessageTemplate>> arrangeMessageParts,
        Func<IReadOnlyList<MessageTemplate>, MessageTemplate> joinMessageParts,
        IReadOnlyList<Action<PropertyName, GetLogProperty, PushMessagePart>> messagePartRegistrations
    )
    {
        _root = root;
        _arrangeMessageParts = arrangeMessageParts;
        _joinMessageParts = joinMessageParts;
        _messagePartRegistrations = messagePartRegistrations;
    }

    public LogEntry From(ActivityScope scope, ActivityStatus status)
    {
        var properties = CollectLogProperties(scope, status);
        var messageParts = CollectMessageParts(properties, scope.Activity, status);
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

    private MessagePartMap CollectMessageParts
    (
        IReadOnlyDictionary<string, object?> properties,
        Activity activity,
        ActivityStatus status
    )
    {
        var parts = new MessagePartMap();
        var get = new GetLogProperty(properties.GetValueOrDefault);
        var push = new PushMessagePart(parts.Push);

        GetMessageParts.From(_root, properties, push, activity, status);

        foreach (var registration in _messagePartRegistrations)
        {
            registration(_root, get, push);
        }

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
        private readonly List<Action<PropertyName, GetLogProperty, PushMessagePart>> _messagePartRegistrations = [];

        private Func<MessageContext, IReadOnlyList<MessageTemplate>> _arrangeMessageParts = context =>
        [
            ..new[]
            {
                context.Parts.Pop(context.Root.Activity.Name),
                context.Parts.Pop(context.Root.Activity.DurationMs),
            }.OfType<MessageTemplate>(),
            ..context.Parts.Select(x => x.Value),
        ];

        private Func<IReadOnlyList<MessageTemplate>, MessageTemplate> _joinMessageParts = JoinByAppending;

        public PropertyName Root { get; set; } = new(Parts: ["wiretap"]);

        public Builder()
        {
            RegisterMessageParts(PushDefaultMessageParts);
        }

        public Builder ArrangeMessageParts(Func<MessageContext, IReadOnlyList<MessageTemplate>> arrange)
        {
            _arrangeMessageParts = arrange;
            return this;
        }

        public Builder JoinMessageParts(Func<IReadOnlyList<MessageTemplate>, MessageTemplate> join)
        {
            _joinMessageParts = join;
            return this;
        }

        public Builder RegisterMessageParts(Action<PropertyName, GetLogProperty, PushMessagePart> registration)
        {
            _messagePartRegistrations.Add(registration);
            return this;
        }

        internal CreateLogEntry Build() => new(
            Root,
            _arrangeMessageParts,
            _joinMessageParts,
            [.._messagePartRegistrations]
        );

        private static void PushDefaultMessageParts(PropertyName root, GetLogProperty get, PushMessagePart push)
        {
            push(
                root.Activity.Name,
                $"{root.Activity.Name:_}[{root.Activity.Status.Code:_}]",
                get(root.Activity.Name),
                get(root.Activity.Status.Code)
            );
            push(
                root.Activity.DurationMs,
                $"Duration: {root.Activity.DurationMs:N0} ms",
                get(root.Activity.DurationMs)
            );
        }

        private static MessageTemplate JoinByAppending(IReadOnlyList<MessageTemplate> entries)
        {
            var template = new StringBuilder(256);
            var args = new List<object?>(32);
            foreach (var message in entries)
            {
                if (string.IsNullOrEmpty(message.Template))
                {
                    continue;
                }

                template.Append(template.Length == 0 ? string.Empty : "; ");
                template.Append(message.Template);
                args.AddRange(message.Args);
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

public class Demo
{
    public void SomeMessages(PropertyName root, GetLogProperty get, PushMessagePart push)
    {
        push(root.Activity.DurationMs, $"Duration: {root.Activity.DurationMs:N0} ms", get(root.Activity.DurationMs));
    }
}
