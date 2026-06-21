using System.Globalization;
using System.Text;

namespace Wiretap.Util.Buzz;

public interface IArrangeMessageParts
{
    IReadOnlyList<MessageTemplate> By(PropertyName root, MessagePartMap parts);
}

public class ArrangeMessageParts : IArrangeMessageParts
{
    public IReadOnlyList<MessageTemplate> By(PropertyName root, MessagePartMap parts)
    {
        return
        [
            ..new[]
            {
                parts.Pop(root.Activity.Name),
                parts.Pop(root.Activity.DurationMs),
            }.OfType<MessageTemplate>(),
            ..parts.Select(x => x.Value),
        ];
    }
}

public interface IJoinMessageParts
{
    MessageTemplate By(IReadOnlyList<MessageTemplate> entries);
}

public class JoinMessagePartsByAppending : IJoinMessageParts
{
    public MessageTemplate By(IReadOnlyList<MessageTemplate> entries)
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

public delegate void MessagePartRegistration(PropertyName root, GetLogProperty get, PushMessagePart push);

interface IGetLogProperties
{
    IDictionary<string, object?> From(ActivityScope scope, ActivityStatus status);
}


public sealed class CreateLogEntry
(
    PropertyName root,
    IArrangeMessageParts arrangeMessageParts,
    IJoinMessageParts joinMessageParts,
    IReadOnlyList<MessagePartRegistration> messagePartRegistrations
)
{
    public PropertyName Root => root;

    public LogEntry From(ActivityScope scope, ActivityStatus status)
    {
        var properties = CollectLogProperties(scope, status);
        var messageParts = CollectMessageParts(properties, scope.Activity, status);
        var message = joinMessageParts.By(arrangeMessageParts.By(Root, messageParts));
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
            Root.Activity.State,
            push,
            scope.Reverse().SkipLast(1).Select(x => x.Activity)
        );

        foreach (var source in new object[] { scope, scope.Activity, status })
        {
            if (source is ILogPropertySource feed)
            {
                feed.LogProperties(Root, push);
            }
        }

        AnnotatedStateItems.PushFromSelf(Root.Activity.State, push, scope.Activity);
        AnnotatedStateItems.PushFromSelf(Root.Activity.State, push, status);
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

        GetMessageParts.From(Root, properties, push, activity, status);

        foreach (var registration in messagePartRegistrations)
        {
            registration(Root, get, push);
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
        private IArrangeMessageParts ArrangeMessageParts { get; set; }= new ArrangeMessageParts();
        private IJoinMessageParts JoinMessageParts { get; set; }= new JoinMessagePartsByAppending();

        private readonly List<MessagePartRegistration> _messagePartRegistrations = [];

        public PropertyName Root { get; set; } = new(Parts: ["wiretap"]);

        public Builder()
        {
            RegisterMessageParts(PushActivityHeader);
            RegisterMessageParts(PushActivityDuration);
            RegisterMessageParts(PushBulkSummary);
        }


        public Builder RegisterMessageParts(MessagePartRegistration registration)
        {
            _messagePartRegistrations.Add(registration);
            return this;
        }

        internal CreateLogEntry Build() => new(
            Root,
            ArrangeMessageParts,
            JoinMessageParts,
            [.._messagePartRegistrations]
        );

        private static void PushActivityHeader(PropertyName root, GetLogProperty get, PushMessagePart push)
        {
            push(
                root.Activity.Name,
                $"{root.Activity.Name:_}[{root.Activity.Status.Code:_}]",
                get(root.Activity.Name),
                get(root.Activity.Status.Code)
            );
        }

        private static void PushActivityDuration(PropertyName root, GetLogProperty get, PushMessagePart push)
        {
            if (get(root.Activity.Role) is "snap")
            {
                push(root.Activity.DurationMs, "Duration: N/A");
                return;
            }

            push(
                root.Activity.DurationMs,
                $"Duration: {root.Activity.DurationMs:N0} ms",
                get(root.Activity.DurationMs)
            );
        }

        private static void PushBulkSummary(PropertyName root, GetLogProperty get, PushMessagePart push)
        {
            var state = root.Activity.State.Append("bulk");

            foreach (var code in new[] { "okay", "noop", "fail", "void" })
            {
                var label = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(code);
                push(
                    state.Append(code),
                    $"{label}: {state.Append($"{code}_rate"):P1} ({state.Append($"{code}_count"):_} of {state.Append("item_count"):_})",
                    get(state.Append($"{code}_rate")),
                    get(state.Append($"{code}_count")),
                    get(state.Append("item_count"))
                );
            }

            push(
                state.Append("throughput_s"),
                $"Throughput: {state.Append("throughput_s"):N1}/s",
                get(state.Append("throughput_s"))
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
    MessagePartMap Parts
);