using System.Text;

namespace Wiretap.Util.Buzz;

// TODO: Replace ComposeMessage once the old MessagePartMap pipeline is gone.
public sealed class ComposeMessage2
{
    private Action<RemarkBuilder> _remarks = remarks =>
    {
        remarks.AddActivity();
        remarks.AddActivityDuration();
    };

    private Action<ArrangeRemarks2> _arrange = arrange =>
    {
        arrange.Add(arrange.Root.Activity.Name);
        arrange.Add(arrange.Root.Activity.DurationMs);
        arrange.AddRemaining();
    };

    private Func<JoinRemarks2, MessageTemplate> _join = join => join.JoinToString("; ");

    public static ComposeMessage2 Default { get; } = new();

    public ComposeMessage2 Remarks(Action<RemarkBuilder> remarks)
    {
        _remarks = remarks;
        return this;
    }

    public ComposeMessage2 Arrange(Action<ArrangeRemarks2> arrange)
    {
        _arrange = arrange;
        return this;
    }

    public ComposeMessage2 Join(Func<JoinRemarks2, MessageTemplate> join)
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
        _arrange(new ArrangeRemarks2(root, remarks, arranged));
        return _join(new JoinRemarks2(arranged));
    }
}

public static class ComposeMessage2Remarks
{
    public static void AddActivity(this RemarkBuilder remarks)
    {
        var activity = remarks.Root.Activity;
        remarks.Add(
            activity.Name,
            $"{activity.Name:_}[{activity.Status.Code:_}]",
            remarks.Details.GetValueOrDefault(activity.Name),
            remarks.Details.GetValueOrDefault(activity.Status.Code)
        );
    }

    public static void AddActivityDuration(this RemarkBuilder remarks)
    {
        var duration = remarks.Root.Activity.DurationMs;
        remarks.Add(
            duration,
            remarks.Details.GetValueOrDefault(remarks.Root.Activity.Role) is "snap"
                ? "Duration: N/A"
                : $"Duration: {duration:N0} ms",
            remarks.Details.GetValueOrDefault(duration)
        );
    }
}

public sealed class ArrangeRemarks2(
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

public sealed class JoinRemarks2(IReadOnlyList<MessageTemplate> remarks) : IReadOnlyList<MessageTemplate>
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
