using JetBrains.Annotations;

namespace Wiretap.Util;

public class QuickBuzz(string name, [StructuredMessageTemplate] string? message = null, params object?[] args)
    : Activity.Buzz, IMessagePartFeed
{
    // core: Quick activities are intentionally named at runtime instead of by their CLR contract type.
    public override string Name => name;

    // core: Quick contracts use the same structured message pattern as LastStatusMessageFeed.
    public void MessageParts(IReadOnlyDictionary<string, object?> properties, PushMessagePart push) => push(message, args);

    public sealed class Okay([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickBuzz>.Okay, IMessagePartFeed
    {
        public void MessageParts(IReadOnlyDictionary<string, object?> properties, PushMessagePart push) => push(message, args);
    }

    public sealed class Noop([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickBuzz>.Noop, IMessagePartFeed
    {
        public void MessageParts(IReadOnlyDictionary<string, object?> properties, PushMessagePart push) => push(message, args);
    }

    public sealed class Fail([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickBuzz>.Fail
    {
        public override void MessageParts(IReadOnlyDictionary<string, object?> properties, PushMessagePart push)
        {
            // core: Keep the exception message behavior from normal Fail statuses, then append quick status text.
            base.MessageParts(properties, push);
            push(message, args);
        }
    }
}

public class QuickSnap(string name, [StructuredMessageTemplate] string? message = null, params object?[] args)
    : Activity.Snap, IMessagePartFeed
{
    // core: Quick activities are intentionally named at runtime instead of by their CLR contract type.
    public override string Name => name;

    // core: Quick contracts use the same structured message pattern as LastStatusMessageFeed.
    public void MessageParts(IReadOnlyDictionary<string, object?> properties, PushMessagePart push) => push(message, args);

    public sealed class Okay([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickSnap>.Okay, IMessagePartFeed
    {
        public void MessageParts(IReadOnlyDictionary<string, object?> properties, PushMessagePart push) => push(message, args);
    }

    public sealed class Noop([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickSnap>.Noop, IMessagePartFeed
    {
        public void MessageParts(IReadOnlyDictionary<string, object?> properties, PushMessagePart push) => push(message, args);
    }

    public sealed class Fail([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickSnap>.Fail
    {
        public override void MessageParts(IReadOnlyDictionary<string, object?> properties, PushMessagePart push)
        {
            // core: Keep the exception message behavior from normal Fail statuses, then append quick status text.
            base.MessageParts(properties, push);
            push(message, args);
        }
    }
}

public class QuickBulk(string name, [StructuredMessageTemplate] string? message = null, params object?[] args)
    : Activity.Bulk<QuickBulk, QuickBuzz>(StatusLogPolicy.Last), IMessagePartFeed
{
    // core: Quick activities are intentionally named at runtime instead of by their CLR contract type.
    public override string Name => name;

    public override StatusLogPolicy StatusLogPolicy { get; init; } = StatusLogPolicy.Last;

    // core: Quick contracts use the same structured message pattern as LastStatusMessageFeed.
    public void MessageParts(IReadOnlyDictionary<string, object?> properties, PushMessagePart push) => push(message, args);

    public sealed class Okay([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickBulk>.Okay, IMessagePartFeed
    {
        public void MessageParts(IReadOnlyDictionary<string, object?> properties, PushMessagePart push) => push(message, args);
    }

    public sealed class Noop([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickBulk>.Noop, IMessagePartFeed
    {
        public void MessageParts(IReadOnlyDictionary<string, object?> properties, PushMessagePart push) => push(message, args);
    }

    public sealed class Fail([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickBulk>.Fail
    {
        public override void MessageParts(IReadOnlyDictionary<string, object?> properties, PushMessagePart push)
        {
            // core: Keep the exception message behavior from normal Fail statuses, then append quick status text.
            base.MessageParts(properties, push);
            push(message, args);
        }
    }
}
