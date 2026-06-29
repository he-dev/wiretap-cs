using System.Collections.Immutable;

namespace Wiretap.Util.Buzz;

public sealed record CreateLogEntry
(
    PropertyName Root,
    IArrangeMessageParts ArrangeMessageParts,
    IJoinMessageParts JoinMessageParts,
    ImmutableArray<MessagePartRegistration> MessagePartRegistrations
)
{
    public static CreateLogEntry Default { get; } = new(
        new PropertyName("wiretap"),
        new ArrangeMessageParts(),
        new JoinMessagePartsByAppending(),
        DefaultMessageParts.All
    );

    public CreateLogEntry RegisterMessageParts(params MessagePartRegistration[] registrations)
    {
        return this with { MessagePartRegistrations = MessagePartRegistrations.AddRange(registrations) };
    }

    public LogEntry From(ActivityScope scope, params object?[] propertySources)
    {
        var status = scope.Activity.Status;
        object?[] sources = [scope, ..propertySources, scope.Activity, status];
        var properties = GetLogProperties.From(Root, sources);
        if (scope.Activity is Activity.Buzz buzz)
        {
            properties[Root.Activity.DurationMs] = buzz.DurationMs;
        }
        var get = new GetLogProperty(properties.GetValueOrDefault);
        var messageParts = GetMessageParts.From(Root, get, scope.Activity, status);
        var push = new PushMessagePart(get, messageParts);
        foreach (var registration in MessagePartRegistrations)
        {
            registration(Root, get, push);
        }

        var message = JoinMessageParts.By(ArrangeMessageParts.By(Root, messageParts));
        return new LogEntry(status.Level, message, properties, status.Exception);
    }
}
