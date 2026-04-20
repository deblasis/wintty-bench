namespace WinttyBench.Cells;

// Exactly one of FixturePath and FixtureKey must be non-null.
// FixturePath: Plan 1 behavior, static path relative to repo root.
// FixtureKey:  Plan 2A+ behavior, resolved at runtime by FixtureResolver
//              using the active FairnessProfile's size table.
public sealed record Cell
{
    public Cell(
        string Id,
        string Shell,
        string Workload,
        string Kpi,
        string? FixturePath,
        string? FixtureKey,
        IReadOnlyDictionary<string, string> WinttyConfigOverrides)
    {
        var pathSet = FixturePath is not null;
        var keySet = FixtureKey is not null;
        if (pathSet == keySet)
            throw new ArgumentException(
                "Cell must set exactly one of FixturePath or FixtureKey",
                pathSet ? nameof(FixturePath) : nameof(FixtureKey));

        this.Id = Id;
        this.Shell = Shell;
        this.Workload = Workload;
        this.Kpi = Kpi;
        this.FixturePath = FixturePath;
        this.FixtureKey = FixtureKey;
        this.WinttyConfigOverrides = WinttyConfigOverrides;
    }

    public string Id { get; init; }
    public string Shell { get; init; }
    public string Workload { get; init; }
    public string Kpi { get; init; }
    public string? FixturePath { get; init; }
    public string? FixtureKey { get; init; }
    public IReadOnlyDictionary<string, string> WinttyConfigOverrides { get; init; }
}
