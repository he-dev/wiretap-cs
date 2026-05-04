using System.Collections.Immutable;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using DiagnosticActivity = System.Diagnostics.Activity;

namespace Wiretap.Core;

public static class Telemetry
{
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
}

[AttributeUsage(AttributeTargets.Class)]
public abstract class OnLogStatusWithLast : Attribute
{
    public sealed class LogOverflow : OnLogStatusWithLast;

    public sealed class Default : OnLogStatusWithLast;
}

[AttributeUsage(AttributeTargets.Class)]
public abstract class OnDisposeWithoutLastStatus : Attribute
{
    public sealed class LogVoid : OnDisposeWithoutLastStatus;

    public sealed class LogMissing : OnDisposeWithoutLastStatus;

    public sealed class Default : OnDisposeWithoutLastStatus;
}

[AttributeUsage(AttributeTargets.Class)]
public class ChannelAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = name;
}

public interface IWithStateItems
{
    public IEnumerable<KeyValuePair<string, object>> EnumerateStateItems();
}

public static class Find<TAttribute> where TAttribute : Attribute
{
    public static AttributeMatch<TAttribute>? From<TActivity>() => From(typeof(TActivity));

    public static AttributeMatch<TAttribute>? From(Type type)
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

        //throw new InvalidOperationException($"The '{typeof(TAttribute).Name}' was not found on any of the checked types [{string.Join(", ", visited.Select(t => t.Name))}].");

        return null;
    }
}

public sealed record AttributeMatch<TAttribute>(TAttribute Attribute, IReadOnlyList<Type> Visited, IReadOnlyList<Type> Path)
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
        public ILogger<TChannel> Channel<TChannel>() => logger.MapAs<T, TChannel>().WithState((nameof(Telemetry.Channel), typeof(TChannel).Name));

        public ILogger<Telemetry.Channel.Output> Output => logger.Channel<T, Telemetry.Channel.Output>();
        public ILogger<Telemetry.Channel.Engine> Engine => logger.Channel<T, Telemetry.Channel.Engine>();

        public ILogger<Telemetry.Stream.Data> Data => logger.MapAs<T, Telemetry.Stream.Data>().WithState((nameof(Telemetry.Stream), nameof(Telemetry.Stream.Data)));
        public ILogger<Telemetry.Stream.Note> Note => logger.MapAs<T, Telemetry.Stream.Note>().WithState((nameof(Telemetry.Stream), nameof(Telemetry.Stream.Note)));
        public ILogger<Telemetry.Stream.Text> Text => logger.MapAs<T, Telemetry.Stream.Text>().WithState((nameof(Telemetry.Stream), nameof(Telemetry.Stream.Text)));

        public ActivityScope<TActivity> Begin<TActivity>(TActivity activity) where TActivity : Activity
        {
            return ActivityScope<TActivity>.Start(logger, activity);
        }
    }
}

// core: This class may not be a logger, because it will circumvent the LogStatus constraints for statuses allowing to apply IStatusOnly to an IStatusWithDuration scope!
public class ActivityScope<TActivity>(ILogger logger, TActivity activity) : IDisposable where TActivity : Activity
{
    private Stack<ActivityStatus<TActivity>> StatusHistory { get; } = new();

    private DiagnosticActivity _Activity { get; } = new DiagnosticActivity(activity.Name).Start();

    public ActivityScope<TActivity> LogStatus(ActivityStatus<TActivity> status)
    {
        Stop(status);

        if (StatusHistory.Any(s => s.IsLast))
        {
            if (activity.OnStopWithLastStatus?.Attribute is OnLogStatusWithLast.LogOverflow)
            {
                // todo: Log which status overflowed.
                new ActivityStatus<TActivity>.Overflow().Log(logger, activity, _Activity.Duration);
            }
            else
            {
                throw new InvalidOperationException($"The code is trying to log another last status for the '{activity.Name}' activity, but activities can have only one last status.");
            }
        }
        else
        {
            status.Log(logger, activity, _Activity.Duration);
        }

        StatusHistory.Push(status);
        return this;
    }

