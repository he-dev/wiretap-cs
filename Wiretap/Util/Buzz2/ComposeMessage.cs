using System.Text;
using Wiretap.Util.Data;

namespace Wiretap.Util.Buzz2;

public sealed class ComposeMessage
{
    private Action<RemarkBuilder> _remarks = remarks =>
    {
        remarks.AddActivity();
        remarks.AddActivityDuration();
    };

    private Action<ArrangeRemarks> _arrange = arrange =>
    {
        arrange.Add(arrange.Root.Buzz.Name);
        arrange.Add(arrange.Root.Buzz.DurationMs);
        arrange.AddRemaining();
    };

    private Func<JoinRemarks, MessageTemplate> _join = join => join.JoinToString("; ");

    public ComposeMessage Remarks(Action<RemarkBuilder> remarks)
    {
        _remarks = remarks;
        return this;
    }

    public ComposeMessage Arrange(Action<ArrangeRemarks> arrange)
    {
        _arrange = arrange;
        return this;
    }

    public ComposeMessage Join(Func<JoinRemarks, MessageTemplate> join)
    {
        _join = join;
        return this;
    }

    public MessageTemplate From(
        PropertyName root,
        DetailCollection details,
        RemarkCollection remarks
    )
    {
        _remarks(new RemarkBuilder(root, details, remarks));

        var arranged = new List<MessageTemplate>();
        _arrange(new ArrangeRemarks(root, remarks, arranged));
        return _join(new JoinRemarks(arranged));
    }
}

public static class ComposeMessage2Remarks
{
    extension(RemarkBuilder remarks)
    {
        public void AddActivity()
        {
            var activity = remarks.Root.Buzz;
            remarks.Add(
                activity.Name,
                $"{activity.Name:.}[{activity.Status.Code:.}]",
                remarks.Details.GetValueOrDefault(activity.Name),
                remarks.Details.GetValueOrDefault(activity.Status.Code)
            );
        }

        public void AddActivityDuration()
        {
            var duration = remarks.Root.Buzz.DurationMs;
            remarks.Add(
                duration,
                remarks.Details.GetValueOrDefault(remarks.Root.Buzz.Role) is "snap"
                    ? "Duration: N/A"
                    : $"Duration: {duration:N0} ms",
                remarks.Details.GetValueOrDefault(duration)
            );
        }
    }
}

public sealed class ArrangeRemarks(
    PropertyName root,
    RemarkCollection remarks,
    List<MessageTemplate> arranged
)
{
    public PropertyName Root { get; } = root;

    public void Add(PropertyName name)
    {
        if (remarks.Pop(name) is { } remark)
        {
            arranged.Add(remark);
        }
    }

    public void AddRemaining()
    {
        arranged.AddRange(remarks.Select(x => x.Value));
        remarks.Clear();
    }
}

public sealed class JoinRemarks(IReadOnlyList<MessageTemplate> remarks) : IReadOnlyList<MessageTemplate>
{
    public MessageTemplate this[int index] => remarks[index];

    public int Count => remarks.Count;

    public IEnumerator<MessageTemplate> GetEnumerator() => remarks.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public MessageTemplate JoinToString(string separator)
    {
        var template = new StringBuilder(256);
        var args = new List<object?>(32);
        foreach (var remark in remarks)
        {
            if (string.IsNullOrEmpty(remark.Template))
            {
                continue;
            }

            template.Append(template.Length == 0 ? string.Empty : separator);
            template.Append(remark.Template);
            args.AddRange(remark.Args);
        }

        return new MessageTemplate(template.ToString(), [.. args]);
    }
}

internal static class RemarkCollectionExtensions
{
    public static void Clear(this RemarkCollection remarks)
    {
        foreach (var name in remarks.Select(x => x.Key).ToList())
        {
            remarks.Pop(name);
        }
    }
}
