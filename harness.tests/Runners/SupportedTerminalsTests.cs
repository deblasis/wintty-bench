using WinttyBench.Runners;
using Xunit;

namespace WinttyBench.Tests.Runners;

public class SupportedTerminalsTests
{
    [Fact]
    public void Throughput_SupportsBoth()
    {
        var r = new ThroughputRunner();
        Assert.Contains("wintty", r.SupportedTerminals);
        Assert.Contains("wt", r.SupportedTerminals);
    }

    [Fact]
    public void Startup_WinttyOnly()
    {
        var r = new StartupRunner();
        Assert.Contains("wintty", r.SupportedTerminals);
        Assert.DoesNotContain("wt", r.SupportedTerminals);
    }

    [Fact]
    public void MemoryRss_WinttyOnly()
    {
        var r = new MemoryRssRunner();
        Assert.Contains("wintty", r.SupportedTerminals);
        Assert.DoesNotContain("wt", r.SupportedTerminals);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [Fact]
    public void Latency_SupportsBoth()
    {
        var r = new LatencyRunner();
        Assert.Contains("wintty", r.SupportedTerminals);
        Assert.Contains("wt", r.SupportedTerminals);
    }
}
