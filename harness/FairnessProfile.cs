namespace WinttyBench;

public sealed record FairnessProfile(
    string PowerPlan,
    bool DefenderExcluded,
    string ProcessPriority,
    bool VmReverted,
    int WarmupIters,
    int MeasuredIters,
    IReadOnlyList<string> Discarded)
{
    public static FairnessProfile Ci() => new(
        PowerPlan: "default",
        DefenderExcluded: false,
        ProcessPriority: "Normal",
        VmReverted: false,
        WarmupIters: 1,
        MeasuredIters: 10,
        Discarded: ["first"]);

    public static FairnessProfile Marketing() => new(
        PowerPlan: "SCHEME_MIN",
        DefenderExcluded: true,
        ProcessPriority: "High",
        VmReverted: true,
        WarmupIters: 3,
        MeasuredIters: 30,
        Discarded: ["first", "last"]);

    public FairnessCapture ToCapture() => new(
        PowerPlan, DefenderExcluded, ProcessPriority, VmReverted,
        WarmupIters, MeasuredIters, Discarded);
}
