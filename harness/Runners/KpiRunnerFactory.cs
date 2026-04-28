using WinttyBench.Cells;
using WinttyBench.Kpis;

namespace WinttyBench.Runners;

public static class KpiRunnerFactory
{
    // Runners are stateless; cache singletons so BenchHost's per-cell loop
    // does not allocate a fresh instance per cell.
    private static readonly ThroughputRunner s_throughput = new();
    private static readonly StartupRunner s_startup = new();
    private static readonly MemoryRssRunner s_memoryRss = new();
#pragma warning disable CA1416 // LatencyRunner is Windows-only; this whole harness is Windows-only.
    private static readonly LatencyRunner s_latency = new();
#pragma warning restore CA1416

    public static IKpiRunner For(Cell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        return cell.Kpi switch
        {
            "throughput_bytes_per_sec" => s_throughput,
            "startup_seconds" => s_startup,
            "rss_peak_bytes" => s_memoryRss,
            "latency_keystroke_to_glyph_ms" => s_latency,
            _ => throw new NotSupportedException($"Unknown KPI '{cell.Kpi}' on cell '{cell.Id}'"),
        };
    }
}
