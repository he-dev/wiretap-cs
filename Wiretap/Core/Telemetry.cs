using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Wiretap.Core;

public abstract class ActivityDomain
{
    // core: Logs about what the system is supposed to produce.
    public abstract class Output : ActivityDomain;

    // core: Logs about what allows the system able to produce.
    public abstract class Engine : ActivityDomain;
}

public abstract class MessageRole
{
    // core: Pure telemetry data.
    public abstract class Data;

    // core: Something nice to know about what is going on.
    public abstract class Clue;

    // core: Readable entries meant for the console.
    public abstract class News;
}

[AttributeUsage(AttributeTargets.Class)]
public abstract class LastStatusPolicy : Attribute
{
    public class MustNotLeak : LastStatusPolicy;

    public class MustBeVoid : LastStatusPolicy;
}

[AttributeUsage(AttributeTargets.Class)]
public class ActivityDomainAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = name;
}

[AttributeUsage(AttributeTargets.Class)]
public abstract class MessageSchema : Attribute
{
    public abstract MessageTemplate From(LogContext context, IEnumerable<MessageTemplate> others);
}

public sealed class CompactMessageSchema(string separator = "; ") : MessageSchema
{
    public override MessageTemplate From(LogContext context, IEnumerable<MessageTemplate> others)
    {
        var root = new List<MessageTemplate>
        {
            new("{Activity}[{Status}]", context.Activity, context.Status),
            new("Elapsed: {ElapsedMs:N0} ms", context.ElapsedMs)
        };

        return root.Concat(others).Aggregate((c, n) => new($"{c.Template}{separator}{n.Template}", [..c.Args, ..n.Args]));
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public class ScopeState(string? name = null) : Attribute
{
    private string? Name { get; } = name;

    private static readonly ConcurrentDictionary<Type, (MemberInfo Member, ScopeState State)[]> Cache = new();

    public static IEnumerable<(string Key, object Value)> From<T>(T source) where T : notnull
    {
        var members = Cache.GetOrAdd(source.GetType(), Discover);

        foreach (var (member, attr) in members)
        {
            var value = member switch
            {
                PropertyInfo property => property.GetValue(source),
                FieldInfo field => field.GetValue(source),
                _ => null
            };

            if (value is not null)
            {
                yield return (attr.Name ?? member.Name, value);
            }
        }
    }

    private static (MemberInfo Member, ScopeState State)[] Discover(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        var properties =
            from property in type.GetProperties(flags)
            let attr = property.GetCustomAttribute<ScopeState>()
            where attr is not null
            select ((MemberInfo)property, attr);

        // core: Primary-constructor parameters become compiler-generated backing fields, so capture those too.

        var fields =
            from field in type.GetFields(flags)
            let attr = field.GetCustomAttribute<ScopeState>()
            where attr is not null
            select ((MemberInfo)field, attr);

        return properties.Concat(fields).ToArray();
    }
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
        public ILogger<ActivityDomain.Output> Output => logger.MapAs<T, ActivityDomain.Output>().WithState((nameof(ActivityDomain), nameof(ActivityDomain.Output)));
        public ILogger<ActivityDomain.Engine> Engine => logger.MapAs<T, ActivityDomain.Engine>().WithState((nameof(ActivityDomain), nameof(ActivityDomain.Engine)));

        public ILogger<MessageRole.Data> Data => logger.MapAs<T, MessageRole.Data>().WithState((nameof(MessageRole), nameof(MessageRole.Data)));
        public ILogger<MessageRole.Clue> Clue => logger.MapAs<T, MessageRole.Clue>().WithState((nameof(MessageRole), nameof(MessageRole.Clue)));
        public ILogger<MessageRole.News> News => logger.MapAs<T, MessageRole.News>().WithState((nameof(MessageRole), nameof(MessageRole.News)));

        public ActivityScope<TActivity> Begin<TActivity>(TActivity activity) where TActivity : Activity
        {
            return ActivityScope<TActivity>.Start(logger, activity);
        }
    }
}

public record LogContext
{
    public required string Activity { get; init; }

    public required string Status { get; init; }

    public required string ActivityDomain { get; init; }

    [ScopeState]
    public required string MessageRole { get; init; }

