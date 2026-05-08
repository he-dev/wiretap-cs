using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Wiretap.Core;

public abstract class Channel
{
    // core: Logs about what the system is supposed to produce.
    public abstract class Output : Channel;

    // core: Logs about what allows the system able to produce.
    public abstract class Engine : Channel;
}

public abstract class Stream
{
    // core: Pure telemetry data.
    public abstract class Data;

    // core: Something nice to know about what is going on.
    public abstract class Note;

    // core: Readable entries meant for the console.
    public abstract class Text;
}

[AttributeUsage(AttributeTargets.Class)]
public class LastStatusMustNotOverflow : Attribute;

[AttributeUsage(AttributeTargets.Class)]
public class LastStatusMustBeVoid : Attribute;

[AttributeUsage(AttributeTargets.Class)]
public class ChannelAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = name;
}

public static class Find<TAttribute> where TAttribute : Attribute
{
    public static AttributeMatch<TAttribute> From<TActivity>() => From(typeof(TActivity));

    public static AttributeMatch<TAttribute> From(Type type)
    {
        var path = new Stack<Type>();
        var visited = new List<Type>();

        for (var current = type; current is not null; current = current.DeclaringType)
        {
            path.Push(current);
            visited.Add(current);

            // core: Collecting only the first attribute of each type.
            if (current.GetCustomAttribute<TAttribute>(inherit: false) is { } attribute)
            {
                return new(attribute, visited.ToArray(), path.ToArray());
            }
        }

        return new(null, visited.ToArray(), path.ToArray());
    }
}

public sealed record AttributeMatch<TAttribute>(TAttribute? Attribute, IReadOnlyList<Type> Visited, IReadOnlyList<Type> Path) where TAttribute : Attribute
{
    public int Depth => Visited.Count;
}

public sealed class LoggerProxy<T>(ILogger inner, IEnumerable<KeyValuePair<string, object?>> items) : ILogger<T>
{
    private ILogger Inner { get; } = inner;

    private IImmutableDictionary<string, object?> Items { get; } = items.ToImmutableDictionary();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => Inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => Inner.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (Items.Count == 0)
        {
            Inner.Log(logLevel, eventId, state, exception, formatter);
        }
        else
        {
            using (BeginScope(Items))
            {
                Inner.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }

    public static ILogger<TOther> As<TOther>(ILogger<T> logger)
    {
        if (typeof(T) == typeof(TOther)) throw new ArgumentException($"The code is trying to map the {typeof(T).Name} logger to the same type it already has. Either the type argument is wrong or the mapping is unnecessary.", nameof(TOther));

        return
            logger is LoggerProxy<T> proxy
                ? new LoggerProxy<TOther>(proxy.Inner, proxy.Items)
                : new LoggerProxy<TOther>(logger, ImmutableDictionary<string, object?>.Empty);
    }

    public static ILogger<T> With(ILogger<T> logger, params (string Key, object? Value)[] items)
    {
        if (items.Length == 0)
        {
            throw new ArgumentException($"The code is trying to extend the {typeof(T).Name} logger scope with no items. Either the call is unnecessary or the items array was not populated correctly.", nameof(items));
        }

        var keyValuePairs = items.Select(item => new KeyValuePair<string, object?>(item.Key, item.Value));

        return
            logger is LoggerProxy<T> proxy
                ? new LoggerProxy<T>(proxy.Inner, proxy.Items.SetItems(keyValuePairs))
                : new LoggerProxy<T>(logger, keyValuePairs);
    }
}

public static class LoggerProxyExtensions
{
    extension<T>(ILogger<T> logger)
    {
        public ILogger<TOther> MapAs<TOther>() => LoggerProxy<T>.As<TOther>(logger);

