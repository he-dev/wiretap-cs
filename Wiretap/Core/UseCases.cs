using Wiretap.Core;

[assembly: CompactMessageSchema]

namespace Wiretap.Core.Activities;

public abstract class Workflow
{
    [LastStatusPolicy.MuteLeaks]
    public class ExecuteStep : ActivityRole.Core
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
public class DeleteFile : ActivityRole.Util
{
    [ScopeStateItem]
    public required string Path { get; init; }

    public sealed class Halt : ExplicitStatus<DeleteFile>.Halt;

    public sealed class Okay : ExplicitStatus<DeleteFile>.Okay;

    public sealed class Fail : ExplicitStatus<DeleteFile>.Fail;
}

[LastStatusPolicy.CanBeVoid]
public class CopyFile : ActivityRole.Util, IWithStateItems
{
    [ScopeStateItem]
    public required string Path { get; init; }

    public IEnumerable<KeyValuePair<string, object?>> StateItems()
    {
        yield return new("CustomItem", "CustomValue");
    }
}