namespace WinttyBench.Kpis;

public static class KpiFactory
{
    public static IKpi For(string kpiName)
    {
        ArgumentNullException.ThrowIfNull(kpiName);
        return kpiName switch
        {
            "throughput_bytes_per_sec" => new ThroughputKpi(),
            "startup_seconds" => new StartupKpi(),
            "rss_peak_bytes" => new MemoryRssKpi(),
            "latency_keystroke_to_glyph_ms" => new LatencyKpi(),
            _ => throw new NotSupportedException($"Unknown KPI '{kpiName}'"),
        };
    }
}
