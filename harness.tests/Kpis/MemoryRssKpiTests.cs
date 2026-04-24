using WinttyBench.Kpis;
using Xunit;

namespace WinttyBench.Tests.Kpis;

public class MemoryRssKpiTests
{
    private static IterationSample Ok(double bytes) => new(bytes, false);
    private static IterationSample Hung() => new(null, true);

    [Fact]
    public void Name_Is_Rss_Peak_Bytes()
    {
        Assert.Equal("rss_peak_bytes", new MemoryRssKpi().Name);
    }

    [Fact]
    public void Computes_P50_From_Byte_Samples()
    {
        var samples = new[]
        {
            Ok(50_000_000), Ok(50_000_000), Ok(50_000_000), Ok(50_000_000),
        };
        var r = new MemoryRssKpi().ComputeFromSamples(samples);
        Assert.Equal(50_000_000, r.ValueP50!.Value, precision: 0);
        Assert.Equal("sampled", r.Source);
    }

    [Fact]
    public void Majority_Hung_Marks_Degraded()
    {
        var samples = new[]
        {
            Ok(50_000_000), Ok(50_000_000), Ok(50_000_000), Ok(50_000_000),
            Hung(), Hung(), Hung(), Hung(), Hung(), Hung(),
        };
        var r = new MemoryRssKpi().ComputeFromSamples(samples);
        Assert.Null(r.ValueP50);
        Assert.Equal("degraded", r.Source);
    }
}
