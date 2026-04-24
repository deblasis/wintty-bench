namespace WinttyBench.Cells;

// Exactly one of FixturePath and FixtureKey must be non-null UNLESS the KPI
// is fixture-less (e.g. "startup_seconds"), in which case both must be null.
// FixturePath: Plan 1 behavior, static path relative to repo root.
// FixtureKey:  Plan 2A+ behavior, resolved at runtime by FixtureResolver
//              using the active FairnessProfile's size table.
public sealed record Cell
{
    // KPIs that do not ingest a fixture. Plan 2B adds "startup_seconds";
    // future non-fixture KPIs (e.g. idle-cpu) extend this set.
    // Defined inline on Cell rather than referenced from KpiFactory to avoid
    // a Cells -> Kpis circular dependency.
    private static readonly HashSet<string> FixtureLessKpis = new(StringComparer.Ordinal);

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
        var isFixtureLess = FixtureLessKpis.Contains(Kpi);

        if (isFixtureLess && (pathSet || keySet))
            throw new ArgumentException(
                $"Cell with fixture-less KPI '{Kpi}' must set neither FixturePath nor FixtureKey",
                pathSet ? nameof(FixturePath) : nameof(FixtureKey));

        if (!isFixtureLess && pathSet == keySet)
        {
            var msg = pathSet
                ? $"Cell with fixture-bearing KPI '{Kpi}' must set exactly one of FixturePath or FixtureKey, not both"
                : $"Cell with fixture-bearing KPI '{Kpi}' must set exactly one of FixturePath or FixtureKey; if '{Kpi}' is intentionally fixture-less, register it in Cell.FixtureLessKpis";
            throw new ArgumentException(msg,
                pathSet ? nameof(FixturePath) : nameof(FixtureKey));
        }

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
