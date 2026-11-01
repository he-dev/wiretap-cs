using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using Wiretap.Core.Logging;
using Wiretap.Core.Logging.Scopes;
using Wiretap.Util;

namespace Wiretap.Core;

public static class LoggerExtensions
{
    extension(ILogger logger)
    {
        public PropertyScope BeginScopeWith(params (string Name, object? Value)[] properties)
        {
            return PropertyScope.FromParams(logger, properties);
        }

        public ActivityScope BeginScopeAbout
        (
            string about,
            (string Name, object? Value)[]? properties = null,
            Action<Activity>? configure = null
        )
        {
            return ActivityLogger.About(logger, about, properties, configure);
        }

        public ActivityScope BeginScopeAbout<TAbout>
        (
            (string Name, object? Value)[]? properties = null,
            Action<Activity>? configure = null
        )
        {
            return logger.BeginScopeAbout(typeof(TAbout).Name, properties, configure);
        }

        public IDisposable ContinueScopeAbout
        (
            string about,
            string traceId,
            string? parentId = null,
            Action<Activity>? configure = null
        )
        {
            return ContinuationScope.About(logger, about, traceId, parentId, configure);
        }

        public ILogger Domain => new StoryLogger(logger, "Domain");
        public ILogger System => new StoryLogger(logger, "System");
    }

}
