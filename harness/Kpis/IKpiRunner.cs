using WinttyBench.Cells;
using WinttyBench.Fixtures;

namespace WinttyBench.Kpis;

// Per-KPI measurement loop. Each implementation owns how it drives Wintty
// and extracts one IterationSample per measured iteration. Samples are
// emitted in the KPI's final unit (bytes/sec for throughput, seconds for
// startup, peak bytes for memory-rss).
public interface IKpiRunner
{
    Task<IReadOnlyList<IterationSample>> RunAsync(
        Cell cell,
        string winttyExe,
        FairnessProfile profile,
        FixtureResolver resolver);
}
