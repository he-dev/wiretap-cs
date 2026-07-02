using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util;
using Wiretap.Util.Scopes;

namespace Wiretap.Core;

public static class LoggerExtensions
{
    extension<T>(ILogger<T> logger)
    {
        public BuzzScope<TActivity> BeginBuzz<TActivity>(TActivity activity) where TActivity : Activity.Buzz
        {
            return new BuzzScope<TActivity>(ActivityLogger.From(logger), activity).Also(x => x.Push());
        }

        public BulkScope<TBulk, TItem> BeginBulk<TBulk, TItem>(Activity.Bulk<TBulk, TItem> activity)
            where TBulk : Activity.Bulk<TBulk, TItem>
            where TItem : Activity.Item
        {
            return new BulkScope<TBulk, TItem>(ActivityLogger.From(logger), (TBulk)activity).Also(x => x.Push());
        }

        public void LogSnap<TActivity>(TActivity activity, ActivityStatus<TActivity> status) where TActivity : Activity.Snap
        {
            SnapScope<TActivity>.Log(logger, activity, status);
        }
    }

}
