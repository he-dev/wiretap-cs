using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Wiretap.Core;

namespace Wiretap.Test;

public class ActivityLifecycleTests
{
    [Theory]
    [InlineData("", "last-must-be-explicit")]
    [InlineData("zero", "|last-must-be-explicit")]
    [InlineData("zero,tick,last", "||")]
    [InlineData("last", "last-must-follow-zero-or-tick")]
    [InlineData("tick,tick,last", "tick-must-follow-zero-or-tick||")]
    [InlineData("zero,zero,last", "|zero-must-occur-only-once|")]
    [InlineData("zero,last,last", "||last-must-occur-only-once")]
    [InlineData("zero,last,tick,last", "||tick-must-follow-zero-or-tick|")]
    public void LogsExpectedDiagnosticsAndSuppressesCompletedFallback(string events, string expected)
    {
        var logger = new CaptureLogger();
        using (var activity = logger.BeginScopeAbout("test"))
        {
            foreach (var name in events.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var target = name switch
                {
                    "zero" => activity.Zero,
                    "tick" => activity.Tick,
                    "last" => activity.Last,
                    _ => throw new ArgumentException(name)
                };
                target.LogInformation("event");
            }
        }

        Assert.Equal(expected.Split('|'), logger.Diagnostics);
    }

    [Fact]
    public void FilteredEventsStillAdvanceLifecycle()
    {
        var logger = new CaptureLogger();
        using (var activity = logger.BeginScopeAbout("filtered"))
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
        var activity = logger.BeginScopeAbout("status", configure: value => captured = value);
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
            var constraints = scopes.OfType<IEnumerable<KeyValuePair<string, object?>>>()
                .SelectMany(properties => properties)
                .Where(property => property.Key == "fixme")
                .SelectMany(property => (string[])property.Value!);
            Diagnostics.Add(string.Join(",", constraints));
        }

        private sealed class Scope(Action dispose) : IDisposable
        {
            public void Dispose() => dispose();
        }
    }
}
