using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Wiretap.Util.Logging;
using Wiretap.Util.Logging.Events;
using Wiretap.Util.Logging.Scopes;

namespace Wiretap.Core;

public static class LoggerExtensions
{
    extension(ILogger logger)
    {
        public SnapLogger Snap(string about, Story story)
        {
            return new SnapLogger(logger, about, story);
        }

        public PropertyLogger With(params (string Name, object? Value)[] properties)
        {
            return PropertyLogger.With(logger, properties);
        }

        public PropertyScope BeginScopeWith(params (string Name, object? Value)[] properties)
        {
            return PropertyScope.With(logger, properties);
        }

        public PlotScope BeginPlotAbout
        (
            string about,
            Story story,
            (string Name, object? Value)[]? properties = null,
            Action<Activity>? configure = null
        )
        {
            return PlotScope.About(logger, about, story, properties, configure);
        }

        public PlotScope BeginPlotAbout<TAbout>
        (
            Story story,
            (string Name, object? Value)[]? properties = null,
            Action<Activity>? configure = null
        )
        {
            return logger.BeginPlotAbout(typeof(TAbout).Name, story, properties, configure);
        }

        public IDisposable ContinuePlotAbout
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
