using Microsoft.Extensions.Logging;
using Wiretap.Util.Data;

namespace Wiretap.Util;

public interface IEventOf<TWatch> where TWatch : Watch;

public interface IItemOf<TBatch> where TBatch : Batch;

public abstract class WatchEvent
{
    public abstract string Role { get; }
    public abstract LogLevel Level { get; }
    public Exception? Exception { get; init; }

    public override string ToString() => GetType().Name;

    internal abstract class First : WatchEvent
    {
        // core: Ready is framework-owned and starts timing/logging for an active buzz.
        internal sealed class Open : First
        {
            public override string Role => "first";

            public override LogLevel Level => LogLevel.Information;
        }
    }

    public abstract class Last : WatchEvent
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

            [MessagePart(Label = "Exception")]
            public string? ExceptionMessage => Exception?.Message;
        }

        // core: Framework fallback when a buzz exits without an explicit last status.
        internal sealed class Void : Last
        {
            public override LogLevel Level => LogLevel.Warning;

            [MessagePart]
            public string Reason => "The buzz exited without an explicit last status.";
        }
    }

    internal abstract class Idle : WatchEvent
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