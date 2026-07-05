using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util;
using Wiretap.Util.Scopes;

namespace Wiretap.Core;

public static class LoggerExtensions
{
    extension<T>(ILogger<T> logger)
    {
        public TBuzz BeginBuzz<TBuzz>(TBuzz activity) where TBuzz : Buzz<TBuzz>
        {
            return activity.Also(x => x.Subscribe(new BuzzScope<TBuzz>(ActivityLogger.From(logger), activity)));

        }

        public Buzz<TBulk>.Bulk<TItem> BeginBulk<TBulk, TItem>(TBulk activity)
            where TBulk : Buzz<TBulk>.Bulk<TItem>
            where TItem : Buzz<TItem>
        {
            return activity.Also(x => x.Subscribe(new BulkScope<TBulk, TItem>(ActivityLogger.From(logger), activity)));
            //return new BulkScope<TBulk, TItem>(ActivityLogger.From(logger), (TBulk)activity).Also(x => x.Push());
        }

        public void LogSnap<TActivity>(TActivity activity, BuzzStatus<TActivity> status) where TActivity : Buzz<TActivity>
        {
            //SnapScope<TActivity>.Log(logger, activity, status);
        }
    }

}
