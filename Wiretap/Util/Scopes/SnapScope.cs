using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util.Buzz;

namespace Wiretap.Util.Scopes;

public class SnapScope<TActivity>(ILogger logger, TActivity activity) : ActivityScope<TActivity>(activity) where TActivity : Activity.Snap
{
    internal static void Log(ILogger logger, TActivity activity, ActivityStatus<TActivity> status)
    {
        using var scope = new SnapScope<TActivity>(logger, activity).Also(x => x.Push());
        scope.Log(status);
    }

    private void Log(ActivityStatus<TActivity> status)
    {
        Configuration.DiagnosticLogger.WarnAboutCustomStatusName(
            $"{ActivityName}.{status.GetType().Name}",
            $"{ActivityName}.{status.Code}"
        );
        if (!Activity.SetStatus(status))
        {
            Configuration.DiagnosticLogger.WarnAboutLastStatusOverwrite(
                Activity.Name,
                Activity.Status.Code,
                status.Code
            );
            return;
        }

        logger.LogEntry(Variant.CreateLogEntryBy.From(this));

        TraceHandle.Stop(ok: status switch
        {
            ActivityStatus<TActivity>.Okay => true,
            ActivityStatus<TActivity>.Fail => false,
            _ => null
        });
    }
}
