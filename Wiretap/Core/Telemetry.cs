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
public class LastStatusOverflowThrows : Attribute;

// todo: this needs a better name.
[AttributeUsage(AttributeTargets.Class)]
public class LastStatusIsVoid : Attribute;

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
        if (items.Length == 0) throw new ArgumentException($"The code is trying to extend the {typeof(T).Name} logger scope with no items. Either the call is unnecessary or the items array was not populated correctly.", nameof(items));

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

        public ILogger<Channel.Output> Output => logger.Channel<T, Channel.Output>();
        public ILogger<Channel.Engine> Engine => logger.Channel<T, Channel.Engine>();

        public ILogger<Stream.Data> Data => logger.MapAs<T, Stream.Data>().WithState((nameof(Stream), nameof(Stream.Data)));
        public ILogger<Stream.Note> Note => logger.MapAs<T, Stream.Note>().WithState((nameof(Stream), nameof(Stream.Note)));
        public ILogger<Stream.Text> Text => logger.MapAs<T, Stream.Text>().WithState((nameof(Stream), nameof(Stream.Text)));

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

// core: This class may not be a logger, because it will circumvent the LogStatus constraints for statuses allowing to apply IStatusOnly to an IStatusWithDuration scope!
public class ActivityScope<TActivity>(ILogger logger, TActivity activity) : IDisposable where TActivity : Activity
{
    // todo: set tags like "wiretap.activity" or "wiretap.channel"
    private ActivityWrapper ActivityWrapper { get; } = new(activity.Name);

    private Stopwatch Stopwatch { get; } = Stopwatch.StartNew();

    // util: Track all status for debugging. It is for free.
    private Stack<ActivityStatus<TActivity>> StatusHistory { get; } = new();

    private bool ContainsLast => StatusHistory.OfType<ILastStatus>().Any();

    public ActivityScope<TActivity> LogStatus(ActivityStatus<TActivity> status)
    {
        if (status is ILastStatus)
        {
            if (status is not IAutoStatus)
            {
                if (activity.LastStatusIsVoid)
                {
                    throw new InvalidOperationException(
                        $"The code is trying to log the '{status.Code}' last status for the '{activity.Name}' activity, " +
                        $"but activities with the '{nameof(LastStatusIsVoid)}' attribute can't have an explicit last status.");
                }

                // core: If the last status is already logged...
                if (ContainsLast)
                {
                    if (activity.LastStatusOverflowThrows)
                    {
                        throw new InvalidOperationException($"The code is trying to log another last status for the '{activity.Name}' activity, but activities can have only one last status.");
                    }

                    status = new ActivityStatus<TActivity>.Overflow(status);
                }
            }

            ActivityWrapper.Stop(isOk: status switch
            {
                ActivityStatus<TActivity>.Ok => true,
                ActivityStatus<TActivity>.Error => false,
                _ => null
            });
        }


        status.Log(logger, activity, Stopwatch.Elapsed);

        StatusHistory.Push(status);
        return this;
    }

    public void Dispose()
    {
        // note: There is no last status!
        if (!StatusHistory.OfType<ILastStatus>().Any())
        {
            switch (activity.LastStatusIsVoid)
            {
                case true:
                    LogStatus(new ActivityStatus<TActivity>.Void());
                    break;
                case false:
                    LogStatus(new ActivityStatus<TActivity>.Missing());
                    break;
            }
        }

        ActivityWrapper.Dispose();
    }

    public static ActivityScope<TActivity> Start<T>(ILogger<T> logger, TActivity activity)
    {
        return new ActivityScope<TActivity>(logger, activity).LogStatus(new ActivityStatus<TActivity>.First());
    }
}

internal sealed class ActivityWrapper(string name) : IDisposable
{
    private static readonly ActivitySource Source = new(nameof(Wiretap));

    private System.Diagnostics.Activity? Native { get; } = Source.StartActivity(name);

    public void Stop(bool? isOk)
    {
        if (Native is { IsStopped: false })
        {
            var statusCode = isOk switch
            {
                true => System.Diagnostics.ActivityStatusCode.Ok,
                false => System.Diagnostics.ActivityStatusCode.Error,
                _ => System.Diagnostics.ActivityStatusCode.Unset
            };

            Native
                .SetStatus(statusCode)
                .Stop();
        }
    }

    public void Dispose() => Native?.Dispose();
}

// meta: This class is required to make the message template work with structured logging as the attribute can only be used on parameters.
public record MessageTemplate([StructuredMessageTemplate] string? Template, params object?[] Args)
{
    // todo: required?
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
            throw new InvalidOperationException("The message templates cannot contain both exceptions.");
        }

        return new($"{left.Template}; {right.Template}", [..left.Args, ..right.Args])
        {
            Level = left.Level > right.Level ? left.Level : right.Level,
            Exception = left.Exception ?? right.Exception
        };
    }
}

public abstract class Activity
{
    protected Activity()
    {
        var channelMatch = Find<ChannelAttribute>.From(GetType());
        Channel = channelMatch.Path.First().Name;

        // note: The activity name begins after the channel, so skip it.
        Name = string.Join(".", channelMatch.Path.Skip(1).Select(t => t.Name));

        LastStatusIsVoid = Find<LastStatusIsVoid>.From(GetType()).Attribute is not null;
        LastStatusOverflowThrows = Find<LastStatusOverflowThrows>.From(GetType()).Attribute is not null;
    }

    public bool LastStatusIsVoid { get; }

    public bool LastStatusOverflowThrows { get; }

    public string Channel { get; }

    public string Name { get; }
}

public interface IMiddleStatus;