        public ILogger<T> WithState(params (string Key, object? Value)[] items) => LoggerProxy<T>.With(logger, items);
    }
}

public static class LoggerExtensions
{
    extension<T>(ILogger<T> logger)
    {
        public ILogger<TChannel> Channel<TChannel>() => logger.MapAs<T, TChannel>().WithState((nameof(Channel), typeof(TChannel).Name));
        public ILogger<TStream> Stream<TStream>() => logger.MapAs<T, TStream>().WithState((nameof(Stream), typeof(TStream).Name));

        public ILogger<Channel.Output> Output => logger.Channel<T, Channel.Output>();
        public ILogger<Channel.Engine> Engine => logger.Channel<T, Channel.Engine>();

        public ILogger<Stream.Data> Data => logger.Stream<T, Stream.Data>();
        public ILogger<Stream.Note> Note => logger.Stream<T, Stream.Note>();
        public ILogger<Stream.Text> Text => logger.Stream<T, Stream.Text>();

        public ActivityScope<TActivity> Begin<TActivity>(TActivity activity) where TActivity : Activity
        {
            return ActivityScope<TActivity>.Start(logger, activity);
        }
    }
}

public static class DictionaryExtensions
{
    extension(IDictionary<string, object> state)
    {
        public void MergeStateFrom<T>(T source)
        {
            if (source is not IEnumerableState enumerableState)
            {
                return;
            }

            foreach (var (key, value) in enumerableState.EnumerateState())
            {
                if (state.TryGetValue(key, out var currentValue))
                {
                    throw new InvalidOperationException($"The type '{typeof(T).FullName}' tries to add the key '{key}' with value '{value}', but it already exists with value '{currentValue}'.");
                }

                state.Add(key, value);
            }
        }
    }
}

public record LogContext(string Activity, string Channel, string Stream, TimeSpan Duration, bool Overflow, IEnumerableState? State) : IEnumerableState
{
    public IEnumerable<(string Key, object Value)> EnumerateState()
    {
        yield return (nameof(Activity), Activity);
        yield return (nameof(Channel), Channel);
        yield return (nameof(Stream), Stream);

        if (Duration > TimeSpan.Zero)
        {
            if (Overflow)
            {
                yield return ("ElapsedMs", (int)Duration.TotalMilliseconds);
            }
            else
            {
                yield return ("DurationMs", (int)Duration.TotalMilliseconds);
            }
        }

        foreach (var (key, value) in State?.EnumerateState() ?? [])
        {
            yield return (key, value);
        }
    }
}

// core: This class may not be a logger, because it will circumvent the LogStatus constraints for statuses allowing to apply IStatusOnly to an IStatusWithDuration scope!
public class ActivityScope<TActivity>(ILogger logger, TActivity activity) : IDisposable where TActivity : Activity
{
    // todo: set tags like "wiretap.activity" or "wiretap.channel"
    private ActivityWrapper ActivityWrapper { get; } = new(activity.Name);

    private Stopwatch Stopwatch { get; } = Stopwatch.StartNew();

    // util: Track all status for debugging. It is for free.
    private Stack<(ActivityStatus<TActivity> Status, LogContext Context)> StatusHistory { get; } = new();

    private bool ContainsLastStatus => StatusHistory.Any(x => x.Status is ILastStatus);

    public ActivityScope<TActivity> LogStatus(ActivityStatus<TActivity> status)
    {
        if (activity.LastStatusMustBeVoid && status is ILastStatus and IUserStatus)
        {
            throw new InvalidOperationException(
                $"The code is trying to log the '{status.Code}' last status for the '{activity.Name}' activity, " +
                $"but activities with the '{nameof(LastStatusMustBeVoid)}' attribute can't have an explicit last status.");
        }

        if (activity.LastStatusMustNotOverflow && ContainsLastStatus)
        {
            throw new InvalidOperationException($"The code is trying to log another last status for the '{activity.Name}' activity, but activities can have only one last status.");
        }

        if (status is ILastStatus)
        {
            // core: This is an overflow!
            if (status is IUserStatus && ContainsLastStatus)
            {
                status = new ActivityStatus<TActivity>.Leak(status);
            }

            ActivityWrapper.Stop(isOk: status switch
            {
                ActivityStatus<TActivity>.Okay => true,
                ActivityStatus<TActivity>.Fail => false,
                _ => null
            });
        }


        var context = new LogContext
        (
            activity.Name,
            activity.Channel,
            nameof(Stream.Data),
            Stopwatch.Elapsed,
            ContainsLastStatus,
            activity as IEnumerableState
        );
        status.Log(logger, context);

        StatusHistory.Push((status, context));
        return this;
    }

