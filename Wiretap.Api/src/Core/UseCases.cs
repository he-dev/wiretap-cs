using System.Collections.Generic;
using Wiretap.Util;
using Wiretap.Util.Buzz;

// ReSharper disable once CheckNamespace
namespace Wires;

public abstract class Workflow
{
    public class ExecuteStep : Activity.Buzz
    {
        public class Now : ExecuteStep
        {
            [Detail]
            public required int StepIndex { get; init; }

            public sealed class Okay : ActivityStatus<Now>.Okay
            {
                [Detail]
                public required int ItemsProcessed { get; init; }
            }

            public sealed class Fail : ActivityStatus<Now>.Fail;
        }
    }
}

public class DeleteFile : Activity.Item
{
    public override string[] Tags { get; init; } = ["io"];

    [Remark]
    public required string Path { get; init; }

    public class Noop : ActivityStatus<DeleteFile>.Noop, IRemarkSource
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

    public sealed class Okay : ActivityStatus<DeleteFile>.Okay;

    public sealed class Fail : ActivityStatus<DeleteFile>.Fail;
}

public class DeleteFolder() : Activity.Bulk<DeleteFolder, DeleteFile>(OmitStatus.Last)
{
    [Remark]
    public required string Path { get; init; }

    public sealed class Okay : ActivityStatus<DeleteFolder>.Okay;

    public sealed class Fail : ActivityStatus<DeleteFolder>.Fail;
}

public class ValidateRecord : Activity.Snap
{
    [Detail]
    public required string RecordId { get; init; }

    public sealed class Okay : ActivityStatus<ValidateRecord>.Okay;

    public sealed class Noop : ActivityStatus<ValidateRecord>.Noop;

    public sealed class Fail : ActivityStatus<ValidateRecord>.Fail;
}

public class CopyFile : Activity.Buzz, IDetailSource
{
    [Detail]
    public required string Path { get; init; }

    public void Details(DetailBuilder details)
    {
        details.Add(new("CustomItem"), "CustomValue");
    }
}
