using Wiretap.Core;

[assembly: CompactMessageSchema]

namespace Wire;

public abstract class Workflow
{
    [LastStatusPolicy.MuteLeaks]
    public class ExecuteStep : Activity.Core
    {
        public class Now : ExecuteStep
        {
            [ScopeStateItem]
            public required int StepIndex { get; init; }

            public sealed class Okay : ExplicitStatus<Now>.Okay
            {
                [ScopeStateItem]
                public required int ItemsProcessed { get; init; }
            }

            public sealed class Fail : ExplicitStatus<Now>.Fail;
        }
    }
}

[LastStatusPolicy.MuteLeaks]
public class DeleteFile : Activity.Buzz
{
    [ScopeStateItem]
    public required string Path { get; init; }

    public sealed class Halt : ExplicitStatus<DeleteFile>.Halt;

    public sealed class Okay : ExplicitStatus<DeleteFile>.Okay;

    public sealed class Fail : ExplicitStatus<DeleteFile>.Fail;
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