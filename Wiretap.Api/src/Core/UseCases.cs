using System;
using Wiretap.Util;
using Wiretap.Util.Data;

// ReSharper disable once CheckNamespace
namespace Wires;

public abstract class Workflow
{
    public class ExecuteStep : Watch
    {
        public class Now : ExecuteStep { }

        [StateItem]
        public required int StepIndex { get; init; }

        public sealed class Okay : WatchEvent.Last.Okay, IEventOf<ExecuteStep>
        {
            [StateItem]
            public required int ItemsProcessed { get; init; }
        }

        public sealed class Fail : WatchEvent.Last.Fail, IEventOf<ExecuteStep>;
    }
}

public class DeleteFile : Batch.Item, IItemOf<DeleteFolder>
{
    public override string[] Tags { get; } = ["io"];

    [MessagePart]
    public required string Path { get; init; }

    public class Noop : WatchEvent.Last.Noop, IEventOf<DeleteFile>, IRemarkSource
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

    public sealed class Okay : WatchEvent.Last.Okay, IEventOf<DeleteFile>;

    public sealed class Fail : WatchEvent.Last.Fail, IEventOf<DeleteFile>;
}

public class DeleteFolder : Batch
{
    [MessagePart]
    public required string Path { get; init; }

    public sealed class Okay : WatchEvent.Last.Okay, IEventOf<DeleteFolder>;

    public sealed class Fail : WatchEvent.Last.Fail, IEventOf<DeleteFolder>;
}

public class ValidateRecord : Watch
{
    [StateItem]
    public required string RecordId { get; init; }

    public sealed class Okay : WatchEvent.Last.Okay, IEventOf<ValidateRecord>;

    public sealed class Noop : WatchEvent.Last.Noop, IEventOf<ValidateRecord>;

    public sealed class Fail : WatchEvent.Last.Fail, IEventOf<ValidateRecord>;
}

public class CopyFile : Watch, IDetailSource
{
    [StateItem]
    public required string Path { get; init; }

    public void Details(DetailBuilder details)
    {
        details.Add(new("CustomItem"), "CustomValue");
    }
}
