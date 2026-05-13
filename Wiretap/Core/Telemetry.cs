using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Wiretap.Core;

public abstract class MessageRole
{
    // core: Pure telemetry data.
    public abstract class Data;

    // core: Something nice to know about what is going on.
    public abstract class Note;

    // core: Readable entries meant for the console.
    public abstract class Echo;
}

[AttributeUsage(AttributeTargets.Class)]
public abstract class LastStatusPolicy : Attribute
{
    public class CanBeVoid : LastStatusPolicy;

    // core: Mutes leaks except fails.
    public class MuteLeaks : LastStatusPolicy
    {
        // core: Does not log leaks.
        public bool Silently { get; init; }
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
public abstract class MessageSchema : Attribute
{
    public abstract MessageTemplate From<TActivity>(LogContext context, ActivityStatus<TActivity> status) where TActivity : ActivityRole;
}

public sealed class CompactMessageSchema(string separator = "; ") : MessageSchema
{
    public override MessageTemplate From<TActivity>(LogContext context, ActivityStatus<TActivity> status)
    {
        return Compose(context, status as IWithMessageParts);
    }

    private MessageTemplate Compose(LogContext context, IWithMessageParts? messageParts)
    {
        var parts = new List<MessageTemplate>
        {
            new("{ActivityRole}: {Activity}[{Status}]", context.ActivityRole, context.Activity, context.Status),
            new("Elapsed: {ElapsedMs:N0} ms", context.ElapsedMs)
        };

        messageParts?.MessageParts(context, (t, a) => parts.Add(new(t, a)));

        // note: Low performance.
         //return root.Concat(others).Aggregate((c, n) => new($"{c.Template}{separator}{n.Template}", [..c.Args, ..n.Args]));

        var msgs = new StringBuilder(200);
        var args = new List<object?>(16);

        foreach (var part in parts)
        {
            if (msgs.Length > 0)
            {
                msgs.Append(separator);
            }

            msgs.Append(part.Template);
            args.AddRange(part.Args);
        }

        return new(msgs.ToString(), args.ToArray());
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public class ScopeStateItem(string? name = null) : Attribute
{
    public string? Name { get; } = name;

    public static void From<T>(T source, AddStateItem add) where T : notnull
    {
        GetScopeStatePropertyValues.From(source, add);
    }
}

public static class GetScopeStatePropertyValues
{
    private static readonly ConcurrentDictionary<Type, Getter[]> Cache = new();

    public static void From<T>(T source, AddStateItem add) where T : notnull
    {
        var getters = Cache.GetOrAdd(source.GetType(), DiscoverStateItems);

        foreach (var getter in getters)
        {
            if (getter.GetValue(source) is {} value)
            {
                add(getter.Key, value);
            }
        }
    }

    private static Getter[] DiscoverStateItems(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        var stateItemGetters =
            from property in type.GetProperties(flags)
            let attr = property.GetCustomAttribute<ScopeStateItem>()
            where attr is not null
            select new Getter(attr.Name ?? property.Name, Getter.Compile(type, property));

        return [..stateItemGetters];
    }

    private sealed record Getter(string Key, Func<object, object?> GetValue)
    {
        public static Func<object, object?> Compile(Type type, PropertyInfo property)
        {
            if (!property.CanRead)
            {
                throw new InvalidOperationException($"The property '{type.Name}.{property.Name}' is marked with '{nameof(ScopeStateItem)}' but does not have a getter.");
            }

            var source = Expression.Parameter(typeof(object), "source");
            var typedSource = Expression.Convert(source, type);
            var value = Expression.Property(typedSource, property);
            var boxedValue = Expression.Convert(value, typeof(object));
            return Expression.Lambda<Func<object, object?>>(boxedValue, source).Compile();
        }
    }
}

public static class BuildActivityName
{
    private static readonly ConcurrentDictionary<Type, string> Cache = new();

    public static string For(Type type) => Cache.GetOrAdd(type, Discover);

    private static string Discover(Type type)
    {
        var names = new Stack<string>();

        for (var current = type; current is not null; current = current.DeclaringType)
        {
            names.Push(current.Name);
        }

        return string.Join(".", names);
    }
}

public sealed class LoggerProxy<T>(ILogger inner, params KeyValuePair<string, object?>[] items) : ILogger<T>
{
    private List<KeyValuePair<string, object?>> Items { get; } = [..items];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        using var scope = Items.Count == 0 ? null : inner.BeginScope(Items);
        inner.Log(logLevel, eventId, state, exception, formatter);
    }

    public LoggerProxy<T> WithStateItem(string key, object? value)
    {
        Items.Add(new(key, value));
        return this;
    }
}

public static class LoggerExtensions
{
    extension<T>(ILogger<T> logger)
    {
        public ILogger<ActivityRole.Core> Core => new LoggerProxy<ActivityRole.Core>(logger).WithStateItem(nameof(ActivityRole), nameof(ActivityRole.Core));
        public ILogger<ActivityRole.Buzz> Buzz => new LoggerProxy<ActivityRole.Buzz>(logger).WithStateItem(nameof(ActivityRole), nameof(ActivityRole.Buzz));

