using Microsoft.Extensions.Logging;
using Wiretap.Util.Data;

namespace Wiretap.Util;

public interface IStatusOf<TBuzz> where TBuzz : Buzz;

public interface IItemOf<TBulk> where TBulk : Buzz.Bulk;

public abstract class BuzzStatus
{
    public abstract string Role { get; }
    public abstract LogLevel Level { get; }
    public string Code => GetType().Name;
    public Exception? Exception { get; init; }

    internal abstract class First : BuzzStatus
    {
        // core: Ready is framework-owned and starts timing/logging for an active buzz.
        internal sealed class Ready : First
        {
            public override string Role => "first";

            public override LogLevel Level => LogLevel.Information;
        }
    }

    public abstract class Last : BuzzStatus
    {
        public override string Role => "last";

        // core: The activity intentionally did nothing.
        public class Noop : Last
        {
            public override LogLevel Level => LogLevel.Information;
        }

        // core: Everything went according to plan.
        public class Okay : Last
        {
            public override LogLevel Level => LogLevel.Information;
        }

        // core: An error occurred.
        public class Fail : Last
        {
            public override LogLevel Level => LogLevel.Error;

            [Remark(Label = "Exception")]
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

    internal abstract class Idle : BuzzStatus
    {
        public override string Role => "idle";

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