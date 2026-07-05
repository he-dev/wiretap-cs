using JetBrains.Annotations;
using Wiretap.Util.Data;

namespace Wiretap.Util;

public class QuickBuzz(string name, [StructuredMessageTemplate] string? message = null, params object?[] args)
    : Buzz<QuickBuzz>, IRemarkSource
{
    // core: Quick activities are intentionally named at runtime instead of by their CLR contract type.
    public override string Name => name;

    // core: Quick contracts carry their optional structured message as contract data.
    public void Remarks(RemarkBuilder remarks)
    {
        remarks.Add(remarks.Root.Activity.Append("message"), message, args);
    }

    public sealed class Okay([StructuredMessageTemplate] string? message = null, params object?[] args)
        : BuzzStatus<QuickBuzz>.Okay, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Activity.Status.Append("message"), message, args);
        }
    }

    public sealed class Noop([StructuredMessageTemplate] string? message = null, params object?[] args)
        : BuzzStatus<QuickBuzz>.Noop, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Activity.Status.Append("message"), message, args);
        }
    }

    public sealed class Fail([StructuredMessageTemplate] string? message = null, params object?[] args)
        : BuzzStatus<QuickBuzz>.Fail, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Activity.Status.Append("message"), message, args);
        }
    }
}


public class QuickBulk(string name, [StructuredMessageTemplate] string? message = null, params object?[] args)
    : Buzz<>.Bulk<QuickBulk, QuickItem>, IRemarkSource
{
    // core: Quick activities are intentionally named at runtime instead of by their CLR contract type.
    public override string Name => name;

    // core: Quick contracts carry their optional structured message as contract data.
    public void Remarks(RemarkBuilder remarks)
    {
        remarks.Add(remarks.Root.Activity.Append("message"), message, args);
    }

    public sealed class Okay([StructuredMessageTemplate] string? message = null, params object?[] args)
        : BuzzStatus<QuickBulk>.Okay, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Activity.Status.Append("message"), message, args);
        }
    }

    public sealed class Noop([StructuredMessageTemplate] string? message = null, params object?[] args)
        : BuzzStatus<QuickBulk>.Noop, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Activity.Status.Append("message"), message, args);
        }
    }

    public sealed class Fail([StructuredMessageTemplate] string? message = null, params object?[] args)
        : BuzzStatus<QuickBulk>.Fail, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Activity.Status.Append("message"), message, args);
        }
    }
}
