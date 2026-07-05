using JetBrains.Annotations;
using Wiretap.Util.Data;

namespace Wiretap.Util;

public class QuickBuzz(string name, [StructuredMessageTemplate] string? message = null, params object?[] args)
    : Activity.Buzz, IRemarkSource
{
    // core: Quick activities are intentionally named at runtime instead of by their CLR contract type.
    public override string Name => name;

    // core: Quick contracts carry their optional structured message as contract data.
    public void Remarks(RemarkBuilder remarks)
    {
        remarks.Add(remarks.Root.Activity.Append("message"), message, args);
    }

    public sealed class Okay([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickBuzz>.Okay, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Activity.Status.Append("message"), message, args);
        }
    }

    public sealed class Noop([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickBuzz>.Noop, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Activity.Status.Append("message"), message, args);
        }
    }

    public sealed class Fail([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickBuzz>.Fail, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Activity.Status.Append("message"), message, args);
        }
    }
}

public class QuickItem(string name, [StructuredMessageTemplate] string? message = null, params object?[] args)
    : Activity.Item, IRemarkSource
{
    // core: Quick activities are intentionally named at runtime instead of by their CLR contract type.
    public override string Name => name;

    public void Remarks(RemarkBuilder remarks)
    {
        remarks.Add(remarks.Root.Activity.Append("message"), message, args);
    }

    public sealed class Okay([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickItem>.Okay, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Activity.Status.Append("message"), message, args);
        }
    }

    public sealed class Noop([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickItem>.Noop, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Activity.Status.Append("message"), message, args);
        }
    }

    public sealed class Fail([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickItem>.Fail, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Activity.Status.Append("message"), message, args);
        }
    }
}

public class QuickBulk(string name, [StructuredMessageTemplate] string? message = null, params object?[] args)
    : Activity.Bulk<QuickBulk, QuickItem>(OmitStatus.First), IRemarkSource
{
    // core: Quick activities are intentionally named at runtime instead of by their CLR contract type.
    public override string Name => name;

    public override OmitStatus OmitStatus { get; init; } = OmitStatus.First;

    // core: Quick contracts carry their optional structured message as contract data.
    public void Remarks(RemarkBuilder remarks)
    {
        remarks.Add(remarks.Root.Activity.Append("message"), message, args);
    }

    public sealed class Okay([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickBulk>.Okay, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Activity.Status.Append("message"), message, args);
        }
    }

    public sealed class Noop([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickBulk>.Noop, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Activity.Status.Append("message"), message, args);
        }
    }

    public sealed class Fail([StructuredMessageTemplate] string? message = null, params object?[] args)
        : ActivityStatus<QuickBulk>.Fail, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Activity.Status.Append("message"), message, args);
        }
    }
}
