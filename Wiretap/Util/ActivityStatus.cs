using Microsoft.Extensions.Logging;
using Wiretap.Util.Services;

namespace Wiretap.Util;

// core: The generic parameter ensures statuses can only be used with their activity contract.
// ReSharper disable once UnusedTypeParameter
public abstract class ActivityStatus<TActivity> : ActivityStatus where TActivity : Activity
{
    // core: The first status emitted by a buzz when its scope is entered.
    internal sealed class Ready : ActivityStatus<TActivity>
    {
        public override string Code => nameof(Ready);

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
    public abstract class Fail : ActivityStatus<TActivity>, IMessagePartFeed, ActivityStatusRole.ILast
    {
        public override string Code => nameof(Fail);

        public override LogLevel Level => LogLevel.Error;

        public void MessageParts(Context context, PushMessagePart push)
        {
            if (Exception is not null)
            {
                push(Exception.Message);
            }
        }
    }

    // core: Framework fallback when a buzz exits without an explicit last status.
    internal sealed class Void : ActivityStatus<TActivity>, IMessagePartFeed, ActivityStatusRole.ILast
    {
        public override string Code => nameof(Void);

        public override LogLevel Level => LogLevel.Warning;

        public void MessageParts(Context context, PushMessagePart push)
        {
            push("The activity scope exited without an explicit last status.");
        }
    }
}

public abstract class ActivityStatus
{
    public abstract string Code { get; }

    public abstract LogLevel Level { get; }

    public Exception? Exception { get; init; }

    public record Context : IStateItemFeed
    {
        public required string Activity { get; init; }
        public required string ActivityRole { get; init; }
        public required int ActivityDepth { get; init; }
        public required string ActivityPath { get; init; }
        public required string? ParentActivity { get; init; }
        public required string ActivityStatus { get; init; }
        public required string MessageRole { get; init; }
        public required long ElapsedMs { get; init; }

        public void StateItems(PushStateItem push)
        {
            push(nameof(Activity), Activity);
            push(nameof(ActivityRole), ActivityRole);
            push(nameof(ActivityDepth), ActivityDepth);
            push(nameof(ActivityPath), ActivityPath);
            push(nameof(ParentActivity), ParentActivity);
            push(nameof(ActivityStatus), ActivityStatus);
            push(nameof(MessageRole), MessageRole);
            push(nameof(ElapsedMs), ElapsedMs);
        }
    }
}
