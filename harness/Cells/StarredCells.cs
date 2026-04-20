using System.Collections.ObjectModel;

namespace WinttyBench.Cells;

public static class StarredCells
{
    private static IReadOnlyDictionary<string, string> Empty { get; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    public static IReadOnlyList<Cell> All { get; } =
    [
        new Cell(
            Id: "C1",
            Shell: "pwsh-7.4",
            Workload: "vtebench_dense_cells",
            Kpi: "throughput_bytes_per_sec",
            FixturePath: "fixtures/vtebench/dense_cells.txt",
            FixtureKey: null,
            WinttyConfigOverrides: Empty),

        new Cell(
            Id: "C2",
            Shell: "pwsh-7.4",
            Workload: "vtebench_scrolling",
            Kpi: "throughput_bytes_per_sec",
            FixturePath: "fixtures/vtebench/scrolling.txt",
            FixtureKey: null,
            WinttyConfigOverrides: Empty),

        // C3 is a single baseline cell at MVP (no utf8-console knob yet).
        // When the knob lands (Plan 4), split into C3a (force) and C3b (never).
        new Cell(
            Id: "C3",
            Shell: "pwsh-7.4",
            Workload: "cjk_jp_mixed_1mb",
            Kpi: "throughput_bytes_per_sec",
            FixturePath: "fixtures/cjk/jp-mixed-1mb.txt",
            FixtureKey: null,
            WinttyConfigOverrides: Empty),

        new Cell(
            Id: "C4",
            Shell: "wsl-ubuntu-24.04",
            Workload: "vtebench_dense_cells",
            Kpi: "throughput_bytes_per_sec",
            FixturePath: "fixtures/vtebench/dense_cells.txt",
            FixtureKey: null,
            WinttyConfigOverrides: Empty),

        new Cell(
            Id: "C5",
            Shell: "wsl-ubuntu-24.04",
            Workload: "vtebench_unicode",
            Kpi: "throughput_bytes_per_sec",
            FixturePath: "fixtures/vtebench/unicode.txt",
            FixtureKey: null,
            WinttyConfigOverrides: Empty),
    ];
}