        public ILogger<MessageRole.Data> Data => new LoggerProxy<MessageRole.Data>(logger).WithStateItem(nameof(MessageRole), nameof(MessageRole.Data));
        public ILogger<MessageRole.Note> Note => new LoggerProxy<MessageRole.Note>(logger).WithStateItem(nameof(MessageRole), nameof(MessageRole.Note));
        public ILogger<MessageRole.Echo> Echo => new LoggerProxy<MessageRole.Echo>(logger).WithStateItem(nameof(MessageRole), nameof(MessageRole.Echo));

        public ActivityScope<TActivity> Begin<TActivity>(TActivity activity) where TActivity : ActivityRole
        {
            return ActivityScope<TActivity>.Start(logger, activity);
        }
    }
}

public record LogContext
{
    public required string Activity { get; init; }
    public required string ActivityRole { get; init; }
    public required string MessageRole { get; init; }
    public required string Status { get; init; }
    public required long ElapsedMs { get; init; }
}

// core: This class may not be a logger, because it will circumvent the LogStatus constraints for statuses allowing to apply IStatusOnly to an IStatusWithDuration scope!
public class ActivityScope<TActivity>(ILogger logger, TActivity activity) : IDisposable where TActivity : ActivityRole
{
    // todo: set tags like "wiretap.activity" or "wiretap.channel"
    private ActivityWrapper ActivityWrapper { get; } = new(activity.Name);

    private Stopwatch Stopwatch { get; } = Stopwatch.StartNew();

    // util: Track all status for debugging. It is for free.
    //public Stack<(ActivityStatus<TActivity> Status, LogContext Context)> StatusHistory { get; } = new();

    private bool ContainsLastStatus { get; set; }

    public ActivityScope<TActivity> LogStatus(ExplicitStatus<TActivity> status)
    {
        // core: Mute leaks except fails.
        if (status is StatusRole.ILast && ContainsLastStatus && status is not ExplicitStatus<TActivity>.Fail)
        {
            if (activity.MuteLeaks is { } muteLeaks)
            {
                return muteLeaks.Silently ? this : LogStatus(new ImplicitStatus<TActivity>.Leak(status));
            }

            throw new InvalidOperationException($"The code is trying to log '{status.Status}' as another last status for the '{activity.Name}' activity, but activities can have only one last status.");
        }

        // meta: Needs to cast so the right overload is called.
        return LogStatus((ActivityStatus<TActivity>)status);
    }

