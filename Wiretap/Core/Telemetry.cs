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

public readonly struct LastStatusPolicySet
{
    public LastStatusPolicy.CanBeVoid? CanBeVoid { get; init; }
    public LastStatusPolicy.MuteLeaks? MuteLeaks { get; init; }
}

public delegate void AddStateItem(string key, object? value);

public interface IWithStateItems
{
    void StateItems(AddStateItem add);
}

public delegate void AppendMessagePart([StructuredMessageTemplate] string? message, params object?[] args);

public interface IWithMessageParts
{
    void MessageParts(ActivityStatus.Context context, AppendMessagePart append);
}

// meta: This class is required to make the message template work with structured logging as the attribute can only be used on parameters.
public record MessageTemplate([StructuredMessageTemplate] string? Template, params object?[] Args);

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
public abstract class MessageSchema : Attribute
{
    public abstract MessageTemplate From(ActivityStatus.Context context, params IWithMessageParts?[] messageParts);
}

// core: Implements a message-schema where parts are joined by the specified separator.
public sealed class CompactMessageSchema(string separator = "; ") : MessageSchema, IWithMessageParts
{
    public override MessageTemplate From(ActivityStatus.Context context, params IWithMessageParts?[] messageParts)
    {
        // note: Does not use LINQ for better performance.

        var msgs = new StringBuilder(256);
        var args = new List<object?>(32);

        var append = new AppendMessagePart((t, a) =>
        {
            if (msgs.Length > 0)
            {
                msgs.Append(separator);
            }

            msgs.Append(t);
            args.AddRange(a);
        });

        foreach (var item in messageParts)
        {
            item?.MessageParts(context, append);
        }

        return new(msgs.ToString(), args.ToArray());
    }

    public void MessageParts(ActivityStatus.Context context, AppendMessagePart append)
    {
        append("{ActivityRole}: {Activity}[{Status}]", context.ActivityRole, context.Activity, context.ActivityStatus);
        append("Elapsed: {ElapsedMs:N0} ms", context.ElapsedMs);
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
            if (getter.GetValue(source) is { } value)
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
        public ActivityScope<TActivity> Begin<TActivity>(TActivity activity) where TActivity : Activity
        {
            return ActivityScope<TActivity>.Begin(logger, activity);
        }

        public ILogger<Activity.Core> Core => new LoggerProxy<Activity.Core>(logger).WithStateItem(nameof(Activity), nameof(Activity.Core));
        public ILogger<Activity.Buzz> Buzz => new LoggerProxy<Activity.Buzz>(logger).WithStateItem(nameof(Activity), nameof(Activity.Buzz));

        public ILogger<MessageRole.Data> Data => new LoggerProxy<MessageRole.Data>(logger).WithStateItem(nameof(MessageRole), nameof(MessageRole.Data));
        public ILogger<MessageRole.Note> Note => new LoggerProxy<MessageRole.Note>(logger).WithStateItem(nameof(MessageRole), nameof(MessageRole.Note));
        public ILogger<MessageRole.Echo> Echo => new LoggerProxy<MessageRole.Echo>(logger).WithStateItem(nameof(MessageRole), nameof(MessageRole.Echo));
    }
}

// core: This is the base class for all activities.
public abstract class Activity
{
    protected Activity()
    {
        var type = GetType();
        Name = BuildActivityName.For(type);
        LastStatusPolicy = new()
        {
            CanBeVoid = type.GetCustomAttribute<LastStatusPolicy.CanBeVoid>(inherit: true),
            MuteLeaks = type.GetCustomAttribute<LastStatusPolicy.MuteLeaks>(inherit: true)
        };
        MessageSchema =
            type.GetCustomAttribute<MessageSchema>(inherit: true)
            ?? type.Assembly.GetCustomAttribute<MessageSchema>()
            ?? Assembly.GetEntryAssembly()?.GetCustomAttribute<MessageSchema>()
            ?? new CompactMessageSchema();
    }


    public abstract string Role { get; }

    public string Name { get; }

    public LastStatusPolicySet LastStatusPolicy { get; }

    public MessageSchema MessageSchema { get; }

    public abstract class Core : Activity
    {
        public override string Role => nameof(Core);
    }

    public abstract class Buzz : Activity
    {
        public override string Role => nameof(Buzz);
    }
}

// core: This class may not be a logger, because it will circumvent the LogStatus constraints for statuses allowing to apply IStatusOnly to an IStatusWithDuration scope!
public class ActivityScope<TActivity>(ILogger logger, TActivity activity) : IDisposable where TActivity : Activity
{
    private ActivityWrapper ActivityWrapper { get; } = new(activity.Name);

    private Stopwatch Stopwatch { get; } = Stopwatch.StartNew();

    private bool ContainsLastStatus { get; set; }

    public TimeSpan Elapsed => Stopwatch.Elapsed;

    public static ActivityScope<TActivity> Begin<T>(ILogger<T> logger, TActivity activity)
    {
        var activityScope = new ActivityScope<TActivity>(logger, activity);
        activityScope.LogStatus(new ImplicitStatus<TActivity>.Zero());
        return activityScope;
    }

