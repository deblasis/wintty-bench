namespace WinttyBench;

public sealed record FairnessProfile(
    string PowerPlan,
    bool DefenderExcluded,
    string ProcessPriority,
    bool VmReverted,
    int WarmupIters,
    int MeasuredIters,
    IReadOnlyList<string> Discarded,
    IReadOnlyDictionary<string, long> FixtureSizeBytesByKey)
{
    private static readonly string[] CiDiscarded = ["first"];
    private static readonly string[] MarketingDiscarded = ["first", "last"];

    // 1 MB in CI mode: swamps startup without blowing the CI 15-min budget.
    private static readonly IReadOnlyDictionary<string, long> CiSizes =
        new Dictionary<string, long>
        {
            ["c10"] = 1L * 1024 * 1024,
            ["c11"] = 1L * 1024 * 1024,
        };

    // 10 MB in marketing mode. 100 MB is deferred to a later plan once the
    // box-setup scripts make very-long runs fair.
    private static readonly IReadOnlyDictionary<string, long> MarketingSizes =
        new Dictionary<string, long>
        {
            ["c10"] = 10L * 1024 * 1024,
            ["c11"] = 10L * 1024 * 1024,
        };

    public static FairnessProfile Ci() => new(
        PowerPlan: "default",
        DefenderExcluded: false,
        ProcessPriority: "Normal",
        VmReverted: false,
        WarmupIters: 1,
        MeasuredIters: 10,
        Discarded: CiDiscarded,
        FixtureSizeBytesByKey: CiSizes);

    public static FairnessProfile Marketing() => new(
        PowerPlan: "SCHEME_MIN",
        DefenderExcluded: true,
        ProcessPriority: "High",
        VmReverted: true,
        WarmupIters: 3,
        MeasuredIters: 30,
        Discarded: MarketingDiscarded,
        FixtureSizeBytesByKey: MarketingSizes);

    public FairnessCapture ToCapture() => new(
        PowerPlan, DefenderExcluded, ProcessPriority, VmReverted,
        WarmupIters, MeasuredIters, Discarded);
}
