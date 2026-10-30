using System;
using Microsoft.Extensions.Logging;

namespace Wiretap.Util;

public abstract class ActivityScope : ILogger, IDisposable
{
    protected abstract ILogger Logger { get; }

    public abstract ILogger Zero { get; }
    public abstract ILogger Tick { get; }
    public abstract ILogger Last { get; }

    public virtual IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => Logger.BeginScope(state);

    public virtual bool IsEnabled(LogLevel logLevel)
        => Logger.IsEnabled(logLevel);

    public virtual void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Logger.Log(logLevel, eventId, state, exception, formatter);

    public abstract void Dispose();
}