    [ScopeState]
    public required long ElapsedMs { get; init; }
}

// core: This class may not be a logger, because it will circumvent the LogStatus constraints for statuses allowing to apply IStatusOnly to an IStatusWithDuration scope!
public class ActivityScope<TActivity>(ILogger logger, TActivity activity) : IDisposable where TActivity : Activity
{
    // todo: set tags like "wiretap.activity" or "wiretap.channel"
    private ActivityWrapper ActivityWrapper { get; } = new(activity.Name);

    private Stopwatch Stopwatch { get; } = Stopwatch.StartNew();

    // util: Track all status for debugging. It is for free.
    private Stack<(ActivityStatus<TActivity> Status, LogContext Context)> StatusHistory { get; } = new();

    private bool ContainsLastStatus => StatusHistory.Any(x => x.Status is StatusRole.ILast);

    public ActivityScope<TActivity> LogStatus(ActivityStatus<TActivity> status)
    {
        if (activity.LastStatusMustBeVoid && status is StatusRole.ILast and StatusRole.IUser)
        {
            throw new InvalidOperationException(
                $"The code is trying to log the '{status.Status}' last status for the '{activity.Name}' activity, " +
                $"but activities with the '{nameof(LastStatusPolicy.MustBeVoid)}' attribute can't have an explicit last status.");
        }

        if (activity.LastStatusMustNotLeak && ContainsLastStatus)
        {
            throw new InvalidOperationException($"The code is trying to log another last status for the '{activity.Name}' activity, but activities can have only one last status.");
        }

        if (status is StatusRole.ILast)
        {
            // core: This is an overflow!
            if (status is StatusRole.IUser && ContainsLastStatus)
            {
                status = new ImplicitStatus<TActivity>.Leak(status);
            }

            ActivityWrapper.Stop(isOk: status switch
            {
                ExplicitStatus<TActivity>.Okay => true,
                ExplicitStatus<TActivity>.Fail => false,
                _ => null
            });
        }

        var context = new LogContext
        {
            Activity = activity.Name,
            Status = status.Status,
            ActivityDomain = activity.ActivityDomain,
            MessageRole = nameof(MessageRole.Data),
            ElapsedMs = (long)Stopwatch.Elapsed.TotalMilliseconds
        };

        var stateItems = ScopeState.From(activity).Concat(ScopeState.From(context));

        if (activity is IProvidesStateItems activityItems)
        {
            stateItems = stateItems.Concat(activityItems.States());
        }

        if (status is IProvidesStateItems statusItems)
        {
            stateItems = stateItems.Concat(statusItems.States());
        }

        stateItems = stateItems.Concat(ScopeState.From(status));

        using (logger.BeginScope(stateItems.ToDictionary()))
        {
            var statusParts = (status as IMessageTemplateParts)?.Parts(context) ?? [];
            var template = activity.MessageSchema.From(context, statusParts);
            logger.Log(status.Level, status.Exception, template.Template, template.Args);

            StatusHistory.Push((status, context));
        }

        return this;
    }

    public void LogDebug([StructuredMessageTemplate] string? message, params object?[] args)
    {
        LogStatus(ImplicitStatus<TActivity>.Busy.Debug(new(message, args)));
    }

    public void LogTrace([StructuredMessageTemplate] string? message, params object?[] args)
    {
        LogStatus(ImplicitStatus<TActivity>.Busy.Trace(new(message, args)));
    }

    public void Dispose()
    {
        if (!ContainsLastStatus)
        {
            if (activity.LastStatusMustBeVoid)
            {
                LogStatus(new ImplicitStatus<TActivity>.Void());
            }
            else
            {
                LogStatus(new ImplicitStatus<TActivity>.Last());
            }
        }

        ActivityWrapper.Dispose();
    }