public interface ILastStatus;

internal interface IAutoStatus;

public interface IEnumerableState
{
    IEnumerable<(string Key, object Value)> EnumerateState();
}

public abstract class ActivityStatus<TActivity> : IEnumerableState where TActivity : Activity
{
    internal virtual string Code => GetType().Name;

    // core: Let inheritors provide their own template.
    protected virtual MessageTemplate Render(TActivity activity, TimeSpan duration)
    {
        var template = new MessageTemplate("{Activity}: {Status}", activity.Name, Code);

        if (duration > TimeSpan.Zero)
        {
            if (this is Overflow)
            {
                template += new MessageTemplate("Elapsed: {ElapsedMs:N0} ms", duration.TotalMilliseconds);
            }
            else
            {
                template += new MessageTemplate("Duration: {DurationMs:N0} ms", duration.TotalMilliseconds);
            }
        }

        return template;
    }

    public virtual IEnumerable<(string, object)> EnumerateState()
    {
        yield break;
    }

    public void Log(ILogger logger, TActivity activity, TimeSpan duration)
    {
        var state = new Dictionary<string, object>
        {
            { nameof(Activity), activity.Name },
            { nameof(Channel), activity.Channel },
            { nameof(Stream), nameof(Stream.Data) }
        };

        state.MergeStateFrom(this);
        state.MergeStateFrom(activity);

        using (logger.BeginScope(state))
        {
            Render(activity, duration).Log(logger);
        }
    }

    public class First : ActivityStatus<TActivity>
    {
        protected override MessageTemplate Render(TActivity activity, TimeSpan duration)
        {
            return base.Render(activity, duration) with { Level = LogLevel.Trace };
        }
    }

    // core: Used when an activity has started but deliberately stops before its normal completion path because a known,
    // non-exceptional condition makes continuation invalid, impossible, or no longer meaningful.
    public abstract class Halt : ActivityStatus<TActivity>, ILastStatus
    {
        public required string Reason { get; init; }

        protected override MessageTemplate Render(TActivity activity, TimeSpan duration)
        {
            return base.Render(activity, duration) with { Level = LogLevel.Warning } + new MessageTemplate("Reason: {Reason}", Reason);
        }
    }

    // core: This status applies when the caller does not care about the result.
    internal class Void : ActivityStatus<TActivity>, ILastStatus, IAutoStatus
    {
        protected override MessageTemplate Render(TActivity activity, TimeSpan duration)
        {
            return base.Render(activity, duration) with { Level = LogLevel.Information };
        }
    }

    // core: This status applies when everything went according to plan.
    public abstract class Ok : ActivityStatus<TActivity>, ILastStatus
    {
        protected override MessageTemplate Render(TActivity activity, TimeSpan duration)
        {
            return base.Render(activity, duration) with { Level = LogLevel.Information };
        }
    }

    // core: This status applies when an error occured.
    public abstract class Error : ActivityStatus<TActivity>, ILastStatus
    {
        public Exception? Exception { get; init; }

        protected override MessageTemplate Render(TActivity activity, TimeSpan duration)
        {
            return base.Render(activity, duration) with { Level = LogLevel.Error, Exception = Exception };
        }
    }

    // core: This status applies when activity was not properly stopped.
    internal class Missing : ActivityStatus<TActivity>, ILastStatus, IAutoStatus
    {
        protected override MessageTemplate Render(TActivity activity, TimeSpan duration)
        {
            return base.Render(activity, duration) with { Level = LogLevel.Warning };
        }
    }

    internal sealed class Overflow(ActivityStatus<TActivity> inner) : ActivityStatus<TActivity>, ILastStatus, IAutoStatus
    {
        internal override string Code => nameof(Overflow);

        public override IEnumerable<(string, object)> EnumerateState()
        {
            // core: Forward inner state too in case it carried context.
            foreach (var item in inner.EnumerateState())
            {
                yield return item;
            }

            // core: Preserve the inner status's code for reference.
            yield return (nameof(Overflow), inner.Code);
        }

        protected override MessageTemplate Render(TActivity activity, TimeSpan duration)
        {
            // core: Raise the level of the inner status to warning.
            return base.Render(activity, duration) with { Level = LogLevel.Warning };
        }
    }
}

[Channel]
public abstract class Output
{
    public abstract class Workflow
    {
        [LastStatusOverflowThrows]
        public class ExecuteStep : Activity
        {
            public class Now : ExecuteStep, IEnumerableState
            {
                public required int StepIndex { get; init; }

                public IEnumerable<(string, object)> EnumerateState()
                {
                    yield return new(nameof(StepIndex), StepIndex);
                }

                public sealed class Ok : ActivityStatus<Now>.Ok
                {
                    // note: Can be either a property or a constructor parameter. Does not really make any difference.
                    public required int ItemsProcessed { get; init; }

                    public override IEnumerable<(string, object)> EnumerateState()
                    {
                        return base.EnumerateState().Append((nameof(ItemsProcessed), ItemsProcessed));
                    }
                }

                public sealed class Error : ActivityStatus<Now>.Error;
            }
        }
    }
}

public abstract class Engine
{
    [LastStatusIsVoid]
    public class DeleteFile : Activity, IEnumerableState
    {
        public required string Path { get; init; }

        public IEnumerable<(string Key, object Value)> EnumerateState()
        {
            yield return new(nameof(Path), Path);
        }

        public sealed class Halt : ActivityStatus<DeleteFile>.Halt;

        public sealed class Ok : ActivityStatus<DeleteFile>.Ok;

        public sealed class Error : ActivityStatus<DeleteFile>.Error;
    }
}