using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public class Configuration
{
    public PropertyName Root { get; init; } = new PropertyName("Wiretap");

    public ComposeMessage ComposeMessage { get; init; } = new ComposeMessage();

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class UseAttribute(string variant) : Attribute
    {
        public string Variant { get; } = variant;
    }

    private record Key(string Name)
    {
        internal static readonly Key Default = new Key(string.Empty);
    }

    private static readonly ConcurrentDictionary<Key, Configuration> Variants = new() { [Key.Default] = new Configuration() };
    private static readonly ConcurrentDictionary<Type, Configuration> Resolved = new();

    internal static DiagnosticLogger DiagnosticLogger { get; private set; } = DiagnosticLogger.Noop;

    public static ITraceContext TraceContext { get; private set; } = new TraceContext();

    public static Configuration Default => Variants[Key.Default];

    public static Configuration? Get(string name) => Variants.GetValueOrDefault(new Key(name));

    public static void LogDiagnosticsWith(ILogger logger) => DiagnosticLogger = new DiagnosticLogger(logger);

    public static void UseDiagnosticsLogger(ILoggerFactory loggerFactory, string category = "Wiretap.Diagnostics") =>
        LogDiagnosticsWith(loggerFactory.CreateLogger(category));

    public static void UseTraceContext(ITraceContext traceContext) => TraceContext = traceContext;

    public static void SetDefault(Func<Configuration> variant)
    {
        Variants[Key.Default] = variant();
        Resolved.Clear();
    }

    public static void AddNamed(string name, Func<Configuration> variant)
    {
        if (!Variants.TryAdd(new Key(name), variant()))
        {
            throw new InvalidOperationException($"Configuration variant '{name}' already exists.");
        }

        Resolved.Clear();
    }

    public static Configuration Resolve(Activity activity) => Resolved.GetOrAdd(activity.GetType(), Resolve);

    private static Configuration Resolve(Type activityType)
    {
        var name = activityType.GetCustomAttributes(typeof(UseAttribute), false)
            .Cast<UseAttribute>()
            .FirstOrDefault()
            ?.Variant;
        if (name is null)
        {
            return Default;
        }

        if (Get(name) is { } variant)
        {
            return variant;
        }

        DiagnosticLogger.WarnAboutMissingConfigurationVariant(name, activityType.FullName ?? activityType.Name);
        return Default;
    }
}