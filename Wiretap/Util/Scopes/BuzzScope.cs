using Microsoft.Extensions.Logging;

namespace Wiretap.Util.Scopes;

public delegate void OnLastStatus<TActivity>(ActivityStatus<TActivity> status, TimeSpan duration)
    where TActivity : Activity.Buzz;

public class BuzzScope<TActivity>
(
    ILogger logger,
    TActivity activity,
    OmitStatus omitStatus = OmitStatus.None,
    OnLastStatus<TActivity>? onLastStatus = null
) : ActivityScope<TActivity>(activity) where TActivity : Activity.Buzz
{
    private bool _disposed;

    protected ILogger Logger => logger;

    public void SetStatus(ActivityStatus<TActivity> status)
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
        }
    }

    private void LogStatus() => logger.LogEntry(Variant.CreateLogEntryBy.From(this));

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
