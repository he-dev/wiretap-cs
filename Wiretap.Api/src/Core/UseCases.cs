using Wiretap.Util;
using Wiretap.Util.Services;

[assembly: MessageTemplateSchema]
[assembly: MessageTemplatePrefix.Compact]

// ReSharper disable once CheckNamespace
namespace Wires;

public abstract class Workflow
{
    public class ExecuteStep : Activity.Core, IWithReadyStatus
    {
        public class Now : ExecuteStep
        {
            [ScopeStateItem]
            public required int StepIndex { get; init; }

            public sealed class Okay : ActivityStatus.Core<Now>.Okay
            {
                [ScopeStateItem]
                public required int ItemsProcessed { get; init; }
            }

            public sealed class Fail : ActivityStatus.Core<Now>.Fail;
        }
    }
}

public class DeleteFile : Activity.Buzz
{
    [FeedToMessagePart]
    public required string Path { get; init; }

    public class Halt : ActivityStatus.Core<DeleteFile>.Halt
    {
        public sealed class NotFound : DeleteFile.Halt
        {
            public override string Reason { get; init; } = "File not found";
        }
    }

    public sealed class Okay : ActivityStatus.Core<DeleteFile>.Okay;

    public sealed class Fail : ActivityStatus.Core<DeleteFile>.Fail;
}

[LastStatusPolicy.CanBeVoid]
public class CopyFile : Activity.Buzz, IStateItemFeed
{
    [ScopeStateItem]
    public required string Path { get; init; }

    public void StateItems(PushStateItem push)
    {
        push("CustomItem", "CustomValue");
    }
}
