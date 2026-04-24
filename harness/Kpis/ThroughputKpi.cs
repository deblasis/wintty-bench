namespace WinttyBench.Kpis;

public sealed class ThroughputKpi : IKpi
{
    public string Name => "throughput_bytes_per_sec";

    public string UnitHint => "B/s";

    public KpiResult ComputeFromSamples(IReadOnlyList<IterationSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0)
            throw new ArgumentException("Need at least one sample", nameof(samples));

        return KpiStats.ComputePercentiles(samples, "hyperfine");
    }
}
