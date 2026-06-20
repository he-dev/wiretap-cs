using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

// note: Experimental replacement for Configuration; it is intentionally not integrated yet.
public static class Configuration2
{
    public sealed record Variant(CreateLogEntry CreateLogEntryBy)
    {
        public Variant() : this(CreateLogEntry.By())
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class UseAttribute(string variant) : Attribute
    {
        public string Variant { get; } = variant;
    }

    private abstract record Key
    {
        internal sealed record Default : Key;
        internal sealed record Named(string Value) : Key;
    }

    private static readonly Key DefaultKey = new Key.Default();
    private static readonly ConcurrentDictionary<Key, Variant> Variants = new()
    {
        [DefaultKey] = new Variant()
    };
    private static readonly ConcurrentDictionary<Type, Variant> Resolved = new();

    internal static ILogger DiagnosticLogger { get; private set; } = NullLogger.Instance;

    public static Variant Default => Variants[DefaultKey];

    public static Variant? Get(string name) =>
        Variants.GetValueOrDefault(new Key.Named(name));

    public static void LogDiagnosticsWith(ILogger logger) => DiagnosticLogger = logger;

    public static void UseDiagnosticsLogger(ILoggerFactory loggerFactory, string category = "Wiretap.Diagnostics") =>
        LogDiagnosticsWith(loggerFactory.CreateLogger(category));

    public static void SetDefault(Func<Variant> variant)
    {
        Variants[DefaultKey] = variant();
        Resolved.Clear();
    }

    public static void AddNamed(string name, Func<Variant> variant)
    {
        if (!Variants.TryAdd(new Key.Named(name), variant()))
        {
            throw new InvalidOperationException($"Configuration variant '{name}' already exists.");
        }
        Resolved.Clear();
    }

    public static Variant Resolve(Activity activity) =>
        Resolved.GetOrAdd(activity.GetType(), Resolve);

    private static Variant Resolve(Type activityType)
    {
        var name = activityType
            .GetCustomAttributes(typeof(UseAttribute), false)
            .Cast<UseAttribute>()
            .FirstOrDefault()
            ?.Variant;
        if (name is null)
        {
            return Default;
        }

        return Get(name) ?? WarnAndUseDefault(name, activityType);
    }

    private static Variant WarnAndUseDefault(string name, Type activityType)
    {
        DiagnosticLogger.LogWarning(
            "Configuration variant {Variant} requested by {ActivityType} was not found; using the default variant.",
            name,
            activityType.FullName
        );
        return Default;
    }
}
