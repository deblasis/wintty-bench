namespace WinttyBench.Kpis;

public interface IKpi
{
    string Name { get; }

    // Short unit label for console/display formatting (e.g. "B/s", "s",
    // "bytes"). Lives on the KPI so adding a new KPI only touches the KPI
    // class + the two factories, not every consumer that prints results.
    string UnitHint { get; }

    // Samples arrive in the KPI's final unit (bytes/sec for throughput,
    // seconds for startup, bytes for RSS). Per-KPI runners own the conversion
    // from raw measurement (wall-clock, peak-reading) into final units before
    // emitting the sample. This keeps the stats layer unit-agnostic.
    KpiResult ComputeFromSamples(IReadOnlyList<IterationSample> samples);
}

public sealed record KpiResult(
    double? ValueP50,
    double? ValueP95,
    double? ValueP99,
    double? ValueStddev,
    IReadOnlyList<IterationSample> RawIterations,
    string Source);
