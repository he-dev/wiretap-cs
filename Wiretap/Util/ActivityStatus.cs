using Microsoft.Extensions.Logging;
using Wiretap.Util.Data;

namespace Wiretap.Util;

public enum LogStatusPolicy
{
    Auto,
    Sure,
    Nope
}

public interface IActivityStatus
{
    string Code { get; }
    LogLevel Level { get; }
    Exception? Exception { get; }
    Func<LogStatusPolicy> LogStatusPolicy { get; }
}

public abstract class ActivityStatus_
{
    protected ActivityStatus_()
    {
        // todo: fix this warning
        // Util.Configuration.Default.DiagnosticLogger.WarnAboutCustomStatusName(
        //     $"{Name}.{status.GetType().Name}",
        //     $"{Name}.{status.Code}"
        // );
    }

    public abstract string Code { get; }

    public abstract LogLevel Level { get; }

    public Exception? Exception { get; init; }

    public Func<LogStatusPolicy> CanLog = () => LogStatusPolicy.Auto;


}

// core: The generic parameter ensures statuses can only be used with their activity contract.
// ReSharper disable once UnusedTypeParameter
public abstract class ActivityStatus<TActivity> : IActivityStatus where TActivity : Activity<TActivity>
{
    public abstract string Code { get; }
    public abstract LogLevel Level { get; }
    public Exception? Exception { get; init; }
    public Func<LogStatusPolicy> LogStatusPolicy  { get; } = () => Util.LogStatusPolicy.Auto;

    // core: The first status emitted by a buzz when its scope is entered.
    internal sealed class Pending : ActivityStatus<TActivity>, ActivityStatusRole.IFirst
    {
        public override string Code => nameof(Pending);

        public override LogLevel Level => LogLevel.Information;
    }    // core: The first status emitted by a buzz when its scope is entered.

    internal sealed class Ready : ActivityStatus<TActivity>, ActivityStatusRole.IFirst
    {
        public override string Code => nameof(Ready);

        public override LogLevel Level => LogLevel.Information;
    }

    internal sealed class Cold : ActivityStatus<TActivity>
    {
        public override string Code => nameof(Cold);

        public override LogLevel Level => LogLevel.Information;
    }

    // core: Everything went according to plan.
    public abstract class Okay : ActivityStatus<TActivity>, ActivityStatusRole.ILast
    {
        public override string Code => nameof(Okay);

        public override LogLevel Level => LogLevel.Information;
    }

    // core: The activity intentionally did nothing.
    public abstract class Noop : ActivityStatus<TActivity>, ActivityStatusRole.ILast
    {
        public override string Code => nameof(Noop);

        public override LogLevel Level => LogLevel.Information;
    }

    // core: An error occurred.
    public abstract class Fail : ActivityStatus<TActivity>, ActivityStatusRole.ILast
    {
        public override string Code => nameof(Fail);

        public override LogLevel Level => LogLevel.Error;

        [Remark("Exception")]
        public string? ExceptionMessage => Exception?.Message;
    }

    // core: Framework fallback when a buzz exits without an explicit last status.
    internal sealed class Void : ActivityStatus<TActivity>, ActivityStatusRole.ILast
    {
        public override string Code => nameof(Void);

        public override LogLevel Level => LogLevel.Warning;

        [Remark]
        public string Reason => "The activity scope exited without an explicit last status.";
    }
}