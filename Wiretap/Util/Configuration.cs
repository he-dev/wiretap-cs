using System.Collections.Concurrent;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public class Configuration
{
    public DiagnosticLogger DiagnosticLogger { get; init; } = DiagnosticLogger.Noop;

    public PropertyName Root { get; init; } = new PropertyName("wiretap");

    public ComposeMessage ComposeMessage { get; init; } = new ComposeMessage();

    public ITraceContext TraceContext { get; init; } = new TraceContext();

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class UseAttribute(string variant) : Attribute
    {
        public string Variant { get; } = variant;
    }

    private record Key(string Name)
    {
        internal static readonly Key Empty = new Key(string.Empty);
    }

    private static readonly ConcurrentDictionary<Key, Configuration> Variants = new() { [Key.Empty] = new Configuration() };
    private static readonly ConcurrentDictionary<Type, Configuration> Resolved = new();

    public static Configuration Default
    {
        get => Variants[Key.Empty];
        set => Variants[Key.Empty] = value;
    }

    public static Configuration? Get(string name) => Variants.GetValueOrDefault(new Key(name));

    public static void Register(string name, Func<Configuration> variant)
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

        Default.DiagnosticLogger.WarnAboutMissingConfigurationVariant(name, activityType.FullName ?? activityType.Name);
        return Default;
    }
}