using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Wiretap.Core;
using Wiretap.Util;
using ActivityEvent = Wiretap.Util.ActivityEvent;

namespace Wiretap.Test;

public class ActivityLifecycleTests
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
        using (var activity = logger.BeginScopeAbout("test", Channel.Official))
        {
            foreach (var name in events.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                ILogger target = name switch
                {
                    "zero" => activity.Zero,
                    "tick" => activity.Tick,
                    "last" => activity.Last,
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
        using (var activity = logger.BeginScopeAbout("filtered", Channel.Official))
        {
            activity.Zero.LogDebug("start");
            activity.Last.LogDebug("end");
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
        var activity = logger.BeginScopeAbout("status", Channel.Official, configure: value => captured = value);
        try
        {
            activity.Zero.LogInformation("start");
            if (error)
                activity.Last.LogError("failed");
            else
                activity.Last.LogInformation("done");

            Assert.Equal(expected, captured!.Status);
        }
        finally
        {
            var duration = captured!.Duration;
            activity.Dispose();
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
            if (properties.LastOrDefault(property => property.Key == "activity.event").Value is ActivityEvent.Beep)
            {
                Diagnostics.Add((string)properties.Last(property => property.Key == "context.beep_code").Value!);
            }
        }

        private sealed class Scope(Action dispose) : IDisposable
        {
            public void Dispose() => dispose();
        }
    }
}
