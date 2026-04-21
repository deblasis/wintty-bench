namespace WinttyBench.Kpis;

public static class KpiFactory
{
    public static IKpi For(string kpiName)
    {
        return kpiName switch
        {
            "throughput_bytes_per_sec" => new ThroughputKpi(),
            // StartupKpi + MemoryRssKpi added in Phases B and C.
            _ => throw new NotSupportedException($"Unknown KPI '{kpiName}'"),
        };
    }
}
