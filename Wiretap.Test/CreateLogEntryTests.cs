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

    private sealed class ParentActivity : Activity.Buzz
    {
        [StateItem("ancestor", Cascade = true)]
        public string Ancestor => "parent";

        [StateItem("local_only")]
        public string LocalOnly => "parent";
    }

    private sealed class ChildActivity : Activity.Buzz
    {
        [StateItem("shared")]
        public string Shared => "activity";

        [StateItem("optional")]
        public string? Optional => null;

        [StateItem("private_value")]
        private string PrivateValue => "hidden";

        public sealed class Okay : ActivityStatus<ChildActivity>.Okay
        {
            [StateItem("shared")]
            public string Shared => "status";
        }
    }
}
