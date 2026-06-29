using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiretap.Core;
using Wiretap.Util;
using Wiretap.Util.Buzz;

namespace Wiretap.Test;

public sealed class CreateLogEntryTests
{
    [Fact]
    public void CollectLogPropertiesAppliesCascadeAndSourcePrecedence()
    {
        using var parent = NullLogger<ParentActivity>.Instance.BeginBuzz(new ParentActivity());
        using var child = NullLogger<ChildActivity>.Instance.BeginBuzz(new ChildActivity());

        child.SetStatus(new ChildActivity.Okay());
        var entry = CreateLogEntry.Default.From(child);

        Assert.Equal("parent", entry["wiretap.activity.state.ancestor"]);
        Assert.False(entry.ContainsKey("wiretap.activity.state.local_only"));
        Assert.Equal("status", entry["wiretap.activity.state.shared"]);
        Assert.False(entry.ContainsKey("wiretap.activity.state.private_value"));
        Assert.False(entry.ContainsKey("wiretap.activity.state.optional"));
    }

    [Fact]
    public void RemarksCanQuoteMessageParts()
    {
        var logger = new CaptureLogger();

        logger.LogSnap(new QuoteRecord(), new QuoteRecord.Okay());

        var message = Assert.Single(logger.Messages);
        Assert.Contains("Path: \"customer records.csv\"", message, StringComparison.Ordinal);
        Assert.Contains("Code: 'A42'", message, StringComparison.Ordinal);
    }

    private sealed class ParentActivity : Activity.Buzz
    {
        [Detail("ancestor", Cascade = true)]
        public string Ancestor => "parent";

        [Detail("local_only")]
        public string LocalOnly => "parent";
    }

    private sealed class ChildActivity : Activity.Buzz
    {
        [Detail("shared")]
        public string Shared => "activity";

        [Detail("optional")]
        public string? Optional => null;

        [Detail("private_value")]
        private string PrivateValue => "hidden";

        public sealed class Okay : ActivityStatus<ChildActivity>.Okay
        {
            [Detail("shared")]
            public string Shared => "status";
        }
    }

    private sealed class QuoteRecord : Activity.Snap
    {
        [Remark("Path", QuoteMode = QuoteMode.Auto)]
        public string Path => "customer records.csv";

        [Remark("Code", QuoteStyle = QuoteStyle.Single, QuoteMode = QuoteMode.Always)]
        public string Code => "A42";

        public sealed class Okay : ActivityStatus<QuoteRecord>.Okay;
    }

    private sealed class CaptureLogger : ILogger<QuoteRecord>
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
