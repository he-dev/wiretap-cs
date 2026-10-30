using Microsoft.Extensions.Logging;
using Wiretap.Util;

namespace Wiretap.Core.Logging;

public sealed class EnvelopeLogger : ActivityScope
{
    protected override ActivityScope Logger { get; }
    public override ILogger Zero => Logger.Zero;
    public override ILogger Tick => Logger.Tick;
    public override ILogger Last => Logger.Last;

    private EnvelopeMode Mode { get; }

    public EnvelopeLogger(ActivityScope outer, EnvelopeMode mode = EnvelopeMode.Auto)
    {
        Logger = outer;
        Mode = mode;

        if (mode is EnvelopeMode.Auto or EnvelopeMode.ZeroOnly)
        {
            outer.Zero.System.LogDebug(null);
        }
    }

    public override void Dispose()
    {
        try
        {
            if (Mode is EnvelopeMode.Auto or EnvelopeMode.LastOnly)
            {
                Logger.Last.System.LogInformation(null);
            }
        }
        finally
        {
            // core: The envelope owns the activity scope supplied to its constructor.
            Logger.Dispose();
        }
    }
}