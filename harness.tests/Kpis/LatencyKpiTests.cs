using WinttyBench.Kpis;
using Xunit;

namespace WinttyBench.Tests.Kpis;

public class LatencyKpiTests
{
    private static IterationSample Ok(double ms) => new(ms, false);
    private static IterationSample Hung() => new(null, true);

    [Fact]
    public void Name_Is_Latency_Keystroke_To_Glyph_Ms()
    {
        Assert.Equal("latency_keystroke_to_glyph_ms", new LatencyKpi().Name);
    }

    [Fact]
    public void UnitHint_Is_Ms()
    {
        Assert.Equal("ms", new LatencyKpi().UnitHint);
    }

    [Fact]
    public void Computes_P50_From_Ms_Samples()
    {
        var samples = new[] { Ok(10), Ok(12), Ok(14), Ok(16), Ok(18) };
        var r = new LatencyKpi().ComputeFromSamples(samples);
        Assert.Equal(14, r.ValueP50!.Value, precision: 3);
        Assert.Equal("wgc", r.Source);
        Assert.Equal(5, r.RawIterations.Count);
    }

    [Fact]
    public void Majority_Hung_Marks_Degraded()
    {
        var samples = new[]
        {
            Ok(10), Ok(10), Ok(10), Ok(10),
            Hung(), Hung(), Hung(), Hung(), Hung(), Hung(),
        };
        var r = new LatencyKpi().ComputeFromSamples(samples);
        Assert.Null(r.ValueP50);
        Assert.Equal("degraded", r.Source);
    }

    [Fact]
    public void Empty_Samples_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new LatencyKpi().ComputeFromSamples(Array.Empty<IterationSample>()));
    }

    [Fact]
    public void Null_Samples_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new LatencyKpi().ComputeFromSamples(null!));
    }
}
