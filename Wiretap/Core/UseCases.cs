namespace Wiretap.Core;

[ActivityDomain]
public abstract class Output
{
    public abstract class Workflow
    {
        [LastStatusPolicy.MustNotLeak]
        public class ExecuteStep : Activity
        {
            public class Now : ExecuteStep
            {
                [ScopeState]
                public required int StepIndex { get; init; }

                public sealed class Okay : ExplicitStatus<Now>.Okay
                {
                    [ScopeState]
                    public required int ItemsProcessed { get; init; }
                }

                public sealed class Fail : ExplicitStatus<Now>.Fail;
            }
        }
    }
}

[CompactMessageSchema]
public abstract class Engine
{
    public class DeleteFile : Activity
    {
        [ScopeState]
        public required string Path { get; init; }

        public sealed class Halt : ExplicitStatus<DeleteFile>.Halt;

        public sealed class Okay : ExplicitStatus<DeleteFile>.Okay;

        public sealed class Fail : ExplicitStatus<DeleteFile>.Fail;
    }

    [LastStatusPolicy.MustBeVoid]
    public class CopyFile : Activity, IProvidesStateItems
    {
        [ScopeState]
        public required string Path { get; init; }

        public IEnumerable<(string Key, object Value)> States()
        {
            yield return new("CustomItem", "CustomValue");
        }
    }
}