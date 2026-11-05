using Microsoft.Extensions.Logging;
using Wiretap.Util.Logging.Scopes;

namespace Wiretap.Util.Logging.Events;

public sealed class ZeroLogger(PlotScope scope) : EventLogger(scope, PlotEvent.Zero)
{
    protected override (string Name, object? Value)[]? Log(LogLevel logLevel)
    {
        if (Scope.History.Contains(PlotEvent.Last))
        {
            Scope.Beep(LogLevel.Error, "no-events-must-follow-last", "Plot {plot.about} logs ZEROs that follow LAST.");
        }

        if (Scope.History.Contains(PlotEvent.Zero))
        {
            Scope.Beep(LogLevel.Error, "zero-occurs-more-than-once", "Plot {plot.about} logs ZERO more than once.");
        }

        if (Scope.History.Contains(PlotEvent.Tick))
        {
            Scope.Beep(LogLevel.Error, "zero-must-precede-tick", "Plot {plot.about} logs ZEROs that follow TICK.");
        }

        return [];
    }
}
