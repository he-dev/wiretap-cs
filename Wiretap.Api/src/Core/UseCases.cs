using Wiretap.Util;
using Wiretap.Util.Data;

// ReSharper disable once CheckNamespace
namespace Wires;

public abstract class Workflow
{
    public class ExecuteStep : Buzz<ExecuteStep>
    {
        [Detail]
        public required int StepIndex { get; init; }

        public sealed class Okay : BuzzStatus<ExecuteStep>.Okay
        {
            [Detail]
            public required int ItemsProcessed { get; init; }
        }

        public sealed class Fail : BuzzStatus<ExecuteStep>.Fail;
    }
}

public class DeleteFile : Buzz<DeleteFile>
{
    public override string[] Tags { get; init; } = ["io"];

    [Remark]
    public required string Path { get; init; }

    public class Noop : BuzzStatus<DeleteFile>.Noop, IRemarkSource
    {
        public virtual string Reason { get; init; } = "Unspecified";

        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(new("Reason"), Reason, options => options.Label = "Reason");
        }

        public sealed class NotFound : DeleteFile.Noop
        {
            public override string Reason { get; init; } = "File not found";
        }
    }

    public sealed class Okay : BuzzStatus<DeleteFile>.Okay;

    public sealed class Fail : BuzzStatus<DeleteFile>.Fail;
}

public class DeleteFolder : Buzz<DeleteFolder>.Bulk<DeleteFile>
{
    [Remark]
    public required string Path { get; init; }

    public sealed class Okay : BuzzStatus<DeleteFolder>.Okay;

    public sealed class Fail : BuzzStatus<DeleteFolder>.Fail;
}

public class ValidateRecord : Buzz<ValidateRecord>
{
    [Detail]
    public required string RecordId { get; init; }

    public sealed class Okay : BuzzStatus<ValidateRecord>.Okay;

    public sealed class Noop : BuzzStatus<ValidateRecord>.Noop;

    public sealed class Fail : BuzzStatus<ValidateRecord>.Fail;
}

public class CopyFile : Buzz<CopyFile>, IDetailSource
{
    [Detail]
    public required string Path { get; init; }

    public void Details(DetailBuilder details)
    {
        details.Add(new("CustomItem"), "CustomValue");
    }
}