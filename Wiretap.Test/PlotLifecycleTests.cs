using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Wiretap.Core;
using Wiretap.Util;

namespace Wiretap.Test;

public class PlotLifecycleTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("zero", "")]
    [InlineData("zero,tick,last", "")]
    [InlineData("last", "")]
    [InlineData("tick,tick,last", "")]
    [InlineData("zero,zero,last", "zero-occurs-more-than-once")]
    [InlineData("zero,last,last", "no-events-must-follow-last")]
    [InlineData("zero,last,tick,last", "no-events-must-follow-last")]
    public void LogsExpectedDiagnosticsAndSuppressesCompletedFallback(string events, string expected)
    {
        var logger = new CaptureLogger();
        using (var plot = logger.BeginPlotAbout("test", Story.Core))
        {
            foreach (var name in events.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                ILogger target = name switch
                {
                    "zero" => plot.Zero,
                    "tick" => plot.Tick,
                    "last" => plot.Last,
                    _ => throw new ArgumentException(name)
                };
                target.LogInformation("event");
            }
        }

        Assert.Equal(expected.Split('|', StringSplitOptions.RemoveEmptyEntries), logger.Diagnostics);
    }

    [Fact]
    public void FilteredEventsStillAdvanceLifecycle()
    {
        var logger = new CaptureLogger();
        using (var plot = logger.BeginPlotAbout("filtered", Story.Core))
        {
            plot.Zero.LogDebug("start");
            plot.Last.LogDebug("end");
        }

        Assert.Empty(logger.Diagnostics);
    }

    [Theory]
    [InlineData(false, ActivityStatusCode.Ok)]
    [InlineData(true, ActivityStatusCode.Error)]
    public void LastPreservesActivityCompletionStatus(bool error, ActivityStatusCode expected)
    {
        var logger = new CaptureLogger();
        Activity? captured = null;
        var plot = logger.BeginPlotAbout("status", Story.Core, configure: value => captured = value);
        try
        {
            plot.Zero.LogInformation("start");
            if (error)
                plot.Last.LogError("failed");
            else
                plot.Last.LogInformation("done");

            Assert.Equal(expected, captured!.Status);
        }
        finally
        {
            var duration = captured!.Duration;
            plot.Dispose();
            Assert.Equal(duration, captured.Duration);
            Assert.Equal(expected, captured.Status);
        }
    }

    private sealed class CaptureLogger : ILogger
    {
        private readonly List<object> scopes = [];
        public List<string> Diagnostics { get; } = [];

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information && logLevel != LogLevel.None;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            scopes.Add(state);
            return new Scope(() => scopes.RemoveAt(scopes.Count - 1));
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var properties = scopes.OfType<IEnumerable<KeyValuePair<string, object?>>>()
                .SelectMany(properties => properties)
                .ToArray();
            if (properties.LastOrDefault(property => property.Key == "plot.event").Value is PlotEvent.Beep)
            {
                Diagnostics.Add((string)properties.Last(property => property.Key == "context.error_code").Value!);
            }
        }

        private sealed class Scope(Action dispose) : IDisposable
        {
            public void Dispose() => dispose();
        }
    }
}
