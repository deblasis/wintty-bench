using System.Globalization;
using WinttyBench.Cells;
using WinttyBench.Fixtures;
using WinttyBench.Kpis;
using WinttyBench.Launchers;

namespace WinttyBench.Runners;

// Samples peak RSS (Process.WorkingSet64) of the Wintty process while a
// WSL `cat <fixture>` workload drives ingest. Each iteration launches
// Wintty, polls WorkingSet64 every SamplingCadence (500ms) for up to
// SamplingWindow (10s), records the high-water mark, then disposes the
// launch (JobObject + tree-kill via LaunchHandle.Dispose). No sentinel:
// RSS is a sampled KPI, not an event KPI, so a "done" signal would
// measure time-to-last-byte (throughput's job) not steady-state memory.
//
// Why these constants:
// - 10s window is long enough to reach steady-state RSS under the 1 MB
//   cat fixture Plan 2B ships; longer adds noise without new signal.
// - 500ms cadence is tight enough to catch the peak, loose enough not
//   to perturb the measurement itself.
// - MinAliveBeforeSampling = 1s catches "Wintty crashed out of init";
//   anything under that window gets marked Hung: true.
//
// Bounds: no per-run wall-clock ceiling. Worst case is
// (WarmupIters + MeasuredIters) * SamplingWindow. CI callers size
// MeasuredIters so worst-case fits the cell budget.
public sealed class MemoryRssRunner : IKpiRunner
{
    private static readonly TimeSpan SamplingWindow = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SamplingCadence = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MinAliveBeforeSampling = TimeSpan.FromSeconds(1);

    public IReadOnlyList<string> SupportedTerminals { get; } = ["wintty"];

    public async Task<IReadOnlyList<IterationSample>> RunAsync(
        Cell cell,
        string terminalName,
        string targetExePath,
        FairnessProfile profile,
        FixtureResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentException.ThrowIfNullOrEmpty(terminalName);
        ArgumentException.ThrowIfNullOrEmpty(targetExePath);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(resolver);

        var handle = await resolver.ResolveAsync(cell, profile);

        var launcher = LauncherFactory.For(terminalName);
        var totalIters = profile.WarmupIters + profile.MeasuredIters;
        var samples = new List<IterationSample>(profile.MeasuredIters);

        for (var i = 0; i < totalIters; i++)
        {
            var isWarmup = i < profile.WarmupIters;

            var shellCmd = BuildWslCatCommand(handle.ShellPath);
            var launch = launcher.Launch(new LaunchRequest(
                TargetExePath: targetExePath,
                ShellCommand: shellCmd,
                ConfigOverrides: cell.WinttyConfigOverrides,
                Cols: 120,
                Rows: 32));

            long peakRss = 0;
            var samplingStart = DateTime.UtcNow;
            var deadline = samplingStart + SamplingWindow;
            var diedEarly = false;

            try
            {
                while (DateTime.UtcNow < deadline)
                {
                    // HasExited after a successful Process.Start is documented not
                    // to throw; the inner catch narrowly covers Refresh()/WorkingSet64
                    // TOCTOU only.
                    if (launch.Process.HasExited)
                    {
                        if (DateTime.UtcNow - samplingStart < MinAliveBeforeSampling)
                            diedEarly = true;
                        break;
                    }
                    try
                    {
                        launch.Process.Refresh();  // required before WorkingSet64
                        var rss = launch.Process.WorkingSet64;
                        if (rss > peakRss) peakRss = rss;
                    }
                    catch (InvalidOperationException)
                    {
                        // TOCTOU: process exited between HasExited check and WorkingSet64
                        // read. Treat as clean exit, not as an iteration-killing error.
                        if (DateTime.UtcNow - samplingStart < MinAliveBeforeSampling)
                            diedEarly = true;
                        break;
                    }
                    Thread.Sleep(SamplingCadence);
                }
            }
            finally
            {
                launch.Dispose();
            }

            if (!isWarmup)
            {
                var hung = diedEarly || peakRss == 0;
                samples.Add(hung
                    ? new IterationSample(Value: null, Hung: true)
                    : new IterationSample(Value: (double)peakRss, Hung: false));
            }
        }

        return samples;
    }

    private static string BuildWslCatCommand(string fixtureWslPath)
    {
        // No sentinel: we cap by time (SamplingWindow) then kill via launch.Dispose().
        // WSL bash exits naturally when stdin closes on parent-death.
        return string.Create(CultureInfo.InvariantCulture,
            $"wsl -d Ubuntu-24.04 bash -c \"cat '{fixtureWslPath}'\"");
    }
}
