namespace WinttyBench.Kpis;

public interface IKpi
{
    string Name { get; }

    KpiResult ComputeFromSamples(IReadOnlyList<double> wallTimesSec, long fixtureBytes);
}

public sealed record KpiResult(
    double ValueP50,
    double ValueP95,
    double ValueP99,
    double ValueStddev,
    IReadOnlyList<double> RawIterations,
    string Source);
