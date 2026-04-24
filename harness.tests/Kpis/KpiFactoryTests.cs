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
}
