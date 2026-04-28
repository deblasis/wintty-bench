using System.Collections.Generic;
using WinttyBench.Cells;
using WinttyBench.Fixtures;

namespace WinttyBench.Kpis;

// Per-KPI measurement loop. Each implementation owns how it drives the
// terminal and extracts one IterationSample per measured iteration.
// Samples are emitted in the KPI's final unit (bytes/sec for throughput,
// seconds for startup, peak bytes for memory-rss, ms for latency).
public interface IKpiRunner
{
    // Terminals this runner can drive. Throughput + Latency support
    // any launcher; Startup + MemoryRss are wintty-only because of
    // process-model differences (wt.exe -> WindowsTerminal.exe handoff
    // for startup; multi-process renderer aggregation for RSS).
    IReadOnlyList<string> SupportedTerminals { get; }

    Task<IReadOnlyList<IterationSample>> RunAsync(
        Cell cell,
        string terminalName,
        string targetExePath,
        FairnessProfile profile,
        FixtureResolver resolver);
}
