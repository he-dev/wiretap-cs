using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util.Buzz;

namespace Wiretap.Util.Scopes;

public class SnapScope<TActivity>(ActivityLogger logger, TActivity activity) : ActivityScope<TActivity>(logger, activity) where TActivity : Activity.Snap
{
    internal static void Log(ILogger logger, TActivity activity, ActivityStatus<TActivity> status)
    {
        using var scope = new SnapScope<TActivity>(new ActivityLogger(logger), activity).Also(x => x.Push());
        scope.Log(status);
    }

    private void Log(ActivityStatus<TActivity> status)
    {
        if (!Activity.SetStatus(status))
        {
            Util.Configuration.DiagnosticLogger.WarnAboutLastStatusOverwrite(
                Activity.Name,
                Activity.Status.Code,
                status.Code
            );
            return;
        }

        TraceHandle.Stop(ok: status switch
        {
            ActivityStatus<TActivity>.Okay => true,
            ActivityStatus<TActivity>.Fail => false,
            _ => null
        });

        LogStatus();
    }
}