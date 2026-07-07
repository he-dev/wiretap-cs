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
            buzz.Subscribe(new BuzzLogger(logger));
            buzz.SetStatus(new BuzzStatus.First.Ready());
            return new(buzz);
        }

        public BulkContext<TBulk> BeginBulk<TBulk>(TBulk bulk)
            where TBulk : Buzz.Bulk
        {
            bulk.Subscribe(new BuzzLogger(logger));
            bulk.SetStatus(new BuzzStatus.First.Ready());
            return new(bulk);
        }

        public void LogStatus<TBuzz, TStatus>(TBuzz buzz, TStatus status)
            where TBuzz : Buzz
            where TStatus : BuzzStatus, IStatusOf<TBuzz>
        {
            using var context = logger.BeginBuzz(buzz);
            context.SetStatus(status);
        }
    }
}
