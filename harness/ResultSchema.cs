using System.Text.Json.Serialization;
using WinttyBench.Kpis;

namespace WinttyBench;

public sealed record ResultEnvelope(
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("release_tag")] string? ReleaseTag,
    [property: JsonPropertyName("env")] EnvCapture Env,
    [property: JsonPropertyName("fairness")] FairnessCapture Fairness,
    [property: JsonPropertyName("terminal")] string Terminal,
    [property: JsonPropertyName("cell_id")] string CellId,
    [property: JsonPropertyName("shell")] string Shell,
    [property: JsonPropertyName("workload")] string Workload,
    [property: JsonPropertyName("kpi")] string Kpi,
    [property: JsonPropertyName("value_p50")] double? ValueP50,
    [property: JsonPropertyName("value_p95")] double? ValueP95,
    [property: JsonPropertyName("value_p99")] double? ValueP99,
    [property: JsonPropertyName("value_stddev")] double? ValueStddev,
    [property: JsonPropertyName("raw_iterations")] IReadOnlyList<IterationSample> RawIterations,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("notes")] string Notes);

public sealed record EnvCapture(
    [property: JsonPropertyName("wintty_sha")] string WinttySha,
    [property: JsonPropertyName("wintty_version")] string WinttyVersion,
    [property: JsonPropertyName("wt_version")] string WtVersion,
    [property: JsonPropertyName("wezterm_version")] string? WezTermVersion,
    [property: JsonPropertyName("windows_build")] string WindowsBuild,
    [property: JsonPropertyName("cpu")] string Cpu,
    [property: JsonPropertyName("gpu")] string Gpu,
    [property: JsonPropertyName("ram_gb")] int RamGb,
    [property: JsonPropertyName("display")] DisplayCapture Display);

public sealed record DisplayCapture(
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("refresh_hz")] int RefreshHz,
    [property: JsonPropertyName("dpi_scale")] double DpiScale);

public sealed record FairnessCapture(
    [property: JsonPropertyName("power_plan")] string PowerPlan,
    [property: JsonPropertyName("defender_excluded")] bool DefenderExcluded,
    [property: JsonPropertyName("process_priority")] string ProcessPriority,
    [property: JsonPropertyName("vm_reverted")] bool VmReverted,
    [property: JsonPropertyName("warmup_iters")] int WarmupIters,
    [property: JsonPropertyName("measured_iters")] int MeasuredIters,
    [property: JsonPropertyName("discarded")] IReadOnlyList<string> Discarded);

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(ResultEnvelope))]
[JsonSerializable(typeof(IterationSample))]
public partial class ResultSchemaContext : JsonSerializerContext { }
