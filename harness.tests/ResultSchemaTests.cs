using System.Text.Json;
using WinttyBench;
using Xunit;

namespace WinttyBench.Tests;

public class ResultSchemaTests
{
    [Fact]
    public void Envelope_Serializes_With_SchemaVersion_1()
    {
        var envelope = new ResultEnvelope(
            SchemaVersion: 1,
            RunId: "2026-04-20T00:00:00Z-abc1234",
            Mode: "ci",
            ReleaseTag: null,
            Env: new EnvCapture(
                WinttySha: "abc1234",
                WinttyVersion: "0.3.0-dev",
                WtVersion: "1.21.3231.0",
                WezTermVersion: null,
                WindowsBuild: "10.0.26200.1000",
                Cpu: "AMD Ryzen 9 7950X",
                Gpu: "NVIDIA RTX 4090",
                RamGb: 64,
                Display: new DisplayCapture(1920, 1080, 60, 1.0)),
            Fairness: new FairnessCapture(
                PowerPlan: "default",
                DefenderExcluded: false,
                ProcessPriority: "Normal",
                VmReverted: false,
                WarmupIters: 1,
                MeasuredIters: 10,
                Discarded: ["first"]),
            CellId: "C1",
            Shell: "pwsh-7.4",
            Workload: "vtebench_dense_cells",
            Kpi: "throughput_bytes_per_sec",
            ValueP50: 184320000,
            ValueP95: 179200000,
            ValueP99: 175000000,
            ValueStddev: 4200000,
            RawIterations: [1, 2, 3],
            Source: "hyperfine",
            Notes: "");

        var json = JsonSerializer.Serialize(envelope, ResultSchemaContext.Default.ResultEnvelope);

        Assert.Contains("\"schema_version\": 1", json);
        Assert.Contains("\"cell_id\": \"C1\"", json);
        Assert.Contains("\"release_tag\": null", json);
    }

    [Fact]
    public void Envelope_Roundtrips()
    {
        var original = new ResultEnvelope(
            1, "run1", "ci", null,
            new EnvCapture("sha", "ver", "wt", null, "win", "cpu", "gpu", 32, new DisplayCapture(1920, 1080, 60, 1.0)),
            new FairnessCapture("default", false, "Normal", false, 1, 10, []),
            "C1", "pwsh-7.4", "vtebench_dense_cells", "throughput_bytes_per_sec",
            100, 95, 90, 5, [100, 100, 100], "hyperfine", "");

        var json = JsonSerializer.Serialize(original, ResultSchemaContext.Default.ResultEnvelope);
        var parsed = JsonSerializer.Deserialize(json, ResultSchemaContext.Default.ResultEnvelope);

        Assert.NotNull(parsed);
        Assert.Equal(original.CellId, parsed.CellId);
        Assert.Equal(original.ValueP50, parsed.ValueP50);
    }
}
