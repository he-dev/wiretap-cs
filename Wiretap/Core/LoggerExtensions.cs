using Microsoft.Extensions.Logging;
using Wiretap.Util;

namespace Wiretap.Core;

public static class LoggerExtensions
{
    extension<T>(ILogger<T> logger)
    {
        public WatchContext<TWatch> BeginWatch<TWatch>(TWatch watch) where TWatch : Watch
        {
            return WatchContext<TWatch>.Builder.Open(logger, watch);
        }

        public BatchContext<TBatch> BeginBatch<TBatch>(TBatch batch) where TBatch : Batch
        {
            return BatchContext<TBatch>.Builder.Open(logger, batch);
        }

        public void LogEvent<TWatch, TWatchEvent>(TWatch watch, TWatchEvent watchEvent)
            where TWatch : Watch
            where TWatchEvent : WatchEvent, IEventOf<TWatch>
        {
            using var context = logger.BeginWatch(watch);
            context.LogEvent(watchEvent);
        }
    }
}
