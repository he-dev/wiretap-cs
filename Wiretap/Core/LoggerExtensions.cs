using Microsoft.Extensions.Logging;
using Wiretap.Util;

namespace Wiretap.Core;

public static class LoggerExtensions
{
    extension<T>(ILogger<T> logger)
    {
        public BuzzContext<TBuzz> BeginBuzz<TBuzz>(TBuzz buzz)
            where TBuzz : Buzz
        {
            buzz.Subscribe(new ActivityLogger(logger));
            buzz.SetStatus(new Status.First.Ready());
            return new(buzz);
        }

        public BulkContext<TBulk> BeginBulk<TBulk>(TBulk bulk)
            where TBulk : Buzz.Bulk
        {
            bulk.Subscribe(new ActivityLogger(logger));
            bulk.SetStatus(new Status.First.Ready());
            return new(bulk);
        }

        public bool LogStatus<TBuzz, TStatus>(TBuzz buzz, TStatus status)
            where TBuzz : Buzz
            where TStatus : Status, IStatusOf<TBuzz>
        {
            using var context = logger.BeginBuzz(buzz);
            return context.SetStatus(status);
        }
    }
}
