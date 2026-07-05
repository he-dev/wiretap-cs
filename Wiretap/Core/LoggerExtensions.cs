using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util;
using Wiretap.Util.Scopes;

namespace Wiretap.Core;

public static class LoggerExtensions
{
    extension<T>(ILogger<T> logger)
    {
        public TActivity BeginBuzz<TActivity>(TActivity activity) where TActivity : Activity<TActivity>
        {
            return activity.Also(x => x.Subscribe(new BuzzScope<TActivity>(ActivityLogger.From(logger), activity)));

        }

        public BulkScope<TBulk, TItem> BeginBulk<TBulk, TItem>(Activity<TItem>.Bulk<TBulk, TItem> activity)
            where TBulk : Activity<TItem>.Bulk<TBulk, TItem>
            where TItem : Activity<TItem>.Item
        {
            return new BulkScope<TBulk, TItem>(ActivityLogger.From(logger), (TBulk)activity).Also(x => x.Push());
        }

        public void LogSnap<TActivity>(TActivity activity, ActivityStatus<TActivity> status) where TActivity : Activity<TActivity>
        {
            //SnapScope<TActivity>.Log(logger, activity, status);
        }
    }

}
