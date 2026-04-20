namespace WinttyBench.Cells;

public sealed record Cell(
    string Id,
    string Shell,
    string Workload,
    string Kpi,
    string FixturePath,
    IReadOnlyDictionary<string, string> WinttyConfigOverrides);
