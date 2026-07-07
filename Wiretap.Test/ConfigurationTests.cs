using Microsoft.Extensions.Logging;
using Wiretap.Core;
using Wiretap.Util;
using Wiretap.Util.Buzz2;

namespace Wiretap.Test;

public sealed class ConfigurationTests
{
    [Fact]
    public void MissingNamedVariantWarnsAndFallsBackToDefault()
    {
        var logger = new CaptureLogger();
        Configuration.Default = new Configuration { DiagnosticLogger = DiagnosticLogger.Create(logger) };

        var resolved = Configuration.Resolve(new MissingVariantActivity());

        Assert.Same(Configuration.Default, resolved);
        Assert.Contains("missing-variant", Assert.Single(logger.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public void ScopeLogsWithResolvedEntryFactory()
    {
        var logger = new CaptureLogger();
        var composeMessage = new ComposeMessage().Join(_ => new MessageTemplate("configured"));
        Configuration.Default = new Configuration { ComposeMessage = composeMessage };

        try
        {
            using var scope = logger.BeginBuzz(new ConfiguredActivity());
        }
        finally
        {
            Configuration.Default = new Configuration();
        }

        Assert.Equal(["configured"], logger.Messages);
    }

    [Configuration.Use("missing-variant")]
    private sealed class MissingVariantActivity : Buzz;

    private sealed class ConfiguredActivity : Buzz;

    private sealed class CaptureLogger : ILogger<ConfiguredActivity>
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
