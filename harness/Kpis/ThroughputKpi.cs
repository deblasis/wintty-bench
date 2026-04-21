namespace WinttyBench.Kpis;

public sealed class ThroughputKpi : IKpi
{
    public string Name => "throughput_bytes_per_sec";

    public KpiResult ComputeFromSamples(IReadOnlyList<IterationSample> samples, long fixtureBytes)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0)
            throw new ArgumentException("Need at least one sample", nameof(samples));

        // Plan 2B refactor: convert wall-seconds -> bytes/sec at sample level,
        // delegate percentile + degraded logic to KpiStats. Bytes/sec conversion
        // will move into ThroughputRunner in a later task; for now it stays
        // here so this task is behavior-preserving.
        var rateSamples = samples.Select(s =>
            s.Hung
                ? new IterationSample(null, true)
                : new IterationSample(fixtureBytes / s.Value!.Value, false))
            .ToArray();

        return KpiStats.ComputePercentiles(rateSamples, "hyperfine");
    }

    public static IReadOnlyList<IterationSample> TrimFirstAndLast(IReadOnlyList<IterationSample> samples)
        => KpiStats.TrimFirstAndLast(samples);

    // Linear-interpolation percentile (same as numpy default). Samples must be sorted.
    public static double Percentile(IReadOnlyList<double> sortedSamples, double p)
        => KpiStats.Percentile(sortedSamples, p);
}
