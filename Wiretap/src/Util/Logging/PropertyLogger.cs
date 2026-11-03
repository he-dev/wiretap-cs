using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Wiretap.Util.Logging;

public class PropertyLogger(ILogger logger, KeyValuePair<string, object?>[] properties) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            using var scope = BeginScope(properties);
            logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }

    public static PropertyLogger With(ILogger logger, (string Name, object? Value)[] properties)
    {
        var keyValuePairs = properties.Select(p => new KeyValuePair<string, object?>(p.Name, p.Value)).ToArray();
        return new PropertyLogger(logger, keyValuePairs);
    }
}
