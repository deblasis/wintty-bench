using WinttyBench.Kpis;
using Xunit;

namespace WinttyBench.Tests;

public class ThroughputKpiTests
{
    private static IterationSample Ok(double bytesPerSec) => new(bytesPerSec, false);
    private static IterationSample Hung() => new(null, true);

    [Fact]
    public void BytesPerSecond_Computes_Throughput_From_Samples()
    {
        // Test inputs are already in bytes/sec (conversion happens upstream).
        var samples = new[] { Ok(1_048_576), Ok(1_048_576), Ok(1_048_576), Ok(1_048_576) };
        var kpi = new ThroughputKpi();

        var result = kpi.ComputeFromSamples(samples);

        Assert.Equal(1_048_576, result.ValueP50!.Value, precision: 0);
        Assert.Equal("hyperfine", result.Source);
        Assert.Equal(4, result.RawIterations.Count);
    }

    [Fact]
    public void Hung_Samples_Excluded_From_Percentile()
    {
        var samples = new[]
        {
            Ok(1_048_576), Ok(1_048_576), Ok(1_048_576), Ok(1_048_576),
            Ok(1_048_576), Ok(1_048_576), Ok(1_048_576),
            Hung(), Hung(), Hung(),
        };
        var kpi = new ThroughputKpi();

        var result = kpi.ComputeFromSamples(samples);

        Assert.Equal(1_048_576, result.ValueP50!.Value, precision: 0);
        Assert.Equal("hyperfine", result.Source);
        Assert.Equal(10, result.RawIterations.Count);
    }

    [Fact]
    public void Majority_Hung_Marks_Degraded()
    {
        var samples = new[]
        {
            Ok(1_048_576), Ok(1_048_576), Ok(1_048_576), Ok(1_048_576),
            Hung(), Hung(), Hung(), Hung(), Hung(), Hung(),
        };
        var kpi = new ThroughputKpi();

        var result = kpi.ComputeFromSamples(samples);

        Assert.Null(result.ValueP50);
        Assert.Equal("degraded", result.Source);
    }

    [Fact]
    public void Name_Is_Throughput_Bytes_Per_Sec()
    {
        Assert.Equal("throughput_bytes_per_sec", new ThroughputKpi().Name);
    }
}
