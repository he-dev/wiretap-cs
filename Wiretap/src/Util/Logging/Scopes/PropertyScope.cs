using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Wiretap.Util.Logging.Scopes;

public class PropertyScope(ILogger logger, KeyValuePair<string, object?>[] properties) : ILogger, IDisposable
{
    private IDisposable? Scope { get; } = logger.BeginScope(properties);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return logger.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }

    public void Dispose() => Scope?.Dispose();

    public static PropertyScope With(ILogger logger, (string Name, object? Value)[] properties)
    {
        var keyValuePairs = properties.Select(p => new KeyValuePair<string, object?>(p.Name, p.Value)).ToArray();
        return new PropertyScope(logger, keyValuePairs);
    }
}