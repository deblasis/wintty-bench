using System.Text.Json.Serialization;

namespace WinttyBench.Kpis;

// One iteration of a KPI measurement. `Value` is null iff `Hung` is true.
// For throughput KPI: Value = wall-clock seconds.
public sealed record IterationSample(
    [property: JsonPropertyName("value")] double? Value,
    [property: JsonPropertyName("hung")] bool Hung);
