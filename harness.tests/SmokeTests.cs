using System.Text.Json;
using WinttyBench;
using Xunit;

namespace WinttyBench.Tests;

public class SmokeTests
{
    [Fact(Skip = "Requires built wintty at WINTTY_TARGET env var; run manually")]
    public void EndToEnd_C1_Emits_Valid_Envelope()
    {
        var target = Environment.GetEnvironmentVariable("WINTTY_TARGET");
        Assert.NotNull(target);
        Assert.True(File.Exists(target), $"WINTTY_TARGET not found: {target}");

        var rc = BenchHost.Run(["--mode=ci", "--cells=C1", $"--target={target}"]);

        Assert.Equal(0, rc);

        var latestResult = Directory.GetFiles("results/ci", "C1.json", SearchOption.AllDirectories)
            .OrderByDescending(File.GetCreationTimeUtc)
            .First();
        var json = File.ReadAllText(latestResult);
        var env = JsonSerializer.Deserialize(json, ResultSchemaContext.Default.ResultEnvelope);

        Assert.NotNull(env);
        Assert.Equal(1, env.SchemaVersion);
        Assert.Equal("C1", env.CellId);
        Assert.Equal("throughput_bytes_per_sec", env.Kpi);
        Assert.True(env.ValueP50 > 0, $"ValueP50 should be positive, got {env.ValueP50}");
        Assert.True(env.RawIterations.Count >= 9, $"CI mode should have >=9 samples after trim, got {env.RawIterations.Count}");
    }
}
