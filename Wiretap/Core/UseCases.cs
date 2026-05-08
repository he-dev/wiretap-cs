namespace Wiretap.Core;

[Channel]
public abstract class Output
{
    public abstract class Workflow
    {
        [LastStatusMustNotOverflow]
        public class ExecuteStep : Activity
        {
            public class Now : ExecuteStep, IEnumerableState
            {
                public required int StepIndex { get; init; }

                public IEnumerable<(string, object)> EnumerateState()
                {
                    yield return new(nameof(StepIndex), StepIndex);
                }

                public sealed class Okay : ActivityStatus<Now>.Okay
                {
                    // note: Can be either a property or a constructor parameter. Does not really make any difference.
                    public required int ItemsProcessed { get; init; }

                    public override IEnumerable<(string, object)> EnumerateState()
                    {
                        return base.EnumerateState().Append((nameof(ItemsProcessed), ItemsProcessed));
                    }
                }

                public sealed class Fail : ActivityStatus<Now>.Fail;
            }
        }
    }
}

public abstract class Engine
{
    public class DeleteFile : Activity, IEnumerableState
    {
        public required string Path { get; init; }

        public IEnumerable<(string Key, object Value)> EnumerateState()
        {
            yield return new(nameof(Path), Path);
        }

        public sealed class Halt : ActivityStatus<DeleteFile>.Halt;

        public sealed class Okay : ActivityStatus<DeleteFile>.Okay;

        public sealed class Fail : ActivityStatus<DeleteFile>.Fail;
    }

    [LastStatusMustBeVoid]
    public class CopyFile : Activity, IEnumerableState
    {
        public required string Path { get; init; }

        public IEnumerable<(string Key, object Value)> EnumerateState()
        {
            yield return new(nameof(Path), Path);
        }
    }
}