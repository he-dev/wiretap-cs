using JetBrains.Annotations;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public class QuickBuzz(string name, [StructuredMessageTemplate] string? message = null, params object?[] args)
    : Activity.Buzz, IMessagePartFeed
{
    // core: Quick activities are intentionally named at runtime instead of by their CLR contract type.
    public override string Name => name;

    // core: Quick contracts carry their optional structured message as contract data.
    public void MessageParts(PropertyName root, GetLogProperty get, PushMessagePart push)
    {
        push.Discrete(root.Activity.Append("message"), message, args);
    }

    public sealed class Okay([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickBuzz>.Okay, IMessagePartFeed
    {
        public void MessageParts(PropertyName root, GetLogProperty get, PushMessagePart push)
        {
            push.Discrete(root.Activity.Status.Append("message"), message, args);
        }
    }

    public sealed class Noop([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickBuzz>.Noop, IMessagePartFeed
    {
        public void MessageParts(PropertyName root, GetLogProperty get, PushMessagePart push)
        {
            push.Discrete(root.Activity.Status.Append("message"), message, args);
        }
    }

    public sealed class Fail([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickBuzz>.Fail, IMessagePartFeed
    {
        public void MessageParts(PropertyName root, GetLogProperty get, PushMessagePart push)
        {
            push.Discrete(root.Activity.Status.Append("message"), message, args);
        }
    }
}

public class QuickSnap(string name, [StructuredMessageTemplate] string? message = null, params object?[] args)
    : Activity.Snap, IMessagePartFeed
{
    // core: Quick activities are intentionally named at runtime instead of by their CLR contract type.
    public override string Name => name;

    // core: Quick contracts carry their optional structured message as contract data.
    public void MessageParts(PropertyName root, GetLogProperty get, PushMessagePart push)
    {
        push.Discrete(root.Activity.Append("message"), message, args);
    }

    public sealed class Okay([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickSnap>.Okay, IMessagePartFeed
    {
        public void MessageParts(PropertyName root, GetLogProperty get, PushMessagePart push)
        {
            push.Discrete(root.Activity.Status.Append("message"), message, args);
        }
    }

    public sealed class Noop([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickSnap>.Noop, IMessagePartFeed
    {
        public void MessageParts(PropertyName root, GetLogProperty get, PushMessagePart push)
        {
            push.Discrete(root.Activity.Status.Append("message"), message, args);
        }
    }

    public sealed class Fail([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickSnap>.Fail, IMessagePartFeed
    {
        public void MessageParts(PropertyName root, GetLogProperty get, PushMessagePart push)
        {
            push.Discrete(root.Activity.Status.Append("message"), message, args);
        }
    }
}

public class QuickItem(string name, [StructuredMessageTemplate] string? message = null, params object?[] args)
    : Activity.Item, IMessagePartFeed
{
    // core: Quick activities are intentionally named at runtime instead of by their CLR contract type.
    public override string Name => name;

    public void MessageParts(PropertyName root, GetLogProperty get, PushMessagePart push)
    {
        push.Discrete(root.Activity.Append("message"), message, args);
    }

    public sealed class Okay([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickItem>.Okay, IMessagePartFeed
    {
        public void MessageParts(PropertyName root, GetLogProperty get, PushMessagePart push)
        {
            push.Discrete(root.Activity.Status.Append("message"), message, args);
        }
    }

    public sealed class Noop([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickItem>.Noop, IMessagePartFeed
    {
        public void MessageParts(PropertyName root, GetLogProperty get, PushMessagePart push)
        {
            push.Discrete(root.Activity.Status.Append("message"), message, args);
        }
    }

    public sealed class Fail([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickItem>.Fail, IMessagePartFeed
    {
        public void MessageParts(PropertyName root, GetLogProperty get, PushMessagePart push)
        {
            push.Discrete(root.Activity.Status.Append("message"), message, args);
        }
    }
}

public class QuickBulk(string name, [StructuredMessageTemplate] string? message = null, params object?[] args)
    : Activity.Bulk<QuickBulk, QuickItem>(StatusLogPolicy.Last), IMessagePartFeed
{
    // core: Quick activities are intentionally named at runtime instead of by their CLR contract type.
    public override string Name => name;

    public override StatusLogPolicy StatusLogPolicy { get; init; } = StatusLogPolicy.Last;

    // core: Quick contracts carry their optional structured message as contract data.
    public void MessageParts(PropertyName root, GetLogProperty get, PushMessagePart push)
    {
        push.Discrete(root.Activity.Append("message"), message, args);
    }

    public sealed class Okay([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickBulk>.Okay, IMessagePartFeed
    {
        public void MessageParts(PropertyName root, GetLogProperty get, PushMessagePart push)
        {
            push.Discrete(root.Activity.Status.Append("message"), message, args);
        }
    }

    public sealed class Noop([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickBulk>.Noop, IMessagePartFeed
    {
        public void MessageParts(PropertyName root, GetLogProperty get, PushMessagePart push)
        {
            push.Discrete(root.Activity.Status.Append("message"), message, args);
        }
    }

    public sealed class Fail([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickBulk>.Fail, IMessagePartFeed
    {
        public void MessageParts(PropertyName root, GetLogProperty get, PushMessagePart push)
        {
            push.Discrete(root.Activity.Status.Append("message"), message, args);
        }
    }
}
