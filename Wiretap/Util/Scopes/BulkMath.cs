using System.Globalization;
using Wiretap.Util.Buzz;

namespace Wiretap.Util.Scopes;

public sealed class BulkMath : ILogPropertySource
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

    public void LogProperties(PropertyName name, PushLogProperty push)
    {
        if (ItemCount == 0)
        {
            return;
        }

        var bulk = name.Activity.State.Append("bulk");

        push(bulk.Append("item_count"), ItemCount);

        foreach (var (code, count) in _statusCounts)
        {
            push(bulk.Append($"{code}_count"), count);
            push(bulk.Append($"{code}_rate"), RateOf(code));
        }

        push(bulk.Append("duration_ms"), DurationMs);
        push(bulk.Append("duration_ms_mean"), DurationMsMean);
        push(bulk.Append("duration_ms_min"), DurationMsMin);
        push(bulk.Append("duration_ms_max"), DurationMsMax);
        push(bulk.Append("duration_ms_std_dev"), DurationMsStdDev);
        push(bulk.Append("throughput_s"), ThroughputS);
    }

    private double RateOf(string code)
    {
        return ItemCount > 0 ? _statusCounts[code] / (double)ItemCount : 0;
    }
}
