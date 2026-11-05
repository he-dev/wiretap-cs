using Microsoft.Extensions.Logging;
using Wiretap.Util.Logging.Scopes;

namespace Wiretap.Util.Logging.Events;

public sealed class TickLogger(PlotScope scope) : EventLogger(scope, PlotEvent.Tick)
{
    protected override (string Name, object? Value)[]? Log(LogLevel logLevel)
    {
        if (Scope.History.Contains(PlotEvent.Last))
        {
            Scope.Beep(LogLevel.Error, "no-events-must-follow-last", "TICK follows LAST for plot {plot.about}.");
        }

        return [];
    }
}
