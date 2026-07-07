using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util;

namespace Wiretap.Core;

public static class LoggerExtensions
{
    extension<T>(ILogger<T> logger)
    {
        public BuzzContext<TBuzz> BeginBuzz<TBuzz>(TBuzz buzz) where TBuzz : Buzz
        {
            buzz.Subscribe(new ActivityLogger(logger));
            buzz.SetStatus(new Status.Ready());
            return new BuzzContext<TBuzz>(buzz);
        }

        public BulkContext<TItem> BeginBulk<TItem>(Buzz.Bulk<TItem> bulk)
            where TItem : Buzz
        {
            bulk.Subscribe(new ActivityLogger(logger));
            bulk.SetStatus(new Status.Ready());
            return new BulkContext<TItem>(bulk);
        }

        public void LogStatus<TBuzz, TStatus>(TBuzz buzz, TStatus status)
            where TBuzz : Buzz
            where TStatus : Status, IAssociatedWith<TBuzz>
        {
            using var context = logger.BeginBuzz(buzz);
            context.SetStatus(status);
        }
    }
}
