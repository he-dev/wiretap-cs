using JetBrains.Annotations;
using Wiretap.Util.Data;

namespace Wiretap.Util;

public class BuzzLite(string name, [StructuredMessageTemplate] string? message = null, params object?[] args)
    : Buzz(name), IRemarkSource
{
    // core: Lite contracts are explicit convenience contracts, not base-type loopholes.
    public void Remarks(RemarkBuilder remarks)
    {
        remarks.Add(remarks.Root.Buzz.Append("message"), message, args);
    }

    public sealed class Okay([StructuredMessageTemplate] string? message = null, params object?[] args)
        : BuzzStatus.Last.Okay, IStatusOf<BuzzLite>, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Buzz.Status.Append("message"), message, args);
        }
    }

    public sealed class Noop([StructuredMessageTemplate] string? message = null, params object?[] args)
        : BuzzStatus.Last.Noop, IStatusOf<BuzzLite>, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Buzz.Status.Append("message"), message, args);
        }
    }

    public sealed class Fail([StructuredMessageTemplate] string? message = null, params object?[] args)
        : BuzzStatus.Last.Fail, IStatusOf<BuzzLite>, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Buzz.Status.Append("message"), message, args);
        }
    }
}

public class BulkLite(string name, [StructuredMessageTemplate] string? message = null, params object?[] args)
    : Buzz.Bulk(name), IRemarkSource
{
    // core: BulkLite gives ad-hoc bulks their own typed status family.
    public void Remarks(RemarkBuilder remarks)
    {
        remarks.Add(remarks.Root.Buzz.Append("message"), message, args);
    }

    public sealed class Okay([StructuredMessageTemplate] string? message = null, params object?[] args)
        : BuzzStatus.Last.Okay, IStatusOf<BulkLite>, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Buzz.Status.Append("message"), message, args);
        }
    }

    public sealed class Noop([StructuredMessageTemplate] string? message = null, params object?[] args)
        : BuzzStatus.Last.Noop, IStatusOf<BulkLite>, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Buzz.Status.Append("message"), message, args);
        }
    }

    public sealed class Fail([StructuredMessageTemplate] string? message = null, params object?[] args)
        : BuzzStatus.Last.Fail, IStatusOf<BulkLite>, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Buzz.Status.Append("message"), message, args);
        }
    }
}

public sealed class BulkLiteItem(string name, [StructuredMessageTemplate] string? message = null, params object?[] args)
    : Buzz(name), IItemOf<BulkLite>, IRemarkSource
{
    // core: BulkLiteItem belongs to BulkLite but derives from the ordinary buzz base.
    public void Remarks(RemarkBuilder remarks)
    {
        remarks.Add(remarks.Root.Buzz.Append("message"), message, args);
    }

    public sealed class Okay([StructuredMessageTemplate] string? message = null, params object?[] args)
        : BuzzStatus.Last.Okay, IStatusOf<BulkLiteItem>, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Buzz.Status.Append("message"), message, args);
        }
    }

    public sealed class Noop([StructuredMessageTemplate] string? message = null, params object?[] args)
        : BuzzStatus.Last.Noop, IStatusOf<BulkLiteItem>, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Buzz.Status.Append("message"), message, args);
        }
    }

    public sealed class Fail([StructuredMessageTemplate] string? message = null, params object?[] args)
        : BuzzStatus.Last.Fail, IStatusOf<BulkLiteItem>, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Buzz.Status.Append("message"), message, args);
        }
    }
}
