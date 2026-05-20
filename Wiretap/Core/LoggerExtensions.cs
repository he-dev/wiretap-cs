using Microsoft.Extensions.Logging;
using Wiretap.Util;

namespace Wiretap.Core;

public static class LoggerExtensions
{
    extension<T>(ILogger<T> logger)
    {
        public ActivityScope<TActivity> Begin<TActivity>(TActivity activity) where TActivity : Activity
        {
            return ActivityScope<TActivity>.Begin(logger, activity);
        }

        public ILogger<Activity.Core> Core => new LoggerProxy<Activity.Core>(logger).WithStateItem(nameof(Activity), nameof(Activity.Core));
        public ILogger<Activity.Buzz> Buzz => new LoggerProxy<Activity.Buzz>(logger).WithStateItem(nameof(Activity), nameof(Activity.Buzz));

        public ILogger<MessageRole.Data> Data => new LoggerProxy<MessageRole.Data>(logger).WithStateItem(nameof(MessageRole), nameof(MessageRole.Data));
        public ILogger<MessageRole.Note> Note => new LoggerProxy<MessageRole.Note>(logger).WithStateItem(nameof(MessageRole), nameof(MessageRole.Note));
        public ILogger<MessageRole.Echo> Echo => new LoggerProxy<MessageRole.Echo>(logger).WithStateItem(nameof(MessageRole), nameof(MessageRole.Echo));
    }
}