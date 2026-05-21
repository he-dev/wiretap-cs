using Microsoft.Extensions.Logging;
using Wiretap.Util;
using Wiretap.Util.Services;

[assembly: MessageTemplateSchema]

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
public class DeleteFile : Activity.Buzz
{
    [ScopeStateItem]
    public required string Path { get; init; }

    public sealed class Halt : ActivityStatus.Core<DeleteFile>.Halt;

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