    public void LogStatus(ExplicitStatus<TActivity> status)
    {
        // core: Mute leaks except fails.
        if (status is StatusRole.ILast && ContainsLastStatus && status is not ExplicitStatus<TActivity>.Fail)
        {
            if (activity.LastStatusPolicy.MuteLeaks is { } muteLeaks)
            {
                if (muteLeaks.Silently)
                {
                    // core: Do not log anything.
                    return;
                }

                // core: Log the leak.
                LogStatus(new ImplicitStatus<TActivity>.Leak(status));

                return;
            }

            throw new InvalidOperationException($"The code is trying to log '{status.Code}' as another last status for the '{activity.Name}' activity, but activities can have only one last status.");
        }

        // meta: Needs to cast so the right overload is called.
        LogStatus((ActivityStatus<TActivity>)status);
    }

    private void LogStatus(ActivityStatus<TActivity> status)
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

        var context = new ActivityStatus.Context
        {
            Activity = activity.Name,
            ActivityStatus = status.Code,
            ActivityRole = activity.Role,
            MessageRole = nameof(MessageRole.Data),
            ElapsedMs = (long)Stopwatch.Elapsed.TotalMilliseconds
        };

        // meta: Using a list rather than Enumerable.Concat for performance reasons.
        var stateItems = new List<KeyValuePair<string, object?>>(16);
        var addStateItem = new AddStateItem((key, value) => stateItems.Add(new(key, value)));

        ScopeStateItem.From(activity, addStateItem);
        ScopeStateItem.From(status, addStateItem);

        (context as IWithStateItems)?.StateItems(addStateItem);
        (activity as IWithStateItems)?.StateItems(addStateItem);
        (status as IWithStateItems)?.StateItems(addStateItem);

        using (logger.BeginScope(stateItems))
        {
            var template = activity.MessageSchema.From(context, activity.MessageSchema as IWithMessageParts, status as IWithMessageParts);
            logger.Log(status.Level, status.Exception, template.Template, template.Args);
        }
    }

    public void LogDebug([StructuredMessageTemplate] string? message, params object?[] args)
    {
        LogStatus(new ImplicitStatus<TActivity>.Busy.Debug(message, args));
    }

    public void LogTrace([StructuredMessageTemplate] string? message, params object?[] args)
    {
        LogStatus(new ImplicitStatus<TActivity>.Busy.Trace(message, args));
    }

    public void Dispose()
    {
        if (!ContainsLastStatus)
        {
            if (activity.LastStatusPolicy.CanBeVoid is null)
            {
                LogStatus(new ImplicitStatus<TActivity>.Void.Warn());
            }
            else
            {
                LogStatus(new ImplicitStatus<TActivity>.Void.Info());
            }
        }

        ActivityWrapper.Dispose();
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

public static class StatusRole
{
    // core: Marks statuses that veto the execution of an activity before reaching its normal completion path.
    public interface IVeto;

    // core: Marks statuses that track the activity's lifecycle.
    public interface ICore;

    // core: Marks statuses that are last in the activity's lifecycle.
    public interface ILast;

    // core: Marks statuses that are automatically logged.
    public interface IAuto;
}

public abstract class ActivityStatus
{
    public virtual string Code => GetType().Name;

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
}

// note: The generic parameter ensures type safety for activity scopes.
public abstract class ActivityStatus<TActivity> : ActivityStatus where TActivity : Activity;

public abstract class ExplicitStatus<TActivity> : ActivityStatus<TActivity> where TActivity : Activity
{
    // core: Used when an activity has started but deliberately stops before its normal completion path because a known,
    // non-exceptional condition makes continuation invalid, impossible, or no longer meaningful.
    public abstract class Halt : ExplicitStatus<TActivity>, IWithMessageParts, StatusRole.ICore, StatusRole.ILast, StatusRole.IVeto
    {
        public override LogLevel Level => LogLevel.Warning;

        public required string Reason { get; init; }

        public void MessageParts(Context context, AppendMessagePart append)
        {
            append("Reason: {Reason}", Reason);
        }
    }

    // core: This status applies when everything went according to plan.
    public abstract class Okay : ExplicitStatus<TActivity>, StatusRole.ICore, StatusRole.ILast
    {
        public override LogLevel Level => LogLevel.Information;
    }

    // core: This status applies when an error occured.
    public abstract class Fail : ExplicitStatus<TActivity>, StatusRole.ICore, StatusRole.ILast
    {
        public override LogLevel Level => LogLevel.Error;
    }
}

internal abstract class ImplicitStatus<TActivity> : ActivityStatus<TActivity> where TActivity : Activity
{
    // note: This is the very first status. Its previous name was "First".
    internal class Zero : ImplicitStatus<TActivity>, StatusRole.IAuto
    {
        public override LogLevel Level => LogLevel.Trace;
    }

    internal abstract class Busy(LogLevel level, [StructuredMessageTemplate] string? message, object?[] args) : ImplicitStatus<TActivity>, StatusRole.IAuto, IWithMessageParts
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
    internal abstract class Void(LogLevel level) : ImplicitStatus<TActivity>, IWithMessageParts, StatusRole.ILast, StatusRole.IAuto
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
    internal sealed class Leak(ActivityStatus<TActivity> inner) : ImplicitStatus<TActivity>, IWithMessageParts, StatusRole.ILast, StatusRole.IAuto
    {
        public override LogLevel Level => LogLevel.Warning;

        public override string Code => nameof(Leak);

        public string StatusLeaking => inner.Code;

        public void MessageParts(Context context, AppendMessagePart append)
        {
            append("Leaking: [{StatusLeaking}]", StatusLeaking);
        }
    }
}