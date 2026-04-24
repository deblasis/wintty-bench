using WinttyBench.Cells;
using WinttyBench.Kpis;

namespace WinttyBench.Runners;

public static class KpiRunnerFactory
{
    public static IKpiRunner For(Cell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        return cell.Kpi switch
        {
            "throughput_bytes_per_sec" => new ThroughputRunner(),
            "startup_seconds" => new StartupRunner(),
            "rss_peak_bytes" => new MemoryRssRunner(),
            _ => throw new NotSupportedException($"Unknown KPI '{cell.Kpi}' on cell '{cell.Id}'"),
        };
    }
}
