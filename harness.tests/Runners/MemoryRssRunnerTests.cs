using WinttyBench;
using WinttyBench.Cells;
using WinttyBench.Fixtures;
using WinttyBench.Runners;
using Xunit;

namespace WinttyBench.Tests.Runners;

[Trait("Category", "Integration")]
public class MemoryRssRunnerTests
{
    [Fact(Skip = "Integration: requires real Wintty.exe + WSL. Unskip locally and set WINTTY_EXE.")]
    public async Task MemoryRssRunner_EmitsPositiveRss_ForC9Shape()
    {
        var exe = Environment.GetEnvironmentVariable("WINTTY_EXE")
            ?? throw new InvalidOperationException("Set WINTTY_EXE to the Wintty.exe path");

        var cell = new Cell(
            Id: "C9",
            Shell: "wsl-ubuntu-24.04",
            Workload: "rss_under_ingest_10s",
            Kpi: "rss_peak_bytes",
            FixturePath: null,
            FixtureKey: "c11",
            WinttyConfigOverrides: new Dictionary<string, string>());

        var profile = FairnessProfile.Ci() with { WarmupIters = 0, MeasuredIters = 1 };
        var resolver = new FixtureResolver(new WslFixtureCache());
        var runner = new MemoryRssRunner();

        var samples = await runner.RunAsync(cell, exe, profile, resolver);

        Assert.Single(samples);
        Assert.False(samples[0].Hung);
        Assert.NotNull(samples[0].Value);
        Assert.InRange(samples[0].Value!.Value, 10L * 1024 * 1024, 2L * 1024 * 1024 * 1024);
    }
}
