using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Wiretap.Util.Logging.Scopes;

namespace Wiretap.Util.Logging.Events;

public sealed class LastLogger(PlotScope scope, bool whenFirstOnly = false) : EventLogger(scope, PlotEvent.Last)
{
    public LastLogger WhenFirstOnly => whenFirstOnly ? this : new LastLogger(Scope, whenFirstOnly: true);

    protected override (string Name, object? Value)[]? Log(LogLevel logLevel)
    {
        if (Scope.History.Contains(PlotEvent.Last))
        {
            // core: Scope closure does not add another last if one already occurred.
            if (whenFirstOnly)
            {
                return null;
            }

            Scope.Beep(LogLevel.Error, "no-events-must-follow-last", "Plot {plot.about} logs more than one LAST.");

            // core: A repeated explicit last preserves the first last's duration and status.
            return
            [
                ("plot.duration_ms", (long)Scope.Activity.Duration.TotalMilliseconds)
            ];
        }

        // core: Only the first last reports omissions and captures the plot's duration.
        if (!Scope.History.Contains(PlotEvent.Zero))
        {
            Scope.Beep(LogLevel.Debug, "does-not-log-zero", "Plot {plot.about} reaches LAST without ZERO.");
        }

        if (whenFirstOnly)
        {
            Scope.Beep(LogLevel.Debug, "logs-last-silently", "Plot {plot.about} ends without an explicit LAST.");
        }

        Scope.Activity.SetEndTime(DateTime.UtcNow);
        switch (logLevel)
        {
            case LogLevel.None:
                // core: Leave the status as-is.
                break;
            case LogLevel.Trace:
            case LogLevel.Debug:
            case LogLevel.Information:
            case LogLevel.Warning:
                Scope.Activity.SetStatus(ActivityStatusCode.Ok);
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                Scope.Activity.SetStatus(ActivityStatusCode.Error);
                break;
        }

        return
        [
            ("plot.duration_ms", (long)Scope.Activity.Duration.TotalMilliseconds)
        ];
    }
}
