using System.Collections.Immutable;
using System.Globalization;

namespace Wiretap.Util.Buzz;

internal static class DefaultMessageParts
{
    public static ImmutableArray<MessagePartRegistration> All { get; } =
    [
        PushActivityHeader,
        PushActivityDuration,
        PushBulkSummary,
    ];

    private static void PushActivityHeader(PropertyName root, GetLogProperty get, PushMessagePart push)
    {
        push.Discrete(
            root.Activity.Name,
            $"{root.Activity.Name:_}[{root.Activity.Status.Code:_}]",
            get(root.Activity.Name),
            get(root.Activity.Status.Code)
        );
    }

    private static void PushActivityDuration(PropertyName root, GetLogProperty get, PushMessagePart push)
    {
        if (get(root.Activity.Role) is "snap")
        {
            push.Discrete(root.Activity.DurationMs, "Duration: N/A");
            return;
        }

        push.Discrete(
            root.Activity.DurationMs,
            $"Duration: {root.Activity.DurationMs:N0} ms",
            get(root.Activity.DurationMs)
        );
    }

    private static void PushBulkSummary(PropertyName root, GetLogProperty get, PushMessagePart push)
    {
        var state = root.Activity.State.Append("bulk");

        foreach (var code in new[] { "okay", "noop", "fail", "void" })
        {
            var countName = state.Append($"{code}_count");
            if (get(countName) is not { } count)
            {
                continue;
            }

            var rateName = state.Append($"{code}_rate");
            var label = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(code);
            push.Discrete(
                state.Append(code),
                $"{label}: {rateName:P1} ({countName:_} of {state.Append("item_count"):_})",
                get(rateName),
                count,
                get(state.Append("item_count"))
            );
        }

        push.Discrete(
            state.Append("throughput_s"),
            $"Throughput: {state.Append("throughput_s"):N1}/s",
            get(state.Append("throughput_s"))
        );
    }
}
