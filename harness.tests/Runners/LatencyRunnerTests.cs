using System.Runtime.Versioning;
using WinttyBench;
using WinttyBench.Cells;
using WinttyBench.Fixtures;
using WinttyBench.Runners;
using Xunit;

namespace WinttyBench.Tests.Runners;

[SupportedOSPlatform("windows")]
public class LatencyRunnerTests
{
    private static string? WinttyExe =>
        Environment.GetEnvironmentVariable("WINTTY_BENCH_E2E_EXE");

    [Fact]
    [Trait("OS", "Windows")]
    [Trait("Category", "Integration")]
    public async Task HappyPath_Produces_AtLeast_One_NonHung_Sample()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Wintty is Windows-only");
        if (string.IsNullOrEmpty(WinttyExe) || !File.Exists(WinttyExe))
            Assert.Skip("WINTTY_BENCH_E2E_EXE unset or path missing");

        var cell = new Cell(
            Id: "C13",
            Shell: "pwsh-7.4",
            Workload: "latency_keystroke_to_glyph",
            Kpi: "latency_keystroke_to_glyph_ms",
            FixturePath: null,
            FixtureKey: null,
            WinttyConfigOverrides: new Dictionary<string, string>(),
            MeasuredItersOverride: 1);

        var profile = FairnessProfile.Ci();
        var resolver = new FixtureResolver(new WslFixtureCache());

        var samples = await new LatencyRunner().RunAsync(cell, WinttyExe!, profile, resolver);
        Assert.NotEmpty(samples);
        var nonHung = samples.Where(s => !s.Hung).ToArray();
        Assert.NotEmpty(nonHung);
        Assert.InRange(nonHung[0].Value!.Value, 0.5, 100.0);
    }
}
