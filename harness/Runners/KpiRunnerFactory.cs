using WinttyBench.Cells;
using WinttyBench.Kpis;

namespace WinttyBench.Runners;

public static class KpiRunnerFactory
{
    // Runners are stateless; cache singletons so BenchHost's per-cell loop
    // does not allocate a fresh instance per cell.
    private static readonly ThroughputRunner s_throughput = new();
    private static readonly StartupRunner s_startup = new();

    public static IKpiRunner For(Cell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        return cell.Kpi switch
        {
            "throughput_bytes_per_sec" => s_throughput,
            "startup_seconds" => s_startup,
            // MemoryRssRunner added in Phase C.
            _ => throw new NotSupportedException($"Unknown KPI '{cell.Kpi}' on cell '{cell.Id}'"),
        };
    }
}
