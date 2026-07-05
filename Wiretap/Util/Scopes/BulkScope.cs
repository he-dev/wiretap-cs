using Wiretap.Meta;

namespace Wiretap.Util.Scopes;

public sealed class BulkScope<TBulk, TItem>(ActivityLogger logger, Buzz<TBulk>.Bulk<TItem> bulk)
    : BuzzScope<TBulk>(logger, (TBulk)bulk)
    where TBulk : Buzz<TBulk>.Bulk<TItem>
    where TItem : Buzz<TItem>;
