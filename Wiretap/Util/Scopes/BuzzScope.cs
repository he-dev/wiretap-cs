namespace Wiretap.Util.Scopes;

public delegate void OnLastStatus(string code, TimeSpan duration);

public class BuzzScope<TActivity>(ActivityLogger logger, TActivity activity)
    : ActivityScope<TActivity>(logger, activity) where TActivity : Activity.Buzz
{
    private bool _disposed;

    public OmitStatus OmitStatus { get; init; } = OmitStatus.None;

    public OnLastStatus OnLastStatus { get; init; } = (_, _) => { };

    public void SetStatus(ActivityStatus<TActivity> status)
    {
        Util.Configuration.Default.DiagnosticLogger.WarnAboutCustomStatusName(
            $"{ActivityName}.{status.GetType().Name}",
            $"{ActivityName}.{status.Code}"
        );

        if (Activity.SetStatus(status))
        {
            if (status is ActivityStatusRole.ILast)
            {
                OnLastStatus(Activity.Status.Code, Activity.Duration);
            }
        }
        else
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
        base.Push();
        Activity.Start();
        Activity.SetStatus(new ActivityStatus<TActivity>.Ready());

        if (!OmitStatus.HasFlag(OmitStatus.First))
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

            if (!OmitStatus.HasFlag(OmitStatus.Last))
            {
                LogStatus();
            }
        }
        finally
        {
            base.Dispose();
            _disposed = true;
        }
    }
}