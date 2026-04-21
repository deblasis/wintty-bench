using System.Text.Json.Serialization;

namespace WinttyBench.Kpis;

// One iteration of a KPI measurement.
// Invariant enforced at construction: Value == null iff Hung == true.
// For throughput KPI: Value = bytes/second (final unit).
// For startup KPI:    Value = seconds.
// For memory-rss KPI: Value = peak bytes.
public sealed record IterationSample
{
    public IterationSample(double? Value, bool Hung)
    {
        if (Hung && Value is not null)
            throw new ArgumentException(
                "Hung iteration must have null Value", nameof(Value));
        if (!Hung && Value is null)
            throw new ArgumentException(
                "Non-hung iteration must have non-null Value", nameof(Value));

        this.Value = Value;
        this.Hung = Hung;
    }

    [JsonPropertyName("value")]
    public double? Value { get; init; }

    [JsonPropertyName("hung")]
    public bool Hung { get; init; }
}
