using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util;

namespace Wiretap.Core;

public static class LoggerExtensions
{
    extension<T>(ILogger<T> logger)
    {
        public TBuzz BeginBuzz<TBuzz>(TBuzz buzz) where TBuzz : Buzz<TBuzz>
        {
            return
                buzz
                    .Also(x => x.Subscribe(new ActivityLogger(logger)))
                    .Also(x => x.SetStatus(new BuzzStatus<TBuzz>.Ready()));
        }

        public Buzz<TBulk>.Bulk<TItem> BeginBulk<TBulk, TItem>(Buzz<TBulk>.Bulk<TItem> bulk)
            where TBulk : Buzz<TBulk>.Bulk<TItem>
            where TItem : Buzz<TItem>
        {
            return
                bulk
                    .Also(x => x.Subscribe(new ActivityLogger(logger)))
                    .Also(x => x.SetStatus(new BuzzStatus<TBulk>.Ready()));
        }

        public void LogSnap<TBuzz>(TBuzz buzz, BuzzStatus<TBuzz> status) where TBuzz : Buzz<TBuzz>
        {
            //SnapScope<TActivity>.Log(logger, activity, status);
        }
    }
}