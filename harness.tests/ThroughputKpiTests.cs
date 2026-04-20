using WinttyBench.Kpis;
using Xunit;

namespace WinttyBench.Tests;

public class ThroughputKpiTests
{
    private static IterationSample Ok(double v) => new(v, false);
    private static IterationSample Hung() => new(null, true);

    [Fact]
    public void BytesPerSecond_Computes_Throughput_From_Samples()
    {
        var samples = new[] { Ok(1.0), Ok(1.0), Ok(1.0), Ok(1.0) };
        var kpi = new ThroughputKpi();

        var result = kpi.ComputeFromSamples(samples, fixtureBytes: 1_048_576);

        Assert.Equal(1_048_576, result.ValueP50!.Value, precision: 0);
        Assert.Equal(1_048_576, result.ValueP95!.Value, precision: 0);
        Assert.Equal(4, result.RawIterations.Count);
        Assert.Equal("hyperfine", result.Source);
    }

    [Fact]
    public void Hung_Samples_Excluded_From_Percentile()
    {
        var samples = new IterationSample[]
        {
            Ok(1.0), Ok(1.0), Ok(1.0), Ok(1.0), Ok(1.0), Ok(1.0), Ok(1.0),
            Hung(), Hung(), Hung(),
        };
        var kpi = new ThroughputKpi();

        var result = kpi.ComputeFromSamples(samples, fixtureBytes: 1_048_576);

        Assert.Equal(1_048_576, result.ValueP50!.Value, precision: 0);
        Assert.Equal("hyperfine", result.Source);
        Assert.Equal(10, result.RawIterations.Count);
    }

    [Fact]
    public void Majority_Hung_Marks_Degraded_With_Null_Values()
    {
        var samples = new IterationSample[]
        {
            Ok(1.0), Ok(1.0), Ok(1.0), Ok(1.0),
            Hung(), Hung(), Hung(), Hung(), Hung(), Hung(),
        };
        var kpi = new ThroughputKpi();

        var result = kpi.ComputeFromSamples(samples, fixtureBytes: 1_048_576);

        Assert.Null(result.ValueP50);
        Assert.Null(result.ValueP95);
        Assert.Null(result.ValueP99);
        Assert.Null(result.ValueStddev);
        Assert.Equal("degraded", result.Source);
    }

    [Fact]
    public void BytesPerSecond_Computes_Stddev()
    {
        var samples = new[] { Ok(1.0), Ok(2.0), Ok(3.0), Ok(4.0), Ok(5.0) };
        var kpi = new ThroughputKpi();

        var result = kpi.ComputeFromSamples(samples, fixtureBytes: 100);

        Assert.NotNull(result.ValueStddev);
        Assert.True(result.ValueStddev > 0);
    }

    [Fact]
    public void Trim_Discards_First_Last()
    {
        var samples = new IterationSample[]
        {
            Ok(10.0), Ok(1.0), Ok(1.0), Ok(1.0), Ok(10.0),
        };
        var trimmed = ThroughputKpi.TrimFirstAndLast(samples);

        Assert.Equal(3, trimmed.Count);
        Assert.All(trimmed, s => Assert.Equal(1.0, s.Value!.Value));
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
