using WinttyBench.Kpis;
using Xunit;

namespace WinttyBench.Tests.Kpis;

public class KpiFactoryTests
{
    [Fact]
    public void For_Throughput_Returns_ThroughputKpi()
    {
        var kpi = KpiFactory.For("throughput_bytes_per_sec");
        Assert.IsType<ThroughputKpi>(kpi);
    }

    [Fact]
    public void For_Unknown_Throws()
    {
        Assert.Throws<NotSupportedException>(() => KpiFactory.For("bogus"));
    }

    [Fact]
    public void For_Startup_Returns_StartupKpi()
    {
        var kpi = KpiFactory.For("startup_seconds");
        Assert.IsType<StartupKpi>(kpi);
    }

    [Fact]
    public void For_Rss_Returns_MemoryRssKpi()
    {
        var kpi = KpiFactory.For("rss_peak_bytes");
        Assert.IsType<MemoryRssKpi>(kpi);
    }
}
