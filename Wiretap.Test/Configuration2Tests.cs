using Microsoft.Extensions.Logging;
using Wiretap.Util;

namespace Wiretap.Test;

public sealed class Configuration2Tests
{
    [Fact]
    public void MissingNamedVariantWarnsAndFallsBackToDefault()
    {
        var logger = new CaptureLogger();
        Configuration2.LogDiagnosticsWith(logger);

        var resolved = Configuration2.Resolve(new MissingVariantActivity());

        Assert.Same(Configuration2.Default, resolved);
        Assert.Contains("missing-variant", Assert.Single(logger.Messages), StringComparison.Ordinal);
    }

    [Configuration2.Use("missing-variant")]
    private sealed class MissingVariantActivity : Activity.Snap;

    private sealed class CaptureLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