    private void Stop(ActivityStatus<TActivity> status)
    {
        if (_Activity.IsStopped || !status.IsLast)
        {
            return;
        }

        var statusCode = status switch
        {
            ActivityStatus<TActivity>.Ok => System.Diagnostics.ActivityStatusCode.Ok,
            ActivityStatus<TActivity>.Error => System.Diagnostics.ActivityStatusCode.Error,
            // core: Inconclusive and Halt are terminal, but their status is unknown.
            _ => System.Diagnostics.ActivityStatusCode.Unset
        };

        _Activity.Stop();
        _Activity.SetStatus(statusCode);
    }

    public void Dispose()
    {
        // note: There is no last status!
        if (!StatusHistory.Any(s => s.IsLast))
        {
            switch (activity.OnDisposeWithoutLastStatus?.Attribute)
            {
                case OnDisposeWithoutLastStatus.LogMissing:
                    LogStatus(new ActivityStatus<TActivity>.Missing());
                    break;
                case OnDisposeWithoutLastStatus.LogVoid:
                    LogStatus(new ActivityStatus<TActivity>.Void());
                    break;
                default:
                    throw new InvalidOperationException($"The activity '{activity.Name}' requires a last status but it was never logged.");
            }
        }

        _Activity.Dispose();
    }

    public static ActivityScope<TActivity> Start<T>(ILogger<T> logger, TActivity activity)
    {
        return new ActivityScope<TActivity>(logger, activity).LogStatus(new ActivityStatus<TActivity>.First());
    }
}

// meta: This class is required to make the message template work with structured logging as the attribute can only be used on parameters.
public record StatusTemplate([StructuredMessageTemplate] string? Message, params object?[] Args);

public abstract class Activity
{
    protected Activity()
    {
        ChannelMatch = Find<ChannelAttribute>.From(GetType()) ?? throw new InvalidOperationException
        (
            $"The activity '{GetType().FullName}' has no channel. One of its declaring types must derive from '{nameof(Channel)}'."
        );

        OnStopWithLastStatus = Find<OnLogStatusWithLast>.From(GetType());

        OnDisposeWithoutLastStatus = Find<OnDisposeWithoutLastStatus>.From(GetType());
    }

    private AttributeMatch<ChannelAttribute> ChannelMatch { get; }

    public AttributeMatch<OnLogStatusWithLast>? OnStopWithLastStatus { get; }

    public AttributeMatch<OnDisposeWithoutLastStatus>? OnDisposeWithoutLastStatus { get; }

    public string Channel => ChannelMatch.Attribute.Name ?? ChannelMatch.Path.First().Name;

    // note: The activity name begins after the channel, so skip it.
    public string Name => string.Join(".", ChannelMatch.Path.Skip(1).Select(t => t.Name));
}

public abstract class ActivityStatus<TActivity>(bool isLast) where TActivity : Activity
{
    protected virtual string Code => GetType().Name;

    public bool IsLast => isLast;

    // core: Let inheritors provide their own template.
    protected virtual StatusTemplate Render(TActivity activity, TimeSpan duration)
    {
        return new("{Activity}: {Status} in {DurationMs:N0} ms", activity.Name, Code, duration.TotalMilliseconds);
    }

    // core: Each status needs to provide its own logging.
    protected abstract void Log(ILogger logger, StatusTemplate template);

    public void Log(ILogger logger, TActivity activity, TimeSpan duration)
    {
        var state = new Dictionary<string, object>
        {
            { nameof(Activity), activity.Name },
            { nameof(Telemetry.Channel), activity.Channel },
            { nameof(Telemetry.Stream), nameof(Telemetry.Stream.Data) }
        };

        MergeStateItems(activity, state);
        MergeStateItems(this, state);

        using (logger.BeginScope(state))
        {
            Log(logger, Render(activity, duration));
        }
    }

    private static void MergeStateItems<T>(T source, IDictionary<string, object> state)
    {
        if (source is IWithStateItems customState)
        {
            var any = false;

            // core: Adding the custom state items to the scope.
            foreach (var (key, value) in customState.EnumerateStateItems())
            {
                if (state.TryGetValue(key, out var currentValue))
                {
                    throw new InvalidOperationException($"The type '{typeof(T).FullName}' tries to add the key '{key}' with value '{value}', but it already exists with value '{currentValue}'.");
                }

                state.Add(key, value);
                any = true;
            }

            if (!any)
            {
                throw new InvalidOperationException($"The type '{typeof(T).FullName}' implements the '{nameof(IWithStateItems)}' interface but returns zero items.");
            }
        }
    }

