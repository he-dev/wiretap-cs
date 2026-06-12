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
            return new BuzzScope<TActivity>(logger, activity).Also(x => x.Push());
        }

        public BulkScope<TActivity, TItem> BeginBulk<TActivity, TItem>(Activity.Bulk<TActivity, TItem> activity)
            where TActivity : Activity.Bulk<TActivity, TItem>
            where TItem : Activity.Buzz
        {
            return new BulkScope<TActivity, TItem>(logger, (TActivity)activity).Also(x => x.Push());
        }

        public void LogSnap<TActivity>(TActivity activity, ActivityStatus<TActivity> status) where TActivity : Activity.Snap
        {
            SnapScope<TActivity>.Log(logger, activity, status);
        }
    }

}
