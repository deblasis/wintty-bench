using WinttyBench.Kpis;
using Xunit;

namespace WinttyBench.Tests.Kpis;

public class StartupKpiTests
{
    private static IterationSample Ok(double seconds) => new(seconds, false);
    private static IterationSample Hung() => new(null, true);

    [Fact]
    public void Name_Is_Startup_Seconds()
    {
        Assert.Equal("startup_seconds", new StartupKpi().Name);
    }

    [Fact]
    public void Computes_P50_From_Second_Samples()
    {
        var samples = new[] { Ok(1.5), Ok(1.5), Ok(1.5), Ok(1.5) };
        var r = new StartupKpi().ComputeFromSamples(samples);
        Assert.Equal(1.5, r.ValueP50!.Value, precision: 3);
        Assert.Equal("stopwatch", r.Source);
        Assert.Equal(4, r.RawIterations.Count);
    }

    [Fact]
    public void Majority_Hung_Marks_Degraded()
    {
        var samples = new[]
        {
            Ok(1.5), Ok(1.5), Ok(1.5), Ok(1.5),
            Hung(), Hung(), Hung(), Hung(), Hung(), Hung(),
        };
        var r = new StartupKpi().ComputeFromSamples(samples);
        Assert.Null(r.ValueP50);
        Assert.Equal("degraded", r.Source);
    }
}
