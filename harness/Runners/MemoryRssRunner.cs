using System.Globalization;
using WinttyBench.Cells;
using WinttyBench.Fixtures;
using WinttyBench.Kpis;
using WinttyBench.Launchers;

namespace WinttyBench.Runners;

public sealed class MemoryRssRunner : IKpiRunner
{
    private static readonly TimeSpan SamplingWindow = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SamplingCadence = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MinAliveBeforeSampling = TimeSpan.FromSeconds(1);

    public async Task<IReadOnlyList<IterationSample>> RunAsync(
        Cell cell,
        string winttyExe,
        FairnessProfile profile,
        FixtureResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(resolver);

        var handle = await resolver.ResolveAsync(cell, profile);

        var launcher = new WinttyLauncher();
        var totalIters = profile.WarmupIters + profile.MeasuredIters;
        var samples = new List<IterationSample>(profile.MeasuredIters);

        for (var i = 0; i < totalIters; i++)
        {
            var isWarmup = i < profile.WarmupIters;

            var shellCmd = BuildWslCatCommand(handle.ShellPath);
            var launch = launcher.Launch(new LaunchRequest(
                TargetExePath: winttyExe,
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
                    if (launch.Process.HasExited)
                    {
                        if (DateTime.UtcNow - samplingStart < MinAliveBeforeSampling)
                            diedEarly = true;
                        break;
                    }
                    launch.Process.Refresh();  // required before WorkingSet64
                    var rss = launch.Process.WorkingSet64;
                    if (rss > peakRss) peakRss = rss;
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
