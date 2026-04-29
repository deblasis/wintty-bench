using WinttyBench;
using Xunit;

namespace WinttyBench.Tests;

public class EnvProbeTests
{
    [Fact]
    public void Capture_DegradesToUnknown_WhenWinttyTargetIsEmpty()
    {
        // BenchHost passes string.Empty to EnvProbe.Capture when no wintty
        // target is configured (e.g. --terminals=wt only). EnvProbe.Capture
        // is documented to silently degrade to "unknown" in that case; this
        // pin keeps the documented contract from drifting -- without the
        // empty-path guard, FileVersionInfo.GetVersionInfo("") throws
        // ArgumentException and the top catch in BenchHost reports it as
        // "Arg error: ...", masking the real source.
        var env = EnvProbe.Capture(string.Empty);

        Assert.Equal("unknown", env.WinttySha);
        Assert.Equal("unknown", env.WinttyVersion);
    }
}
