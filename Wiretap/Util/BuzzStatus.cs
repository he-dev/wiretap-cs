using Microsoft.Extensions.Logging;
using Wiretap.Util.Data;

namespace Wiretap.Util;

public interface IStatusOf<TBuzz> where TBuzz : Buzz;

public interface IItemOf<TBulk> where TBulk : Buzz.Bulk;

public abstract class Status
{
    public abstract LogLevel Level { get; }
    public string Code => GetType().Name;
    public Exception? Exception { get; init; }

    internal abstract class First : Status
    {
        // core: Ready is framework-owned and starts timing/logging for an active buzz.
        internal sealed class Ready : First
        {
            public override LogLevel Level => LogLevel.Information;
        }
    }

    public abstract class Last : Status
    {
        // core: The activity intentionally did nothing.
        public class Noop : Last, IStatusOf<Buzz>, IStatusOf<Buzz.Bulk>
        {
            public override LogLevel Level => LogLevel.Information;
        }

        // core: Everything went according to plan.
        public class Okay : Last, IStatusOf<Buzz>, IStatusOf<Buzz.Bulk>
        {
            public override LogLevel Level => LogLevel.Information;
        }

        // core: An error occurred.
        public class Fail : Last, IStatusOf<Buzz>, IStatusOf<Buzz.Bulk>
        {
            public override LogLevel Level => LogLevel.Error;

            [Remark("Exception")]
            public string? ExceptionMessage => Exception?.Message;
        }

        // core: Framework fallback when a buzz exits without an explicit last status.
        internal sealed class Void : Last
        {
            public override LogLevel Level => LogLevel.Warning;

            [Remark]
            public string Reason => "The buzz exited without an explicit last status.";
        }
    }

    internal abstract class Idle : Status
    {
        // core: The first status emitted by a buzz when its scope is entered.
        internal sealed class Pending : Idle
        {
            public override LogLevel Level => LogLevel.Debug;
        }

        // core: Cold marks a buzz that has left the active lifecycle.
        internal sealed class Cold : Idle
        {
            public override LogLevel Level => LogLevel.Debug;
        }
    }
}