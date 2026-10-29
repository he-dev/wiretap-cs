using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Wiretap.Core;

/// <summary>Measures samples overall and by a single optional outcome label.</summary>
public sealed class Benchmark
{
    private ConcurrentDictionary<string, Welford> Labels { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Welford Total { get; } = new();

    /// <summary>Returns statistics for an existing label; unknown labels throw KeyNotFoundException.</summary>
    public Welford this[string label] =>
        !Labels.TryGetValue(label, out var statistics)
            ? throw new KeyNotFoundException($"No benchmark samples have been recorded for label '{label}'.")
            : statistics;

    /// <summary>Count relative to another label, or Total when relativeTo is omitted.</summary>
    public double? Ratio(string label, string? relativeTo = null)
    {
        var count = this[label].Count;
        var relativeCount = relativeTo is null ? Total.Count : this[relativeTo].Count;
        return relativeCount == 0 ? null : (double)count / relativeCount;
    }

    public Sample Measure() => new(this);

    private void Record(string? label, double durationMs)
    {
        Total.Add(durationMs);
        if (label is not null)
        {
            Labels.GetOrAdd(label, static _ => new Welford()).Add(durationMs);
        }
    }

    /// <summary>A using block records one measurement on exit, including exceptional exits.</summary>
    public sealed class Sample(Benchmark benchmark) : IDisposable
    {
        private long StartedAt { get; } = Stopwatch.GetTimestamp();
        private string? Label { get; set; }
        private bool Disposed { get; set; }

        public void CountAs(string label)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            ArgumentNullException.ThrowIfNull(label);

            if (Label is not null)
            {
                throw new InvalidOperationException($"This sample has already been classified as '{Label}'.");
            }

            Label = label;
        }

        public void Dispose()
        {
            // core: IDisposable may be called more than once; record the sample only once.
            if (Disposed)
            {
                return;
            }

            var durationMs = Stopwatch.GetElapsedTime(StartedAt).TotalMilliseconds;
            Disposed = true;
            benchmark.Record(Label, durationMs);
        }
    }
}

/// <summary>Streaming duration statistics using Welford's algorithm; no samples are retained.</summary>
public sealed class Welford
{
    private double M2 { get; set; }

    public long Count { get; private set; }
    public double DurationMs { get; private set; }
    public double MeanMs { get; private set; }

    // core: Sample variance and deviation are unavailable until two measurements exist.
    public double? Variance => Count < 2 ? null : M2 / (Count - 1);
    public double? StdDev => Variance is { } variance ? Math.Sqrt(variance) : null;

    /// <summary>Samples per measured millisecond, excluding gaps between samples.</summary>
    public double ThroughputMs => DurationMs > 0 ? Count / DurationMs : 0;

    internal void Add(double durationMs)
    {
        DurationMs += durationMs;
        Count++;
        var delta = durationMs - MeanMs;
        MeanMs += delta / Count;
        M2 += delta * (durationMs - MeanMs);
    }
}