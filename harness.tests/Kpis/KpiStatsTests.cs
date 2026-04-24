using WinttyBench.Kpis;

namespace WinttyBench.Tests.Kpis;

public class KpiStatsTests
{
    private static IterationSample Ok(double v) => new(v, false);
    private static IterationSample Hung() => new(null, true);

    [Fact]
    public void ComputePercentiles_Flat_Samples_Returns_Uniform_Percentiles()
    {
        var samples = new[] { Ok(10), Ok(10), Ok(10), Ok(10) };
        var r = KpiStats.ComputePercentiles(samples, "test");
        Assert.Equal(10, r.ValueP50!.Value, 0);
        Assert.Equal(10, r.ValueP95!.Value, 0);
        Assert.Equal(10, r.ValueP99!.Value, 0);
        Assert.Equal("test", r.Source);
        Assert.Equal(4, r.RawIterations.Count);
    }

    [Fact]
    public void ComputePercentiles_Majority_Hung_Returns_Degraded()
    {
        var samples = new[] { Ok(1), Ok(1), Ok(1), Ok(1), Hung(), Hung(), Hung(), Hung(), Hung(), Hung() };
        var r = KpiStats.ComputePercentiles(samples, "test");
        Assert.Null(r.ValueP50);
        Assert.Null(r.ValueP95);
        Assert.Null(r.ValueP99);
        Assert.Null(r.ValueStddev);
        Assert.Equal("degraded", r.Source);
    }

    [Fact]
    public void ComputePercentiles_Exactly_Half_Hung_Is_Not_Degraded()
    {
        var samples = new[] { Ok(1), Ok(1), Ok(1), Ok(1), Hung(), Hung(), Hung(), Hung() };
        var r = KpiStats.ComputePercentiles(samples, "test");
        Assert.NotNull(r.ValueP50);
        Assert.Equal("test", r.Source);
    }

    [Fact]
    public void ComputePercentiles_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => KpiStats.ComputePercentiles(Array.Empty<IterationSample>(), "test"));
    }

    [Fact]
    public void ComputePercentiles_Single_Sample_Returns_That_Value()
    {
        var r = KpiStats.ComputePercentiles(new[] { Ok(42) }, "test");
        Assert.Equal(42, r.ValueP50!.Value, 0);
        Assert.Equal(42, r.ValueP99!.Value, 0);
    }

    [Fact]
    public void TrimFirstAndLast_Returns_Middle()
    {
        var samples = new[] { Ok(100), Ok(1), Ok(1), Ok(1), Ok(100) };
        var trimmed = KpiStats.TrimFirstAndLast(samples);
        Assert.Equal(3, trimmed.Count);
        Assert.All(trimmed, s => Assert.Equal(1, s.Value!.Value));
    }

    [Fact]
    public void Percentile_P50_Of_Odd_Count_Returns_Middle()
    {
        var p = KpiStats.Percentile(new double[] { 1, 2, 3, 4, 5 }, 0.50);
        Assert.Equal(3.0, p);
    }

    [Fact]
    public void Percentile_P95_Returns_Near_Max()
    {
        var p = KpiStats.Percentile(Enumerable.Range(1, 100).Select(i => (double)i).ToArray(), 0.95);
        Assert.InRange(p, 94.0, 96.0);
    }
}
