using Microsoft.Extensions.Logging;
using Wiretap.Core;

namespace Wiretap.Util;

public sealed class LoggerProxy<T>(ILogger inner, params KeyValuePair<string, object?>[] items) : ILogger<T>
{
    private List<KeyValuePair<string, object?>> Items { get; } = [..items];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        using (inner.BeginScope(ActivityScope.CurrentItemTags()))
        using (Items.Count == 0 ? null : inner.BeginScope(Items))
        {
            inner.Log(logLevel, eventId, state, exception, formatter);
        }
    }

    public LoggerProxy<T> WithStateItem(string key, object? value)
    {
        Items.Add(new(key, value));
        return this;
    }
}