using WinttyBench.Kpis;
using Xunit;

namespace WinttyBench.Tests;

public class ThroughputKpiTests
{
    [Fact]
    public void BytesPerSecond_Computes_Throughput_From_Samples()
    {
        // 1 MB fixture, 4 iterations each took 1 second -> 1 MB/s
        var samples = new[] { 1.0, 1.0, 1.0, 1.0 };
        var kpi = new ThroughputKpi();

        var result = kpi.ComputeFromSamples(samples, fixtureBytes: 1_048_576);

        Assert.Equal(1_048_576, result.ValueP50, precision: 0);
        Assert.Equal(1_048_576, result.ValueP95, precision: 0);
        Assert.Equal(4, result.RawIterations.Count);
    }

    [Fact]
    public void BytesPerSecond_Computes_Stddev()
    {
        var samples = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
        var kpi = new ThroughputKpi();

        var result = kpi.ComputeFromSamples(samples, fixtureBytes: 100);

        Assert.True(result.ValueStddev > 0);
        Assert.Equal(5, result.RawIterations.Count);
    }

    [Fact]
    public void BytesPerSecond_Trim_Discards_First_Last()
    {
        // samples ordered by iteration index: [first=slow, ..., last=slow]
        var samples = new[] { 10.0, 1.0, 1.0, 1.0, 10.0 };
        var kpi = new ThroughputKpi();

        var trimmed = ThroughputKpi.TrimFirstAndLast(samples);

        Assert.Equal(3, trimmed.Count);
        Assert.All(trimmed, s => Assert.Equal(1.0, s));
    }

    [Fact]
    public void Percentile_P50_Of_Odd_Count_Returns_Middle()
    {
        var samples = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
        var p50 = ThroughputKpi.Percentile(samples, 0.50);
        Assert.Equal(3.0, p50);
    }

    [Fact]
    public void Percentile_P95_Returns_Near_Max()
    {
        var samples = Enumerable.Range(1, 100).Select(i => (double)i).ToArray();
        var p95 = ThroughputKpi.Percentile(samples, 0.95);
        Assert.InRange(p95, 94.0, 96.0);
    }
}