    public void Dispose()
    {
        if (!ContainsLastStatus)
        {
            if (activity.LastStatusMustBeVoid)
            {
                LogStatus(new ActivityStatus<TActivity>.Void());
            }
            else
            {
                LogStatus(new ActivityStatus<TActivity>.Last());
            }
        }

        ActivityWrapper.Dispose();
    }

    public static ActivityScope<TActivity> Start<T>(ILogger<T> logger, TActivity activity)
    {
        return new ActivityScope<TActivity>(logger, activity).LogStatus(new ActivityStatus<TActivity>.Zero());
    }
}

internal sealed class ActivityWrapper(string name) : IDisposable
{
    private static readonly ActivitySource Source = new(nameof(Wiretap));

    private System.Diagnostics.Activity? Inner { get; } = Source.StartActivity(name);

    public void Stop(bool? isOk)
    {
        if (Inner is { IsStopped: false })
        {
            var statusCode = isOk switch
            {
                true => System.Diagnostics.ActivityStatusCode.Ok,
                false => System.Diagnostics.ActivityStatusCode.Error,
                _ => System.Diagnostics.ActivityStatusCode.Unset
            };

            Inner
                .SetStatus(statusCode)
                .Stop();
        }
    }

    public void Dispose() => Inner?.Dispose();
}

// meta: This class is required to make the message template work with structured logging as the attribute can only be used on parameters.
public record MessageTemplate([StructuredMessageTemplate] string? Template, params object?[] Args)
{
    public LogLevel Level { get; init; }

    public Exception? Exception { get; init; }

    public void Log(ILogger logger)
    {
        if (Level == LogLevel.None)
        {
            throw new InvalidOperationException("Message templates without log-level cannot be logged.");
        }

        logger.Log(Level, Exception, Template, Args);
    }

    public static MessageTemplate operator +(MessageTemplate left, MessageTemplate right)
    {
        if (left.Exception is not null && right.Exception is not null)
        {
            throw new InvalidOperationException("Only one message template can contain an exception, but both do.");
        }

        return new($"{left.Template}{right.Template}", [..left.Args, ..right.Args])
        {
            Level = left.Level > right.Level ? left.Level : right.Level,
            Exception = left.Exception ?? right.Exception
        };
    }
}

// core: This is the base class for all activities.
public abstract class Activity
{
    protected Activity()
    {
        var channelMatch = Find<ChannelAttribute>.From(GetType());
        Channel = channelMatch.Path.First().Name;

        // note: The activity name begins by convention after the channel, so skip it.
        Name = string.Join(".", channelMatch.Path.Skip(1).Select(t => t.Name));

        LastStatusMustBeVoid = Find<LastStatusMustBeVoid>.From(GetType()).Attribute is not null;
        LastStatusMustNotOverflow = Find<LastStatusMustNotOverflow>.From(GetType()).Attribute is not null;
    }

    public string Channel { get; }
    public string Name { get; }
    public bool LastStatusMustBeVoid { get; }
    public bool LastStatusMustNotOverflow { get; }
}

// core: Marks statuses that veto the execution of an activity before reaching its normal completion path.
public interface IVetoStatus;

// core: Marks statuses that users can log.
public interface IUserStatus;

// core: Marks statuses that are last in the activity's lifecycle.
public interface ILastStatus;

// core: Marks statuses that are automatically logged.
internal interface IAutoStatus;

public interface IEnumerableState
{
    IEnumerable<(string Key, object Value)> EnumerateState();
}

public abstract class ActivityStatus<TActivity> : IEnumerableState where TActivity : Activity
{
    internal virtual string Code => GetType().Name;

