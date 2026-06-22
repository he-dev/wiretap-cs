using Microsoft.Extensions.Logging;
using Wiretap.Core;
using Wiretap.Util;
using Wiretap.Util.Buzz;

namespace Wiretap.Test;

public sealed class ConfigurationTests
{
    [Fact]
    public void MissingNamedVariantWarnsAndFallsBackToDefault()
    {
        var logger = new CaptureLogger();
        Configuration.LogDiagnosticsWith(logger);

        var resolved = Configuration.Resolve(new MissingVariantActivity());

        Assert.Same(Configuration.Default, resolved);
        Assert.Contains("missing-variant", Assert.Single(logger.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public void ScopeLogsWithResolvedEntryFactory()
    {
        var logger = new CaptureLogger();
        var factory = CreateLogEntry.Default with { JoinMessageParts = new ConfiguredMessageParts() };
        Configuration.SetDefault(() => new Configuration.Variant(factory));

        try
        {
            using var scope = logger.BeginBuzz(new ConfiguredActivity());
        }
        finally
        {
            Configuration.SetDefault(() => new Configuration.Variant());
        }

        Assert.Equal(["configured", "configured"], logger.Messages);
    }

    [Configuration.Use("missing-variant")]
    private sealed class MissingVariantActivity : Activity.Snap;

    private sealed class ConfiguredActivity : Activity.Buzz;

    private sealed class ConfiguredMessageParts : IJoinMessageParts
    {
        public MessageTemplate By(IReadOnlyList<MessageTemplate> entries) => new("configured");
    }

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
