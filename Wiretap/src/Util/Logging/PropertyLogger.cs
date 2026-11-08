using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Wiretap.Util.Logging;

public class PropertyLogger(ILogger logger, KeyValuePair<string, object?>[] properties) : ILogger
{
    private Dictionary<string, object?> Properties { get; set; } = properties.ToDictionary();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        // core: Remove own properties when they are being overriden by later decorators.
        if (state is IEnumerable<KeyValuePair<string, object?>> items)
        {
            foreach (var (key, _) in items)
            {
                Properties.Remove(key);
            }
        }

        return logger.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            // core: Call logger directly to avoid removing own properties.
            using var scope = logger.BeginScope(Properties);
            logger.Log(logLevel, eventId, state, exception, formatter);
        }

        // core: Reset properties in case this logger is reused.
        Properties = properties.ToDictionary();
    }

    public static PropertyLogger With(ILogger logger, (string Name, object? Value)[] properties)
    {
        var keyValuePairs = properties.Select(p => new KeyValuePair<string, object?>(p.Name, p.Value)).ToArray();
        return new PropertyLogger(logger, keyValuePairs);
    }
}