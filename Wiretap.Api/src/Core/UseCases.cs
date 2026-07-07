using Wiretap.Util;
using Wiretap.Util.Data;

// ReSharper disable once CheckNamespace
namespace Wires;

public abstract class Workflow
{
    public class ExecuteStep : Buzz
    {
        public class Now : ExecuteStep { }

        [Detail]
        public required int StepIndex { get; init; }

        public sealed class Okay : Status.Okay, IAssociatedWith<ExecuteStep>
        {
            [Detail]
            public required int ItemsProcessed { get; init; }
        }

        public sealed class Fail : Status.Fail, IAssociatedWith<ExecuteStep>;
    }
}

public class DeleteFile : Buzz
{
    public override string[] Tags { get; } = ["io"];

    [Remark]
    public required string Path { get; init; }

    public class Noop : Status.Noop, IAssociatedWith<DeleteFile>, IRemarkSource
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

    public sealed class Okay : Status.Okay, IAssociatedWith<DeleteFile>;

    public sealed class Fail : Status.Fail, IAssociatedWith<DeleteFile>;
}

public class DeleteFolder : Buzz.Bulk<DeleteFile>
{
    [Remark]
    public required string Path { get; init; }

    public sealed class Okay : Status.Okay, IAssociatedWith<DeleteFolder>;

    public sealed class Fail : Status.Fail, IAssociatedWith<DeleteFolder>;
}

public class ValidateRecord : Buzz
{
    [Detail]
    public required string RecordId { get; init; }

    public sealed class Okay : Status.Okay, IAssociatedWith<ValidateRecord>;

    public sealed class Noop : Status.Noop, IAssociatedWith<ValidateRecord>;

    public sealed class Fail : Status.Fail, IAssociatedWith<ValidateRecord>;
}

public class CopyFile : Buzz, IDetailSource
{
    [Detail]
    public required string Path { get; init; }

    public void Details(DetailBuilder details)
    {
        details.Add(new("CustomItem"), "CustomValue");
    }
}
