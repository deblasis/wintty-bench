namespace WinttyBench.Kpis;

public interface IKpi
{
    string Name { get; }

    KpiResult ComputeFromSamples(IReadOnlyList<IterationSample> samples, long fixtureBytes);
}

// ValueP50/P95/P99/Stddev are null when all samples hung (source = "degraded").
public sealed record KpiResult(
    double? ValueP50,
    double? ValueP95,
    double? ValueP99,
    double? ValueStddev,
    IReadOnlyList<IterationSample> RawIterations,
    string Source);
