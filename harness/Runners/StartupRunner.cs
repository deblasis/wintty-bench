using System.Diagnostics;
using System.Globalization;
using WinttyBench.Cells;
using WinttyBench.Fixtures;
using WinttyBench.Kpis;
using WinttyBench.Launchers;

namespace WinttyBench.Runners;

public sealed class StartupRunner : IKpiRunner
{
    public Task<IReadOnlyList<IterationSample>> RunAsync(
        Cell cell,
        string winttyExe,
        FairnessProfile profile,
        FixtureResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(profile);

        var launcher = new WinttyLauncher();
        var totalIters = profile.WarmupIters + profile.MeasuredIters;
        var samples = new List<IterationSample>(profile.MeasuredIters);

        for (var i = 0; i < totalIters; i++)
        {
            var isWarmup = i < profile.WarmupIters;
            var sentinelPath = Path.Combine(Path.GetTempPath(),
                $"wintty-bench-start-{Guid.NewGuid():N}.marker");
            if (File.Exists(sentinelPath)) File.Delete(sentinelPath);

            var shellCmd = BuildPwshStartupCommand(sentinelPath);

            var launch = launcher.Launch(new LaunchRequest(
                TargetExePath: winttyExe,
                ShellCommand: shellCmd,
                ConfigOverrides: cell.WinttyConfigOverrides,
                Cols: 120,
                Rows: 32));

            var sw = Stopwatch.StartNew();
            var hung = false;
            try
            {
                SentinelWaiter.WaitForSentinel(sentinelPath, SentinelWaiter.DefaultExitTimeout);
            }
            catch (TimeoutException)
            {
                hung = true;
            }
            finally
            {
                sw.Stop();
                launch.Dispose();
                try { if (File.Exists(sentinelPath)) File.Delete(sentinelPath); }
                catch (IOException) { /* best effort */ }
            }

            if (!isWarmup)
            {
                samples.Add(hung
                    ? new IterationSample(Value: null, Hung: true)
                    : new IterationSample(Value: sw.Elapsed.TotalSeconds, Hung: false));
            }
        }

        return Task.FromResult<IReadOnlyList<IterationSample>>(samples);
    }

    private static string BuildPwshStartupCommand(string sentinelPath)
    {
        // No -NoProfile: $PROFILE loads in the normal sequence and its cost is
        // part of the "time to first prompt" number we want to measure.
        // `function global:prompt` overrides the prompt after profile load,
        // so the sentinel fires at first prompt-ready and exits.
        // Single-quote escape: pwsh single-quoted strings escape ' as ''.
        var escapedPath = sentinelPath.Replace("'", "''", StringComparison.Ordinal);
        return string.Create(CultureInfo.InvariantCulture,
            $"pwsh -NoLogo -Command \"$env:WINTTY_BENCH_SENTINEL='{escapedPath}'; function global:prompt {{ New-Item -ItemType File -Force -Path $env:WINTTY_BENCH_SENTINEL | Out-Null; exit }}\"");
    }
}
