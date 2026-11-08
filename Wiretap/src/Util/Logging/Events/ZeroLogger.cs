using Microsoft.Extensions.Logging;
using Wiretap.Util.Logging.Scopes;

namespace Wiretap.Util.Logging.Events;

public sealed class ZeroLogger(ActivityScope scope) : EventLogger(scope, ActivityEvent.Zero)
{
    protected override (string Name, object? Value)[]? Log(LogLevel logLevel)
    {
        if (Scope.History.Contains(ActivityEvent.Last))
        {
            Scope.Beep(LogLevel.Error, "no-events-must-follow-last", "Activity {activity.about} logs ZEROs that follow LAST.");
        }

        if (Scope.History.Contains(ActivityEvent.Zero))
        {
            Scope.Beep(LogLevel.Error, "zero-occurs-more-than-once", "Activity {activity.about} logs ZERO more than once.");
        }

        if (Scope.History.Contains(ActivityEvent.Tick))
        {
            Scope.Beep(LogLevel.Error, "zero-must-precede-tick", "Activity {activity.about} logs ZEROs that follow TICK.");
        }

        return [];
    }
}
