namespace WinttyBench.Kpis;

public sealed class ThroughputKpi : IKpi
{
    public string Name => "throughput_bytes_per_sec";

    public KpiResult ComputeFromSamples(IReadOnlyList<IterationSample> samples, long fixtureBytes)
    {
        if (samples.Count == 0)
            throw new ArgumentException("Need at least one sample", nameof(samples));

        var hung = samples.Count(s => s.Hung);
        // More than half of iterations hung -> emit null values with the
        // "degraded" source marker so downstream readers see what happened
        // instead of a p50 computed from 2 survivors.
        if (hung * 2 > samples.Count)
        {
            return new KpiResult(
                ValueP50: null, ValueP95: null, ValueP99: null, ValueStddev: null,
                RawIterations: samples, Source: "degraded");
        }

        var throughputs = samples
            .Where(s => !s.Hung && s.Value.HasValue)
            .Select(s => fixtureBytes / s.Value!.Value)
            .ToArray();
        Array.Sort(throughputs);

        return new KpiResult(
            ValueP50: Percentile(throughputs, 0.50),
            ValueP95: Percentile(throughputs, 0.95),
            ValueP99: Percentile(throughputs, 0.99),
            ValueStddev: Stddev(throughputs),
            RawIterations: samples,
            Source: "hyperfine");
    }

    public static IReadOnlyList<IterationSample> TrimFirstAndLast(IReadOnlyList<IterationSample> samples)
    {
        if (samples.Count <= 2) return samples;
        return samples.Skip(1).Take(samples.Count - 2).ToArray();
    }

    // Linear-interpolation percentile (same as numpy default). Samples must be sorted.
    public static double Percentile(IReadOnlyList<double> sortedSamples, double p)
    {
        if (sortedSamples.Count == 0) return 0;
        if (sortedSamples.Count == 1) return sortedSamples[0];

        var rank = p * (sortedSamples.Count - 1);
        var lo = (int)Math.Floor(rank);
        var hi = (int)Math.Ceiling(rank);
        if (lo == hi) return sortedSamples[lo];

        var frac = rank - lo;
        return sortedSamples[lo] + frac * (sortedSamples[hi] - sortedSamples[lo]);
    }

    private static double Stddev(double[] samples)
    {
        if (samples.Length < 2) return 0;
        var mean = samples.Average();
        var sumSq = samples.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sumSq / (samples.Length - 1));
    }
}
