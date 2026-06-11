using Microsoft.Extensions.Logging;
using Wiretap.Util;

namespace Wiretap.Core;

public static class LoggerExtensions
{
    extension<T>(ILogger<T> logger)
    {
        public BuzzScope<TActivity> BeginBuzz<TActivity>(TActivity activity) where TActivity : Activity.Buzz
        {
            return BuzzScope<TActivity>.BeginBuzz(logger, activity);
        }

        public void LogSnap<TActivity>(TActivity activity, ActivityStatus<TActivity> status) where TActivity : Activity.Snap
        {
            using var scope = new SnapScope<TActivity>(logger, activity);
            scope.Log(status);
        }

        public ILogger<MessageRole.Data> Data => new LoggerProxy<MessageRole.Data>(logger).WithStateItem(nameof(MessageRole), nameof(MessageRole.Data));
        public ILogger<MessageRole.Note> Note => new LoggerProxy<MessageRole.Note>(logger).WithStateItem(nameof(MessageRole), nameof(MessageRole.Note));
        public ILogger<MessageRole.Echo> Echo => new LoggerProxy<MessageRole.Echo>(logger).WithStateItem(nameof(MessageRole), nameof(MessageRole.Echo));
    }

}
