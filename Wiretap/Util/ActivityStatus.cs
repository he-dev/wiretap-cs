using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Wiretap.Util.Services;

namespace Wiretap.Util;

// note: The generic parameter ensures type safety for activity scopes.
// ReSharper disable once UnusedTypeParameter
public abstract class ActivityStatus<TActivity> : ActivityStatus where TActivity : Activity;

public abstract class ActivityStatus
{
    //public virtual string Code => GetType().Name;
    public abstract string Code { get; }

    public abstract LogLevel Level { get; }

    public Exception? Exception { get; init; }

    public record Context : IWithStateItems
    {
        public required string Activity { get; init; }
        public required string ActivityRole { get; init; }
        public required string ActivityStatus { get; init; }
        public required string MessageRole { get; init; }
        public required long ElapsedMs { get; init; }

        public void StateItems(AddStateItem add)
        {
            add(nameof(Activity), Activity);
            add(nameof(ActivityRole), ActivityRole);
            add(nameof(ActivityStatus), ActivityStatus);
            add(nameof(MessageRole), MessageRole);
            add(nameof(ElapsedMs), ElapsedMs);
        }
    }

    public abstract class Core<TActivity> : ActivityStatus<TActivity> where TActivity : Activity
    {
        // core: Used when an activity has started but deliberately stops before its normal completion path because a known,
        // non-exceptional condition makes continuation invalid, impossible, or no longer meaningful.
        public abstract class Halt : Core<TActivity>, IWithMessageParts, ActivityStatusRole.ILast
        {
            public override string Code => nameof(Halt);

            public override LogLevel Level => LogLevel.Warning;

            public virtual string Reason { get; init; } = "Unspecified";

            public void MessageParts(Context context, AppendMessagePart append)
            {
                append("Reason: {Reason}", Reason);
            }
        }

        // core: This status applies when everything went according to plan.
        public abstract class Okay : Core<TActivity>, ActivityStatusRole.ILast
        {
            public override string Code => nameof(Okay);

            public override LogLevel Level => LogLevel.Information;
        }

        // core: This status applies when an error occured.
        public abstract class Fail : Core<TActivity>, IWithMessageParts, ActivityStatusRole.ILast
        {
            public override string Code => nameof(Fail);

            public override LogLevel Level => LogLevel.Error;

            public void MessageParts(Context context, AppendMessagePart append)
            {
                if (Exception is not null)
                {
                    append(Exception.Message);
                }
            }
        }
    }

    internal abstract class Auto<TActivity> : ActivityStatus<TActivity> where TActivity : Activity
    {
        // note: This is the very first status. Its previous name was "First".
        internal class Zero(LogLevel? level = null) : Auto<TActivity>
        {
            public override string Code => nameof(Zero);

            public override LogLevel Level => level ?? LogLevel.Trace;
        }

        internal abstract class Busy(LogLevel level, [StructuredMessageTemplate] string? message, object?[] args) : Auto<TActivity>, IWithMessageParts
        {
            public override LogLevel Level => level;

            public override string Code => nameof(Busy);

            public void MessageParts(Context context, AppendMessagePart append)
            {
                append(message, args);
            }

            public class Debug([StructuredMessageTemplate] string? message, object?[] args) : Busy(LogLevel.Debug, message, args);

            public class Trace([StructuredMessageTemplate] string? message, object?[] args) : Busy(LogLevel.Trace, message, args);
        }

        // core: This status applies when the caller does not care about the result.
        internal abstract class Void(LogLevel level) : Auto<TActivity>, IWithMessageParts, ActivityStatusRole.ILast
        {
            public override LogLevel Level => level;

            public override string Code => nameof(Void);

            public abstract void MessageParts(Context context, AppendMessagePart append);

            internal class Info() : Void(LogLevel.Information)
            {
                public override void MessageParts(Context context, AppendMessagePart append)
                {
                    append($"{nameof(LastStatusPolicy.CanBeVoid)} policy is set; it allows omitting an explicit last status.");
                }
            }

            internal class Warn() : Void(LogLevel.Warning)
            {
                public override void MessageParts(Context context, AppendMessagePart append)
                {
                    append($"An explicit last status is missing; using this as fallback.");
                }
            }
        }

        // core: This status wraps another last status when it overflows.
        internal sealed class Leak(ActivityStatus<TActivity> inner) : Auto<TActivity>, IWithMessageParts, ActivityStatusRole.ILast
        {
            public override LogLevel Level => LogLevel.Warning;

            public override string Code => nameof(Leak);

            public string StatusLeaking => inner.Code;

            public void MessageParts(Context context, AppendMessagePart append)
            {
                (inner as IWithMessageParts)?.MessageParts(context, append);
                append("Leaking: [{StatusLeaking}]", StatusLeaking);
            }
        }
    }
}

public interface IWithZeroStatus
{
    public LogLevel ZeroStatusLevel => LogLevel.Information;
}