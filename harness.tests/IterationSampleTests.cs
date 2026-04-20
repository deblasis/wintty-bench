using System.Text.Json;
using WinttyBench.Kpis;
using Xunit;

namespace WinttyBench.Tests;

public class IterationSampleTests
{
    [Fact]
    public void Hung_Sample_Serializes_With_Null_Value()
    {
        var sample = new IterationSample(Value: null, Hung: true);
        var json = JsonSerializer.Serialize(sample);
        Assert.Contains("\"value\":null", json, StringComparison.Ordinal);
        Assert.Contains("\"hung\":true", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Healthy_Sample_Serializes_With_Value()
    {
        var sample = new IterationSample(Value: 1.5, Hung: false);
        var json = JsonSerializer.Serialize(sample);
        Assert.Contains("\"value\":1.5", json, StringComparison.Ordinal);
        Assert.Contains("\"hung\":false", json, StringComparison.Ordinal);
    }
}
