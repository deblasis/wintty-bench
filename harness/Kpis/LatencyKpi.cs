namespace WinttyBench.Kpis;

public sealed class LatencyKpi : IKpi
{
    public string Name => "latency_keystroke_to_glyph_ms";

    public string UnitHint => "ms";

    public KpiResult ComputeFromSamples(IReadOnlyList<IterationSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0)
            throw new ArgumentException("Need at least one sample", nameof(samples));

        // "wgc": values come from WGC frame timestamps (QPC). Distinct label
        // from "stopwatch" (StartupKpi) and "hyperfine" (ThroughputKpi) so
        // chart builders can distinguish measurement mechanisms.
        return KpiStats.ComputePercentiles(samples, "wgc");
    }
}