    // core: Let inheritors provide their own template.
    protected virtual MessageTemplate Render(LogContext context)
    {
        var template = new MessageTemplate("{Activity}[{Status}]", context.Activity, Code);

        if (context.Duration > TimeSpan.Zero)
        {
            if (this is Leak)
            {
                template += new MessageTemplate(" at {ElapsedMs:N0} ms", context.Duration.TotalMilliseconds);
            }
            else
            {
                template += new MessageTemplate(" at {DurationMs:N0} ms", context.Duration.TotalMilliseconds);
            }
        }

        return template;
    }

    public virtual IEnumerable<(string, object)> EnumerateState()
    {
        yield break;
    }

    public void Log(ILogger logger, LogContext context)
    {
        var state = new Dictionary<string, object>();

        state.MergeStateFrom(context);
        state.MergeStateFrom(this);

        using (logger.BeginScope(state))
        {
            Render(context).Log(logger);
        }
    }

    // note: This is the very first status. Its previous name was "First".
    internal class Zero : ActivityStatus<TActivity>, IAutoStatus
    {
        protected override MessageTemplate Render(LogContext context)
        {
            return base.Render(context) with { Level = LogLevel.Trace };
        }
    }

    // core: Used when an activity has started but deliberately stops before its normal completion path because a known,
    // non-exceptional condition makes continuation invalid, impossible, or no longer meaningful.
    public abstract class Halt : ActivityStatus<TActivity>, ILastStatus, IVetoStatus, IUserStatus
    {
        public required string Reason { get; init; }

        protected override MessageTemplate Render(LogContext context)
        {
            return base.Render(context) with { Level = LogLevel.Warning } + new MessageTemplate("Reason: {Reason}", Reason);
        }
    }

    // core: This status applies when the caller does not care about the result.
    internal class Void : ActivityStatus<TActivity>, ILastStatus, IAutoStatus
    {
        protected override MessageTemplate Render(LogContext context)
        {
            return base.Render(context) with { Level = LogLevel.Information };
        }
    }

    // core: This status applies when everything went according to plan.
    public abstract class Okay : ActivityStatus<TActivity>, ILastStatus, IUserStatus
    {
        protected override MessageTemplate Render(LogContext context)
        {
            return base.Render(context) with { Level = LogLevel.Information };
        }
    }

    // core: This status applies when an error occured.
    public abstract class Fail : ActivityStatus<TActivity>, ILastStatus, IUserStatus
    {
        public Exception? Exception { get; init; }

        protected override MessageTemplate Render(LogContext context)
        {
            return base.Render(context) with { Level = LogLevel.Error, Exception = Exception };
        }
    }

    // core: This status applies when activity was not properly stopped.
    internal class Last : ActivityStatus<TActivity>, ILastStatus, IAutoStatus
    {
        protected override MessageTemplate Render(LogContext context)
        {
            return base.Render(context) with { Level = LogLevel.Warning };
        }
    }

    // core: This status wraps another last status when it overflows.
    internal sealed class Leak(ActivityStatus<TActivity> inner) : ActivityStatus<TActivity>, ILastStatus, IAutoStatus
    {
        internal override string Code => nameof(Leak);

        public override IEnumerable<(string, object)> EnumerateState()
        {
            // core: Forward inner state too in case it carried context.
            foreach (var item in inner.EnumerateState())
            {
                yield return item;
            }

            // core: Preserve the inner status's code for reference.
            yield return (nameof(Leak), inner.Code);
        }

        protected override MessageTemplate Render(LogContext context)
        {
            // core: Raise the level of the inner status to warning.
            return base.Render(context) with { Level = LogLevel.Warning };
        }
    }
}