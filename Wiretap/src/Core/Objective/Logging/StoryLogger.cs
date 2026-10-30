using System;
using Microsoft.Extensions.Logging;

namespace Wiretap.Core.Logging;

public class StoryLogger(ILogger logger, string story) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // core: Forward filtered calls too so an underlying event logger can advance its lifecycle.
        using var storyScope = IsEnabled(logLevel) ? logger.BeginScopeWith(("story", story)) : null;
        logger.Log(logLevel, eventId, state, exception, formatter);
    }
}