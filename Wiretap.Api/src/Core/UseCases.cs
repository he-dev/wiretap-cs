using System.Collections.Generic;
using Wiretap.Util;
using Wiretap.Util.Buzz;

// ReSharper disable once CheckNamespace
namespace Wires;

public abstract class Workflow
{
    public class ExecuteStep : Activity.Buzz
    {
        public class Now : ExecuteStep
        {
            [ScopeStateItem]
            public required int StepIndex { get; init; }

            public sealed class Okay : ActivityStatus<Now>.Okay
            {
                [ScopeStateItem]
                public required int ItemsProcessed { get; init; }
            }

            public sealed class Fail : ActivityStatus<Now>.Fail;
        }
    }
}

public class DeleteFile : Activity.Buzz
{
    [FeedToMessagePart]
    public required string Path { get; init; }

    public class Noop : ActivityStatus<DeleteFile>.Noop, IMessagePartFeed
    {
        public virtual string Reason { get; init; } = "Unspecified";

        public void MessageParts(IReadOnlyDictionary<string, object?> properties, PushMessagePart push)
        {
            push("Reason: {wiretap.activity.state.Reason}", Reason);
        }

        public sealed class NotFound : DeleteFile.Noop
        {
            public override string Reason { get; init; } = "File not found";
        }
    }

    public sealed class Okay : ActivityStatus<DeleteFile>.Okay;

    public sealed class Fail : ActivityStatus<DeleteFile>.Fail;
}

public class DeleteFolder() : Activity.Bulk<DeleteFolder, DeleteFile>(StatusLogPolicy.Last)
{
    [FeedToMessagePart]
    public required string Path { get; init; }

    public sealed class Okay : ActivityStatus<DeleteFolder>.Okay;

    public sealed class Fail : ActivityStatus<DeleteFolder>.Fail;
}

public class ValidateRecord : Activity.Snap
{
    [FeedToStateItem]
    public required string RecordId { get; init; }

    public sealed class Okay : ActivityStatus<ValidateRecord>.Okay;

    public sealed class Noop : ActivityStatus<ValidateRecord>.Noop;

    public sealed class Fail : ActivityStatus<ValidateRecord>.Fail;
}

public class CopyFile : Activity.Buzz, IStateItemFeed
{
    [ScopeStateItem]
    public required string Path { get; init; }

    public void StateItems(PushStateItem push)
    {
        push("CustomItem", "CustomValue");
    }
}
