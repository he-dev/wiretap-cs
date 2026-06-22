using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Wiretap.Util;

// note: This draft is intentionally not wired into Configuration or ActivityScope yet.
public sealed class DiagnosticLogger(ILogger logger) : ILogger
{
    private readonly UniqueKeys<Warning> _warnings = new();

    public static DiagnosticLogger Noop { get; } = new(NullLogger.Instance);

    public void WarnOnce(string contract, object key, Action<ILogger> write)
    {
        if (_warnings.First(new Warning(contract, key)))
        {
            write(this);
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
        logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>
    (
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    ) => logger.Log(logLevel, eventId, state, exception, formatter);

    private sealed record Warning(string Contract, object Key);

    private sealed class UniqueKeys<TKey> where TKey : notnull
    {
        private readonly Lock _lock = new();
        private readonly HashSet<TKey> _warnings = [];

        public bool First(TKey key)
        {
            lock (_lock)
            {
                return _warnings.Add(key);
            }
        }
    }
}
