using Wiretap.Meta;

namespace Wiretap.Util.Scopes;

public sealed class BulkScope<TBulk, TItem>(ActivityLogger logger, TBulk bulk)
    : BuzzScope<TBulk>(logger, bulk)
    where TBulk : Buzz<TBulk>.Bulk<TItem>
    where TItem : Buzz<TItem>;
