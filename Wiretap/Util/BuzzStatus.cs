using Microsoft.Extensions.Logging;
using Wiretap.Util.Data;

namespace Wiretap.Util;

public enum LogStatusPolicy
{
    Auto,
    Sure,
    Nope
}

public interface IAssociatedWith<T> where T : Buzz;

public class Status
{
    public virtual string Code => GetType().Name;
    public virtual LogLevel Level => LogLevel.Information;
    public Exception? Exception { get; init; }
    public Func<LogStatusPolicy> LogPolicy { get; init; } = () => Util.LogStatusPolicy.Auto;

    // core: The first status emitted by a buzz when its scope is entered.
    internal sealed class Pending : Status, ActivityStatusRole.IFirst;

    // core: Ready is framework-owned and starts timing/logging for an active buzz.
    internal sealed class Ready : Status, ActivityStatusRole.IFirst;

    // core: Cold marks a buzz that has left the active lifecycle.
    internal sealed class Cold : Status;

    // core: Everything went according to plan.
    public class Okay : Status, IAssociatedWith<Buzz>, ActivityStatusRole.ILast;

    // core: The activity intentionally did nothing.
    public class Noop : Status, IAssociatedWith<Buzz>, ActivityStatusRole.ILast;

    // core: An error occurred.
    public class Fail : Status, IAssociatedWith<Buzz>, ActivityStatusRole.ILast
    {
        public override LogLevel Level => LogLevel.Error;

        [Remark("Exception")]
        public string? ExceptionMessage => Exception?.Message;
    }

    // core: Framework fallback when a buzz exits without an explicit last status.
    internal sealed class Void : Status, ActivityStatusRole.ILast
    {
        public override LogLevel Level => LogLevel.Warning;

        [Remark]
        public string Reason => "The buzz exited without an explicit last status.";
    }
}
