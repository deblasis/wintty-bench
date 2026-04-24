namespace WinttyBench.Kpis;

public sealed class MemoryRssKpi : IKpi
{
    public string Name => "rss_peak_bytes";

    public string UnitHint => "bytes";

    public KpiResult ComputeFromSamples(IReadOnlyList<IterationSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0)
            throw new ArgumentException("Need at least one sample", nameof(samples));

        // "sampled": values come from periodic Process.WorkingSet64 reads
        // over a fixed-duration workload. Distinct from throughput's
        // "hyperfine" and startup's "stopwatch" labels.
        return KpiStats.ComputePercentiles(samples, "sampled");
    }
}