    private ActivityScope<TActivity> LogStatus(ActivityStatus<TActivity> status)
    {
        if (status is StatusRole.ILast)
        {
            ContainsLastStatus = true;
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
            ActivityRole = activity.Role,
            MessageRole = nameof(MessageRole.Data),
            ElapsedMs = (long)Stopwatch.Elapsed.TotalMilliseconds
        };

        // meta: Using a list rather than Enumerable.Concat for performance reasons.
        var stateItems = new List<KeyValuePair<string, object?>>(16)
        {
            new(nameof(LogContext.Activity), context.Activity),
            new(nameof(LogContext.ActivityRole), context.ActivityRole),
            new(nameof(LogContext.Status), context.Status),
            new(nameof(LogContext.MessageRole), context.MessageRole),
            new(nameof(LogContext.ElapsedMs), context.ElapsedMs),
        };

        var addStateItem = new AddStateItem((s, o) => stateItems.Add(new(s, o)));

        ScopeStateItem.From(activity, addStateItem);
        ScopeStateItem.From(status, addStateItem);

        (activity as IWithStateItems)?.StateItems(addStateItem);
        (status as IWithStateItems)?.StateItems(addStateItem);

        using (logger.BeginScope(stateItems))
        {
            var template = activity.MessageSchema.From(context, status);
            logger.Log(status.Level, status.Exception, template.Template, template.Args);
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
            var level = activity.CanBeVoid is null ? LogLevel.Warning : LogLevel.Information;
            LogStatus(new ImplicitStatus<TActivity>.Void(level));
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
public abstract class ActivityRole
{
    protected ActivityRole()
    {
        Name = BuildActivityName.For(GetType());
        CanBeVoid = GetType().GetCustomAttribute<LastStatusPolicy.CanBeVoid>(inherit: true);
        MuteLeaks = GetType().GetCustomAttribute<LastStatusPolicy.MuteLeaks>(inherit: true);
        MessageSchema =
            GetType().GetCustomAttribute<MessageSchema>(inherit: true)
            ?? GetType().Assembly.GetCustomAttribute<MessageSchema>()
            ?? Assembly.GetEntryAssembly()?.GetCustomAttribute<MessageSchema>()
            ?? new CompactMessageSchema();
    }


    public abstract string Role { get; }

    public string Name { get; }

    public LastStatusPolicy.CanBeVoid? CanBeVoid { get; }

    public LastStatusPolicy.MuteLeaks? MuteLeaks { get; }

    public MessageSchema MessageSchema { get; }

    public abstract class Core : ActivityRole
    {
        public override string Role => nameof(Core);
    }

    public abstract class Buzz : ActivityRole
    {
        public override string Role => nameof(Buzz);
    }
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

public interface IWithStateItems
{
    void StateItems(AddStateItem add);
}

public interface IWithMessageParts
{
    void MessageParts(LogContext context, AppendMessagePart append);
}

public delegate void AppendMessagePart([StructuredMessageTemplate] string? message, params object?[] args);

public delegate void AddStateItem(string key, object? value);

public abstract class ActivityStatus<TActivity> where TActivity : ActivityRole
{
    private static readonly ConcurrentDictionary<Type, Func<ActivityStatus<TActivity>>> Cache = new();

    public virtual string Status => GetType().Name;

    public abstract LogLevel Level { get; }

    public Exception? Exception { get; init; }
}

public abstract class ExplicitStatus<TActivity> : ActivityStatus<TActivity> where TActivity : ActivityRole
{
    // core: Used when an activity has started but deliberately stops before its normal completion path because a known,
    // non-exceptional condition makes continuation invalid, impossible, or no longer meaningful.
    public abstract class Halt : ExplicitStatus<TActivity>, IWithMessageParts, StatusRole.ILast, StatusRole.IVeto, StatusRole.IUser
    {
        public override LogLevel Level => LogLevel.Warning;

        [ScopeStateItem]
        public required string Reason { get; init; }

        public void MessageParts(LogContext context, AppendMessagePart append)
        {
            append("Reason: {Reason}", Reason);
        }
    }

    // core: This status applies when everything went according to plan.
    public abstract class Okay : ExplicitStatus<TActivity>, StatusRole.ILast, StatusRole.IUser
    {
        public override LogLevel Level => LogLevel.Information;
    }

    // core: This status applies when an error occured.
    public abstract class Fail : ExplicitStatus<TActivity>, StatusRole.ILast, StatusRole.IUser
    {
        public override LogLevel Level => LogLevel.Error;
    }
}

internal abstract class ImplicitStatus<TActivity> : ActivityStatus<TActivity> where TActivity : ActivityRole
{
    // note: This is the very first status. Its previous name was "First".
    internal class Zero : ImplicitStatus<TActivity>, StatusRole.IAuto
    {
        public override LogLevel Level => LogLevel.Trace;
    }

    internal class Busy(LogLevel level, MessageTemplate template) : ImplicitStatus<TActivity>, StatusRole.IAuto, IWithMessageParts
    {
        public override LogLevel Level => level;

        public void MessageParts(LogContext context, AppendMessagePart append)
        {
            append(template.Template, template.Args);
        }

        public static Busy Debug(MessageTemplate template) => new(LogLevel.Debug, template);

        public static Busy Trace(MessageTemplate template) => new(LogLevel.Trace, template);
    }

    // core: This status applies when the caller does not care about the result.
    internal class Void(LogLevel level) : ImplicitStatus<TActivity>, IWithMessageParts, StatusRole.ILast, StatusRole.IAuto
    {
        public override LogLevel Level => level;

        public void MessageParts(LogContext context, AppendMessagePart append)
        {
            if (level == LogLevel.Information)
            {
                append($"{nameof(LastStatusPolicy.CanBeVoid)} policy is set; it allows omitting an explicit last status.");
            }

            if (level == LogLevel.Warning)
            {
                append($"An explicit last status is missing; using this as fallback.");
            }
        }
    }

    // core: This status wraps another last status when it overflows.
    internal sealed class Leak(ActivityStatus<TActivity> inner) : ImplicitStatus<TActivity>, IWithMessageParts, StatusRole.ILast, StatusRole.IAuto
    {
        public override LogLevel Level => LogLevel.Warning;

        public override string Status => nameof(Leak);

        [ScopeStateItem]
        public string StatusLeaking => inner.Status;

        public void MessageParts(LogContext context, AppendMessagePart append)
        {
            append("Leaking: [{StatusLeaking}]", StatusLeaking);
        }
    }
}