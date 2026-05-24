using Wiretap.Util;
using Wiretap.Util.Services;

[assembly: MessageTemplateSchema]
[assembly: MessageTemplatePrefix.Compact]

// ReSharper disable once CheckNamespace
namespace Wires;

public abstract class Workflow
{
    [LastStatusPolicy.MuteLeaks]
    public class ExecuteStep : Activity.Core, IWithZeroStatus
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

[LastStatusPolicy.MuteLeaks]
public class DeleteFile : Activity.Buzz, IWithMessageParts
{
    //[ScopeStateItem]
    public required string Path { get; init; }

    public void MessageParts(ActivityStatus.Context context, AppendMessagePart append)
    {
        append("Path: '{Path}'", Path);
    }

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
public class CopyFile : Activity.Buzz, IWithStateItems
{
    [ScopeStateItem]
    public required string Path { get; init; }

    public void StateItems(AddStateItem add)
    {
        add("CustomItem", "CustomValue");
    }
}