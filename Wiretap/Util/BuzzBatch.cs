using System.Globalization;
using Wiretap.Util.Services;

namespace Wiretap.Util;

internal sealed class BuzzBatch : IStateItemFeed, IMessagePartFeed
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

    public void Count(ActivityStatus status, long durationMs)
    {
        ItemCount++;

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

    public void StateItems(PushStateItem push)
    {
        if (ItemCount == 0)
        {
            return;
        }

        push("item_count", ItemCount);

        foreach (var (code, count) in _statusCounts)
        {
            push($"{code}_count", count);
            push($"{code}_rate", RateOf(code));
        }

        push("duration_ms", DurationMs);
        push("duration_ms_mean", DurationMsMean);
        push("duration_ms_min", DurationMsMin);
        push("duration_ms_max", DurationMsMax);
        push("duration_ms_std_dev", DurationMsStdDev);
        push("throughput_s", ThroughputS);
    }

    public void MessageParts(ActivityStatus.Context context, PushMessagePart push)
    {
        if (ItemCount == 0)
        {
            return;
        }

        foreach (var code in _statusCounts.Keys)
        {
            var label = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(code);
            push($"{label}: {{{code}_rate:P1}} ({{{code}_count}} of {{item_count}})", RateOf(code), _statusCounts[code], ItemCount);
        }

        push("Throughput: {throughput_s:N1}/s", ThroughputS);
    }

    private double RateOf(string code)
    {
        return ItemCount > 0 ? _statusCounts[code] / (double)ItemCount : 0;
    }
}