    public static ActivityScope<TActivity> Start<T>(ILogger<T> logger, TActivity activity)
    {
        return new ActivityScope<TActivity>(logger, activity).LogStatus(new ImplicitStatus<TActivity>.Zero());
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
public record MessageTemplate([StructuredMessageTemplate] string? Template, params object?[] Args);

// core: This is the base class for all activities.
public abstract class Activity
{
    protected Activity()
    {
        var activityDomainMatch = Find<ActivityDomainAttribute>.From(GetType());
        ActivityDomain = activityDomainMatch.Path.First().Name;

        // note: The activity name begins by convention after the channel, so skip it.
        Name = string.Join(".", activityDomainMatch.Path.Skip(1).Select(t => t.Name));

        LastStatusMustBeVoid = Find<LastStatusPolicy.MustBeVoid>.From(GetType()).Attribute is not null;
        LastStatusMustNotLeak = Find<LastStatusPolicy.MustNotLeak>.From(GetType()).Attribute is not null;
        MessageSchema = Find<MessageSchema>.From(GetType()).Attribute ?? new CompactMessageSchema();
    }


    [ScopeState]
    public string ActivityDomain { get; }

    [ScopeState(nameof(Activity))]
    public string Name { get; }

    public bool LastStatusMustBeVoid { get; }

    public bool LastStatusMustNotLeak { get; }

    public MessageSchema MessageSchema { get; }
}

public static class StatusRole
{
    // core: Marks statuses that veto the execution of an activity before reaching its normal completion path.
    public interface IVeto;

    // core: Marks statuses that users can log.
    public interface IUser;

    // core: Marks statuses that are last in the activity's lifecycle.
    public interface ILast;

    // core: Marks statuses that are automatically logged.
    public interface IAuto;
}

public interface IProvidesStateItems
{
    IEnumerable<(string Key, object Value)> States();
}

public interface IMessageTemplateParts
{
    IEnumerable<MessageTemplate> Parts(LogContext context);
}

public abstract class ActivityStatus<TActivity> where TActivity : Activity
{
    [ScopeState]
    public virtual string Status => GetType().Name;

    public abstract LogLevel Level { get; }

    public Exception? Exception { get; init; }
}

public abstract class ExplicitStatus<TActivity> : ActivityStatus<TActivity> where TActivity : Activity
{
    // core: Used when an activity has started but deliberately stops before its normal completion path because a known,
    // non-exceptional condition makes continuation invalid, impossible, or no longer meaningful.
    public abstract class Halt : ActivityStatus<TActivity>, IMessageTemplateParts, StatusRole.ILast, StatusRole.IVeto, StatusRole.IUser
    {
        public override LogLevel Level => LogLevel.Warning;

        public required string Reason { get; init; }

        public IEnumerable<MessageTemplate> Parts(LogContext context)
        {
            yield return new("Reason: {Reason}", Reason);
        }
    }

    // core: This status applies when everything went according to plan.
    public abstract class Okay : ActivityStatus<TActivity>, StatusRole.ILast, StatusRole.IUser
    {
        public override LogLevel Level => LogLevel.Information;
    }

    // core: This status applies when an error occured.
    public abstract class Fail : ActivityStatus<TActivity>, StatusRole.ILast, StatusRole.IUser
    {
        public override LogLevel Level => LogLevel.Error;
    }
}

internal abstract class ImplicitStatus<TActivity> : ActivityStatus<TActivity> where TActivity : Activity
{
    // note: This is the very first status. Its previous name was "First".
    internal class Zero : ActivityStatus<TActivity>, StatusRole.IAuto
    {
        public override LogLevel Level => LogLevel.Trace;
    }

    internal class Busy(LogLevel level, MessageTemplate template) : ActivityStatus<TActivity>, StatusRole.IAuto, IMessageTemplateParts
    {
        public override LogLevel Level => level;

        public IEnumerable<MessageTemplate> Parts(LogContext context)
        {
            yield return template;
        }

        public static Busy Debug(MessageTemplate template) => new(LogLevel.Debug, template);

        public static Busy Trace(MessageTemplate template) => new(LogLevel.Trace, template);
    }

    // core: This status applies when the caller does not care about the result.
    internal class Void : ActivityStatus<TActivity>, StatusRole.ILast, StatusRole.IAuto
    {
        public override LogLevel Level => LogLevel.Information;
    }

    // core: This status applies when activity was not properly stopped.
    internal class Last : ActivityStatus<TActivity>, StatusRole.ILast, StatusRole.IAuto
    {
        public override LogLevel Level => LogLevel.Warning;
    }

    // core: This status wraps another last status when it overflows.
    internal sealed class Leak(ActivityStatus<TActivity> inner) : ActivityStatus<TActivity>, StatusRole.ILast, StatusRole.IAuto
    {
        public override LogLevel Level => LogLevel.Warning;

        public override string Status => nameof(Leak);

        [ScopeState]
        public string StatusLeaking => inner.Status;
    }
}