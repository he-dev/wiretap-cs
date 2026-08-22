using JetBrains.Annotations;
using Wiretap.Util.Data;

namespace Wiretap.Util;

public class WatchLite(string name, [StructuredMessageTemplate] string? message = null, params object?[] args)
    : Watch(name), IRemarkSource
{
    // core: Lite contracts are explicit convenience contracts, not base-type loopholes.
    public void Remarks(RemarkBuilder remarks)
    {
        remarks.Add(remarks.Root.Buzz.Append("message"), message, args);
    }

    public sealed class Okay([StructuredMessageTemplate] string? message = null, params object?[] args)
        : WatchEvent.Last.Okay, IEventOf<WatchLite>, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Buzz.Status.Append("message"), message, args);
        }
    }

    public sealed class Noop([StructuredMessageTemplate] string? message = null, params object?[] args)
        : WatchEvent.Last.Noop, IEventOf<WatchLite>, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Buzz.Status.Append("message"), message, args);
        }
    }

    public sealed class Fail([StructuredMessageTemplate] string? message = null, params object?[] args)
        : WatchEvent.Last.Fail, IEventOf<WatchLite>, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Buzz.Status.Append("message"), message, args);
        }
    }
}

public class BatchLite(string name, [StructuredMessageTemplate] string? message = null, params object?[] args)
    : Batch(name), IRemarkSource
{
    // core: BulkLite gives ad-hoc bulks their own typed status family.
    public void Remarks(RemarkBuilder remarks)
    {
        remarks.Add(remarks.Root.Buzz.Append("message"), message, args);
    }

    public sealed class Okay([StructuredMessageTemplate] string? message = null, params object?[] args)
        : WatchEvent.Last.Okay, IEventOf<BatchLite>, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Buzz.Status.Append("message"), message, args);
        }
    }

    public sealed class Noop([StructuredMessageTemplate] string? message = null, params object?[] args)
        : WatchEvent.Last.Noop, IEventOf<BatchLite>, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Buzz.Status.Append("message"), message, args);
        }
    }

    public sealed class Fail([StructuredMessageTemplate] string? message = null, params object?[] args)
        : WatchEvent.Last.Fail, IEventOf<BatchLite>, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Buzz.Status.Append("message"), message, args);
        }
    }
}

public sealed class BulkLiteItem(string name, [StructuredMessageTemplate] string? message = null, params object?[] args)
    : Batch.Item(name), IItemOf<BatchLite>, IRemarkSource
{
    // core: BulkLiteItem belongs to BulkLite but derives from the ordinary buzz base.
    public void Remarks(RemarkBuilder remarks)
    {
        remarks.Add(remarks.Root.Buzz.Append("message"), message, args);
    }

    public sealed class Okay([StructuredMessageTemplate] string? message = null, params object?[] args)
        : WatchEvent.Last.Okay, IEventOf<BulkLiteItem>, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Buzz.Status.Append("message"), message, args);
        }
    }

    public sealed class Noop([StructuredMessageTemplate] string? message = null, params object?[] args)
        : WatchEvent.Last.Noop, IEventOf<BulkLiteItem>, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Buzz.Status.Append("message"), message, args);
        }
    }

    public sealed class Fail([StructuredMessageTemplate] string? message = null, params object?[] args)
        : WatchEvent.Last.Fail, IEventOf<BulkLiteItem>, IRemarkSource
    {
        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(remarks.Root.Buzz.Status.Append("message"), message, args);
        }
    }
}
