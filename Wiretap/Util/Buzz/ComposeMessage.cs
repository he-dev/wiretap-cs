using System.Collections.Immutable;
using JetBrains.Annotations;

namespace Wiretap.Util.Buzz;

public delegate void PushMessagePart(
    PropertyName name,
    [StructuredMessageTemplate] string? message,
    params object?[] args
);

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

        foreach (var include in Include)
        {
            include(root, get, parts.Push);
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
