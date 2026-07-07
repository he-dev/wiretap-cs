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

        public sealed class Okay : Status.Last.Okay, IStatusOf<ExecuteStep>
        {
            [Detail]
            public required int ItemsProcessed { get; init; }
        }

        public sealed class Fail : Status.Last.Fail, IStatusOf<ExecuteStep>;
    }
}

public class DeleteFile : Buzz, IItemOf<DeleteFolder>
{
    public override string[] Tags { get; } = ["io"];

    [Remark]
    public required string Path { get; init; }

    public class Noop : Status.Last.Noop, IStatusOf<DeleteFile>, IRemarkSource
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

    public sealed class Okay : Status.Last.Okay, IStatusOf<DeleteFile>;

    public sealed class Fail : Status.Last.Fail, IStatusOf<DeleteFile>;
}

public class DeleteFolder : Buzz.Bulk
{
    [Remark]
    public required string Path { get; init; }

    public sealed class Okay : Status.Last.Okay, IStatusOf<DeleteFolder>;

    public sealed class Fail : Status.Last.Fail, IStatusOf<DeleteFolder>;
}

public class ValidateRecord : Buzz
{
    [Detail]
    public required string RecordId { get; init; }

    public sealed class Okay : Status.Last.Okay, IStatusOf<ValidateRecord>;

    public sealed class Noop : Status.Last.Noop, IStatusOf<ValidateRecord>;

    public sealed class Fail : Status.Last.Fail, IStatusOf<ValidateRecord>;
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
