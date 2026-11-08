using Microsoft.Extensions.Logging;
using Wiretap.Util.Logging.Scopes;

namespace Wiretap.Util.Logging.Events;

public sealed class TickLogger(ActivityScope scope) : EventLogger(scope, ActivityEvent.Tick)
{
    protected override (string Name, object? Value)[]? Log(LogLevel logLevel)
    {
        if (Scope.History.Contains(ActivityEvent.Last))
        {
            Scope.Beep(LogLevel.Error, "no-events-must-follow-last", "TICK follows LAST for activity {activity.about}.");
        }

        return [];
    }
}