    // core: First status always logs at trace level.
    public class First() : ActivityStatus<TActivity>(isLast: false)
    {
        protected override void Log(ILogger logger, StatusTemplate template) => logger.LogTrace(template.Message, template.Args);
    }

    // core: Used when an activity has started but deliberately stops before its normal completion path because a known,
    // non-exceptional condition makes continuation invalid, impossible, or no longer meaningful.
    public abstract class Halt(string reason) : ActivityStatus<TActivity>(isLast: true), IWithStateItems
    {
        public IEnumerable<KeyValuePair<string, object>> EnumerateStateItems()
        {
            yield return new(nameof(reason), reason);
        }

        protected override void Log(ILogger logger, StatusTemplate template) => logger.LogWarning(template.Message, template.Args);
    }

    // core: This status applies when the caller does not care about the result.
    public class Void() : ActivityStatus<TActivity>(isLast: true)
    {
        protected override void Log(ILogger logger, StatusTemplate template) => logger.LogInformation(template.Message, template.Args);
    }

    // core: This status applies when everything went according to plan.
    public abstract class Ok() : ActivityStatus<TActivity>(isLast: true)
    {
        protected override void Log(ILogger logger, StatusTemplate template) => logger.LogInformation(template.Message, template.Args);
    }

    // core: This status applies when an error occured.
    public abstract class Error() : ActivityStatus<TActivity>(isLast: true)
    {
        public Exception? Exception { get; init; }

        protected override void Log(ILogger logger, StatusTemplate template) => logger.LogError(Exception, template.Message, template.Args);
    }

    // core: This status applies when activity was not properly stopped.
    public class Missing() : ActivityStatus<TActivity>(isLast: true)
    {
        protected override void Log(ILogger logger, StatusTemplate template) => logger.LogWarning(template.Message, template.Args);
    }

    // code: This status applies when the same status was logged multiple times: Ok -> Ok.
    public class Overflow() : ActivityStatus<TActivity>(isLast: false)
    {
        protected override void Log(ILogger logger, StatusTemplate template) => logger.LogWarning(template.Message, template.Args);
    }
}

[Channel]
public abstract class Output : Telemetry.Channel.Output
{
    public abstract class Workflow
    {
        [OnDisposeWithoutLastStatus.LogMissing]
        public class ExecuteStep : Activity
        {
            public class Now : ExecuteStep, IWithStateItems
            {
                public required int StepIndex { get; init; }

                public IEnumerable<KeyValuePair<string, object>> EnumerateStateItems()
                {
                    yield return new(nameof(StepIndex), StepIndex);
                }

                public sealed class Ok : ActivityStatus<Now>.Ok, IWithStateItems
                {
                    // note: Can be either a property or a constructor parameter. Does not really make any difference.
                    public required int ItemsProcessed { get; init; }

                    public IEnumerable<KeyValuePair<string, object>> EnumerateStateItems()
                    {
                        yield return new(nameof(ItemsProcessed), ItemsProcessed);
                    }
                }

                public sealed class Error : ActivityStatus<Now>.Error;
            }
        }
    }
}

[Channel]
public abstract class Engine : Telemetry.Channel.Engine
{
    [OnDisposeWithoutLastStatus.LogVoid]
    [OnLogStatusWithLast.LogOverflow]
    public class DeleteFile : Activity, IWithStateItems
    {
        public required string Path { get; init; }

        public IEnumerable<KeyValuePair<string, object>> EnumerateStateItems()
        {
            yield return new(nameof(Path), Path);
        }

        public sealed class Halt(string reason) : ActivityStatus<DeleteFile>.Halt(reason);

        public sealed class Ok : ActivityStatus<DeleteFile>.Ok;

        public sealed class Error : ActivityStatus<DeleteFile>.Error;
    }
}