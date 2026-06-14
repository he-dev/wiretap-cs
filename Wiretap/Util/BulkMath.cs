using System.Globalization;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public sealed class BulkMath : IStateItemFeed, IMessagePartFeed
{
    private readonly Dictionary<string, int> _statusCounts = new(StringComparer.Ordinal);
    private double _durationMean;
    private double _durationM2;

    public int ItemCount { get; private set; }

    public long DurationMs { get; private set; }

    public long? DurationMsMin { get; private set; }

    public long? DurationMsMax { get; private set; }

    public double DurationMsMean => _durationMean;

    public double DurationMsStdDev => ItemCount > 1 ? Math.Sqrt(_durationM2 / (ItemCount - 1)) : 0;

    public double ThroughputS => DurationMs > 0 ? ItemCount / (DurationMs / 1000.0) : 0;

    public void Count(ActivityStatus status, TimeSpan duration)
    {
        ItemCount++;
        var durationMs = (long)duration.TotalMilliseconds;

        var code = status.Code.ToLower(CultureInfo.InvariantCulture);
        _statusCounts[code] = _statusCounts.GetValueOrDefault(code) + 1;

        DurationMs += durationMs;
        DurationMsMin = DurationMsMin is null ? durationMs : Math.Min(DurationMsMin.Value, durationMs);
        DurationMsMax = DurationMsMax is null ? durationMs : Math.Max(DurationMsMax.Value, durationMs);

        // util: Welford's algorithm tracks variance without storing every item duration.
        var delta = durationMs - _durationMean;
        _durationMean += delta / ItemCount;
        var delta2 = durationMs - _durationMean;
        _durationM2 += delta * delta2;
    }

    public void StateItems(PropertyName name, PushStateItem push)
    {
        if (ItemCount == 0)
        {
            return;
        }

        push(name.Activity.State.Append("item_count"), ItemCount);

        foreach (var (code, count) in _statusCounts)
        {
            push(name.Activity.State.Append($"{code}_count"), count);
            push(name.Activity.State.Append($"{code}_rate"), RateOf(code));
        }

        push(name.Activity.State.Append("duration_ms"), DurationMs);
        push(name.Activity.State.Append("duration_ms_mean"), DurationMsMean);
        push(name.Activity.State.Append("duration_ms_min"), DurationMsMin);
        push(name.Activity.State.Append("duration_ms_max"), DurationMsMax);
        push(name.Activity.State.Append("duration_ms_std_dev"), DurationMsStdDev);
        push(name.Activity.State.Append("throughput_s"), ThroughputS);
    }

    public void MessageParts(PropertyName root, GetStateItem get, PushMessagePart push)
    {
        if (ItemCount == 0)
        {
            return;
        }

        foreach (var code in _statusCounts.Keys)
        {
            var label = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(code);
            push(
                $"{label}: {root.Activity.State.Append($"{code}_rate"):P1} ({root.Activity.State.Append($"{code}_count"):_} of {root.Activity.State.Append("item_count"):_})",
                RateOf(code),
                _statusCounts[code],
                ItemCount
            );
        }

        push($"Throughput: {root.Activity.State.Append("throughput_s"):N1}/s", ThroughputS);
    }

    private double RateOf(string code)
    {
        return ItemCount > 0 ? _statusCounts[code] / (double)ItemCount : 0;
    }
}
