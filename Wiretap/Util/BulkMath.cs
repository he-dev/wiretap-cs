using System.Globalization;

namespace Wiretap.Util;

public sealed class BulkMath
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

    public double ThroughputMs => DurationMs > 0 ? ItemCount / (double)DurationMs : 0;

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

}
