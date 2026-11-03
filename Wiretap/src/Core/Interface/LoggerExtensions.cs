using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Wiretap.Util.Logging;
using Wiretap.Util.Logging.Scopes;

namespace Wiretap.Core;

public static class LoggerExtensions
{
    extension(ILogger logger)
    {
        public PropertyLogger With(params (string Name, object? Value)[] properties)
        {
            return PropertyLogger.With(logger, properties);
        }

        public PropertyScope BeginScopeWith(params (string Name, object? Value)[] properties)
        {
            return PropertyScope.With(logger, properties);
        }

        public ActivityScope BeginScopeAbout
        (
            string about,
            (string Name, object? Value)[]? properties = null,
            Action<Activity>? configure = null
        )
        {
            return ActivityScope.About(logger, about, properties, configure);
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

        public ILogger WithCallerInfo
        (
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0
        )
        {
            return PropertyLogger.With(logger,
            [
                ("source.func", member),
                ("source.file", file),
                ("source.line", line),
            ]);
        }
    }
}