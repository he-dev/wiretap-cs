using Microsoft.Extensions.Logging;

namespace Wiretap.Util.Scopes;

public delegate void OnLastStatus<TActivity>(ActivityStatus<TActivity> status, TimeSpan duration)
    where TActivity : Activity.Buzz;

public class BuzzScope<TActivity>
(
    ActivityLogger logger,
    TActivity activity,
    OmitStatus omitStatus = OmitStatus.None,
    OnLastStatus<TActivity>? onLastStatus = null
) : ActivityScope<TActivity>(logger, activity) where TActivity : Activity.Buzz
{
    private bool _disposed;

    public void SetStatus(ActivityStatus<TActivity> status)
    {
        Util.Configuration.Default.DiagnosticLogger.WarnAboutCustomStatusName(
            $"{ActivityName}.{status.GetType().Name}",
            $"{ActivityName}.{status.Code}"
        );

        if (!Activity.SetStatus(status))
        {
            Util.Configuration.Default.DiagnosticLogger.WarnAboutLastStatusOverwrite(
                Activity.Name,
                Activity.Status.Code,
                status.Code
            );
        }
    }

    internal override void Push()
    {
        Activity.Start();
        base.Push();
        Activity.SetStatus(new ActivityStatus<TActivity>.Ready());

        if (!omitStatus.HasFlag(OmitStatus.First))
        {
            LogStatus();
        }
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (Activity.Status is not ActivityStatusRole.ILast)
            {
                Activity.SetStatus(new ActivityStatus<TActivity>.Void());
            }

            TraceHandle.Stop(ok: Activity.Status switch
            {
                ActivityStatus<TActivity>.Okay => true,
                ActivityStatus<TActivity>.Fail => false,
                _ => null
            });

            if (!omitStatus.HasFlag(OmitStatus.Last))
            {
                LogStatus();
            }

            onLastStatus?.Invoke((ActivityStatus<TActivity>)Activity.Status, Activity.Duration);
        }
        finally
        {
            base.Dispose();
            _disposed = true;
        }
    }
}
