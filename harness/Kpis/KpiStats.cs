namespace WinttyBench.Kpis;

public static class KpiStats
{
    public static KpiResult ComputePercentiles(
        IReadOnlyList<IterationSample> samples,
        string sourceLabel)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0)
            throw new ArgumentException("Need at least one sample", nameof(samples));

        var hung = samples.Count(s => s.Hung);
        // More than half of iterations hung -> emit null values with the
        // "degraded" source marker so downstream readers see what happened
        // instead of a p50 computed from a handful of survivors.
        if (hung * 2 > samples.Count)
        {
            return new KpiResult(
                ValueP50: null, ValueP95: null, ValueP99: null, ValueStddev: null,
                RawIterations: samples, Source: "degraded");
        }

        var values = samples
            .Where(s => !s.Hung && s.Value.HasValue)
            .Select(s => s.Value!.Value)
            .ToArray();
        Array.Sort(values);

        return new KpiResult(
            ValueP50: Percentile(values, 0.50),
            ValueP95: Percentile(values, 0.95),
            ValueP99: Percentile(values, 0.99),
            ValueStddev: Stddev(values),
            RawIterations: samples,
            Source: sourceLabel);
    }

    public static IReadOnlyList<IterationSample> TrimFirstAndLast(IReadOnlyList<IterationSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        // Always materialize a fresh array. The Count<=2 short-circuit used to
        // return the caller's list unchanged; the asymmetry made callers wary
        // of whether the result aliased its input. Consistent fresh copy
        // removes the doubt at a cost of a few allocations on small inputs.
        if (samples.Count <= 2) return samples.ToArray();
        return samples.Skip(1).Take(samples.Count - 2).ToArray();
    }

    // Linear-interpolation percentile (same as numpy default). Samples must be sorted.
    public static double Percentile(IReadOnlyList<double> sortedSamples, double p)
    {
        ArgumentNullException.ThrowIfNull(sortedSamples);
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
