using WinttyBench;
using WinttyBench.Cells;
using Xunit;

namespace WinttyBench.Tests;

public class CellTests
{
    [Fact]
    public void StarredCells_Contains_Five_After_C5_Added()
    {
        var all = StarredCells.All;
        Assert.Equal(5, all.Count);
        Assert.Contains(all, c => c.Id == "C5");
    }

    [Fact]
    public void C5_Targets_Wsl_Vtebench_Unicode_Throughput()
    {
        var c5 = StarredCells.All.Single(c => c.Id == "C5");
        Assert.Equal("wsl-ubuntu-24.04", c5.Shell);
        Assert.Equal("vtebench_unicode", c5.Workload);
        Assert.Equal("throughput_bytes_per_sec", c5.Kpi);
        Assert.Equal("fixtures/vtebench/unicode.txt", c5.FixturePath);
        Assert.Null(c5.FixtureKey);
    }

    [Fact]
    public void C1_Targets_Pwsh_Vtebench_Dense_Throughput()
    {
        var c1 = StarredCells.All.Single(c => c.Id == "C1");

        Assert.Equal("pwsh-7.4", c1.Shell);
        Assert.Equal("vtebench_dense_cells", c1.Workload);
        Assert.Equal("throughput_bytes_per_sec", c1.Kpi);
        Assert.Equal("fixtures/vtebench/dense_cells.txt", c1.FixturePath);
        Assert.Null(c1.FixtureKey);
    }

    [Fact]
    public void C2_Targets_Pwsh_Vtebench_Scrolling_Throughput()
    {
        var c2 = StarredCells.All.Single(c => c.Id == "C2");

        Assert.Equal("pwsh-7.4", c2.Shell);
        Assert.Equal("vtebench_scrolling", c2.Workload);
        Assert.Equal("throughput_bytes_per_sec", c2.Kpi);
        Assert.Equal("fixtures/vtebench/scrolling.txt", c2.FixturePath);
        Assert.Null(c2.FixtureKey);
    }

    [Fact]
    public void C3_Is_Single_Cjk_Baseline_Without_Locale_Override()
    {
        // At MVP the utf8-console knob does not exist yet. C3 captures CJK
        // throughput under current config as a single baseline. When the knob
        // lands (Plan 4), split into C3a (force) and C3b (never).
        var c3 = StarredCells.All.Single(c => c.Id == "C3");

        Assert.Equal("pwsh-7.4", c3.Shell);
        Assert.Equal("cjk_jp_mixed_1mb", c3.Workload);
        Assert.Equal("fixtures/cjk/jp-mixed-1mb.txt", c3.FixturePath);
        Assert.Empty(c3.WinttyConfigOverrides);
        Assert.Null(c3.FixtureKey);
    }

    [Fact]
    public void C4_Targets_Wsl()
    {
        var c4 = StarredCells.All.Single(c => c.Id == "C4");

        Assert.Equal("wsl-ubuntu-24.04", c4.Shell);
        Assert.Equal("vtebench_dense_cells", c4.Workload);
        Assert.Null(c4.FixtureKey);
    }

    [Fact]
    public void Cell_Rejects_Both_Path_And_Key()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Cell(
            Id: "X",
            Shell: "pwsh-7.4",
            Workload: "w",
            Kpi: "throughput_bytes_per_sec",
            FixturePath: "fixtures/vtebench/dense_cells.txt",
            FixtureKey: "c10",
            WinttyConfigOverrides: new Dictionary<string, string>()));

        Assert.Contains("exactly one", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cell_Rejects_Neither_Path_Nor_Key()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Cell(
            Id: "X",
            Shell: "pwsh-7.4",
            Workload: "w",
            Kpi: "throughput_bytes_per_sec",
            FixturePath: null,
            FixtureKey: null,
            WinttyConfigOverrides: new Dictionary<string, string>()));

        Assert.Contains("exactly one", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
