using WinttyBench;
using WinttyBench.Cells;
using WinttyBench.Fixtures;
using WinttyBench.Runners;
using Xunit;

namespace WinttyBench.Tests.Runners;

[Trait("Category", "Integration")]
public class StartupRunnerTests
{
    [Fact(Skip = "Integration: requires real Wintty.exe. Unskip locally and set WINTTY_EXE.")]
    public async Task StartupRunner_EmitsPositiveSeconds_ForPwsh()
    {
        var exe = Environment.GetEnvironmentVariable("WINTTY_EXE")
            ?? throw new InvalidOperationException("Set WINTTY_EXE to the Wintty.exe path");

        var cell = new Cell(
            Id: "C8",
            Shell: "pwsh-7.4",
            Workload: "shell_startup",
            Kpi: "startup_seconds",
            FixturePath: null,
            FixtureKey: null,
            WinttyConfigOverrides: new Dictionary<string, string>());

        var profile = FairnessProfile.Ci() with { WarmupIters = 0, MeasuredIters = 1 };
        var resolver = new FixtureResolver(new WslFixtureCache());
        var runner = new StartupRunner();

        var samples = await runner.RunAsync(cell, exe, profile, resolver);

        Assert.Single(samples);
        Assert.False(samples[0].Hung);
        Assert.NotNull(samples[0].Value);
        Assert.InRange(samples[0].Value!.Value, 0.1, 30.0);
    }
}
