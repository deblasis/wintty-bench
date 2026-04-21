namespace WinttyBench.Kpis;

public sealed class StartupKpi : IKpi
{
    public string Name => "startup_seconds";

    public string UnitHint => "s";

    public KpiResult ComputeFromSamples(IReadOnlyList<IterationSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0)
            throw new ArgumentException("Need at least one sample", nameof(samples));

        // "stopwatch": values come from Stopwatch.Elapsed over launch -> prompt-ready
        // sentinel. Different label from ThroughputKpi's "hyperfine" so chart
        // builders can distinguish the measurement mechanism.
        return KpiStats.ComputePercentiles(samples, "stopwatch");
    }
}
